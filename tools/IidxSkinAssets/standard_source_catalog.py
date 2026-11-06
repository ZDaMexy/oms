"""Explicit source bindings for the retained 26 and independently supplied 24 HDDs.

s23/s24 are author-library aliases, not original game family numbers. The
composition receipt preserves each original path and archive hash. ARC SCORE
theme IDs never imply that their historical executable has been supplied.
"""
from pathlib import Path
import json

BASE_STYLES = (3, 8, 12, 14, 16, 17, 19, 21, 25, 26)
EARLIER_STYLES = (23, 24)
COMPOSITION = "oms-standard-24-and-26-source-composition.v1"
EARLIER_ALIASES = {
    "gmframe0": "gmframe23", "gmframe24": "gmframe24",
    **{name: "s23/" + name for name in ("basic_frame", "common", "gameparts", "graph", "title")},
    **{"0/" + name: "s24/" + name for name in ("basic_frame", "common", "gameparts", "graph", "title")},
    "fcombo00": "source24/fcombo00",
}


def metadata(library):
    return json.loads((Path(library) / "library.json").read_text(encoding="utf-8-sig"))


def expanded(data):
    composition = data.get("sourceComposition")
    if composition is None:
        return False
    if composition.get("contract") != COMPOSITION or composition.get("aliases24") != EARLIER_ALIASES:
        raise ValueError("Unrecognized Standard source composition; do not infer source families")
    return True


def styles(data):
    return tuple(sorted((*BASE_STYLES, *EARLIER_STYLES))) if expanded(data) else BASE_STYLES


def families(data):
    return ("0", "1", "s23", "s24") if expanded(data) else ("0", "1")


def layout_family(style):
    return {23: "s23", 24: "s24"}.get(style, "0")


def presentation_family(style):
    return "1" if style == 25 else layout_family(style)


def centre_translation(style, layout, wide=False):
    """24 native SP anchors; 23 retained art follows that caller as an adaptation."""
    if layout == "dp" or wide:
        return 0
    offset = 35 if style in EARLIER_STYLES else 36
    return -offset if layout == "p2" else offset


def art_identity(data):
    return "IIDX24+IIDX26" if expanded(data) else "IIDX26-only"


def family_suffix(family):
    if family not in ("0", "1", "s23", "s24"):
        raise ValueError("Unqualified Standard common family")
    return "" if family == "0" else "f" + family
