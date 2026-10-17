"""Build a local, read-only-source IIDX art catalogue and sprite browser."""

import argparse
from collections import Counter
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import re
import shutil
import sys
from urllib.parse import quote

sys.dont_write_bytecode = True

CATEGORIES = (
    (r"gmframe.*", "框体"),
    (r"notes.*|lnote.*", "音符与长条"),
    (r"judge.*", "判定"),
    (r"beam.*", "光柱"),
    (r"exp\d+", "击打演出"),
    (r"fcombo.*", "全连演出"),
    (r"turn.*", "转盘"),
    (r"lane\d+(?:_anim)?", "挡板候选"),
    (r"gameparts.*|basic_frame.*|common.*|graph.*", "共用界面组件"),
    (r"mselect.*|navigate_mselect.*|opt_select.*|notes_radar.*", "选曲与选项"),
    (r"result.*|d_result.*", "结算"),
    (r"\d+|i_\d+.*", "歌曲相关候选"),
    (r"qp.*|qpro.*|story_chara.*", "角色"),
    (r"sub_.*", "副屏与定制预览"),
)


def category(path):
    for pattern, label in CATEGORIES:
        if re.fullmatch(pattern, path.stem):
            return label
    return "其它美术"


def candidate_style(path):
    match = re.fullmatch(r"gmframe(\d{1,2})(?:_om)?", path.stem)
    return int(match[1]) if match else None


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def inventory(game_root):
    graphic = game_root / "contents/data/graphic"
    roots = [graphic]
    mods = game_root / "contents/data_mods"
    if mods.is_dir():
        roots.extend(sorted(p / "graphic" for p in mods.iterdir()
                            if p.name != "_cache" and (p / "graphic").is_dir()))
    rows = []
    for root in roots:
        for path in sorted(root.rglob("*")):
            if not path.is_file():
                continue
            if not path.resolve().is_relative_to(game_root):
                raise ValueError(f"Graphic source escapes the game root: {path}")
            stat = path.stat()
            rows.append({"path": path.relative_to(game_root).as_posix(),
                         "bytes": stat.st_size, "mtimeNs": stat.st_mtime_ns,
                         "extension": path.suffix.lower(), "category": category(path),
                         "candidateStyle": candidate_style(path)})
    return rows


def save_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def extract_copy(copied, output, image_class, ifs_class, cropped):
    archive = ifs_class(str(copied), super_abort_if_bad=True)
    try:
        for node in [*archive.tree.all_folders, *archive.tree.all_files]:
            if not (output / node.full_path).resolve().is_relative_to(output.resolve()):
                raise ValueError("IFS entry escapes its extraction directory")
        archive.extract(path=str(output), progress=False, recurse=False,
                        extract_manifest=not cropped, rename_dupes=True,
                        tex_only=cropped, crop_to_uvrect=cropped)
        return {node.name: {"rawPath": node.full_path.replace("\\", "/"),
                            "imageRect": node.imgrect, "uvRect": node.uvrect}
                for node in archive.tree.all_files if isinstance(node, image_class)}
    finally:
        archive.close()


def build(args, parser):
    game_root = args.game_root.resolve(strict=True)
    output = args.output.resolve()
    if output.is_relative_to(game_root) or game_root.is_relative_to(output):
        parser.error("Output and game root must be separate, non-overlapping directories.")
    if output.drive.lower() == os.environ.get("SystemDrive", "C:").lower():
        parser.error("Use a non-system drive for the local art library.")
    if output.exists():
        parser.error("Output already exists; existing libraries are never overwritten.")
    if not output.parent.is_dir():
        parser.error("The output parent directory must already exist.")
    graphic = game_root / "contents/data/graphic"
    if not graphic.is_dir():
        parser.error("This selection requires a contents/data/graphic game layout.")
    if args.ifstools_path:
        sys.path.insert(0, str(args.ifstools_path.resolve(strict=True)))
    from ifstools import IFS
    from ifstools.handlers.image_file import ImageFile
    from PIL import Image

    selection = json.loads(args.selection.read_text(encoding="utf-8"))
    if not isinstance(selection, list) or not selection or not all(isinstance(p, str) for p in selection):
        parser.error("Selection must be a non-empty JSON array of relative IFS paths.")
    if len(selection) != len(set(selection)):
        parser.error("Selection contains duplicate archive paths.")
    sources = []
    for relative in selection:
        source = (game_root / relative).resolve(strict=True)
        if not source.is_relative_to(graphic.resolve()) or source.suffix.lower() != ".ifs":
            parser.error(f"Selected input must be an IFS inside contents/data/graphic: {relative}")
        sources.append(source)

    before = inventory(game_root)
    selected_bytes = sum(p.stat().st_size for p in sources)
    # Copies, two image representations and decoder work need working headroom.
    if shutil.disk_usage(output.parent).free < selected_bytes * 4 + 256 * 1024 * 1024:
        parser.error("Insufficient working space for this archive selection.")
    output.mkdir()
    save_json(output / "source-inventory.json", before)
    save_json(output / "run.json", {"status": "running", "selection": selection})
    packages, textures = [], []
    for source in sources:
        relative = source.relative_to(game_root)
        before_hash = digest(source)
        copied = output / "source-copies" / relative
        copied.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, copied)
        if digest(copied) != before_hash:
            raise RuntimeError("Source copy did not match the original archive")
        package_root = output / "packages" / relative.with_suffix("")
        raw, sprites = package_root / "raw", package_root / "sprites"
        raw.parent.mkdir(parents=True, exist_ok=False)
        metadata = extract_copy(copied, raw, ImageFile, IFS, cropped=False)
        extract_copy(copied, sprites, ImageFile, IFS, cropped=True)
        raw_files = sorted(p for p in raw.rglob("*") if p.is_file())
        sprite_files = sorted(sprites.glob("*.png"))
        for path in raw.rglob("*.png"):
            with Image.open(path) as image:
                image.verify()
        for path in sprite_files:
            with Image.open(path) as image:
                width, height = image.size
                image.verify()
            info = metadata[path.name]
            image_rect, uv_rect = info["imageRect"], info["uvRect"]
            if (width, height) != (uv_rect[1] - uv_rect[0], uv_rect[3] - uv_rect[2]):
                raise RuntimeError("Cropped sprite dimensions do not match texture UV metadata")
            textures.append({"package": relative.as_posix(), "name": path.name,
                             "category": category(source), "candidateStyle": candidate_style(source),
                             "width": width, "height": height,
                             "imageRect": image_rect, "uvRect": uv_rect,
                             "sprite": quote(path.relative_to(output).as_posix(), safe="/"),
                             "raw": quote((raw / info["rawPath"]).relative_to(output).as_posix(), safe="/"),
                             "sha256": digest(path)})
        after_hash = digest(source)
        if after_hash != before_hash:
            raise RuntimeError("Original archive changed while the library was being built")
        package = {"source": relative.as_posix(), "category": category(source),
                   "candidateStyle": candidate_style(source), "sha256": before_hash,
                   "sourceUnchanged": True, "spriteCount": len(sprite_files),
                   "rawFileCount": len(raw_files),
                   "rawExtensions": dict(Counter(p.suffix.lower() for p in raw_files))}
        packages.append(package)
        save_json(output / "packages.json", packages)
        print(f"{relative.as_posix()}: {len(sprite_files)} verified UV sprites", flush=True)

    if inventory(game_root) != before:
        raise RuntimeError("Graphic source file names, sizes or last-write times changed")
    hashes = Counter(t["sha256"] for t in textures)
    result = {"createdAt": datetime.now(timezone(timedelta(hours=8))).isoformat(),
              "tool": "ifstools", "toolVersion": importlib.metadata.version("ifstools"),
              "status": "complete", "sourceMetadataUnchanged": True,
              "sourceFileCount": len(before), "packages": packages, "textures": textures,
              "uniqueSpriteContents": len(hashes),
              "note": "Filename-based version candidates; extraction does not establish complete skin coverage."}
    save_json(output / "library.json", result)
    template = Path(__file__).with_name("browser.html").read_text(encoding="utf-8")
    embedded = json.dumps(result, ensure_ascii=False).replace("<", "\\u003c").replace(">", "\\u003e")
    (output / "index.html").write_text(template.replace("__LIBRARY_DATA__", embedded), encoding="utf-8")
    save_json(output / "run.json", {key: value for key, value in result.items()
                                    if key not in ("packages", "textures")})
    print(f"Complete: {len(packages)} archives, {len(textures)} verified sprites; source unchanged.")
    print(output / "index.html")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--selection", type=Path, default=Path(__file__).with_name("selection-33-hdd.json"))
    parser.add_argument("--ifstools-path", type=Path)
    build(parser.parse_args(), parser)
