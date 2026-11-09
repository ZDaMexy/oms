"""Explicit source bindings for the independently supplied Standard HDDs.

s23/s24 are author-library aliases, not original game family numbers. The
composition receipt preserves each original path and archive hash. ARC SCORE
theme IDs never imply that their historical executable has been supplied. The
source22 keys identify source packages, not six proved native menu choices.
The unsuffixed source22/gmframe0 is retained for research, not a complete
playable theme. Original21 has its own qualified caller and shared-source
closure, so its separately identified source21/gmframe0 remains eligible.
Original20 uses a separate source20/s20 identity; recognizing that identity
does not qualify its geometry or native callers as equivalent to later HDDs.
"""
from pathlib import Path
import json

BASE_STYLES = (3, 8, 12, 14, 16, 17, 19, 21, 25, 26)
EARLIER_STYLES = (23, 24)
COMPOSITION = "oms-standard-24-and-26-source-composition.v1"
COMPOSITION22 = "oms-standard-22-24-and-26-source-composition.v1"
COMPOSITION21 = "oms-standard-21-22-24-and-26-source-composition.v1"
COMPOSITION20 = "oms-standard-20-21-22-24-and-26-source-composition.v1"
EARLIER_ALIASES = {
    "gmframe0": "gmframe23", "gmframe24": "gmframe24",
    **{name: "s23/" + name for name in ("basic_frame", "common", "gameparts", "graph", "title")},
    **{"0/" + name: "s24/" + name for name in ("basic_frame", "common", "gameparts", "graph", "title")},
    "fcombo00": "source24/fcombo00",
}
SOURCE22_FRAMES = {
    "source22-gmframe0": "gmframe0",
    "source22-gmframe0-00": "gmframe0_00",
    "source22-gmframe0-01": "gmframe0_01",
    "source22-gmframe8": "gmframe8",
    "source22-gmframe9": "gmframe9",
    "source22-gmframe10": "gmframe10",
}
SOURCE22_STYLES = tuple(SOURCE22_FRAMES)
# The retained bare archive has no complete score/BPM/Hi-Speed/Gauge/progress
# closure or qualified native caller. Keep its identity, not a playable option.
SOURCE22_AVAILABLE_STYLES = tuple(style for style in SOURCE22_STYLES if style != "source22-gmframe0")
SOURCE22_ALIASES = {
    **{name: "source22/" + name for name in SOURCE22_FRAMES.values()},
    **{name: "s22/" + name for name in
       ("basic_frame", "basic_frame_00", "basic_frame_01", "common", "common_k", "gameparts", "graph", "title")},
    **{name: "source22/" + name for name in
       ("lane027", "lane040", "lane058", "lane068", "lane086", "lane087", "lane088", "lane089", "lnote00", "notes_cn00")},
}
SOURCE21_FRAMES = {"source21-gmframe0": "gmframe0"}
SOURCE21_STYLES = tuple(SOURCE21_FRAMES)
SOURCE21_ALIASES = {
    "gmframe0": "source21/gmframe0",
    **{name: "s21/" + name for name in ("basic_frame", "common", "gameparts", "graph", "title")},
}
SOURCE21_CUSTOM_PACKAGES = (
    *(f"beam{i:02}" for i in range(9)), *(f"exp{i:02}" for i in range(15)),
    *(f"fcombo{i:02}" for i in range(13)), *(f"judge{i:02}" for i in range(10)),
    *(f"lane{i:03}" for i in range(72)), "lnote00",
    *(f"notes{i:02}" for i in range(12)), "notes_cn00", *(f"turn{i:02}" for i in range(16)),
)
SOURCE20_FRAMES = {"source20-gmframe0": "gmframe0"}
SOURCE20_STYLES = tuple(SOURCE20_FRAMES)
SOURCE20_CORE_ALIASES = {
    "gmframe0": "source20/gmframe0",
    **{name: "s20/" + name for name in ("basic_frame", "common", "gameparts", "graph", "title")},
}
SOURCE20_DISTINCT_CUSTOM_ALIASES = {
    name: "source20/" + name for name in ("beam00", "notes00", "turn00")
}
SOURCE20_ALIASES = {**SOURCE20_CORE_ALIASES, **SOURCE20_DISTINCT_CUSTOM_ALIASES}
SOURCE20_CUSTOM_PACKAGES = (
    *(f"beam{i:02}" for i in range(6)), *(f"exp{i:02}" for i in range(13)),
    *(f"fcombo{i:02}" for i in range(11)), *(f"judge{i:02}" for i in range(6)),
    *(f"lane{i:03}" for i in range(43)), "lnote00",
    *(f"notes{i:02}" for i in range(10)), "notes_cn00", *(f"turn{i:02}" for i in range(13)),
)


def source_style(value):
    """Parse an existing numeric identity or an exact source-qualified key."""
    if isinstance(value, str):
        if value in SOURCE22_FRAMES or value in SOURCE21_FRAMES or value in SOURCE20_FRAMES:
            return value
        if value.isascii() and value.isdigit():
            value = int(value)
    if type(value) is int and value in (*BASE_STYLES, *EARLIER_STYLES):
        return value
    raise ValueError("Unqualified Standard frame identity: " + repr(value))


def frame_token(style):
    style = source_style(style)
    return f"{style:02}" if isinstance(style, int) else style.removeprefix("source")


def frame_id(style):
    style = source_style(style)
    return f"iidx{style:02}" if isinstance(style, int) else style


def package_key(style):
    style = source_style(style)
    if style in SOURCE20_FRAMES:
        return "source20/" + SOURCE20_FRAMES[style]
    if style in SOURCE21_FRAMES:
        return "source21/" + SOURCE21_FRAMES[style]
    return f"gmframe{style:02}" if isinstance(style, int) else "source22/" + SOURCE22_FRAMES[style]


def metadata(library):
    return json.loads((Path(library) / "library.json").read_text(encoding="utf-8-sig"))


def expanded(data):
    composition = data.get("sourceComposition")
    if composition is None:
        return False
    valid = composition.get("aliases24") == EARLIER_ALIASES and (
        composition.get("contract") == COMPOSITION or
        (composition.get("contract") in (COMPOSITION22, COMPOSITION21, COMPOSITION20) and
         composition.get("aliases22") == SOURCE22_ALIASES and
         (composition.get("contract") not in (COMPOSITION21, COMPOSITION20) or composition.get("aliases21") == SOURCE21_ALIASES) and
         (composition.get("contract") != COMPOSITION20 or composition.get("aliases20") == SOURCE20_ALIASES)))
    if not valid:
        raise ValueError("Unrecognized Standard source composition; do not infer source families")
    return True


def includes22(data):
    return expanded(data) and data["sourceComposition"]["contract"] in (COMPOSITION22, COMPOSITION21, COMPOSITION20)


def includes21(data):
    return expanded(data) and data["sourceComposition"]["contract"] in (COMPOSITION21, COMPOSITION20)


def includes20(data):
    return expanded(data) and data["sourceComposition"]["contract"] == COMPOSITION20


def retained_styles(data):
    """All preserved source identities, including archival-only frame art."""
    if not expanded(data):
        return BASE_STYLES
    return (tuple(sorted((*BASE_STYLES, *EARLIER_STYLES))) +
            (SOURCE22_STYLES if includes22(data) else ()) + (SOURCE21_STYLES if includes21(data) else ()) +
            (SOURCE20_STYLES if includes20(data) else ()))


def styles(data):
    """Source-qualified frames eligible for gameplay-theme generation."""
    return tuple(style for style in retained_styles(data)
                 if style not in SOURCE22_STYLES or style in SOURCE22_AVAILABLE_STYLES)


def families(data):
    if not expanded(data):
        return ("0", "1")
    return (("0", "1", "s23", "s24") + (("s22",) if includes22(data) else ()) +
            (("s21",) if includes21(data) else ()) + (("s20",) if includes20(data) else ()))


def layout_family(style):
    style = source_style(style)
    if style in SOURCE20_FRAMES:
        return "s20"
    if style in SOURCE21_FRAMES:
        return "s21"
    if style in SOURCE22_FRAMES:
        return "s22"
    return {23: "s23", 24: "s24"}.get(style, "0")


def presentation_family(style):
    style = source_style(style)
    return "1" if style == 25 else layout_family(style)


def centre_translation(style, layout, wide=False):
    """Qualified 20/21/22/24 steady SP anchors after their centre movement.

    The retained 23 and bare source22/gmframe0 art follows its supplying HDD's
    caller as an explicit adaptation; this does not prove a native menu route.
    """
    style = source_style(style)
    if style in SOURCE20_FRAMES:
        # Original20's own movie setter uses signed 35px; wide/single-DP
        # use their independent movie without enabling that movement.
        if layout not in ("p1", "p2", "dp"):
            raise ValueError("Unqualified Original20 layout")
        return 0 if layout == "dp" or wide else -35 if layout == "p2" else 35
    if layout == "dp" or wide:
        return 0
    offset = 35 if style in EARLIER_STYLES or style in SOURCE22_FRAMES or style in SOURCE21_FRAMES else 36
    return -offset if layout == "p2" else offset


def art_identity(data):
    if includes20(data):
        return "IIDX20+IIDX21+IIDX22+IIDX24+IIDX26"
    if includes21(data):
        return "IIDX21+IIDX22+IIDX24+IIDX26"
    if includes22(data):
        return "IIDX22+IIDX24+IIDX26"
    return "IIDX24+IIDX26" if expanded(data) else "IIDX26-only"


def family_suffix(family):
    if family not in ("0", "1", "s23", "s24", "s22", "s21", "s20"):
        raise ValueError("Unqualified Standard common family")
    return "" if family == "0" else "f" + family
