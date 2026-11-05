"""Windows/LAN bridge: authenticated loopback HTTP, DNDL upload, DNDP callback.

The public endpoint is a private Microsoft dev tunnel. Only the result listener
binds to the LAN, restricted to the configured console IP and per-job token.
"""
import argparse
from dataclasses import dataclass, field
import hashlib
import hmac
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import ipaddress
import json
from pathlib import Path
import re
import secrets
import socket
import threading
import time
import uuid

import protocol

TERMINAL = {'completed', 'failed', 'expired'}
SCENES = {'Options': (3, 0), 'Oblique': (3, 0), 'Cube': (0, 0), 'Detail': (1, 1)}
VIDEOS = {'480i': (480, 1), '480p': (480, 2), '576i': (576, 1), '576p': (576, 2)}


def route_address(console):
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as connection:
        connection.connect((console, 4299))
        return connection.getsockname()[0]


def config(path):
    path = Path(path).resolve()
    value = json.loads(path.read_text())
    target = ipaddress.IPv4Address(value['console_address'])
    kind = value.get('target_kind', 'gamecube')
    if kind not in ('gamecube', 'simulator'):
        raise ValueError('target_kind must be gamecube or simulator')
    if not target.is_private or target.is_multicast or target.is_unspecified or target.is_link_local:
        raise ValueError('Console must have a private LAN IPv4 address')
    if kind == 'gamecube' and target.is_loopback:
        raise ValueError('A loopback target must be labeled simulator')
    value['target_kind'] = kind
    value.setdefault('callback_address', route_address(str(target)))
    value.setdefault('api_port', 6280)
    value.setdefault('callback_port', 6281)
    value.setdefault('launcher_port', 4299)
    value.setdefault('capture_timeout', 120)
    value.setdefault('ready_timeout', 30)
    for name in ('api_port', 'callback_port', 'launcher_port'):
        if not isinstance(value[name], int) or not 1 <= value[name] <= 65535:
            raise ValueError('Invalid TCP port: ' + name)
    if not 10 <= value['capture_timeout'] <= 600 or not 1 <= value['ready_timeout'] <= 120:
        raise ValueError('Invalid operation timeout')
    secret = Path(value['api_token_file'])
    if not secret.is_absolute():
        secret = path.parent / secret
    value['api_token'] = secret.read_text().strip()
    if len(value['api_token']) < 32:
        raise ValueError('API token must have at least 32 characters')
    root = Path(value.get('artifacts', 'results'))
    value['artifacts'] = root if root.is_absolute() else path.parent / root
    return value


@dataclass
class Job:
    id: str
    token: str
    spec: dict
    created: float = field(default_factory=time.time)
    state: str = 'prepared'
    image: dict = field(default_factory=dict)
    launch: dict = field(default_factory=dict)
    result: dict = field(default_factory=dict)
    error: str = ''
    event: threading.Event = field(default_factory=threading.Event)

    def public(self):
        return {key: getattr(self, key) for key in ('id', 'spec', 'created', 'state', 'image', 'launch', 'result', 'error')}


class Bridge:
    def __init__(self, settings):
        self.settings = settings
        self.lock = threading.RLock()
        self.jobs = {}
        self.active = None
        self.closed = threading.Event()
        self.settings['artifacts'].mkdir(parents=True, exist_ok=True)
        # Persist results, never executions. An interrupted launch is not
        # reconstructed/replayed after a Windows process restart.
        for path in self.settings['artifacts'].glob('*/job.json'):
            if not re.fullmatch('[0-9a-f]{32}',path.parent.name):
                continue
            data=json.loads(path.read_text())
            job=Job(path.parent.name,'',data['spec'])
            for name in ('created','state','image','launch','result','error'):
                setattr(job,name,data[name])
            if job.state not in TERMINAL:
                job.state='failed';job.error='Bridge restarted; execution was not replayed'
                self.save(job)
            self.jobs[job.id]=job
        self.callback = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        # Do not use SO_REUSEADDR on Windows: it permits an unrelated process
        # to bind the active endpoint. A second bridge must fail to start.
        if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
            self.callback.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        self.callback.bind((self.settings['callback_address'], self.settings['callback_port']))
        self.callback.listen(4)
        self.callback.settimeout(.5)
        self.settings['callback_port'] = self.callback.getsockname()[1]
        self.thread = threading.Thread(target=self.callbacks, daemon=True)
        self.thread.start()

    def close(self):
        self.closed.set()
        self.callback.close()
        self.thread.join(timeout=2)

    def target(self):
        return {key: self.settings[key] for key in ('target_kind', 'console_address', 'launcher_port', 'callback_address', 'callback_port')}

    def prepare(self, spec):
        if set(spec) - {'scene', 'video', 'label', 'source_revision', 'working_tree_sha256', 'build_profile'}:
            raise ValueError('Unknown job fields')
        spec = dict(spec)
        spec.setdefault('scene', 'Options')
        spec.setdefault('video', '480i')
        spec.setdefault('label', 'VideoMode')
        if not isinstance(spec['scene'],str) or not isinstance(spec['video'],str) or spec['scene'] not in SCENES or spec['video'] not in VIDEOS:
            raise ValueError('Unsupported scene/video mode')
        if not isinstance(spec['label'], str) or len(spec['label']) > 80 or any(ord(c) < 32 for c in spec['label']):
            raise ValueError('Invalid label')
        if spec.get('build_profile') != 'Release-O3-LTO-no-debugger':
            raise ValueError('Performance jobs require an optimized Release manifest')
        if not isinstance(spec.get('source_revision'),str) or not re.fullmatch('[0-9a-f]{40}', spec['source_revision']):
            raise ValueError('A full source revision is required')
        if not isinstance(spec.get('working_tree_sha256'),str) or not re.fullmatch('[0-9a-f]{64}', spec['working_tree_sha256']):
            raise ValueError('A source-tree digest is required')
        with self.lock:
            if self.active and self.active.state == 'prepared' and time.time() - self.active.created > 900:
                self.active.state = 'expired'
            if self.active and self.active.state not in TERMINAL:
                raise RuntimeError('Another job owns the console')
            job = Job(uuid.uuid4().hex, secrets.token_hex(16), spec)
            self.jobs[job.id] = job
            self.active = job
        return {'id': job.id, 'callback_token': job.token, **self.target(), 'spec': spec}

    def get(self, identifier):
        if not re.fullmatch('[0-9a-f]{32}', identifier):
            raise KeyError('Invalid job ID')
        with self.lock:
            return self.jobs[identifier]

    def cancel_prepared(self, identifier):
        with self.lock:
            job=self.get(identifier)
            if job.state!='prepared':
                raise RuntimeError('An uploaded/running job cannot be cancelled or replayed')
            job.state='failed';job.error='Build cancelled before upload';self.save(job)
            return job.public()

    def save(self, job):
        directory = self.settings['artifacts'] / job.id
        directory.mkdir(exist_ok=True)
        temporary = directory / 'job.json.tmp'
        temporary.write_text(json.dumps(job.public(), indent=2) + '\n')
        temporary.replace(directory / 'job.json')

    def submit(self, identifier, image, expected_hash):
        with self.lock:
            job = self.get(identifier)
            if job is not self.active or job.state != 'prepared':
                raise RuntimeError('Job is not prepared; an upload is never replayed')
            if time.time() - job.created > 900:
                job.state = 'expired'
                raise RuntimeError('Prepared job expired')
            job.image = protocol.validate_elf(image, job.token)
            if not hmac.compare_digest(job.image['sha256'], expected_hash):
                raise ValueError('Uploaded ELF hash mismatch')
            directory = self.settings['artifacts'] / job.id
            directory.mkdir(exist_ok=True)
            (directory / 'image.elf').write_bytes(image)
            job.state = 'launching'
            self.save(job)
        threading.Thread(target=self.run, args=(job, image), daemon=True).start()
        return job.public()

    def run(self, job, image):
        try:
            launched = protocol.launch(self.settings['console_address'], self.settings['launcher_port'], image,
                                       ready_timeout=self.settings['ready_timeout'])
            with self.lock:
                job.launch = launched
                job.state = 'measuring'
                self.save(job)
            # No console probes, debugger interrupts or callbacks from this host
            # during the measurement window. HTTP polling reads only this job.
            if not job.event.wait(self.settings['capture_timeout']):
                raise TimeoutError('No validated native telemetry arrived; execution is not replayed. Check the console/receiver.')
            deadline = time.monotonic() + self.settings['ready_timeout']
            ready = False
            while time.monotonic() < deadline:
                try:
                    protocol.probe(self.settings['console_address'], self.settings['launcher_port'], timeout=1)
                    ready = True
                    break
                except (OSError, ValueError, ConnectionError):
                    time.sleep(.2)
            with self.lock:
                job.result['receiver_ready_after_run'] = ready
                if not ready:
                    job.result['next_run_requires_attention'] = 'Telemetry is valid; resident receiver did not become ready. Return to it before another job.'
                job.state = 'completed'
                self.save(job)
        except Exception as error:
            with self.lock:
                job.state = 'failed'
                job.error = str(error)
                self.save(job)

    def callbacks(self):
        while not self.closed.is_set():
            try:
                stream, address = self.callback.accept()
            except socket.timeout:
                continue
            except OSError:
                break
            with stream:
                stream.settimeout(30)
                try:
                    if address[0] != self.settings['console_address']:
                        raise ValueError('Callback did not originate from the configured target')
                    token, rows, digest = protocol.telemetry(stream)
                    with self.lock:
                        job = self.active
                        if job is None or job.state not in ('launching', 'measuring') or not hmac.compare_digest(job.token, token):
                            raise ValueError('Callback is not for the active job')
                        if job.event.is_set():
                            raise ValueError('Duplicate callback')
                        height, scan = VIDEOS[job.spec['video']]
                        face, editing = SCENES[job.spec['scene']]
                        if any((r['height'], r['scan'], r['face'], r['editing']) != (height, scan, face, editing) for r in rows):
                            raise ValueError('Native scene/mode differs from the prepared job')
                        summary, csv = protocol.results(rows)
                        directory = self.settings['artifacts'] / job.id
                        (directory / 'frames.csv').write_text(csv)
                        job.result = {'frames': len(rows), 'units': 'microseconds', 'summary': summary,
                                      'payload_sha256': digest, 'received_at': time.time(), 'target': self.target(),
                                      'measurement_origin': 'native-clock-over-BBA' if self.settings['target_kind'] == 'gamecube' else 'simulated-target-not-hardware',
                                      'callback_peer':list(address),
                                      'capture_note': 'Raw timing only; no hardware screenshot/capture card is claimed.'}
                        self.save(job)
                        stream.sendall(b'OKAY')
                        job.event.set()
                except (OSError, ValueError, ConnectionError, UnicodeError):
                    # Unauthenticated/malformed callbacks cannot poison the job.
                    # A valid target can still deliver its bounded result later.
                    continue

    def check_receiver(self):
        with self.lock:
            if self.active and self.active.state not in TERMINAL:
                raise RuntimeError('Do not probe the console while a job owns it')
            return protocol.probe(self.settings['console_address'], self.settings['launcher_port'])


class Api(BaseHTTPRequestHandler):
    server_version = 'DndBridge/1'

    def log_message(self, fmt, *args):
        # No tokens, request bodies or headers are written to logs.
        pass

    def respond(self, status, value, content_type='application/json'):
        body = json.dumps(value).encode() if content_type == 'application/json' else value.encode()
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(body)

    def authorized(self):
        expected = 'Bearer ' + self.server.bridge.settings['api_token']
        return hmac.compare_digest(self.headers.get('Authorization', '').encode('utf-8'), expected.encode('utf-8'))

    def do_GET(self):
        if not self.authorized():
            return self.respond(401, {'error': 'Unauthorized'})
        try:
            if self.path == '/v1/target':
                return self.respond(200, self.server.bridge.target())
            match = re.fullmatch('/v1/jobs/([0-9a-f]{32})(/frames.csv)?', self.path)
            if not match:
                return self.respond(404, {'error': 'Not found'})
            job = self.server.bridge.get(match[1])
            if match[2]:
                path = self.server.bridge.settings['artifacts'] / job.id / 'frames.csv'
                if not path.is_file():
                    return self.respond(409, {'error': 'No validated timing log yet'})
                return self.respond(200, path.read_text(), 'text/csv')
            return self.respond(200, job.public())
        except KeyError:
            return self.respond(404, {'error': 'Job not found'})

    def do_POST(self):
        if not self.authorized():
            return self.respond(401, {'error': 'Unauthorized'})
        try:
            self.connection.settimeout(60)
            length = int(self.headers.get('Content-Length', '-1'))
            if not 0 <= length <= protocol.MAX_IMAGE:
                return self.respond(413, {'error': 'Content-Length must fit the bounded request'})
            if self.path == '/v1/probe':
                if length:
                    raise ValueError('Probe has no request body')
                return self.respond(200, self.server.bridge.check_receiver())
            if self.path == '/v1/jobs':
                if length > 4096:
                    return self.respond(413, {'error': 'Job description too large'})
                spec = json.loads(self.rfile.read(length))
                if not isinstance(spec, dict):
                    raise ValueError('Expected a job object')
                return self.respond(201, self.server.bridge.prepare(spec))
            cancel = re.fullmatch('/v1/jobs/([0-9a-f]{32})/cancel', self.path)
            if cancel:
                if length>4096:
                    raise ValueError('Cancellation body too large')
                self.rfile.read(length)
                return self.respond(200,self.server.bridge.cancel_prepared(cancel[1]))
            match = re.fullmatch('/v1/jobs/([0-9a-f]{32})/image', self.path)
            if not match:
                return self.respond(404, {'error': 'Not found'})
            image = self.rfile.read(length)
            if len(image) != length:
                raise ValueError('Truncated ELF upload')
            return self.respond(202, self.server.bridge.submit(match[1], image, self.headers.get('X-Image-Sha256', '')))
        except (ValueError, KeyError, UnicodeError):
            return self.respond(400, {'error': 'Invalid request or ELF/job mismatch'})
        except RuntimeError as error:
            return self.respond(409, {'error': str(error)})
        except OSError:
            return self.respond(502, {'error': 'Target or local storage unavailable'})


def server(bridge):
    result = ThreadingHTTPServer(('127.0.0.1', bridge.settings['api_port']), Api)
    result.daemon_threads = True
    result.bridge = bridge
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True)
    args = parser.parse_args()
    bridge = Bridge(config(args.config))
    api = server(bridge)
    print(f'Bridge API on loopback port {api.server_port}; target={bridge.settings["target_kind"]}; console={bridge.settings["console_address"]}', flush=True)
    try:
        api.serve_forever()
    finally:
        api.server_close()
        bridge.close()


if __name__ == '__main__':
    main()
