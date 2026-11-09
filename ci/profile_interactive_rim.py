"""Read the normal DOL's Y-triggered profiler after uninterrupted rendering."""
import csv
import hashlib
import json
import statistics
import struct
import subprocess
import time


def collect(frames, dol, root, press, release):
    from profile_videomode import FIELDS
    symbols = {}
    table = subprocess.check_output(['nm', '-a', str(dol.with_suffix('.elf'))], text=True)
    for line in table.splitlines():
        parts = line.split()
        if len(parts) == 3:
            name = parts[2].split('.lto_priv.')[0]
            if name == 'dnd_value_heap':
                symbols['HeapPointer'] = int(parts[0], 16)
            for field in ('Count', 'Ready', 'Samples'):
                if name == 'dnd_static_IplFrameProfiler_' + field:
                    symbols[field] = int(parts[0], 16)
    assert {'Count', 'Ready', 'Samples'} <= symbols.keys()
    conn = frames.connection
    press()
    assert frames.request(conn, f'z0,{frames.address:x},4') == 'OK'
    frames.send(conn, 'c')
    time.sleep(.2)
    release()
    # No debugger stops, screenshots, frame dumping or app storage I/O during
    # the 30-frame warmup and 96-sample window. VSync remains enabled.
    time.sleep(30)
    assert frames.request(conn, f'Z0,{frames.address:x},4') == 'OK'
    frames.request(conn, 'c', 45)
    frames.stable_frame()
    integer = lambda address: int.from_bytes(frames.read_bytes(address, 4), 'big')
    count, ready = integer(symbols['Count']), integer(symbols['Ready'])
    if count != 96 or not ready:
        raise RuntimeError(f'Uninterrupted profile incomplete: {count}/96, ready={ready}')
    pointer = integer(symbols['Samples'])
    header = struct.unpack('>6I', frames.read_bytes(pointer, 24))
    assert header[2:4] == (96 * len(FIELDS), 4)
    values = struct.unpack('>' + 'i' * (96 * len(FIELDS)), frames.read_bytes(pointer + 24, 96 * len(FIELDS) * 4))
    rows = [dict(zip(FIELDS, values[i:i + len(FIELDS)])) for i in range(0, len(values), len(FIELDS))]
    assert all(r['frame_us'] > 0 and r['face'] == 1 and not r['editing'] for r in rows)
    with (root / 'frames.csv').open('w') as out:
        writer = csv.DictWriter(out, fieldnames=FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    result = {'scope': 'Dolphin guest clock; normal interactive Video face, idle motion enabled; not hardware',
              'samples': 96, 'warmup': 30, 'vsync': True, 'interrupted_during_sampling': False,
              'dol_sha256': hashlib.sha256(dol.read_bytes()).hexdigest(),
              'mean_us': {k: statistics.mean(r[k] for r in rows) for k in FIELDS[:12]},
              'frame_p95_us': sorted(r['frame_us'] for r in rows)[91],
              'active_mean_us': statistics.mean(r['frame_us'] - r['retrace_us'] for r in rows)}
    if 'HeapPointer' in symbols:
        heap = integer(symbols['HeapPointer'])
        start, capacity, used, blocks, free_list, collections = struct.unpack('>6I', frames.read_bytes(heap, 24))
        assert 0 < used <= capacity <= 262144
        result['managed_heap'] = {'capacity_bytes': capacity, 'arena_used_bytes': used, 'collections': collections,
                                  'scope': 'post-window arena; not peak or live bytes'}
    (root / 'profile.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result), flush=True)
