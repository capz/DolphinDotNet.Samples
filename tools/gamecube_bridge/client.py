"""Build an optimized native job, upload it through the Windows bridge and fetch CSV.
Secrets come from DND_BRIDGE_API_TOKEN and DND_TUNNEL_ACCESS_TOKEN, never CLI args.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
from urllib.error import HTTPError
from urllib.parse import urlparse
from urllib.request import Request, build_opener, HTTPRedirectHandler

import protocol


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, newurl):
        # Never forward bridge/tunnel credentials to an OAuth/foreign origin.
        return None


class Client:
    def __init__(self, url):
        target = urlparse(url)
        if target.scheme != 'https' and not (target.scheme == 'http' and target.hostname in ('127.0.0.1', 'localhost')):
            raise ValueError('Bridge URL requires HTTPS (HTTP is allowed only for loopback tests)')
        if target.username or target.password or target.query or target.fragment:
            raise ValueError('Bridge URL must not contain credentials/query/fragment')
        self.url = url.rstrip('/')
        self.token = os.environ.get('DND_BRIDGE_API_TOKEN', '')
        if not self.token:
            raise ValueError('Configure DND_BRIDGE_API_TOKEN as a cloud secret')

    def request(self, path, body=None, raw=False, headers=None):
        values = {'Authorization': 'Bearer ' + self.token, 'X-Tunnel-Skip-AntiPhishing-Page': 'true'}
        tunnel = os.environ.get('DND_TUNNEL_ACCESS_TOKEN')
        if tunnel:
            values['X-Tunnel-Authorization'] = 'tunnel ' + tunnel
        values.update(headers or {})
        if isinstance(body, dict):
            body = json.dumps(body).encode()
            values['Content-Type'] = 'application/json'
        elif body is not None:
            values['Content-Type'] = 'application/octet-stream'
        if body is not None:
            values['Content-Length'] = str(len(body))
        request = Request(self.url + path, data=body, headers=values, method='POST' if body is not None else 'GET')
        try:
            with build_opener(NoRedirect()).open(request, timeout=120) as reply:
                data = reply.read()
                return data if raw else json.loads(data)
        except HTTPError as error:
            # Tokens/headers are deliberately not included in exception output.
            raise RuntimeError(f'Bridge HTTP {error.code}; check private tunnel access, API credentials and job state') from None


def source_digest(root):
    paths = subprocess.run(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=root, check=True, capture_output=True).stdout.split(b'\0')
    digest = hashlib.sha256()
    for value in sorted(set(paths)):
        if not value:
            continue
        path = root / os.fsdecode(value)
        if path.is_file() and path.suffix in ('.cs', '.csproj', '.props', '.targets', '.json'):
            digest.update(value + b'\0' + hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def optimized(makefile):
    source = makefile.read_text()
    if '-Og' in source or '-fno-inline' in source or 'DND_LTO ?= 1' not in source or '-O3 -flto' not in source:
        raise ValueError('Native Makefile is not optimized Release/O3/LTO; refuse to report it as production performance')


def run(args):
    root = args.repo.resolve()
    destination = args.output.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    client = Client(args.bridge)
    revision = subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=root, check=True, capture_output=True, text=True).stdout.strip()
    spec = {'scene': args.scene, 'video': args.video, 'label': args.label, 'source_revision': revision,
            'working_tree_sha256': source_digest(root), 'build_profile': 'Release-O3-LTO-no-debugger'}
    prepared = client.request('/v1/jobs', spec)
    identifier = prepared['id']
    print('Prepared hardware job ' + identifier, flush=True)
    environment = os.environ.copy()
    environment.update(IplRunnerHost=prepared['callback_address'], IplRunnerPort=str(prepared['callback_port']), IplRunnerToken=prepared['callback_token'])
    command = ['dotnet', 'build', 'VideoMode/VideoMode.csproj', '-c', 'Release', '-p:DndBuildNative=true',
               '-p:DndBuildBackend=' + args.backend, '-p:DndDebugSymbols=false', '-p:DndGdbTransport=Off',
               '-p:IplHardwareRunner=true', '-p:IplFrameLogDevice=None', '-p:IplRunnerScene=' + args.scene,
               '-p:IplRunnerVideo=' + args.video, '-p:IplHistoricalRenderer=' + str(getattr(args,'baseline_renderer',False)).lower()]
    try:
        with (destination / 'build.log').open('w') as log:
            subprocess.run(command, cwd=root, env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
        optimized(root / 'VideoMode/obj/Release/net8.0/native/Makefile')
        image = protocol.compact_elf((root / 'VideoMode/bin/Release/net8.0/VideoMode.elf').read_bytes(), prepared['callback_token'])
        info = protocol.validate_elf(image, prepared['callback_token'])
    except Exception:
        try:
            client.request('/v1/jobs/' + identifier + '/cancel', {})
        except Exception:
            pass
        raise
    # Only one submit request. A lost upload/execution reply is never replayed.
    client.request('/v1/jobs/' + identifier + '/image', image, headers={'X-Image-Sha256': info['sha256']})
    print('ELF submitted; waiting for native post-window telemetry', flush=True)
    deadline = time.monotonic() + 720
    while time.monotonic() < deadline:
        job = client.request('/v1/jobs/' + identifier)
        (destination / 'job.json').write_text(json.dumps(job, indent=2) + '\n')
        if job['state'] == 'completed':
            (destination / 'frames.csv').write_bytes(client.request('/v1/jobs/' + identifier + '/frames.csv', raw=True))
            print(json.dumps({'job': identifier, 'target': job['result']['target'], 'timings': job['result']['summary']['frame_us'],
                              'receiver_ready': job['result']['receiver_ready_after_run']}), flush=True)
            return
        if job['state'] in ('failed', 'expired'):
            raise RuntimeError(job['error'] or 'Hardware job failed; execution was not retried')
        time.sleep(2)  # Poll bridge memory only; never interrupt/probe the console.
    raise TimeoutError('Bridge job did not finish; execution is not replayed')


def baseline(args):
    """Preserve current instrumentation; replace only the two baseline renderers.
    Keep the owned comparison worktree for review, never overwrite user sources.
    """
    root=args.repo.resolve()
    dirty=subprocess.run(['git','status','--porcelain'],cwd=root,check=True,capture_output=True).stdout
    if dirty:
        raise ValueError('Commit source changes before creating the isolated baseline comparison')
    comparison=args.output.resolve()/'baseline-source'
    if comparison.exists():
        raise ValueError('Baseline source directory already exists; use a fresh output directory')
    comparison.parent.mkdir(parents=True,exist_ok=True)
    subprocess.run(['git','worktree','add','--detach',str(comparison),'HEAD'],cwd=root,check=True)
    for name in ('IplRenderer.cs','GameCubeCanvas.cs'):
        data=subprocess.run(['git','show','0178139:VideoMode/'+name],cwd=root,check=True,capture_output=True).stdout
        (comparison/'VideoMode'/name).write_bytes(data)
    args.repo=comparison
    run(args)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bridge', required=True)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--scene', choices=['Options', 'Oblique', 'Cube', 'Detail'], default='Options')
    parser.add_argument('--video', choices=['480i', '480p', '576i', '576p'], default='480i')
    parser.add_argument('--label', default='VideoMode')
    parser.add_argument('--backend', choices=['Docker', 'Native'], default='Docker')
    parser.add_argument('--baseline-renderer',action='store_true',help='Create a reviewable isolated worktree with pre-optimization rendering and current measurement/reporting code')
    args=parser.parse_args()
    baseline(args) if args.baseline_renderer else run(args)
