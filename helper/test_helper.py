import asyncio
import time
import unittest
from unittest.mock import AsyncMock, patch
from autobgm_helper import Bridge, parse_status, query_status


class StatusTests(unittest.TestCase):
    def test_any_player_playing(self):
        self.assertEqual(parse_status(0, 'Paused\nPlaying\nStopped\n', ''), b'1')

    def test_paused_and_stopped(self):
        self.assertEqual(parse_status(0, 'Paused\nStopped\n', ''), b'0')

    def test_players_closed(self):
        self.assertEqual(parse_status(1, '', 'No players found'), b'0')

    def test_failure_is_unknown(self):
        for code, out, err in [(1, '', 'D-Bus unavailable'), (0, 'garbage', ''), (0, '', '')]:
            self.assertEqual(parse_status(code, out, err), b'?')


class AsyncTests(unittest.IsolatedAsyncioTestCase):
    async def test_rejects_excess_clients_and_incoming_requests(self):
        bridge = Bridge()
        bridge.MAX_CLIENTS = 1
        server = await asyncio.start_server(bridge.serve_client, '127.0.0.1', 0)
        port = server.sockets[0].getsockname()[1]
        async with server:
            reader, writer = await asyncio.open_connection('127.0.0.1', port)
            self.assertEqual(await asyncio.wait_for(reader.readexactly(1), 2), b'?')
            extra_reader, extra_writer = await asyncio.open_connection('127.0.0.1', port)
            self.assertEqual(await asyncio.wait_for(extra_reader.read(1), 2), b'')
            self.assertEqual(len(bridge.clients), 1)
            extra_writer.close()
            await extra_writer.wait_closed()
            writer.write(b'GET / HTTP/1.0\r\n\r\n')
            await writer.drain()
            self.assertEqual(await asyncio.wait_for(reader.read(1), 2), b'')
            writer.close()
            await writer.wait_closed()
            self.assertFalse(bridge.clients)

    async def test_timeout_reaps_playerctl(self):
        process = AsyncMock()
        process.returncode = None
        process.kill = unittest.mock.Mock()
        process.communicate.side_effect = asyncio.TimeoutError
        with patch('asyncio.create_subprocess_exec', return_value=process):
            with self.assertRaises(asyncio.TimeoutError):
                await query_status('spotify', 'firefox')
        process.kill.assert_called_once()
        process.wait.assert_awaited_once()

    async def test_socket_transitions_staleness_and_reconnect(self):
        bridge = Bridge()
        server = await asyncio.start_server(bridge.serve_client, '127.0.0.1', 0)
        port = server.sockets[0].getsockname()[1]
        async with server:
            for _ in range(2):
                reader, writer = await asyncio.open_connection('127.0.0.1', port)
                self.assertEqual(await asyncio.wait_for(reader.readexactly(1), 2), b'?')
                for state in (b'1', b'0'):
                    bridge.state, bridge.updated = state, time.monotonic()
                    self.assertEqual(await asyncio.wait_for(reader.readexactly(1), 2), state)
                bridge.updated = 0
                self.assertEqual(await asyncio.wait_for(reader.readexactly(1), 2), b'?')
                writer.close()
                await writer.wait_closed()
        await asyncio.sleep(0.6)
        self.assertFalse(bridge.clients)


if __name__ == '__main__':
    unittest.main()
