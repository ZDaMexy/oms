"""Exercise reference navigation locally, without opening any game or socket."""

import base64
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from itertools import repeat
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import reference_api


class FakeConnection:
    def __init__(self, data):
        self.data = data
        self.requests = []
        self.pending = b""

    def sendall(self, encoded):
        request = json.loads(encoded[:-1])
        self.requests.append(request)
        self.pending = json.dumps({"id": request["id"], "errors": [], "data": self.data}).encode() + b"\0"

    def recv(self, maximum):
        chunk, self.pending = self.pending[:maximum], self.pending[maximum:]
        return chunk


class ReferenceApiTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="oms-reference-api-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.capture_root = self.root / "captures"
        self.capture_root.mkdir()
        self.session_path = self.root / "session.json"
        self.session_path.write_text(json.dumps({
            "host": "127.0.0.1", "port": 21338, "password": "test-only-placeholder",
            "captureRoot": str(self.capture_root),
        }), encoding="utf-8")
        self.api = reference_api.ReferenceApi(self.session_path)
        self.image = b"\xff\xd8\xff\xd9"
        self.connection = FakeConnection([1234, 960, 540, base64.b64encode(self.image).decode()])
        self.api.connection = self.connection
        # Encryption is outside these tests; zero XOR lets the fake inspect the wire request.
        self.api.cipher = repeat(0)
        self.socket_guard = patch.object(reference_api.socket, "create_connection", side_effect=AssertionError("Tests must not connect to a game"))
        self.socket_guard.start()
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
