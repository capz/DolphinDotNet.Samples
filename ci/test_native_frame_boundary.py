#!/usr/bin/env python3
"""Socket-level integrity tests for the read-only native XFB capture helper."""
from contextlib import contextmanager
import hashlib
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


if __name__=='__main__':unittest.main()
