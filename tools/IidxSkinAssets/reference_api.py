"""Capture and navigate the isolated IIDX reference through its local Spice API."""

import argparse
import base64
import json
from pathlib import Path
import socket
import time


def rc4(key):
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
        self.connection = socket.create_connection(("127.0.0.1", self.session["port"]), timeout=8)
        self.cipher = rc4(self.session["password"].encode())
        return self

    def __exit__(self, exception_type, exception, traceback):
        self.connection.close()
        self.connection = self.cipher = None

    def call(self, module, function, params=()):
        allowed = {("capture", "get_screens"), ("capture", "get_jpg"),
                   ("buttons", "read"), ("buttons", "write"), ("buttons", "write_reset"),
                   ("analogs", "read"), ("analogs", "write"), ("analogs", "write_reset"),
                   ("coin", "insert"), ("touch", "write"), ("touch", "write_reset")}
        if (module, function) not in allowed:
            raise ValueError("Operation is outside reference capture and navigation")
        self.request_id += 1
        request = json.dumps({"id": self.request_id, "module": module, "function": function,
                              "params": list(params)}, separators=(",", ":")).encode() + b"\0"
        temporary_connection = self.connection is None
        if temporary_connection:
            self.__enter__()
        try:
            encoded = bytes(value ^ next(self.cipher) for value in request)
            self.connection.sendall(encoded)
            reply = bytearray()
            while not reply or reply[-1] != 0:
                chunk = self.connection.recv(65536)
                if not chunk:
                    raise RuntimeError("Reference API closed its connection before replying")
                reply.extend(value ^ next(self.cipher) for value in chunk)
                if len(reply) > 32 * 1024 * 1024:
                    raise RuntimeError("Reference API response exceeded the capture budget")
        finally:
            if temporary_connection:
                self.__exit__(None, None, None)
        response = json.loads(reply[:-1])
        if response["id"] != self.request_id or response["errors"]:
            raise RuntimeError(f"Reference API {module}.{function}: {response.get('errors')}")
        return response["data"]

    def capture(self, path, screen=0):
        result = self.call("capture", "get_jpg", [screen, 100, 1])
        if len(result) != 4:
            raise RuntimeError("Reference screen is not ready for capture")
        timestamp, width, height, encoded = result
        data = base64.b64decode(encoded, validate=True)
        path = Path(path).resolve()
        root = Path(self.session["captureRoot"]).resolve()
        if not path.is_relative_to(root):
            raise ValueError("Screenshots must stay in the reference evidence directory")
        if not path.parent.is_dir():
            raise ValueError("Screenshot parent directory must already exist")
        with path.open("xb") as stream:
            stream.write(data)
        return {"path": str(path), "screen": screen, "timestamp": timestamp,
                "width": width, "height": height, "bytes": len(data)}

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


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session", type=Path, required=True)
    sub = parser.add_subparsers(dest="action", required=True)
    sub.add_parser("buttons")
    sub.add_parser("screens")
    sub.add_parser("analogs")
    sub.add_parser("coin")
    capture = sub.add_parser("capture")
    capture.add_argument("--screen", type=int, default=0)
    capture.add_argument("--output", type=Path, required=True)
    press = sub.add_parser("press")
    press.add_argument("--button", action="append", required=True)
    press.add_argument("--milliseconds", type=int, default=120)
    args = parser.parse_args()
    api = ReferenceApi(args.session)
    if args.action == "capture":
        result = api.capture(args.output, args.screen)
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
