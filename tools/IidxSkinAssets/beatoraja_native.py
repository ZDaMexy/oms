"""Small native beatoraja primitives shared by the independent skin exporters."""

from __future__ import annotations

import hashlib
import io
import json
from pathlib import Path
from typing import Any

from PIL import Image

from beatoraja_author import source_path


class Lua(str):
    """Reviewed Lua expression, never author-supplied executable text."""


def lua(value: Any) -> str:
    if isinstance(value, Lua):
        return str(value)
    if value is None:
        return "nil"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    if isinstance(value, (float, int)):
        return json.dumps(value, allow_nan=False)
    if isinstance(value, (list, tuple)):
        return "{" + ",".join(lua(item) for item in value) + "}"
    if isinstance(value, dict):
        return "{" + ",".join("[" + lua(key) + "]=" + lua(item) for key, item in value.items()) + "}"
    raise TypeError(f"Unsupported Lua data type: {type(value).__name__}")


class AssetSink:
    """Copy only referenced files; content hashes deduplicate within one family."""

    def __init__(self, author_root: Path, output: Path):
        self.author_root = author_root.resolve(strict=True)
        self.output = output.resolve(strict=True)
        self.assets = self.output / "assets"
        self.assets.mkdir(exist_ok=True)
        self.source: dict[str, dict[str, str]] = {}
        self.provenance: dict[str, dict[str, Any]] = {}
        self.dimensions: dict[str, tuple[int, int]] = {}
        self._loaded_sources: dict[str, str] = {}
        self._loaded_bakes: dict[tuple[Path, str], str] = {}
        self._gauge_pulse_palettes: dict[tuple, tuple[str, int, int]] = {}
        self._entrance_quads: dict[tuple, tuple[str | None, dict]] = {}
        self._cover_atlases: dict[str, tuple] = {}
        self._cover_verified_pages: set[tuple] = set()

    def _add(self, data: bytes, suffix: str, origin: str | None = None) -> str:
        digest = hashlib.sha256(data).hexdigest()
        source_id = "s" + digest[:24]
        relative = "assets/" + digest + suffix
        path = self.output / relative
        if digest in self.provenance:
            # This sink owns a fresh output and consumes frozen inputs. A hash
            # already written by this instance need not reread the same PNG for
            # every independent frame/component fragment.
            if origin and origin not in self.provenance[digest]["sources"]:
                self.provenance[digest]["sources"].append(origin)
            return source_id
        if path.exists():
            if path.read_bytes() != data:
                raise ValueError("Asset content identity collision")
        else:
            path.write_bytes(data)
        self.source[source_id] = {"id": source_id, "path": relative}
        self.provenance.setdefault(digest, {"bytes": len(data), "sources": []})
        if origin and origin not in self.provenance[digest]["sources"]:
            self.provenance[digest]["sources"].append(origin)
        if suffix == ".png":
            with Image.open(io.BytesIO(data)) as image:
                self.dimensions[source_id] = image.size
        return source_id

    def add_source(self, relative: str) -> str:
        if relative in self._loaded_sources:
            return self._loaded_sources[relative]
        path = source_path(self.author_root, relative)
        if path.suffix.lower() not in (".png", ".ttf", ".otf"):
            raise ValueError("Only declared image/font assets are copied")
        self._loaded_sources[relative] = self._add(path.read_bytes(), path.suffix.lower(), relative)
        return self._loaded_sources[relative]

    def add_raster(self, image: Image.Image) -> str:
        stream = io.BytesIO()
        image.save(stream, format="PNG", optimize=True)
        return self._add(stream.getvalue(), ".png")

    def add_baked_page(self, index_path: Path, page: dict[str, Any]) -> str:
        path = source_path(index_path.parent, page["file"])
        key = (path, page["sha256"])
        if key in self._loaded_bakes:
            return self._loaded_bakes[key]
        data = path.read_bytes()
        if hashlib.sha256(data).hexdigest() != page["sha256"]:
            raise ValueError("Baked atlas hash does not match verified index")
        self._loaded_bakes[key] = self._add(data, ".png", "baked:" + page["sha256"])
        return self._loaded_bakes[key]

    def add_font_descriptor(self, text: str) -> str:
        source_id = self._add(text.encode("utf-8"), ".fnt")
        return self.source[source_id]["path"]

    def add_native_font(self, path: Path) -> str:
        if path.suffix.lower() not in (".ttf", ".otf") or not path.is_file():
            raise ValueError("Expected an explicitly supplied native font file")
        source_id = self._add(path.read_bytes(), path.suffix.lower(), "native-font:" + path.name)
        return self.source[source_id]["path"]


def anchor(value: str) -> tuple[float, float]:
    return {"top-left": (0, 0), "top-centre": (.5, 0), "top-right": (1, 0),
            "centre-left": (0, .5), "centre": (.5, .5), "centre-right": (1, .5),
            "bottom-left": (0, 1), "bottom-centre": (.5, 1), "bottom-right": (1, 1)}[value]


def node_rect(properties: dict[str, Any], parent: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
    """OMS relative coordinates converted to absolute, top-left pixels."""
    px, py, pw, ph = parent
    ax, ay = anchor(properties.get("anchor", "top-left"))
    ox, oy = anchor(properties.get("origin", "top-left"))
    w = pw * properties.get("width", 1) * properties.get("scale-x", 1)
    h = ph * properties.get("height", 1) * properties.get("scale-y", 1)
    return (px + pw * (properties.get("x", 0) + ax) - ox * w,
            py + ph * (properties.get("y", 0) + ay) - oy * h, w, h)


def native_rect(rect: tuple[float, float, float, float], canvas_height: int) -> dict[str, int]:
    x, y, w, h = rect
    return {"x": round(x), "y": round(canvas_height - y - h), "w": round(w), "h": round(h)}


def colour(properties: dict[str, Any], parent: tuple[float, float, float, float] = (1, 1, 1, 1)) -> tuple[float, float, float, float]:
    raw = properties.get("colour", "#ffffffff").lstrip("#")
    if len(raw) != 8:
        raise ValueError("Expected author RGBA colour")
    c = [int(raw[i:i + 2], 16) / 255 * parent[i // 2] for i in range(0, 8, 2)]
    # These frozen authors put brightness on one white carrier per affected
    # branch. It changes RGB only; fading alpha would expose the lane below.
    brightness = max(0, min(1, properties.get("brightness", 1)))
    c[:3] = [value * brightness for value in c[:3]]
    c[3] *= properties.get("opacity", 1)
    return tuple(c)


def tint_pixels(image: Image.Image, rgba: tuple[float, float, float, float]) -> Image.Image:
    """NoteSet ignores destination colour; tint selected pixels once at export.

    Dimensions and factor-one channels remain exact. The frozen note sources
    only bind RGB brightness, so their original alpha is unchanged.
    """
    image = image.convert("RGBA")
    channels = [channel.point([round(value * max(0, min(1, factor))) for value in range(256)])
                if factor != 1 else channel for channel, factor in zip(image.split(), rgba)]
    return Image.merge("RGBA", channels)


def native_colour(rgba: tuple[float, float, float, float]) -> dict[str, int]:
    return dict(zip(("r", "g", "b", "a"), (round(max(0, min(1, v)) * 255) for v in rgba)))


def all_of(expressions: list[str]) -> Lua | None:
    return Lua("function() return " + " and ".join("(" + e + ")" for e in expressions) + " end") if expressions else None


def native_song_expression(source: str) -> str | None:
    """Use actual BMS metadata; difficulty 5 remains INSANE, not LEGGENDARIA."""
    if source == "song.difficulty":
        return ('(function() local labels={"BEGINNER","NORMAL","HYPER","ANOTHER","INSANE"}; '
                'for i=1,5 do if main_state.option(150+i) then return labels[i] end end; return "" end)()')
    if source == "song.level":
        return ('(function() local n=main_state.number(96); if n == -2147483648 or n == 2147483647 '
                'then return "" end; return string.format("%d",n) end)()')
    return None


def native_offset_expression(source: str) -> tuple[str, str] | None:
    """Official visual judge offset and AUTO setting, not OMS replay history."""
    return {
        "offset.value": ("main_state.number(12)", "number"),
        "offset.available": (
            "(main_state.number(12) ~= -2147483648 and main_state.number(12) ~= 2147483647)", "boolean"),
        "offset.automatic": ("(main_state.event_index(75) == 1)", "boolean"),
        "offset.automatic-available": (
            "(main_state.event_index(75) == 0 or main_state.event_index(75) == 1)", "boolean"),
    }.get(source)


def scene_properties(node: dict[str, Any], scene: dict[str, Any], context: dict[str, Any]):
    """Resolve known author values, returning every remaining dynamic operation."""
    properties = {**node.get("properties", {})}
    if "resource" in node:
        properties["resource"] = node["resource"]
    dynamic = []
    for kind in ("bindings", "variants"):
        for operation in scene.get(kind, []):
            if operation["target"] != node["id"]:
                continue
            source = operation["source"]
            if source not in context:
                dynamic.append({"kind": kind, **operation})
                continue
            value = context[source]
            if kind == "variants":
                key = str(value).lower() if isinstance(value, bool) else str(value)
                case = next((v for v in operation["cases"] if v["key"] == key), None)
                value = case.get("resource", case.get("value")) if case else operation.get("default")
            properties[operation["property"]] = value
    return properties, dynamic
