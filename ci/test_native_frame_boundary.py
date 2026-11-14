#!/usr/bin/env python3
"""Socket-level integrity tests for the read-only native XFB capture helper."""
from contextlib import contextmanager
import hashlib
import re
import socket
import threading
import time
import unittest

from native_frame_boundary import NativeFrames, _memory_reply


def packet(payload):
    return b'$'+payload+b'#'+format(sum(payload)&255,'02x').encode('ascii')


def exact(connection,length):
    data=bytearray()
    while len(data)<length:
        part=connection.recv(length-len(data))
        if not part:raise ConnectionError('Test peer closed')
        data.extend(part)
    return bytes(data)


@contextmanager
def sockets():
    local,peer=socket.socketpair()
    local.settimeout(1);peer.settimeout(1)
    try:yield local,peer
    finally:local.close();peer.close()


class CountingSocket:
    def __init__(self,connection):self.connection=connection;self.receives=0
    def recv(self,*args):self.receives+=1;return self.connection.recv(*args)
    def settimeout(self,value):self.connection.settimeout(value)
    def sendall(self,value):self.connection.sendall(value)


class NativeMemoryPackets(unittest.TestCase):
    def test_fragmented_header_payload_and_checksum(self):
        with sockets() as (local,peer):
            errors=[]
            def send_fragments():
                try:
                    wire=b'+-'+packet(b'00aB10ff')
                    for part in (wire[:1],wire[1:3],wire[3:6],wire[6:-2],wire[-2:-1],wire[-1:]):
                        peer.sendall(part);time.sleep(.003)
                    self.assertEqual(exact(peer,1),b'+')
                except BaseException as error:errors.append(error)
            sender=threading.Thread(target=send_fragments);sender.start()
            self.assertEqual(_memory_reply(local,time.monotonic()+1),b'00aB10ff')
            sender.join(2);self.assertFalse(sender.is_alive())
            if errors:raise errors[0]

    def test_packet_limit_and_next_reply_are_preserved(self):
        with sockets() as (local,peer):
            payload=b'ab'*4096;next_packet=packet(b'OK')
            peer.sendall(b'+'+packet(payload)+next_packet)
            self.assertEqual(_memory_reply(local,time.monotonic()+1),payload)
            self.assertEqual(exact(peer,1),b'+')
            self.assertEqual(local.recv(len(next_packet),socket.MSG_PEEK),next_packet)
            self.assertEqual(_memory_reply(local,time.monotonic()+1),b'OK')
            self.assertEqual(exact(peer,1),b'+')

    def test_corrupt_packets_are_not_acknowledged(self):
        for wire in (b'$00#zz',b'$00#00',b'$00#+a',b'!'):
            with self.subTest(wire=wire),sockets() as (local,peer):
                peer.sendall(wire)
                with self.assertRaises(ValueError):_memory_reply(local,time.monotonic()+1)
                peer.settimeout(.01)
                with self.assertRaises(TimeoutError):peer.recv(1)

    def test_oversize_truncation_and_timeout_fail_closed(self):
        with sockets() as (local,peer):
            peer.sendall(b'$'+b'0'*8193+b'#00')
            with self.assertRaisesRegex(ValueError,'Oversized'):_memory_reply(local,time.monotonic()+1)
        for wire in (b'$',b'$0000',b'$00#',b'$00#6'):
            with self.subTest(wire=wire),sockets() as (local,peer):
                peer.sendall(wire);peer.shutdown(socket.SHUT_WR)
                with self.assertRaises(ConnectionError):_memory_reply(local,time.monotonic()+1)
        with sockets() as (local,peer):
            with self.assertRaises(TimeoutError):_memory_reply(local,time.monotonic()+.01)

    @staticmethod
    def frames(connection):
        frames=NativeFrames.__new__(NativeFrames);frames.connection=connection
        frames.send=lambda conn,command:conn.sendall(packet(command.encode('ascii')))
        frames.request=lambda *args:(_ for _ in ()).throw(AssertionError('Memory reads must use the fast path'))
        return frames

    def test_full_xfb_integrity_with_unsolicited_replies(self):
        # A real 640x480 YUYV buffer, so the test exercises every packet rather
        # than inferring whole-frame integrity from a small header read.
        expected=bytes(range(256))*2400
        with sockets() as (local,peer):
            counted=CountingSocket(local);frames=self.frames(counted);errors=[]
            def memory_server():
                try:
                    for offset in range(0,len(expected),2048):
                        command=bytearray()
                        self.assertEqual(exact(peer,1),b'$')
                        while True:
                            byte=exact(peer,1)
                            if byte==b'#':break
                            command.extend(byte)
                        self.assertEqual(exact(peer,2),format(sum(command)&255,'02x').encode('ascii'))
                        n=min(2048,len(expected)-offset)
                        self.assertEqual(command.decode('ascii'),f'm{0x80010000+offset:x},{n:x}')
                        data=expected[offset:offset+n].hex().encode('ascii')
                        extra=packet(b'')+packet(b'T0540:80000100;') if offset==0 else b''
                        peer.sendall(b'+'+extra+packet(data))
                        self.assertEqual(exact(peer,3 if extra else 1),b'+++' if extra else b'+')
                except BaseException as error:errors.append(error)
            sender=threading.Thread(target=memory_server);sender.start()
            actual=frames.read_bytes(0x80010000,len(expected))
            sender.join(2);self.assertFalse(sender.is_alive())
            if errors:raise errors[0]
            self.assertEqual(actual,expected)
            self.assertEqual(hashlib.sha256(actual).digest(),hashlib.sha256(expected).digest())
            self.assertLess(counted.receives,10000,'Capture must use chunk reads, not one recv per hex digit')

    def test_memory_payload_length_hex_and_address_bounds(self):
        for reply in (b'E01',b'00',b'gggg',b'000000'):
            with self.subTest(reply=reply),sockets() as (local,peer):
                frames=self.frames(local);peer.sendall(packet(reply))
                with self.assertRaisesRegex(RuntimeError,'Incomplete native framebuffer read'):frames.read_bytes(0x80000000,2)
        frames=NativeFrames.__new__(NativeFrames)
        self.assertEqual(frames.read_bytes(0xffffffff,0),b'')
        for address,length in ((-1,1),(0,-1),(0x100000000,0),(0xffffffff,2),('address',4),(0,1.5)):
            with self.subTest(address=address,length=length):
                with self.assertRaises(ValueError):frames.read_bytes(address,length)


class NativeBoundaryProtocol(unittest.TestCase):
    """Exercise client/server ordering with real packets, without an emulator."""
    address=0x80012340
    serial=0x80300000

    def frames(self,connection):
        frames=NativeFrames.__new__(NativeFrames)
        frames.connection=connection;frames.address=self.address;frames.serial=self.serial;frames.count=10
        frames.commands=[]
        def send(conn,command):
            # These are the only controls needed here. In particular, reject
            # register/memory writes and raw interrupts at the protocol boundary.
            self.assertIn(command,('c',f'z0,{self.address:x},4',f'Z0,{self.address:x},4',f'm{self.serial:x},4'))
            frames.commands.append(command);conn.sendall(packet(command.encode('ascii')))
        frames.send=send
        frames.reply=lambda conn,deadline:_memory_reply(conn,deadline).decode('ascii')
        def request(conn,command,seconds=1):
            send(conn,command)
            return frames.reply(conn,time.monotonic()+seconds)
        frames.request=request
        def parse_stop_pc(reply):
            match=re.fullmatch(r'T[0-9a-fA-F]{2}40:([0-9a-fA-F]{8});.*',reply)
            if not match:raise ValueError('Missing PC in test stop reply')
            return int(match[1],16)
        frames.parse_stop_pc=parse_stop_pc
        return frames

    def command(self,peer,expected):
        self.assertEqual(exact(peer,1),b'$')
        data=bytearray()
        while True:
            byte=exact(peer,1)
            if byte==b'#':break
            data.extend(byte)
        self.assertEqual(exact(peer,2),format(sum(data)&255,'02x').encode('ascii'))
        self.assertEqual(data.decode('ascii'),expected)

    def reply(self,peer,payload):
        peer.sendall(b'+'+packet(payload))
        self.assertEqual(exact(peer,1),b'+')

    def quiet(self,peer):
        peer.settimeout(.02)
        try:
            with self.assertRaises(TimeoutError):peer.recv(1)
        finally:peer.settimeout(1)

    def run_protocol(self,scenario,action):
        with sockets() as (local,peer):
            frames=self.frames(local);errors=[]
            def serve():
                try:scenario(peer)
                except BaseException as error:
                    errors.append(error)
                    peer.shutdown(socket.SHUT_RDWR)
            worker=threading.Thread(target=serve);worker.start()
            try:action(frames)
            finally:
                worker.join(2)
                if worker.is_alive():
                    local.shutdown(socket.SHUT_RDWR);worker.join(2)
            self.assertFalse(worker.is_alive())
            if errors:raise errors[0]
            return frames

    def test_pause_consumes_ok_and_stop_in_either_order(self):
        stop=f'T0540:{self.address:08x};01:81700000;'.encode('ascii')
        for replies in ((b'OK',stop),(stop,b'OK')):
            with self.subTest(first=replies[0]):
                def scenario(peer):
                    self.command(peer,f'Z0,{self.address:x},4')
                    self.reply(peer,b'')
                    self.reply(peer,replies[0])
                    # Reading the counter after just OK or just T05 would
                    # falsely claim that rearming and pausing both completed.
                    self.quiet(peer)
                    self.reply(peer,replies[1])
                    self.command(peer,f'm{self.serial:x},4')
                    self.reply(peer,b'0000002a')
                    self.quiet(peer)
                frames=self.run_protocol(scenario,lambda frames:frames.pause_at_boundary())
                self.assertEqual(frames.count,42)
                self.assertEqual(frames.commands,[f'Z0,{self.address:x},4',f'm{self.serial:x},4'])

    def test_pause_rejects_wrong_stop_or_breakpoint_error(self):
        for bad in (f'T0540:{self.address+4:08x};'.encode('ascii'),b'E01',b'T0240:80012340;'):
            with self.subTest(reply=bad):
                def scenario(peer):
                    self.command(peer,f'Z0,{self.address:x},4')
                    self.reply(peer,bad);self.quiet(peer)
                def action(frames):
                    with self.assertRaisesRegex(RuntimeError,'Unexpected'):
                        frames.pause_at_boundary()
                    self.assertEqual(frames.count,10)
                frames=self.run_protocol(scenario,action)
                self.assertEqual(frames.commands,[f'Z0,{self.address:x},4'])

    def test_pause_requires_both_acceptance_and_stop(self):
        for only in (b'OK',f'T0540:{self.address:08x};'.encode('ascii')):
            with self.subTest(only=only):
                def scenario(peer):
                    self.command(peer,f'Z0,{self.address:x},4')
                    self.reply(peer,only);peer.shutdown(socket.SHUT_WR)
                def action(frames):
                    with self.assertRaises(ConnectionError):frames.pause_at_boundary()
                    self.assertEqual(frames.count,10)
                frames=self.run_protocol(scenario,action)
                self.assertEqual(frames.commands,[f'Z0,{self.address:x},4'])

    def rearmed_stop(self,peer,address):
        self.command(peer,f'z0,{self.address:x},4');self.reply(peer,b'OK')
        self.command(peer,'c')
        self.command(peer,f'Z0,{self.address:x},4');self.reply(peer,b'OK')
        self.reply(peer,f'T0540:{address:08x};'.encode('ascii'))

    def test_repeated_advance_rearms_without_resuming_a_queued_stop(self):
        def scenario(peer):
            for serial in range(11,15):
                self.rearmed_stop(peer,self.address)
                self.command(peer,f'm{self.serial:x},4')
                self.reply(peer,f'{serial:08x}'.encode('ascii'))
            self.quiet(peer)
        def action(frames):
            frames.advance(0);frames.advance(-1);self.assertEqual(frames.count,10)
            frames.advance(2);self.assertEqual(frames.count,12)
            frames.advance(2);self.assertEqual(frames.count,14)
        frames=self.run_protocol(scenario,action)
        self.assertEqual(frames.commands,[f'z0,{self.address:x},4','c',f'Z0,{self.address:x},4',f'm{self.serial:x},4']*4)

    def test_advance_rejects_duplicate_and_backwards_serials(self):
        for bad_serial in (11,10):
            with self.subTest(serial=bad_serial):
                def scenario(peer):
                    for serial in (11,bad_serial):
                        self.rearmed_stop(peer,self.address)
                        self.command(peer,f'm{self.serial:x},4')
                        self.reply(peer,f'{serial:08x}'.encode('ascii'))
                    self.quiet(peer)
                def action(frames):
                    with self.assertRaisesRegex(RuntimeError,'counter did not advance'):frames.advance(2)
                self.run_protocol(scenario,action)

    def test_advance_rejects_wrong_pc_before_reading_counter(self):
        def scenario(peer):
            self.rearmed_stop(peer,self.address+4);self.quiet(peer)
        def action(frames):
            with self.assertRaisesRegex(RuntimeError,'Unexpected native presentation stop'):frames.advance(1)
            self.assertEqual(frames.count,10)
        frames=self.run_protocol(scenario,action)
        self.assertEqual(frames.commands,[f'z0,{self.address:x},4','c',f'Z0,{self.address:x},4'])


if __name__=='__main__':unittest.main()
