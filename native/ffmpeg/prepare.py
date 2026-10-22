#!/usr/bin/env python3
"""Verify the signed FFmpeg release and its pinned upstream maintenance patches."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile
import urllib.request


def run(arguments, cwd=None):
    try:
        result = subprocess.run(arguments, cwd=cwd, check=True, capture_output=True, text=True, timeout=30)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        def tail(value):
            if isinstance(value, bytes):
                value = value.decode("utf-8", errors="replace")
            return (value or "")[-8192:]

        status = f"exit {error.returncode}" if isinstance(error, subprocess.CalledProcessError) else "timeout after 30s"
        raise RuntimeError(f"Command failed ({status}): {arguments!r}\n"
                           f"stdout:\n{tail(error.stdout)}\nstderr:\n{tail(error.stderr)}") from error
    return result.stdout + result.stderr


def download(url, destination, expected_hash=None):
    if not destination.exists():
        with urllib.request.urlopen(url, timeout=60) as response:
            data = response.read(32 * 1024 * 1024 + 1)
        if len(data) > 32 * 1024 * 1024:
            raise ValueError("FFmpeg source download exceeds its size limit")
        if expected_hash and hashlib.sha256(data).hexdigest() != expected_hash:
            raise ValueError("FFmpeg source checksum mismatch: " + destination.name)
        destination.write_bytes(data)
    if expected_hash and hashlib.sha256(destination.read_bytes()).hexdigest() != expected_hash:
        raise ValueError("Cached FFmpeg source checksum mismatch: " + destination.name)


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("cache", type=Path)
parser.add_argument("source", type=Path)
args = parser.parse_args()
lock_path = Path(__file__).with_name("source.lock.json")
lock_bytes = lock_path.read_bytes()
lock = json.loads(lock_bytes)
args.cache.mkdir(parents=True, exist_ok=True)
if args.source.exists():
    raise FileExistsError("Use a new empty source directory; existing source is never deleted or trusted implicitly: " + str(args.source))
for item in lock["sources"] + lock["patches"]:
    download(item["url"], args.cache / item["name"], item["sha256"])
signature = args.cache / "ffmpeg-4.4.8.tar.xz.asc"
download(lock["releaseSignatureUrl"], signature)
keyring = args.cache / "gnupg"
keyring.mkdir(mode=0o700, exist_ok=True)
# This pipeline imports a pinned public key and verifies a public signature. It
# does not need a secret-key agent; a failed first-run agent autostart must not
# turn an otherwise successful public-key import into exit status 2 on WSL.
gpg = ["gpg", "--no-options", "--batch", "--no-autostart", "--no-auto-key-retrieve", "--homedir", str(keyring)]
key = args.cache / "ffmpeg-devel.asc"
key_info = run(gpg + ["--with-colons", "--import-options", "show-only", "--import", str(key)])
fingerprints = [line.split(":")[9] for line in key_info.splitlines() if line.startswith("fpr:")]
if not fingerprints or fingerprints[0] != lock["releaseKeyFingerprint"]:
    raise ValueError("Unexpected FFmpeg release signing key")
run(gpg + ["--import", str(key)])
verification = run(gpg + ["--status-fd", "1", "--verify", str(signature), str(args.cache / "ffmpeg-4.4.8.tar.xz")])
if "[GNUPG:] VALIDSIG " + lock["releaseKeyFingerprint"] + " " not in verification:
    raise ValueError("FFmpeg signature did not match the pinned signing key")
(args.cache / "signature-verification.txt").write_text(verification)
args.source.mkdir(parents=True)
with tarfile.open(args.cache / "ffmpeg-4.4.8.tar.xz", "r:xz") as archive:
    for member in archive.getmembers():
        if not member.name.startswith("ffmpeg-4.4.8/") and member.name != "ffmpeg-4.4.8":
            raise ValueError("Unexpected FFmpeg release archive root")
    archive.extractall(args.source, filter="data")
source_root = args.source / "ffmpeg-4.4.8"
for item in lock["patches"]:
    run(["patch", "--batch", "--forward", "--fuzz=0", "-p1", "-i", str((args.cache / item["name"]).resolve())], cwd=source_root)
identity = {"version": lock["version"], "upstreamCommit": lock["upstreamCommit"],
            "lockSha256": hashlib.sha256(lock_bytes).hexdigest(), "releaseSignatureVerified": True}
(args.source / "source-identity.json").write_text(json.dumps(identity, indent=2) + "\n")
print(source_root.resolve())
