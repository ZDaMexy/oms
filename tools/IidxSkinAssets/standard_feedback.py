"""Original IIDX26 feedback typography and public OMS display contributions.

The external font is the 26 software's DF Heisei Gothic W7 collection. Glyphs
are rasterised by a private, memory-only GDI font/DC; no application or game is
controlled. The source renderer registers 22px font 2 and 16px font 5, then uses
12x20 and 8x18 fixed cells. This exporter does not claim identical GPU sampling.

``build_feedback`` returns manifest/scene arrays, files and material targets.
Use the caller's existing Global ``decoration`` information marker and replace
Stage ``stage.foreground`` declarations with the returned targets. Global
feedback uses the full canvas; each exact Stage owns its timing direction.
"""
from __future__ import annotations

import ctypes
from ctypes import wintypes
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import unquote


FRAME_IDS = frozenset((3, 8, 12, 14, 16, 17, 19, 21, 25, 26))
PACKAGE = "contents/data/graphic/0/gameparts.ifs"
FONT_FAMILY = "ＤＦ平成ゴシック体W7"
LOGICAL = 768 / 720
DIGITS = "0123456789.+-x%"
# 26 bm2dx's own table, indexed by setting and SP/DP then player. These are
# software UI positions, independently of the selected historical cabinet.
TIMING_POSITIONS = {
    "a": {"sp": ((164, 327), (1053, 327)), "dp": ((419, 327), (800, 327))},
    "b": {"sp": ((353, 359), (874, 359)), "dp": ((202, 359), (1030, 359))},
    "c": {"sp": ((353, 447), (874, 447)), "dp": ((608, 437), (648, 437))},
}
OPTIONS = [
    {"id": "realtime-judge", "type": "toggle", "label": "实时 JUDGE", "default": True},
    {"id": "timing-position", "type": "choice", "label": "FAST/SLOW 位置", "default": "a",
     "choices": [{"value": key, "label": label} for key, label in
                 (("a", "Position A"), ("b", "Position B"), ("c", "Position C"), ("off", "OFF"))]},
]


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def contained(root, relative):
    path = (root / unquote(relative)).resolve(strict=True)
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError("Feedback source leaves its qualified library")
    return path


def node(identifier, kind="container", properties=None, resource=None, *, slot=None, children=()):
    value = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
             "properties": properties or {}, "effects": [], "children": list(children)}
    if resource is not None:
        value["resource"] = resource
    if slot is not None:
        value["slot"] = slot
    return value


def rasterize_source_text(font_path, text, *, cache_height=22, cell=(12, 20)):
    """Return source-face grayscale glyphs in the native fixed cells.

    The caller supplies a particular external font, never a system fallback.
    Both the selected GDI face and ink bounds are checked. The returned PIL
    image contains white RGB and original glyph coverage as alpha, permitting
    the source's separately evidenced native colour to be applied by a node.
    """
    from PIL import Image
    font_path = Path(font_path).resolve(strict=True)
    if not font_path.is_file() or not text or any(ord(c) > 127 for c in text):
        raise ValueError("Source feedback rasterisation requires nonempty ASCII and a regular font file")
    if (cache_height, tuple(cell)) not in ((22, (12, 20)), (16, (8, 18))):
        raise ValueError("Use one of the two directly evidenced 26 font-cell pairs")
    if not hasattr(ctypes, "WinDLL"):
        raise RuntimeError("The original Windows GDI font export requires Windows")

    class Point(ctypes.Structure):
        _fields_ = [("x", wintypes.LONG), ("y", wintypes.LONG)]

    class Metrics(ctypes.Structure):
        _fields_ = [("width", wintypes.DWORD), ("height", wintypes.DWORD), ("origin", Point),
                    ("advance_x", ctypes.c_short), ("advance_y", ctypes.c_short)]

    class Fixed(ctypes.Structure):
        _fields_ = [("fraction", ctypes.c_ushort), ("value", ctypes.c_short)]

    class Matrix(ctypes.Structure):
        _fields_ = [("m11", Fixed), ("m12", Fixed), ("m21", Fixed), ("m22", Fixed)]

    class TextMetrics(ctypes.Structure):
        _fields_ = [(n, wintypes.LONG) for n in ("height", "ascent", "descent", "internal", "external",
                    "average", "maximum", "weight", "overhang", "aspect_x", "aspect_y")]
        _fields_ += [(n, wintypes.WCHAR) for n in ("first", "last", "default", "break_char")]
        _fields_ += [(n, wintypes.BYTE) for n in ("italic", "underlined", "struck", "pitch", "charset")]

    gdi = ctypes.WinDLL("gdi32", use_last_error=True)
    gdi.AddFontResourceExW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, ctypes.c_void_p]
    gdi.AddFontResourceExW.restype = ctypes.c_int
    gdi.RemoveFontResourceExW.argtypes = gdi.AddFontResourceExW.argtypes
    gdi.RemoveFontResourceExW.restype = wintypes.BOOL
    gdi.CreateCompatibleDC.argtypes = [ctypes.c_void_p]
    gdi.CreateCompatibleDC.restype = ctypes.c_void_p
    gdi.CreateFontW.argtypes = [ctypes.c_int] * 5 + [wintypes.DWORD] * 8 + [wintypes.LPCWSTR]
    gdi.CreateFontW.restype = ctypes.c_void_p
    gdi.SelectObject.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
    gdi.SelectObject.restype = ctypes.c_void_p
    gdi.GetTextFaceW.argtypes = [ctypes.c_void_p, ctypes.c_int, wintypes.LPWSTR]
    gdi.GetTextFaceW.restype = ctypes.c_int
    gdi.GetTextMetricsW.argtypes = [ctypes.c_void_p, ctypes.POINTER(TextMetrics)]
    gdi.GetTextMetricsW.restype = wintypes.BOOL
    gdi.GetGlyphOutlineW.argtypes = [ctypes.c_void_p, wintypes.UINT, wintypes.UINT, ctypes.POINTER(Metrics),
                                    wintypes.DWORD, ctypes.c_void_p, ctypes.POINTER(Matrix)]
    gdi.GetGlyphOutlineW.restype = wintypes.DWORD
    gdi.GetFontData.argtypes = [ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD]
    gdi.GetFontData.restype = wintypes.DWORD
    gdi.DeleteObject.argtypes = gdi.DeleteDC.argtypes = [ctypes.c_void_p]
    gdi.DeleteObject.restype = gdi.DeleteDC.restype = wintypes.BOOL
    if not gdi.AddFontResourceExW(str(font_path), 0x10, None):
        raise ValueError("The external source font could not be privately registered")
    dc = font = previous = None
    try:
        dc = gdi.CreateCompatibleDC(None)
        font = gdi.CreateFontW(cache_height, 0, 0, 0, 400, 0, 0, 0, 128, 0, 0, 4, 0, FONT_FAMILY)
        if not dc or not font:
            raise RuntimeError("Cannot create the private source-font memory context")
        previous = gdi.SelectObject(dc, font)
        face = ctypes.create_unicode_buffer(128)
        if not gdi.GetTextFaceW(dc, len(face), face) or face.value not in (FONT_FAMILY, "DFHSGothic-W7"):
            raise ValueError(f"Windows substituted a different source font: {face.value!r}")
        from fontTools.ttLib import TTFont
        with TTFont(font_path, fontNumber=0, lazy=True) as original:
            for tag in ("cmap", "glyf"):
                expected = original.reader[tag]
                size = gdi.GetFontData(dc, int.from_bytes(tag.encode("ascii"), "little"), 0, None, 0)
                if size != len(expected):
                    raise ValueError("Selected Windows font does not contain the source face tables")
                data = ctypes.create_string_buffer(size)
                if (gdi.GetFontData(dc, int.from_bytes(tag.encode("ascii"), "little"), 0, data, size) != size
                        or data.raw != expected):
                    raise ValueError("Selected Windows font differs from the external 26 source face")
        tm = TextMetrics()
        if not gdi.GetTextMetricsW(dc, ctypes.byref(tm)):
            raise RuntimeError("Cannot read original font metrics")
        matrix = Matrix(Fixed(0, 1), Fixed(0, 0), Fixed(0, 0), Fixed(0, 1))
        result = Image.new("RGBA", (len(text) * cell[0], cell[1]))
        for i, character in enumerate(text):
            gm = Metrics()
            size = gdi.GetGlyphOutlineW(dc, ord(character), 6, ctypes.byref(gm), 0, None, ctypes.byref(matrix))
            if size == 0xffffffff:
                raise ValueError("Source font is missing a requested glyph")
            if not size:
                continue
            buffer = ctypes.create_string_buffer(size)
            if gdi.GetGlyphOutlineW(dc, ord(character), 6, ctypes.byref(gm), size, buffer, ctypes.byref(matrix)) != size:
                raise RuntimeError("Source font glyph changed while being read")
            stride = (gm.width + 3) & ~3
            coverage = bytes(min(255, buffer.raw[y * stride + x] * 255 // 64)
                             for y in range(gm.height) for x in range(gm.width))
            alpha = Image.frombytes("L", (gm.width, gm.height), coverage)
            ink = Image.new("RGBA", alpha.size, "white")
            ink.putalpha(alpha)
            x = (cell[0] - gm.advance_x) // 2 + gm.origin.x
            y = (cell[1] - tm.height) // 2 + tm.ascent - gm.origin.y
            if x < 0 or y < 0 or x + gm.width > cell[0] or y + gm.height > cell[1]:
                raise ValueError(f"Source glyph {character!r} exceeds its evidenced fixed cell")
            result.alpha_composite(ink, (i * cell[0] + x, y))
        return result
    finally:
        if previous and dc:
            gdi.SelectObject(dc, previous)
        if font:
            gdi.DeleteObject(font)
        if dc:
            gdi.DeleteDC(dc)
        gdi.RemoveFontResourceExW(str(font_path), 0x10, None)


def prepare_feedback_font(font_path, output):
    """Export the shared 26 digit cells and literal label sprites.

    ``fonts['normal'/'small']`` are public bitmap-font IDs. ``labels`` maps
    ``(size, literal)`` to a texture resource plus its original fixed-cell size.
    Root Start authors may reuse the normal digits (including .+-x%).
    """
    font_path, output = Path(font_path).resolve(strict=True), Path(output).resolve()
    font_sha = digest(font_path)
    family = "standard.feedback.font.h" + font_sha[:16]
    result = {key: [] for key in ("resources", "bitmapFonts", "files")}
    result.update(fonts={}, labels={})
    labels_by_size = {"normal": ("PG:", "GR:", "GD:", "BD:", "PR:", "CB:", "FAST", "SLOW", "FA", "SL"),
                      "small": ("MAX", "300", "200", "100", "50", "MISS", "CB:", "FA:", "/SL:")}
    for size, cache, cell in (("normal", 22, (12, 20)), ("small", 16, (8, 18))):
        labels = labels_by_size[size]
        glyphs = []
        atlas = rasterize_source_text(font_path, DIGITS, cache_height=cache, cell=cell)
        relative = f"standard/feedback/font-{font_sha}-{size}.png"
        identifier = family + "." + size
        atlas_resource = identifier + ".atlas"
        result["resources"].append({"id": atlas_resource, "type": "texture", "path": relative, "filter": "nearest"})
        for i, character in enumerate(DIGITS):
            glyphs.append({"character": character, "resource": atlas_resource, "advance": cell[0],
                           "rect": [i * cell[0], 0, cell[0], cell[1]]})
        for i, label in enumerate(labels, 1):
            raster = rasterize_source_text(font_path, label, cache_height=cache, cell=cell)
            resource = identifier + ".label.n" + str(i)
            label_relative = f"standard/feedback/label-{font_sha}-{size}-{i}.png"
            label_path = output / label_relative
            label_path.parent.mkdir(parents=True, exist_ok=True)
            raster.save(label_path)
            result["files"].append({"source": str(label_path), "path": label_relative, "sha256": digest(label_path)})
            result["resources"].append({"id": resource, "type": "texture", "path": label_relative, "filter": "nearest"})
            result["labels"][size, label] = {"resource": resource, "width": raster.width, "height": raster.height}
        path = output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        atlas.save(path)
        result["files"].append({"source": str(path), "path": relative, "sha256": digest(path)})
        result["fonts"][size] = identifier
        result["bitmapFonts"].append({"id": identifier, "lineHeight": cell[1], "glyphs": glyphs})
    result["receipt"] = {"sourceFont": str(font_path), "sourceFontSHA256": font_sha, "face": FONT_FAMILY,
        "source": "IIDX26 native font IDs2/5, original TTC face0, caches22/16, fixed cells12x20/8x18",
        "rasterizer": "Windows memory GDI GetGlyphOutlineW GGO_GRAY8_BITMAP; source outlines, fixed-cell metric projection",
        "limits": "No claim of byte-identical native GPU filtering, screen transform, or final executable glyph cache."}
    return result


def source_hook(reference, centre_movie, layout):
    player = "2p" if layout == "p2" else "1p"
    draws = [d for d in reference["layouts"][centre_movie]["draws"]
             if "judge_" + player in d["game_bindings"] and d["texture"] == "judge_waku"]
    if len(draws) != 1:
        raise ValueError("Selected 26 centre movie must have one original JUDGE hook")
    draw = draws[0]
    if draw["asset"]["package"] != "0/gameparts" or (draw["asset"]["width"], draw["asset"]["height"]) != (111, 163):
        raise ValueError("JUDGE hook is not the independently qualified 26 source")
    if any(abs(draw["matrix"][k] - v) > 1e-9 for k, v in (("a", 1), ("b", 0), ("c", 0), ("d", 1))):
        raise ValueError("Original JUDGE hook needs a non-translational projection; do not flatten it to bounds")
    return draw


def build_feedback(library, frame_map, *, style, layout, centre_movie, centre_dx, contexts, font_path, output,
                   judgement_position_option="judgement-position", prepared_font=None):
    library, frame_map = Path(library).resolve(strict=True), Path(frame_map).resolve(strict=True)
    inventory, reference = read(library / "library.json"), read(frame_map)
    if style not in FRAME_IDS or layout not in ("p1", "p2", "dp"):
        raise ValueError("Feedback requires a qualified Standard cabinet/layout")
    if inventory["status"] != "complete" or inventory["sourceMetadataUnchanged"] is not True:
        raise ValueError("Feedback requires the complete verified 26 library")
    if (reference["package"] != f"gmframe{style:02}" or reference["common_family"] != "0"
            or reference["stage"] != {"width": 1280, "height": 720, "fps": 60.0}):
        raise ValueError("Feedback reference does not belong to this 26 cabinet")
    if centre_movie not in reference["layouts"]:
        raise ValueError("Missing selected original centre movie")
    if centre_dx not in (-36, 0, 36):
        raise ValueError("Unqualified caller centre translation")
    if not re.fullmatch(r"[a-z][a-z0-9-]*", judgement_position_option):
        raise ValueError("Judgement position must name the shared public choice option")
    contexts = list(contexts)
    if not contexts or any(c["layout"] != layout for c in contexts):
        raise ValueError("Filter actual material contexts by this layout")
    font = prepared_font or prepare_feedback_font(font_path, output)
    if font["receipt"]["sourceFontSHA256"] != digest(font_path):
        raise ValueError("Prepared font belongs to a different source file")
    result = {key: [] for key in ("resources", "bitmapFonts", "animations", "templates", "instances", "bindings",
                                  "variants", "tracks", "options", "targets", "files", "numericSources")}
    for key in ("resources", "bitmapFonts", "files"):
        result[key].extend(font[key])
    result["options"].extend(OPTIONS)
    prefix = f"standard.feedback.f{style:02}.{layout}." + centre_movie.replace("_", "-")
    hook = source_hook(reference, centre_movie, layout)
    row = next(r for r in inventory["textures"] if r["package"] == PACKAGE and r["name"] == "judge_waku.png")
    path = contained(library, row["sprite"])
    if digest(path) != row["sha256"] or row["sha256"] != hook["asset"]["sha256"]:
        raise ValueError("JUDGE original PNG and source geometry inventory disagree")
    panel = "standard.feedback.panel.h" + row["sha256"][:24]
    relative = "standard/art/" + row["sha256"] + ".png"
    result["resources"].append({"id": panel, "type": "texture", "path": relative, "filter": "nearest"})
    result["files"].append({"source": str(path), "path": relative, "sha256": row["sha256"]})
    empty = Path(output) / "standard/feedback/empty.png"
    empty.parent.mkdir(parents=True, exist_ok=True)
    from PIL import Image
    Image.new("RGBA", (1, 1)).save(empty)
    empty_sha = digest(empty)

    def bind(target, prop, source):
        result["bindings"].append({"id": target + ".bind." + prop, "target": target, "property": prop, "source": source})

    def variant(target, source, cases, default=False, prop="visible"):
        result["variants"].append({"id": target + ".variant." + prop, "target": target, "property": prop,
            "source": source, "default": default,
            "cases": [{"id": target + ".case.n" + str(i), "key": key, "value": value}
                      for i, (key, value) in enumerate(cases.items())]})

    def track(target, prop, source, values, easing="linear"):
        identifier = target + ".track." + prop
        result["tracks"].append({"id": identifier, "type": "tween", "target": target, "property": prop,
            "easing": easing, "loop": False, "clock": source,
            "keyframes": [{"id": identifier + ".n" + str(i), "time": time, "value": value}
                          for i, (time, value) in enumerate(values)]})

    def register(root, slot, material):
        template = root["id"] + ".template"
        result["templates"].append({"id": template, "root": root})
        result["instances"].append({"id": template + ".instance", "template": template,
                                    "material": {"slot": slot, "resource": material}})
        result["files"].append({"source": str(empty), "path": material + ".png", "sha256": empty_sha})

    def label(identifier, text, size, x, y, *, colour="#ffffffff", coords=(1280, 720)):
        source = font["labels"][size, text]
        return node(identifier, "sprite", {"x": x / coords[0], "y": y / coords[1],
                    "width": source["width"] / coords[0], "height": source["height"] / coords[1], "colour": colour}, source["resource"])

    def number(identifier, source, size, x, y, width):
        height = 20 if size == "normal" else 18
        value = node(identifier, "text", {"x": x / 1280, "y": y / 720, "width": width / 1280,
            "height": height / 720, "text": "", "bitmap-font": font["fonts"][size], "font-size": height * LOGICAL,
            "format": "integer", "alignment": "right", "colour": "#ffffffff"})
        bind(identifier, "text", source)
        advance = 12 if size == "normal" else 8
        track(identifier, "font-size", source, [(0, height * LOGICAL)] +
              [(10 ** (n - 1), height * LOGICAL * min(1, width / (n * advance))) for n in range(5, 8)] +
              [(10_000_000, height * LOGICAL * min(1, width / (10 * advance)))], "step")
        return value

    rulesets = sorted({c["ruleset"] for c in contexts})
    if any(r not in ("bms", "mania") for r in rulesets):
        raise ValueError("Unqualified feedback ruleset")
    if "bms" in rulesets:
        result["numericSources"].append({"id": "standard-poor", "operation": "add",
                                         "left": "judgement.count.miss", "right": "judgement.count.ok"})
    information_material = f"markers/frame-{layout}-information"
    for ruleset in rulesets:
        identity = prefix + ".judge." + ruleset
        root = node(identity + ".root", slot="decoration")
        content = node(identity + ".canvas", properties={"z": -40})
        preference = node(root["id"] + ".preference", children=[content])
        root["children"].append(preference)
        b = hook["bounds"]
        x, y = b["left"] + centre_dx, b["top"]
        content["children"].append(node(identity + ".panel", "sprite",
            {"x": x / 1280, "y": y / 720, "width": (b["right"] - b["left"]) / 1280,
             "height": (b["bottom"] - b["top"]) / 720}, panel))
        entries = (("PG:", "perfect"), ("GR:", "great"), ("GD:", "good"), ("BD:", "meh"), ("PR:", "miss")) if ruleset == "bms" else (
            ("MAX", "perfect"), ("300", "great"), ("200", "good"), ("100", "ok"), ("50", "meh"), ("MISS", "miss"))
        # BMS keeps the five source rows. Mania has a separately recorded six
        # grade adaptation using the source's smaller font within this panel.
        size, first, pitch = ("normal", 30, 20) if ruleset == "bms" else ("small", 24, 16)
        entries = [*entries, ("CB:", None)]
        for i, (caption, grade) in enumerate(entries):
            yy = y + first + pitch * i
            content["children"].append(label(identity + ".label.n" + str(i), caption, size, x + 13, yy))
            source = "combo.breaks" if grade is None else "judgement.count." + grade
            if ruleset == "bms" and grade == "miss":
                source = "value.standard-poor"
            offset = 36 if size == "normal" else 32
            content["children"].append(number(identity + ".value.n" + str(i), source, size, x + 13 + offset, yy, 48))
        fy = y + (150 if ruleset == "bms" else 136)
        content["children"].extend((label(identity + ".fa", "FA:", "small", x + 13, fy),
            number(identity + ".fast", "judgement.count.fast", "small", x + 37, fy, 32),
            label(identity + ".sl", "/SL:", "small", x + 69, fy),
            number(identity + ".slow", "judgement.count.slow", "small", x + 101, fy, 32)))
        variant(root["id"], "layout.ruleset", {ruleset: True})
        bind(preference["id"], "visible", "option.realtime-judge")
        variant(content["id"], "preparation.active", {"true": False}, default=True)
        register(root, "decoration", information_material)

    timing_materials = {}
    timing_receipt = []
    for group in sorted({c["group"] for c in contexts}):
        if group not in ((0, 1) if layout == "dp" else (0,)):
            raise ValueError("Unexpected Stage group in feedback context")
        side = group if layout == "dp" else int(layout == "p2")
        movie = "center_frame_dp_1p" if layout == "dp" else ("2p_frame" if side else "1p_frame")
        field_draws = [d for d in reference["layouts"][movie]["draws"] if f"lane_{side + 1}p" in d["game_bindings"]]
        if not field_draws:
            raise ValueError("Missing native lane area for exact Stage timing projection")
        field = {key: (min if key in ("left", "top") else max)(d["bounds"][key] for d in field_draws)
                 for key in ("left", "top", "right", "bottom")}
        fw, fh = field["right"] - field["left"], field["bottom"] - field["top"]
        identity = prefix + f".timing.g{group}"
        material = f"standard/markers/timing-f{style:02}-{layout}-g{group}"
        timing_materials[group] = material
        root = node(identity + ".root", slot="stage.foreground")
        lift = node(identity + ".lift")
        root["children"].append(lift)
        track(lift["id"], "y", "lift.offset", [(0, 0), (1, -1)])
        for setting, offset in (("normal", 0), ("up100", 100), ("up200", 200), ("up300", 300)):
            height = node(identity + ".height." + setting, properties={"y": -offset / fh})
            lift["children"].append(height)
            variant(height["id"], "option." + judgement_position_option, {setting: True})
            for position, tables in TIMING_POSITIONS.items():
                px, py = tables["dp" if layout == "dp" else "sp"][side]
                location = node(height["id"] + ".position." + position)
                height["children"].append(location)
                variant(location["id"], "option.timing-position", {position: True})
                for direction, colour in (("fast", "#0000ffff"), ("slow", "#ff0000ff")):
                    text = ("FA" if direction == "fast" else "SL") if layout == "dp" and position == "c" else direction.upper()
                    art = label(location["id"] + "." + direction, text, "normal", px - field["left"], py - field["top"],
                                colour=colour, coords=(fw, fh))
                    location["children"].append(art)
                    variant(art["id"], "judgement.timing-direction", {direction: True})
                timing_receipt.append({"group": group, "setting": position, "judgementPosition": setting,
                                       "judgementOffset": offset, "sourcePosition": [px, py], "stageBounds": field})
        variant(root["id"], "preparation.active", {"true": False}, default=True)
        register(root, "stage.foreground", material)
    for context in contexts:
        result["targets"].append({"scope": "Stage", "target": context["stage"], "slot": "stage.foreground",
                                  "material": timing_materials[context["group"]]})
        if context["group"] == 0:
            result["targets"].append({"scope": "Global", "target": context["suffix"], "slot": "decoration",
                                      "material": information_material})
    result["receipt"] = {"contract": "oms-standard-feedback.v1", "style": style, "layout": layout,
        "sourceLibrary": str(library), "frameMap": str(frame_map), "sourceMovie": centre_movie,
        "judgeHook": hook, "callerCentreOffset": centre_dx, "font": font["receipt"], "timing": timing_receipt,
        "mount": "Existing Global decoration information marker uses the full 1280x720 canvas; ruleset, JUDGE preference and preparation are separate gates.",
        "bmsRows": "PG/GR/GD/BD/PR + CB; original PR includes miss+empty-POOR, declared as the caller-integrated value.standard-poor sum.",
        "maniaRows": "Explicit OMS six-grade + CB compact adaptation with original smaller font; no IIDX judgement semantics substituted.",
        "fastSlow": "Original A/B/C coordinates, source blue FAST/red SLOW; DP-C uses original FA/SL; exact Stage timing-direction.",
        "nativeEvidence": {"fontRegistrationRVA": "0x146f0", "fontCellRVA": "0x114ee0", "judgeRVA": "0x347900",
                           "timingRVA": "0x351800", "timingPositionRVA": "0x52a610", "colourRVA": ["0xafa20", "0xafa50"]},
        "limits": ["Public real timing and counting semantics are OMS truth; this author does not reproduce native judgement math.",
                   "Timing direction lifetime follows the public exact-group retained state; native complete timing calibration remains unverified.",
                   "JUDGE native fixed-cell FA/SL line can extend beyond its original backplate; the native source coordinates are retained.",
                   "Source opening remains the cabinet's responsibility; feedback enters after preparation, without invented slide timing.",
                   "Original source fonts are rasterised locally, not copied from another skin; final Player sampling still needs visual verification."]}
    return result
