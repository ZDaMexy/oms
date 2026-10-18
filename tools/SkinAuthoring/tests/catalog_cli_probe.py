"""Synthetic CLI boundary checks; run only after UseDevelopmentStorage and the current tool build.

No game/HDD art is read. --fixtures-only never starts .NET. Each invocation requires a new work
directory under this checkout's .dev-cache/temp and leaves fixtures/logs for diagnosis.
"""

import argparse
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import zlib


MANIFEST = "oms-gameplay-skin-manifest.v1"
SCENE = "oms-gameplay-skin-scene.v1"


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, separators=(",", ":")), encoding="utf-8")


def chunk(kind, payload):
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload))


def image_bytes(width=4096, height=4096):
    compressor = zlib.compressobj()
    row = b"\0" + bytes((40, 100, 170, 255)) * width
    compressed = b"".join(compressor.compress(row) for _ in range(height)) + compressor.flush()
    header = chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    return b"\x89PNG\r\n\x1a\n" + header + chunk(b"IDAT", compressed) + chunk(b"IEND", b"")


def scene(node):
    return {"contract": SCENE, "root": node, "tracks": [], "stateMachines": [], "bindings": [],
            "variants": [], "templates": [], "instances": []}


def node(identity, kind="container", **fields):
    return {"id": identity, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
            "properties": {}, "effects": [], "children": [], **fields}


def manifest(resources=()):
    return {"contract": MANIFEST, "scene": "gameplay-skin.scene.json", "sceneContract": SCENE,
            "eventContract": "oms-gameplay-skin-event.v1", "resources": list(resources)}


def make_base(root, png):
    root.mkdir()
    (root / "skin.ini").write_text("[General]\nName: Synthetic catalog probe\nAuthor: OMS tests\nVersion: 2.7\n", encoding="utf-8")
    basis = manifest()
    basis["contentCatalog"] = "content.json"
    basis["options"] = [{"id": "size", "label": "Size", "type": "choice", "default": "normal",
                         "choices": [{"value": "normal", "label": "Normal"}, {"value": "long", "label": "Long"}]}]
    write_json(root / "gameplay-skin.json", basis)
    write_json(root / "gameplay-skin.scene.json", scene(node("dispatch")))
    choices = []
    (root / "images").mkdir()
    for index in range(5):
        identity = f"n{index}"
        image_path = f"images/{identity}.png"
        (root / image_path).write_bytes(png)
        folder = f"branches/{identity}"
        write_json(root / folder / "manifest.json", manifest([{"id": f"texture.{identity}", "type": "texture", "path": image_path}]))
        fragment = node(f"fragment.{identity}", blend="alpha", slot="hud.text",
                        children=[node(f"image.{identity}", "sprite", resource=f"texture.{identity}", blend="alpha")])
        write_json(root / folder / "scene.json", scene(fragment))
        (root / folder / "parts.ini").write_text(
            "[GameplaySkin.Common:1]\nTarget: Global ruleset=any keymode=any stage-mode=any\n"
            f'hud.text: resource Provide "images/{identity}"\n', encoding="utf-8")
        choices.append({"value": identity, "label": identity, "manifest": f"{folder}/manifest.json",
                        "scene": f"{folder}/scene.json", "ini": f"{folder}/parts.ini"})
    write_json(root / "content.json", {"contract": "oms-gameplay-skin-content.v1",
                                      "modules": [{"id": "frame", "label": "Frame", "default": "n0", "choices": choices}]})


def edit(path, mutate):
    value = json.loads(path.read_text(encoding="utf-8"))
    mutate(value)
    write_json(path, value)


def generate(work):
    png = image_bytes()
    base = work / "catalog-inactive-over-64mp"
    make_base(base, png)
    cases = [(base.name, 0, "")]

    def copy_case(name, expected=1, diagnostic=""):
        path = work / name
        shutil.copytree(base, path)
        cases.append((name, expected, diagnostic))
        return path

    ordinary = copy_case("ordinary-still-over-64mp", diagnostic=".png")
    edit(ordinary / "gameplay-skin.json", lambda value: value.pop("contentCatalog"))
    # The default module contributes one 16MP texture. Ordinary packages keep their 64MP limit;
    # selected catalog content permits 96MP, without charging inactive alternatives to that limit.
    for name, image_count, expected in (("catalog-active-80mp-within-96mp", 5, 0), ("catalog-active-over-96mp", 7, 1)):
        active = copy_case(name, expected=expected, diagnostic="OMS-SKIN-SCENE-" if expected else "")
        (active / "base").mkdir()
        for index in range(1, image_count):
            (active / f"base/n{index}.png").write_bytes(png)
        edit(active / "gameplay-skin.json", lambda value: value["resources"].extend(
            {"id": f"base.n{i}", "type": "texture", "path": f"base/n{i}.png"} for i in range(1, image_count)))

    bad_png = copy_case("inactive-corrupt-png", diagnostic="images/n4.png")
    # Correct signature/IHDR keep identification plausible; the last inactive PNG has invalid IDAT data.
    header = png[:33]
    (bad_png / "images/n4.png").write_bytes(header + chunk(b"IDAT", b"bad-zlib-data") + chunk(b"IEND", b""))
    variant = copy_case("inactive-malformed-variant", diagnostic="OMS-SKIN-SCENE-")
    invalid_scene = json.loads((variant / "branches/n4/scene.json").read_text())
    invalid_scene["root"]["id"] = "variant.root"
    invalid_scene["root"]["children"][0]["resource"] = "texture.not-declared"
    write_json(variant / "branches/n4/variant.json", invalid_scene)
    edit(variant / "content.json", lambda value: value["modules"][0]["choices"][4].update(
        variants=[{"when": {"size": "long"}, "manifest": "branches/n4/manifest.json", "scene": "branches/n4/variant.json"}]))

    bad_clip = copy_case("inactive-malformed-recorded-clip", diagnostic="OMS-SKIN-SCENE-")
    edit(bad_clip / "branches/n4/manifest.json", lambda value: value.update(
        animations=[{"id": "clip.n4", "path": "clips/n4.json", "resources": ["texture.n4"]}]))
    write_json(bad_clip / "clips/n4.json", {"contract": "oms-gameplay-recorded-animation.v1"})
    bad_ini = copy_case("inactive-ini-with-unusual-extension", diagnostic="missing-inactive")
    (bad_ini / "branches/n4/parts.fragment").write_text(
        '[GameplaySkin.Common:1]\nTarget: Global ruleset=any keymode=any stage-mode=any\nhud.text: resource Provide "missing-inactive"\n', encoding="utf-8")
    edit(bad_ini / "content.json", lambda value: value["modules"][0]["choices"][4].update(ini="branches/n4/parts.fragment"))
    unknown = copy_case("unknown-malformed-json", diagnostic="unknown.json")
    (unknown / "unknown.json").write_text("{", encoding="utf-8")

    many = copy_case("catalog-more-than-4096-files", expected=0)
    for index in range(4100):
        (many / f"extra-{index}.txt").write_bytes(b"")
    ordinary_many = work / "ordinary-still-limited-to-4096"
    shutil.copytree(many, ordinary_many)
    edit(ordinary_many / "gameplay-skin.json", lambda value: value.pop("contentCatalog"))
    cases.append((ordinary_many.name, 1, "4096"))
    overflow = copy_case("catalog-over-8192-files", diagnostic="8192")
    for index in range(8193):
        (overflow / f"extra-{index}.txt").write_bytes(b"")
    structured = copy_case("catalog-over-64mib-structured", diagnostic="64 MiB")
    padding = '"' + "x" * (33 * 1024 * 1024) + '"'
    for index in range(2):
        (structured / f"metadata-{index}.json").write_text(padding, encoding="ascii")
    return cases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--cli-dll", type=Path)
    parser.add_argument("--fixtures-only", action="store_true")
    arguments = parser.parse_args()
    repository = Path(__file__).resolve().parents[3]
    work = arguments.work.resolve()
    if not work.is_relative_to(repository / ".dev-cache/temp"):
        parser.error("--work must be a new directory below this checkout's .dev-cache/temp")
    if not Path(os.environ.get("TEMP", "")).resolve().is_relative_to(repository / ".dev-cache"):
        parser.error("Source UseDevelopmentStorage.ps1 in this shell before running this probe")
    if not arguments.fixtures_only and (arguments.cli_dll is None or not arguments.cli_dll.is_file()):
        parser.error("--cli-dll must name the just-built tool, or use --fixtures-only")
    work.mkdir(parents=True, exist_ok=False)
    cases = generate(work)
    write_json(work / "cases.json", [{"name": name, "exitCode": code, "diagnostic": diagnostic} for name, code, diagnostic in cases])
    if arguments.fixtures_only:
        print(json.dumps({"fixtures": len(cases), "directory": str(work), "dotnetExecuted": False}))
        return
    results = []
    for name, expected, diagnostic in cases:
        result = subprocess.run(["dotnet", str(arguments.cli_dll.resolve()), "check", str(work / name)], cwd=repository,
                                capture_output=True, encoding="utf-8", errors="replace", check=False)
        (work / f"{name}.stdout.txt").write_text(result.stdout, encoding="utf-8")
        (work / f"{name}.stderr.txt").write_text(result.stderr, encoding="utf-8")
        passed = result.returncode == expected and (not diagnostic or diagnostic in result.stdout + result.stderr)
        results.append({"name": name, "passed": passed, "expectedExitCode": expected, "actualExitCode": result.returncode})
        print(json.dumps(results[-1]))
    write_json(work / "results.json", results)
    if not all(result["passed"] for result in results):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
