"""Capture and navigate the isolated IIDX reference through its local Spice API."""

import argparse
import base64
import json
from pathlib import Path
import socket
import time

from Crypto.Cipher import ARC4


def rc4(key):
    """Legacy keystream interface retained for existing offline/shutdown helpers."""
    state = list(range(256))
    offset = 0
    for index in range(256):
        offset = (offset + state[index] + key[index % len(key)]) % 256
        state[index], state[offset] = state[offset], state[index]
    index = offset = 0
    while True:
        index = (index + 1) % 256
        offset = (offset + state[index]) % 256
        state[index], state[offset] = state[offset], state[index]
        yield state[(state[index] + state[offset]) % 256]


class ReferenceApi:
    def __init__(self, session_path):
        self.session = json.loads(Path(session_path).read_text(encoding="utf-8"))
        if self.session["host"] != "127.0.0.1" or not self.session["password"]:
            raise ValueError("Reference API requires localhost and a per-run password")
        self.connection = None
        self.cipher = None
        self.request_id = 0

    def __enter__(self):
        if self.connection is not None:
            raise RuntimeError("Reference API connection is already open")
        cipher = ARC4.new(self.session["password"].encode(), drop=0)
        self.connection = socket.create_connection(("127.0.0.1", self.session["port"]), timeout=8)
        # The wire protocol shares one continuous state across both directions and calls.
        self.cipher = cipher
        return self

    def __exit__(self, exception_type, exception, traceback):
        self.connection.close()
        self.connection = self.cipher = None

    def call(self, module, function, params=()):
        allowed = {("capture", "get_screens"), ("capture", "get_jpg"),
                   ("buttons", "read"), ("buttons", "write"), ("buttons", "write_reset"),
                   ("analogs", "read"), ("analogs", "write"), ("analogs", "write_reset"),
                   ("coin", "insert"), ("touch", "write"), ("touch", "write_reset"),
                   ("keypads", "write")}
        if (module, function) not in allowed:
            raise ValueError("Operation is outside reference capture and navigation")
        params = list(params)
        if (module, function) == ("keypads", "write"):
            if len(params) != 2 or type(params[0]) is not int or params[0] not in (0, 1):
                raise ValueError("Keypad must be 0 (P1) or 1 (P2)")
            if not isinstance(params[1], str) or not params[1] or any(character not in "0123456789AD" for character in params[1]):
                raise ValueError("Keypad navigation accepts only a non-empty string of 0-9, A and D")
        self.request_id += 1
        request = json.dumps({"id": self.request_id, "module": module, "function": function,
                              "params": params}, separators=(",", ":")).encode() + b"\0"
        temporary_connection = self.connection is None
        if temporary_connection:
            self.__enter__()
        try:
            encoded = self.cipher.encrypt(request)
            self.connection.sendall(encoded)
            reply = bytearray()
            while not reply or reply[-1] != 0:
                chunk = self.connection.recv(65536)
                if not chunk:
                    raise RuntimeError("Reference API closed its connection before replying")
                reply.extend(self.cipher.encrypt(chunk))
                if len(reply) > 32 * 1024 * 1024:
                    raise RuntimeError("Reference API response exceeded the capture budget")
        finally:
            if temporary_connection:
                self.__exit__(None, None, None)
        response = json.loads(reply[:-1])
        if response["id"] != self.request_id or response["errors"]:
            raise RuntimeError(f"Reference API {module}.{function}: {response.get('errors')}")
        return response["data"]

    def capture(self, path, screen=0, quality=100, divide=1):
        if type(quality) is not int or not 1 <= quality <= 100:
            raise ValueError("JPEG quality must be an integer from 1 to 100")
        if type(divide) is not int or not 1 <= divide <= 8:
            raise ValueError("Capture divide must be an integer from 1 to 8")
        path = Path(path).resolve()
        root = Path(self.session["captureRoot"]).resolve()
        if path == root or not path.is_relative_to(root):
            raise ValueError("Screenshots must stay in the reference evidence directory")
        if not path.parent.is_dir():
            raise ValueError("Screenshot parent directory must already exist")
        if path.exists():
            raise FileExistsError("Screenshot already exists")
        result = self.call("capture", "get_jpg", [screen, quality, divide])
        if len(result) != 4:
            raise RuntimeError("Reference screen is not ready for capture")
        timestamp, width, height, encoded = result
        data = base64.b64decode(encoded, validate=True)
        with path.open("xb") as stream:
            stream.write(data)
        return {"path": str(path), "screen": screen, "timestamp": timestamp,
                "width": width, "height": height, "bytes": len(data), "quality": quality, "divide": divide}

    def keypad_write(self, keypad, keys):
        """Write navigation characters sequentially; this does not insert or inspect a card."""
        return self.call("keypads", "write", [keypad, keys])

    def press(self, names, milliseconds=120):
        if not names or not 1 <= milliseconds <= 2000:
            raise ValueError("Specify buttons and a press duration from 1 to 2000 ms")
        known = {row[0] for row in self.call("buttons", "read")}
        if any(name not in known for name in names):
            raise ValueError("Button name is not present in this game")
        try:
            self.call("buttons", "write", [[name, 1.0] for name in names])
            time.sleep(milliseconds / 1000)
        finally:
            self.call("buttons", "write", [[name, 0.0] for name in names])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session", type=Path, required=True)
    sub = parser.add_subparsers(dest="action", required=True)
    sub.add_parser("buttons")
    sub.add_parser("screens")
    sub.add_parser("analogs")
    sub.add_parser("coin")
    capture = sub.add_parser("capture")
    capture.add_argument("--screen", type=int, default=0)
    capture.add_argument("--quality", type=int, default=100)
    capture.add_argument("--divide", type=int, default=1)
    capture.add_argument("--output", type=Path, required=True)
    press = sub.add_parser("press")
    press.add_argument("--button", action="append", required=True)
    press.add_argument("--milliseconds", type=int, default=120)
    keypad = sub.add_parser("keypad-write")
    keypad.add_argument("--keypad", type=int, choices=(0, 1), default=0)
    keypad.add_argument("--keys", required=True)
    args = parser.parse_args(argv)
    api = ReferenceApi(args.session)
    if args.action == "capture":
        result = api.capture(args.output, args.screen, quality=args.quality, divide=args.divide)
    elif args.action == "keypad-write":
        api.keypad_write(args.keypad, args.keys)
        result = {"keypad": args.keypad, "characters": len(args.keys)}
    elif args.action == "press":
        api.press(args.button, args.milliseconds)
        result = {"released": args.button}
    elif args.action == "buttons":
        result = api.call("buttons", "read")
    elif args.action == "analogs":
        result = api.call("analogs", "read")
    elif args.action == "screens":
        result = api.call("capture", "get_screens")
    else:
        result = api.call("coin", "insert", [1])
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
