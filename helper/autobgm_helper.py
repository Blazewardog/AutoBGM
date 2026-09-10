#!/usr/bin/env python3
"""Publish MPRIS playback status to the Wine plugin over loopback TCP."""
import argparse
import asyncio
import contextlib
import os
import shutil
import time


def parse_status(returncode: int, stdout: str, stderr: str) -> bytes:
    statuses = stdout.splitlines()
    if returncode == 0 and statuses and all(s in ('Playing', 'Paused', 'Stopped') for s in statuses):
        return b'1' if 'Playing' in statuses else b'0'
    if returncode == 1 and 'No players found' in stderr:
        return b'0'
    return b'?'


async def query_status(players: str, ignore: str) -> bytes:
    args = ['playerctl', '--all-players']
    if players:
        args += ['--player', players]
    if ignore:
        args += ['--ignore-player', ignore]
    process = await asyncio.create_subprocess_exec(
        *args, 'status', stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE,
        env={**os.environ, 'LC_ALL': 'C'})
    try:
        stdout, stderr = await asyncio.wait_for(process.communicate(), timeout=2)
        return parse_status(process.returncode, stdout.decode(), stderr.decode())
    finally:
        if process.returncode is None:
            with contextlib.suppress(ProcessLookupError):
                process.kill()
            await process.wait()


class Bridge:
    MAX_CLIENTS = 16

    def __init__(self, players='', ignore=''):
        self.players = players
        self.ignore = ignore
        self.state = b'?'
        self.updated = 0.0
        self.clients = set()

    async def poll(self):
        while True:
            try:
                self.state = await query_status(self.players, self.ignore)
            except (OSError, asyncio.TimeoutError, UnicodeError):
                self.state = b'?'
            self.updated = time.monotonic()
            await asyncio.sleep(0.5)

    async def serve_client(self, reader, writer):
        if len(self.clients) >= self.MAX_CLIENTS:
            writer.close()
            return
        self.clients.add(writer)
        try:
            while not reader.at_eof():
                state = self.state if time.monotonic() - self.updated < 3 else b'?'
                writer.write(state)
                await asyncio.wait_for(writer.drain(), timeout=2)
                # This is a send-only protocol. Reject input (including HTTP
                # requests) and detect closed clients without retaining them.
                try:
                    await asyncio.wait_for(reader.read(1), timeout=0.5)
                    break
                except asyncio.TimeoutError:
                    pass
        except (ConnectionError, asyncio.TimeoutError, OSError):
            pass
        finally:
            self.clients.discard(writer)
            writer.close()
            with contextlib.suppress(ConnectionError, OSError):
                await writer.wait_closed()


async def run(args):
    bridge = Bridge(args.players, args.ignore)
    server = await asyncio.start_server(bridge.serve_client, '127.0.0.1', args.port, limit=64)
    print(f'Auto BGM helper listening on 127.0.0.1:{args.port}', flush=True)
    async with server:
        await asyncio.gather(bridge.poll(), server.serve_forever())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=37984)
    parser.add_argument('--players', default='', help='Comma-separated playerctl player names; empty means all')
    parser.add_argument('--ignore', default='', help='Comma-separated playerctl player names to exclude')
    args = parser.parse_args()
    if not 1024 <= args.port <= 65535:
        parser.error('--port must be between 1024 and 65535')
    if not shutil.which('playerctl'):
        parser.error('Install playerctl using your distribution package manager first')
    try:
        asyncio.run(run(args))
    except KeyboardInterrupt:
        pass


if __name__ == '__main__':
    main()
