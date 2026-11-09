"""DNDL v1 ELF launch and DNDP v2 post-window telemetry (stdlib only).

Interoperates with capz/DolphinDotNet.Debuggers' documented resident receiver.
This independent client does not use a hardware debugger or mutate target RAM.
"""
import csv
import hashlib
import io
import math
import secrets
import socket
import statistics
import struct
import time
import zlib

MAX_IMAGE = 16 * 1024 * 1024 - 4096
FIELDS = 'frame_us prepare_us submit_us end_us gpu_wait_us retrace_us capture_us blur_us draws triangles begin_copy_us gate_us width height scan face editing'.split()


def exact(stream, count):
    data = bytearray()
    while len(data) < count:
        part = stream.recv(count - len(data))
        if not part:
            raise ConnectionError('Target closed before the complete message arrived')
        data.extend(part)
    return bytes(data)


def validate_elf(image, token=None):
    return _validate_elf(image, token, MAX_IMAGE)


def _validate_elf(image, token, maximum):
    if not 52 <= len(image) <= maximum:
        raise ValueError('ELF size exceeds the resident receiver limit')
    h = struct.unpack('>16sHHIIIIIHHHHHH', image[:52])
    if h[0][:7] != b'\x7fELF\x01\x02\x01' or h[1:3] != (2, 20) or h[3] != 1 or h[8] != 52:
        raise ValueError('Expected a big-endian fixed-address PowerPC ELF32')
    entry, phoff, phsize, phcount = h[4], h[5], h[9], h[10]
    if phsize != 32 or not 1 <= phcount <= 16 or phoff < 52 or phoff + phcount * 32 > len(image):
        raise ValueError('Invalid ELF program headers')
    ranges, executable = [], False
    for i in range(phcount):
        kind, offset, address, physical, files, memory, flags, align = struct.unpack_from('>8I', image, phoff + i * 32)
        if kind in (2, 3):
            raise ValueError('Dynamic ELF linking/relocation is unsupported')
        if kind != 1 or not memory:
            continue
        if files > memory or offset + files > len(image) or physical != address or address & 3 or flags & ~7:
            raise ValueError('Invalid ELF load segment')
        if align > 1 and (align & (align - 1) or address % align != offset % align):
            raise ValueError('Invalid ELF load alignment')
        if not 0x80003100 <= address < address + memory <= 0x81600000:
            raise ValueError('ELF load range overlaps the resident launcher or leaves cached MEM1')
        if any(address < end and start < address + memory for start, end in ranges):
            raise ValueError('Overlapping ELF load segments')
        ranges.append((address, address + memory))
        executable |= bool(flags & 1 and address <= entry and entry + 4 <= address + files)
    if not ranges or not executable or entry & 3:
        raise ValueError('ELF entry is not in an executable load segment')
    if token and token.encode('utf-16-be') not in image:
        raise ValueError('ELF does not contain this job token; rebuild with the prepared job configuration')
    return {'sha256': hashlib.sha256(image).hexdigest(), 'bytes': len(image), 'entry': entry, 'segments': len(ranges)}


def compact_elf(image, token=None):
    """Keep exact PT_LOAD payloads/addresses; omit nonresident debug sections.

    The resident loader uses program headers, not section/symbol tables. GCC can
    emit a >16 MiB debug ELF for an executable whose resident bytes are <3 MiB.
    Never raise the receiver limit or change any executable/loadable byte.
    """
    _validate_elf(image, token, max(MAX_IMAGE, len(image)))
    header = list(struct.unpack('>16sHHIIIIIHHHHHH', image[:52]))
    loads = []
    for i in range(header[10]):
        segment = list(struct.unpack_from('>8I', image, header[5] + i * 32))
        if segment[0] == 1 and segment[5]:
            loads.append(segment)
    header[5], header[6], header[10] = 52, 0, len(loads)
    header[11] = header[12] = header[13] = 0
    packed = bytearray(52 + 32 * len(loads))
    struct.pack_into('>16sHHIIIIIHHHHHH', packed, 0, *header)
    for i, segment in enumerate(loads):
        old_offset, address, files, alignment = segment[1], segment[2], segment[4], segment[7]
        if alignment & (alignment - 1) or alignment > MAX_IMAGE:
            raise ValueError('Invalid ELF load alignment')
        alignment = max(4, alignment)
        # Preserve ELF p_offset/p_vaddr congruence, including page-aligned loads.
        offset = len(packed) + (address - len(packed)) % alignment
        if offset + files > MAX_IMAGE:
            raise ValueError('ELF loadable image exceeds the resident receiver limit')
        packed.extend(b'\0' * (offset - len(packed)))
        packed.extend(image[old_offset:old_offset + files])
        segment[1] = offset
        struct.pack_into('>8I', packed, 52 + i * 32, *segment)
    result = bytes(packed)
    validate_elf(result, token)
    return result


def frame(stream, command, value=0, detail=0):
    stream.sendall(struct.pack('>4sHHII', b'DNDL', 1, command, value, detail))


def response(stream, expected):
    magic, version, command, value, detail = struct.unpack('>4sHHII', exact(stream, 16))
    if magic != b'DNDL' or version != 1:
        raise ValueError('Endpoint is not a DNDL v1 resident launcher')
    if command == 7:
        raise ValueError(f'Receiver rejected the request (code {value}, detail {detail})')
    if command != expected:
        raise ValueError(f'Unexpected DNDL reply {command}; expected {expected}')
    return value, detail


def ready(stream):
    nonce = secrets.randbits(32)
    frame(stream, 1, nonce)
    reply, capacity = response(stream, 2)
    limit = capacity & 0x7fffffff
    if reply != nonce or not 52 <= limit <= MAX_IMAGE:
        raise ValueError('Stale/invalid receiver readiness acknowledgement')
    return nonce, limit, bool(capacity & 0x80000000)


def probe(address, port, timeout=3):
    with socket.create_connection((address, port), timeout=timeout) as stream:
        stream.settimeout(timeout)
        nonce, capacity, compression = ready(stream)
        frame(stream, 8, nonce)
        return {'ready': True, 'maximum_image_bytes': capacity, 'compression': compression}


def launch(address, port, image, ready_timeout=30, upload_timeout=60, progress=None):
    validate_elf(image)
    deadline = time.monotonic() + ready_timeout
    stream, last = None, None
    while time.monotonic() < deadline:
        try:
            candidate = socket.create_connection((address, port), timeout=min(2, max(.1, deadline - time.monotonic())))
            candidate.settimeout(2)
            try:
                handshake = ready(candidate)
            except Exception:
                candidate.close()
                raise
            stream = candidate
            break
        except (OSError, ConnectionError, TimeoutError) as error:
            last = error
            time.sleep(.2)
    if stream is None:
        raise TimeoutError(f'Resident receiver is not ready: {last}')
    with stream:
        nonce, limit, compression = handshake
        if len(image) > limit:
            frame(stream, 8, nonce)
            raise ValueError('ELF exceeds this receiver capacity')
        # Once UPLOAD is sent, never retry this session or execute automatically.
        stream.settimeout(upload_timeout)
        crc = zlib.crc32(image) & 0xffffffff
        packed = zlib.compress(image) if compression else image
        compressed = compression and len(packed) + 4 < len(image)
        payload = packed if compressed else image
        frame(stream, 10 if compressed else 3, len(image), crc)
        if compressed:
            stream.sendall(struct.pack('>I', len(payload)))
        for at in range(0, len(payload), 3968):
            stream.sendall(payload[at:at + 3968])
            if progress:
                progress(min(at + 3968, len(payload)), len(payload))
        accepted, checksum = response(stream, 4)
        if (accepted, checksum) != (len(image), crc):
            raise ValueError('Receiver accepted a different ELF; execution was not requested')
        frame(stream, 5, nonce)
        if response(stream, 6) != (nonce, 0):
            raise ValueError('Execution acknowledgement mismatch; console state is unknown')
        frame(stream, 9, nonce)
        return {'committed': True, 'compressed': compressed, 'transferred_bytes': len(payload)}


def telemetry(stream):
    header = exact(stream, 64)
    magic, version, size = struct.unpack_from('>4sHH', header)
    if (magic, version, size) != (b'DNDP', 2, 64):
        raise ValueError('Invalid DNDP telemetry header')
    token = header[8:40].decode('ascii')
    columns, rows, length, checksum, output_height, header_crc = struct.unpack_from('>6I', header, 40)
    if columns != len(FIELDS) or rows != 96 or length != rows * columns * 4 or output_height not in (480,576):
        raise ValueError('Unexpected telemetry schema or bounds')
    if zlib.crc32(header[:60]) & 0xffffffff != header_crc:
        raise ValueError('Telemetry header checksum mismatch')
    payload = exact(stream, length)
    if zlib.crc32(payload) & 0xffffffff != checksum:
        raise ValueError('Telemetry payload checksum mismatch')
    ints = struct.unpack('>' + 'i' * rows * columns, payload)
    records = [dict(zip(FIELDS, ints[at:at + columns])) for at in range(0, len(ints), columns)]
    for record in records:
        if any(value < 0 for value in record.values()) or not 0 < record['frame_us'] <= 10000000:
            raise ValueError('Invalid frame duration/counter')
        if record['width'] != 640 or not 240<=record['height']<=576 or record['scan'] not in (1, 2):
            raise ValueError('Unexpected native video mode')
        if record['face'] > 5 or record['editing'] not in (0, 1):
            raise ValueError('Unexpected native scene state')
        if record['retrace_us'] > record['frame_us']:
            raise ValueError('Retrace exceeds total frame time')
    return token, records, hashlib.sha256(payload).hexdigest(), output_height


def results(records):
    summary = {}
    for name in FIELDS[:12] + ['active_us']:
        values = sorted(row[name] if name != 'active_us' else row['frame_us'] - row['retrace_us'] for row in records)
        summary[name] = {'mean': statistics.mean(values), 'median': statistics.median(values),
                         'p95': values[math.ceil(len(values) * .95) - 1], 'max': max(values)}
    text = io.StringIO()
    writer = csv.DictWriter(text, FIELDS, lineterminator='\n')
    writer.writeheader()
    writer.writerows(records)
    return summary, text.getvalue()
