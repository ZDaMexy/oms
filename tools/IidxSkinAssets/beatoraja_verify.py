"""Verify a generated modular package or completed official-engine captures.

Hashes bind the actual local payload. They do not replace visual acceptance or
prove every combination of the native skin's options.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image

from beatoraja_author import source_path


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def package_receipt_path(root: Path) -> Path:
    """Select one sidecar, retaining historical packages without ambiguity."""
    candidates = [name for name in ("package-receipt.json.txt", "package-receipt.json")
                  if (root / name).exists()]
    if len(candidates) != 1:
        raise ValueError("Expected exactly one package receipt: package-receipt.json.txt or package-receipt.json")
    source_path(root, candidates[0])
    return root / candidates[0]


def verify_package(root: Path):
    root = root.resolve(strict=True)
    receipt_path = package_receipt_path(root)
    receipt = read(receipt_path)
    if receipt.get("contract") != "oms-beatoraja-modular-package.v1":
        raise ValueError("Expected the modular-package receipt")
    declared = dict(receipt["entryFilesSha256"])
    sizes = {}
    for shard in receipt["shards"].values():
        if shard["path"] in declared:
            raise ValueError("Duplicate payload path")
        declared[shard["path"]] = shard["sha256"]
        sizes[shard["path"]] = shard["bytes"]
    assets = {}
    for path in (root / "assets").iterdir():
        if not path.is_file() or path.suffix not in (".png", ".fnt", ".ttf", ".otf"):
            raise ValueError("Unexpected asset: " + path.name)
        if path.stem in assets:
            raise ValueError("Ambiguous content-addressed asset")
        assets[path.stem] = path
    if set(assets) != set(receipt["assets"]):
        raise ValueError("Asset inventory differs from the package receipt")
    for sha, asset in receipt["assets"].items():
        relative = assets[sha].relative_to(root).as_posix()
        declared[relative] = sha
        sizes[relative] = asset["bytes"]
    declared[receipt_path.name] = digest(receipt_path)
    rows, png_count = [], 0
    for relative, expected in sorted(declared.items()):
        path = source_path(root, relative)
        actual = digest(path)
        if actual.lower() != expected.lower() or relative in sizes and path.stat().st_size != sizes[relative]:
            raise ValueError("Payload hash/size differs: " + relative)
        if path.suffix == ".png":
            with Image.open(path) as image:
                image.verify()
            png_count += 1
        rows.append({"path": relative, "sha256": actual, "bytes": path.stat().st_size})
    actual_runtime = {p.relative_to(root).as_posix() for p in root.rglob("*")
                      if p.is_file() and p.suffix in (".lua", ".luaskin")}
    expected_runtime = {p for p in declared if Path(p).suffix in (".lua", ".luaskin")}
    if actual_runtime != expected_runtime:
        raise ValueError("Runtime script inventory differs from the package receipt")
    identity = hashlib.sha256(json.dumps(rows, separators=(",", ":")).encode("utf-8")).hexdigest()
    return {"root": str(root), "family": receipt["family"], "payloadSha256": identity,
            "receiptSha256": declared[receipt_path.name], "files": len(rows),
            "bytes": sum(row["bytes"] for row in rows), "pngs": png_count, "inventory": rows}


def verify_capture(root: Path):
    root = root.resolve(strict=True)
    receipt, host = read(root / "receipt.json"), read(root / "host-receipt.json")
    if (not all(receipt.get(key) for key in ("success", "normalDispose", "diagnosticsClean", "skinIdentityVerified", "pngWritesFinished"))
            or receipt.get("pngWriteFailures") or not host.get("exited") or host.get("timedOut") or host.get("exitCode") not in (0, -1)):
        raise ValueError("Capture did not pass its native completion and disposal gates")
    files = set()
    for frame in receipt["frames"]:
        relative = frame["file"]
        if relative in files:
            raise ValueError("Duplicate capture frame")
        files.add(relative)
        path = source_path(root, relative)
        if digest(path) != frame["sha256"]:
            raise ValueError("Capture PNG hash differs: " + relative)
        with Image.open(path) as image:
            if image.size != (frame["width"], frame["height"]):
                raise ValueError("Capture PNG dimensions differ")
            image.verify()
    if files != {p.relative_to(root).as_posix() for p in (root / "frames").glob("*.png")}:
        raise ValueError("Capture PNG inventory differs")
    manifest = read(root / "capture-workspace.json")
    return {"root": str(root), "scenario": receipt["scenario"], "mode": receipt["mode"],
            "skin": receipt["skinRequested"], "frames": len(files), "result": receipt.get("result"),
            "skinEntrySha256": manifest.get("skinEntrySha256"),
            "packageReceiptSha256": manifest.get("packageReceiptSha256"),
            "receiptSha256": digest(root / "receipt.json"), "exitCode": host["exitCode"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kind", choices=("package", "capture"))
    parser.add_argument("roots", type=Path, nargs="+")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Output must be new; retain earlier evidence unchanged")
    verify = verify_package if args.kind == "package" else verify_capture
    report = {"kind": args.kind, "passed": True, "results": [verify(root) for root in args.roots]}
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"PASS {len(report['results'])} {args.kind} entries; evidence: {args.output}")


if __name__ == "__main__":
    main()
