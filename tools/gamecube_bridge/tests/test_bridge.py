import hashlib
import http.client
import json
from pathlib import Path
import socket
import struct
import sys
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
import zlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import client
import protocol
import service

TOKEN = '0123456789abcdef0123456789abcdef'  # Public fixture, never a deployment secret.


def elf(token=TOKEN):
    image = bytearray(1024)
    ident = b'\x7fELF\x01\x02\x01' + b'\0' * 9
    struct.pack_into('>16sHHIIIIIHHHHHH', image, 0, ident, 2, 20, 1, 0x80004000, 52, 0, 0, 52, 32, 1, 0, 0, 0)
    struct.pack_into('>8I', image, 52, 1, 84, 0x80004000, 0x80004000, 940, 940, 5, 4)
    image[128:192] = token.encode('utf-16-be')
    return bytes(image)


def packet(token=TOKEN, scene='Options', video='480i'):
    height, scan = service.VIDEOS[video]
    face, editing = service.SCENES[scene]
    row = [50048, 7528, 41362, 1137, 3, 1121, 616, 1690, 102, 17504, 3, 0, 640, height, scan, face, editing]
    payload = struct.pack('>17i', *row) * 96
    header = bytearray(64)
    struct.pack_into('>4sHH', header, 0, b'DNDP', 1, 64)
    header[8:40] = token.encode()
    struct.pack_into('>6I', header, 40, 17, 96, len(payload), zlib.crc32(payload), 0, 0)
    struct.pack_into('>I', header, 56, zlib.crc32(header[:56]))
    return bytes(header) + payload


class Receiver:
    """Protocol fake, not hardware. COMMIT triggers a post-window test callback."""
    def __init__(self, accept_crc=True, compressed=True):
        self.listener = socket.socket()
        self.listener.bind(('127.0.0.1', 0))
        self.listener.listen(4)
        self.port = self.listener.getsockname()[1]
        self.listener.settimeout(.1)
        self.stop = threading.Event()
        self.accept_crc = accept_crc
        self.compressed = compressed
        self.on_commit = None
        self.commits = 0
        self.uploads = 0
        self.images = []
        self.errors = []
        self.thread = threading.Thread(target=self.loop, daemon=True)
        self.thread.start()

    def loop(self):
        while not self.stop.is_set():
            try:
                stream, _ = self.listener.accept()
            except socket.timeout:
                continue
            except OSError:
                break
            try:
                with stream:
                    stream.settimeout(3)
                    self.session(stream)
            except Exception as error:
                if not self.stop.is_set():
                    self.errors.append(error)

    def receive(self, stream):
        magic, version, command, value, detail = struct.unpack('>4sHHII', protocol.exact(stream, 16))
        if (magic, version) != (b'DNDL', 1):
            raise ValueError('Invalid test request')
        return command, value, detail

    def session(self, stream):
        command, nonce, detail = self.receive(stream)
        assert command == 1 and not detail
        protocol.frame(stream, 2, nonce, protocol.MAX_IMAGE | (0x80000000 if self.compressed else 0))
        command, size, crc = self.receive(stream)
        if command == 8:
            assert size == nonce
            return
        self.uploads += 1
        assert command in (3, 10)
        if command == 10:
            length = struct.unpack('>I', protocol.exact(stream, 4))[0]
            image = zlib.decompress(protocol.exact(stream, length))
        else:
            image = protocol.exact(stream, size)
        assert len(image) == size and zlib.crc32(image) == crc
        self.images.append(image)
        protocol.frame(stream, 4, size, crc if self.accept_crc else crc ^ 1)
        if not self.accept_crc:
            # Host must close, without EXECUTE or replay.
            assert stream.recv(16) == b''
            return
        assert self.receive(stream) == (5, nonce, 0)
        protocol.frame(stream, 6, nonce)
        assert self.receive(stream) == (9, nonce, 0)
        self.commits += 1
        if self.on_commit:
            threading.Thread(target=self.on_commit, daemon=True).start()

    def close(self):
        self.stop.set()
        self.listener.close()
        self.thread.join(2)


def settings(root, port):
    return {'target_kind': 'simulator', 'console_address': '127.0.0.1', 'launcher_port': port,
            'callback_address': '127.0.0.1', 'callback_port': 0, 'api_port': 0,
            'api_token': 'a' * 64, 'artifacts': root, 'ready_timeout': 1, 'capture_timeout': 2}


def spec(**changes):
    result = {'scene': 'Options', 'video': '480i', 'label': 'fixture', 'source_revision': 'a' * 40,
              'working_tree_sha256': 'b' * 64, 'build_profile': 'Release-O3-LTO-no-debugger'}
    result.update(changes)
    return result


class ProtocolTests(unittest.TestCase):
    def test_release_elf_and_token_binding(self):
        self.assertEqual(protocol.validate_elf(elf(), TOKEN)['segments'], 1)
        with self.assertRaises(ValueError):
            protocol.validate_elf(elf(), 'f' * 32)
        changed = bytearray(elf())
        struct.pack_into('>I', changed, 52 + 8, 0x81600000)
        struct.pack_into('>I', changed, 52 + 12, 0x81600000)
        with self.assertRaises(ValueError):
            protocol.validate_elf(changed)

    def test_bad_entry_and_architecture(self):
        changed = bytearray(elf())
        struct.pack_into('>I', changed, 24, 0x80006000)
        with self.assertRaises(ValueError):
            protocol.validate_elf(changed)
        changed[18:20] = b'\0\x3e'
        with self.assertRaises(ValueError):
            protocol.validate_elf(changed)

    def test_compressed_and_legacy_upload_acknowledgements(self):
        for compressed in (False, True):
            receiver = Receiver(compressed=compressed)
            try:
                self.assertTrue(protocol.probe('127.0.0.1', receiver.port)['ready'])
                result = protocol.launch('127.0.0.1', receiver.port, elf())
                self.assertEqual(result['compressed'], compressed)
                deadline = time.monotonic() + 2
                while receiver.commits < 1 and time.monotonic() < deadline:
                    time.sleep(.01)
                self.assertEqual(receiver.commits, 1)
                self.assertEqual(receiver.images, [elf()])
                self.assertFalse(receiver.errors)
            finally:
                receiver.close()

    def test_crc_rejection_never_executes_or_replays(self):
        receiver = Receiver(accept_crc=False)
        try:
            with self.assertRaises(ValueError):
                protocol.launch('127.0.0.1', receiver.port, elf())
            self.assertEqual(receiver.uploads, 1)
            self.assertEqual(receiver.commits, 0)
        finally:
            receiver.close()

    def test_native_telemetry_crc_mode_and_summary(self):
        a, b = socket.socketpair()
        try:
            a.sendall(packet())
            token, rows, digest = protocol.telemetry(b)
            self.assertEqual(token, TOKEN)
            self.assertEqual(len(rows), 96)
            summary, csv = protocol.results(rows)
            self.assertEqual(summary['frame_us']['p95'], 50048)
            self.assertEqual(summary['active_us']['median'], 48927)
            self.assertEqual(len(csv.splitlines()), 97)
            self.assertEqual(len(digest), 64)
        finally:
            a.close(); b.close()

    def test_corrupt_and_oversized_telemetry(self):
        for offset in (63, 100):
            a, b = socket.socketpair()
            try:
                raw = bytearray(packet()); raw[offset] ^= 1
                a.sendall(raw)
                with self.assertRaises(ValueError):
                    protocol.telemetry(b)
            finally:
                a.close(); b.close()


class BridgeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.receiver = Receiver()
        self.bridge = service.Bridge(settings(Path(self.temp.name), self.receiver.port))
        self.api = service.server(self.bridge)
        self.api_thread = threading.Thread(target=self.api.serve_forever, daemon=True)
        self.api_thread.start()

    def tearDown(self):
        self.api.shutdown(); self.api.server_close()
        self.bridge.close(); self.receiver.close()
        self.temp.cleanup()

    def request(self, method, path, value=None, auth=True, headers=None):
        conn = http.client.HTTPConnection('127.0.0.1', self.api.server_port, timeout=5)
        body = json.dumps(value).encode() if isinstance(value, dict) else value
        hdr = {'Authorization': 'Bearer ' + 'a' * 64} if auth else {}
        hdr.update(headers or {})
        conn.request(method, path, body=body, headers=hdr)
        response = conn.getresponse()
        raw = response.read()
        status = response.status
        conn.close()
        return status, json.loads(raw) if response.getheader('Content-Type') == 'application/json' else raw

    def test_auth_and_single_console_ownership(self):
        self.assertEqual(self.request('GET', '/v1/target', auth=False)[0], 401)
        status, job = self.request('POST', '/v1/jobs', spec())
        self.assertEqual(status, 201)
        self.assertEqual(self.request('POST', '/v1/jobs', spec())[0], 409)
        self.assertEqual(self.request('POST', '/v1/probe', b'')[0], 409)
        self.assertEqual(self.request('POST', '/v1/jobs/' + job['id'] + '/cancel', {})[0], 200)
        self.assertEqual(self.request('POST', '/v1/jobs', spec())[0], 201)

    def test_invalid_job_inputs(self):
        for value in (spec(scene=[]), spec(source_revision=7), spec(working_tree_sha256=None), spec(build_profile='Debug'), spec(host='bad')):
            self.assertEqual(self.request('POST', '/v1/jobs', value)[0], 400)
        self.assertEqual(self.request('GET', '/v1/jobs/../../secret')[0], 404)

    def test_end_to_end_job_restores_receiver_and_preserves_csv(self):
        _, prepared = self.request('POST', '/v1/jobs', spec())
        identifier = prepared['id']
        token = prepared['callback_token']
        def callback():
            with socket.create_connection(('127.0.0.1', self.bridge.settings['callback_port'])) as stream:
                stream.settimeout(3)
                stream.sendall(packet(token))
                self.assertEqual(protocol.exact(stream, 4), b'OKAY')
        self.receiver.on_commit = callback
        image = elf(token)
        status, _ = self.request('POST', '/v1/jobs/' + identifier + '/image', image,
                                 headers={'X-Image-Sha256': hashlib.sha256(image).hexdigest()})
        self.assertEqual(status, 202)
        deadline = time.monotonic() + 4
        while time.monotonic() < deadline:
            _, result = self.request('GET', '/v1/jobs/' + identifier)
            if result['state'] in service.TERMINAL:
                break
            time.sleep(.01)
        self.assertEqual(result['state'], 'completed', result)
        self.assertEqual(result['result']['measurement_origin'], 'simulated-target-not-hardware')
        self.assertTrue(result['result']['receiver_ready_after_run'])
        self.assertNotIn('callback_token', result)
        status, csv = self.request('GET', '/v1/jobs/' + identifier + '/frames.csv')
        self.assertEqual(status, 200)
        self.assertEqual(len(csv.splitlines()), 97)
        self.assertEqual(self.request('POST', '/v1/jobs/' + identifier + '/image', image)[0], 409)
        self.assertEqual(self.receiver.commits, 1)

    def test_callback_token_does_not_poison_active_job(self):
        prepared = self.bridge.prepare(spec())
        job = self.bridge.get(prepared['id'])
        job.state = 'measuring'
        with socket.create_connection(('127.0.0.1', self.bridge.settings['callback_port'])) as stream:
            stream.sendall(packet('f' * 32))
            self.assertEqual(stream.recv(4), b'')
        self.assertFalse(job.event.is_set())
        self.assertEqual(job.state, 'measuring')

    def test_client_does_not_follow_auth_redirect(self):
        handler = client.NoRedirect()
        self.assertIsNone(handler.redirect_request(None, None, 302, '', {}, 'https://elsewhere.invalid'))
        with patch.dict('os.environ', {'DND_BRIDGE_API_TOKEN': 'a' * 64}):
            with self.assertRaises(ValueError):
                client.Client('http://192.168.1.2:6280')

# Optional interoperability check against the pinned production C receiver.
# The executable validates bytes/ELF and NEVER executes PowerPC machine code.
class NativeReceiverTests(unittest.TestCase):
    @unittest.skipUnless(__import__('os').environ.get('DND_LAUNCHER_STDIO_FIXTURE'), 'production C fixture not configured')
    def test_python_upload_against_production_c_receiver(self):
        import os
        import subprocess
        listener=socket.socket();listener.bind(('127.0.0.1',0));listener.listen(1)
        outcome=[]
        def relay():
            stream,_=listener.accept()
            with stream:
                process=subprocess.Popen([os.environ['DND_LAUNCHER_STDIO_FIXTURE']],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
                def upstream():
                    try:
                        while True:
                            data=stream.recv(8192)
                            if not data:break
                            process.stdin.write(data);process.stdin.flush()
                    finally:
                        process.stdin.close()
                worker=threading.Thread(target=upstream,daemon=True);worker.start()
                try:
                    while True:
                        data=process.stdout.read1(8192)
                        if not data:break
                        stream.sendall(data)
                    process.wait(timeout=5)
                    outcome.append((process.returncode,process.stderr.read()))
                finally:
                    process.stdout.close();process.stderr.close()
                    if process.poll() is None:process.kill();process.wait()
        thread=threading.Thread(target=relay,daemon=True);thread.start()
        try:
            self.assertTrue(protocol.launch('127.0.0.1',listener.getsockname()[1],elf())['committed'])
            thread.join(5)
            self.assertFalse(thread.is_alive())
            self.assertEqual(outcome[0][0],0,outcome)
            self.assertIn(b'VALIDATED',outcome[0][1])
        finally:
            listener.close()


if __name__ == '__main__':
    unittest.main()
