"""Exercise reference navigation locally, without opening any game or socket."""

import base64
import binascii
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from itertools import cycle
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import reference_api

DUMMY_PASSWORD = "0123456789abcdef" * 3
DUMMY_KEY = DUMMY_PASSWORD.encode()


class LegacyCipher:
    """Independent old generator acts as the fake server's continuous wire state."""
    def __init__(self, key=DUMMY_KEY):
        self.stream = reference_api.rc4(key)

    def encrypt(self, data):
        return bytes(value ^ next(self.stream) for value in data)


class FakeConnection:
    def __init__(self, data):
        self.data = data
        self.requests = []
        self.encoded_requests = []
        self.pending = b""
        self.offset = 0
        self.server_cipher = LegacyCipher()
        self.fragment_sizes = cycle([65536])
        self.id_offset = 0
        self.errors = []
        self.raw_response = None
        self.receive_error = None
        self.closed = False

    def sendall(self, encoded):
        if self.offset != len(self.pending):
            raise AssertionError("A new request arrived before the previous response was consumed")
        self.encoded_requests.append(encoded)
        plain = self.server_cipher.encrypt(encoded)
        if not plain.endswith(b"\0"):
            raise AssertionError("The request cipher lost its terminator")
        request = json.loads(plain[:-1])
        self.requests.append(request)
        response = self.raw_response if self.raw_response is not None else json.dumps(
            {"id": request["id"] + self.id_offset, "errors": self.errors, "data": self.data}, ensure_ascii=False).encode() + b"\0"
        self.pending = self.server_cipher.encrypt(response)
        self.offset = 0

    def recv(self, maximum):
        if self.receive_error is not None:
            raise self.receive_error
        length = min(maximum, next(self.fragment_sizes))
        chunk = self.pending[self.offset:self.offset + length]
        self.offset += len(chunk)
        return chunk

    def close(self):
        self.closed = True


class ReferenceApiTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="oms-reference-api-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.capture_root = self.root / "captures"
        self.capture_root.mkdir()
        self.session_path = self.root / "session.json"
        self.session_path.write_text(json.dumps({
            "host": "127.0.0.1", "port": 21338, "password": DUMMY_PASSWORD,
            "captureRoot": str(self.capture_root),
        }), encoding="utf-8")
        self.api = reference_api.ReferenceApi(self.session_path)
        self.image = b"\xff\xd8\xff\xd9"
        self.connection = FakeConnection([1234, 960, 540, base64.b64encode(self.image).decode()])
        self.api.connection = self.connection
        self.api.cipher = reference_api.ARC4.new(DUMMY_KEY, drop=0)
        self.socket_guard = patch.object(reference_api.socket, "create_connection", side_effect=AssertionError("Tests must not connect to a game"))
        self.socket_mock = self.socket_guard.start()
        self.addCleanup(self.socket_guard.stop)

    def test_default_capture_keeps_full_quality_and_resolution(self):
        output = self.capture_root / "default.jpg"
        receipt = self.api.capture(output)
        self.assertEqual(self.connection.requests[0]["params"], [0, 100, 1])
        self.assertEqual(output.read_bytes(), self.image)
        self.assertEqual(receipt["path"], str(output.resolve()))
        self.assertEqual((receipt["quality"], receipt["divide"]), (100, 1))

    def test_capture_sends_quality_and_downsampling_boundaries(self):
        for index, (quality, divide) in enumerate([(1, 1), (100, 8), (70, 2)]):
            with self.subTest(quality=quality, divide=divide):
                output = self.capture_root / f"frame-{index}.jpg"
                receipt = self.api.capture(output, screen=1, quality=quality, divide=divide)
                request = self.connection.requests[-1]
                self.assertEqual((request["module"], request["function"]), ("capture", "get_jpg"))
                self.assertEqual(request["params"], [1, quality, divide])
                self.assertEqual((receipt["timestamp"], receipt["width"], receipt["height"]), (1234, 960, 540))
                self.assertEqual(output.read_bytes(), self.image)

    def test_invalid_capture_quality_does_not_request_a_frame(self):
        for value in [-1, 0, 101, True, 1.5, "70", None]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.api.capture(self.capture_root / "invalid.jpg", quality=value)
        self.assertEqual(self.connection.requests, [])
        self.assertEqual(list(self.capture_root.iterdir()), [])

    def test_invalid_capture_divide_does_not_request_a_frame(self):
        for value in [-1, 0, 9, False, 2.0, "2", None]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.api.capture(self.capture_root / "invalid.jpg", divide=value)
        self.assertEqual(self.connection.requests, [])
        self.assertEqual(list(self.capture_root.iterdir()), [])

    def test_capture_rejects_paths_outside_evidence_before_requesting(self):
        for output in [self.root / "outside.jpg", self.capture_root / ".." / "traversal.jpg", self.capture_root]:
            with self.subTest(output=output), self.assertRaises(ValueError):
                self.api.capture(output)
        self.assertEqual(self.connection.requests, [])
        self.assertFalse((self.root / "outside.jpg").exists())
        self.assertFalse((self.root / "traversal.jpg").exists())

    def test_capture_requires_an_existing_parent(self):
        with self.assertRaises(ValueError):
            self.api.capture(self.capture_root / "missing" / "frame.jpg")
        self.assertEqual(self.connection.requests, [])
        self.assertFalse((self.capture_root / "missing").exists())

    def test_capture_preserves_existing_output_without_requesting(self):
        output = self.capture_root / "retained.jpg"
        output.write_bytes(b"retained evidence")
        with self.assertRaises(FileExistsError):
            self.api.capture(output)
        self.assertEqual(output.read_bytes(), b"retained evidence")
        self.assertEqual(self.connection.requests, [])

    def test_unready_capture_does_not_create_an_output(self):
        self.connection.data = []
        output = self.capture_root / "unready.jpg"
        with self.assertRaises(RuntimeError):
            self.api.capture(output, quality=70, divide=2)
        self.assertFalse(output.exists())

    def test_public_legacy_generator_and_native_48_byte_key_are_byte_equal(self):
        self.assertEqual(len(DUMMY_KEY), 48)
        self.assertEqual(LegacyCipher(b"Key").encrypt(b"Plaintext"), bytes.fromhex("bbf316e8d940af0ad3"))
        payload = bytes(range(256)) * 273
        expected = LegacyCipher().encrypt(payload)
        self.assertEqual(reference_api.ARC4.new(DUMMY_KEY, drop=0).encrypt(payload), expected)
        # All cut points around a complete256-byte RC4 state cycle, including empty ends.
        short = payload[:257]
        for cut in range(len(short) + 1):
            with self.subTest(cut=cut):
                cipher = reference_api.ARC4.new(DUMMY_KEY, drop=0)
                self.assertEqual(cipher.encrypt(short[:cut]) + cipher.encrypt(short[cut:]), expected[:len(short)])
        cipher = reference_api.ARC4.new(DUMMY_KEY, drop=0)
        self.assertEqual(cipher.encrypt(payload[:65536]) + cipher.encrypt(b"") + cipher.encrypt(payload[65536:]), expected)

    def test_real_call_keeps_one_shared_stream_across_fragments_and_requests(self):
        original_cipher = self.api.cipher
        self.connection.fragment_sizes = cycle([1, 2, 7, 65536, 13, 4096])
        jpeg_data = [1234, 1920, 1080, base64.b64encode(bytes(range(256)) * 300).decode()]
        calls = [("capture", "get_jpg", [0, 100, 1], jpeg_data),
                 ("buttons", "read", [], [["P1 1", 0, False]]),
                 ("capture", "get_jpg", [0, 70, 2], [1235, 960, 540, base64.b64encode(self.image).decode()])]
        for module, function, params, data in calls:
            self.connection.data = data
            self.assertEqual(self.api.call(module, function, params), data)
            self.assertIs(self.api.cipher, original_cipher)
        self.assertEqual([request["id"] for request in self.connection.requests], [1, 2, 3])
        # The independent generator/server and the C/client finish at the exact same byte.
        following = bytes(range(256))
        self.assertEqual(self.api.cipher.encrypt(following), self.connection.server_cipher.encrypt(following))

    def test_single_byte_recv_can_split_utf8_json_and_the_terminator(self):
        self.connection.fragment_sizes = cycle([1])
        self.connection.data = ["键盘 🎹", "nul is escaped: \0"]
        self.assertEqual(self.api.call("capture", "get_screens"), self.connection.data)

    def test_temporary_connections_have_fresh_streams_and_close_after_each_call(self):
        self.api.connection = self.api.cipher = None
        connections = [FakeConnection([1]), FakeConnection([2])]
        with patch.object(reference_api.socket, "create_connection", side_effect=connections) as connect:
            for expected, connection in enumerate(connections, 1):
                self.assertEqual(self.api.call("capture", "get_screens"), [expected])
                self.assertTrue(connection.closed)
                self.assertIsNone(self.api.connection)
                self.assertIsNone(self.api.cipher)
            self.assertEqual(connect.call_count, 2)
            connect.assert_called_with(("127.0.0.1", 21338), timeout=8)
        self.assertEqual([connection.requests[0]["id"] for connection in connections], [1, 2])

    def test_context_manager_reuses_connection_and_closes_on_response_error(self):
        self.api.connection = self.api.cipher = None
        connection = FakeConnection([0])
        with patch.object(reference_api.socket, "create_connection", return_value=connection) as connect:
            with self.assertRaises(RuntimeError), self.api as api:
                cipher = api.cipher
                self.assertEqual(api.call("capture", "get_screens"), [0])
                self.assertEqual(api.call("capture", "get_screens"), [0])
                self.assertIs(api.cipher, cipher)
                connection.errors = ["offline response error"]
                api.call("capture", "get_screens")
            connect.assert_called_once_with(("127.0.0.1", 21338), timeout=8)
        self.assertTrue(connection.closed)
        self.assertIsNone(self.api.connection)
        self.assertIsNone(self.api.cipher)

    def test_an_open_connection_cannot_be_entered_again(self):
        with self.assertRaisesRegex(RuntimeError, "already open"):
            self.api.__enter__()
        self.socket_mock.assert_not_called()

    def test_eof_before_the_reply_terminator_closes_a_temporary_connection(self):
        self.api.connection = self.api.cipher = None
        connection = FakeConnection([])
        connection.raw_response = b'{"id":1,"errors":[],"data":[]}'
        with patch.object(reference_api.socket, "create_connection", return_value=connection):
            with self.assertRaisesRegex(RuntimeError, "closed its connection"):
                self.api.call("capture", "get_screens")
        self.assertTrue(connection.closed)
        self.assertIsNone(self.api.connection)
        self.assertIsNone(self.api.cipher)

    def test_wrong_response_id_and_server_errors_never_return_data(self):
        for wrong_id in (True, False):
            with self.subTest(wrong_id=wrong_id):
                self.connection.id_offset = 1 if wrong_id else 0
                self.connection.errors = [] if wrong_id else ["offline failure"]
                with self.assertRaisesRegex(RuntimeError, "Reference API capture.get_screens"):
                    self.api.call("capture", "get_screens")

    def test_parse_and_transport_failures_keep_temporary_cleanup(self):
        for transport in (False, True):
            with self.subTest(transport=transport):
                self.api.connection = self.api.cipher = None
                connection = FakeConnection([])
                connection.raw_response = b"not-json\0"
                connection.receive_error = ConnectionError("offline receive failure") if transport else None
                with patch.object(reference_api.socket, "create_connection", return_value=connection):
                    with self.assertRaises(ConnectionError if transport else json.JSONDecodeError):
                        self.api.call("capture", "get_screens")
                self.assertTrue(connection.closed)
                self.assertIsNone(self.api.connection)
                self.assertIsNone(self.api.cipher)

    def test_connect_failure_leaves_no_partial_client_state(self):
        self.api.connection = self.api.cipher = None
        with patch.object(reference_api.socket, "create_connection", side_effect=OSError("offline connect failure")):
            with self.assertRaises(OSError):
                self.api.call("capture", "get_screens")
        self.assertIsNone(self.api.connection)
        self.assertIsNone(self.api.cipher)

    def test_invalid_native_key_fails_before_a_socket_is_opened(self):
        self.api.connection = self.api.cipher = None
        self.api.session["password"] = DUMMY_PASSWORD * 6
        with self.assertRaises(ValueError):
            self.api.call("capture", "get_screens")
        self.assertIsNone(self.api.connection)
        self.assertIsNone(self.api.cipher)
        self.socket_mock.assert_not_called()

    def test_encrypted_reply_still_enforces_the_32_mib_limit(self):
        self.connection.server_cipher = reference_api.ARC4.new(DUMMY_KEY, drop=0)
        self.connection.raw_response = b"x" * (32 * 1024 * 1024 + 1)
        with self.assertRaisesRegex(RuntimeError, "response exceeded the capture budget"):
            self.api.call("capture", "get_screens")

    def test_invalid_capture_base64_does_not_create_a_screenshot(self):
        self.connection.data = [1234, 1920, 1080, "@@not-base64@@"]
        output = self.capture_root / "invalid-base64.jpg"
        with self.assertRaises(binascii.Error):
            self.api.capture(output)
        self.assertFalse(output.exists())

    def test_nonlocal_or_empty_password_sessions_fail_before_connecting(self):
        for host, password in [("localhost", DUMMY_PASSWORD), ("::1", DUMMY_PASSWORD),
                               ("192.0.2.1", DUMMY_PASSWORD), ("127.0.0.1", "")]:
            with self.subTest(host=host, empty_password=not password):
                invalid = self.root / "dummy-invalid-session.json"
                invalid.write_text(json.dumps({"host": host, "port": 21338, "password": password}), encoding="utf-8")
                with self.assertRaises(ValueError):
                    reference_api.ReferenceApi(invalid)
        self.socket_mock.assert_not_called()

    def test_keypad_write_sends_separate_keypad_and_character_string(self):
        self.connection.data = []
        for keypad, keys in [(0, "0123456789AD"), (1, "2")]:
            with self.subTest(keypad=keypad):
                self.assertEqual(self.api.keypad_write(keypad, keys), [])
                request = self.connection.requests[-1]
                self.assertEqual((request["module"], request["function"]), ("keypads", "write"))
                self.assertEqual(request["params"], [keypad, keys])

    def test_keypad_rejects_invalid_indices_before_navigation(self):
        for value in [-1, 2, True, False, 0.0, "0", None]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.api.keypad_write(value, "2")
        self.assertEqual(self.connection.requests, [])

    def test_keypad_rejects_non_navigation_characters_before_navigation(self):
        for value in ["", "a", "d", "1 2", "\n", "２", "A0X", ["2"], 2, None]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.api.keypad_write(0, value)
        self.assertEqual(self.connection.requests, [])

    def test_raw_keypad_call_cannot_bypass_navigation_validation(self):
        for params in [[], [0], [0, "2", "3"], [2, "2"], [0, "X"]]:
            with self.subTest(params=params), self.assertRaises(ValueError):
                self.api.call("keypads", "write", params)
        self.assertEqual(self.connection.requests, [])

    def test_does_not_expose_cards_memory_or_process_control(self):
        operations = [("card", "get_cards"), ("card", "insert"), ("memory", "read"), ("memory", "write"),
                      ("control", "exit"), ("control", "restart"), ("control", "session_refresh"),
                      ("keypads", "set"), ("keypads", "get")]
        for module, function in operations:
            with self.subTest(module=module, function=function), self.assertRaises(ValueError):
                self.api.call(module, function)
        self.assertEqual(self.connection.requests, [])

    def test_capture_cli_forwards_explicit_quality_and_divide(self):
        output = self.capture_root / "cli.jpg"
        with patch.object(reference_api, "ReferenceApi") as api_class, redirect_stdout(StringIO()):
            api_class.return_value.capture.return_value = {"path": str(output)}
            reference_api.main(["--session", str(self.session_path), "capture", "--screen", "1", "--quality", "70", "--divide", "2", "--output", str(output)])
        api_class.return_value.capture.assert_called_once_with(output, 1, quality=70, divide=2)

    def test_capture_cli_defaults_are_unchanged(self):
        output = self.capture_root / "cli-default.jpg"
        with patch.object(reference_api, "ReferenceApi") as api_class, redirect_stdout(StringIO()):
            api_class.return_value.capture.return_value = {"path": str(output)}
            reference_api.main(["--session", str(self.session_path), "capture", "--output", str(output)])
        api_class.return_value.capture.assert_called_once_with(output, 0, quality=100, divide=1)

    def test_keypad_cli_forwards_navigation_without_echoing_keys(self):
        stdout = StringIO()
        with patch.object(reference_api, "ReferenceApi") as api_class, redirect_stdout(stdout):
            reference_api.main(["--session", str(self.session_path), "keypad-write", "--keypad", "1", "--keys", "AD2"])
        api_class.return_value.keypad_write.assert_called_once_with(1, "AD2")
        self.assertEqual(json.loads(stdout.getvalue()), {"keypad": 1, "characters": 3})

    def test_keypad_cli_rejects_an_invalid_index_before_opening_a_session(self):
        with patch.object(reference_api, "ReferenceApi") as api_class, redirect_stderr(StringIO()), self.assertRaises(SystemExit):
            reference_api.main(["--session", str(self.session_path), "keypad-write", "--keypad", "2", "--keys", "2"])
        api_class.assert_not_called()


if __name__ == "__main__":
    unittest.main()
