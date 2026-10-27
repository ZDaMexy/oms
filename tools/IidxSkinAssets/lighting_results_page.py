"""First read-only results page using original main-screen Until33 bitmaps.

All placement, scale and static layer order here are an explicit OMS adaptation.
This module neither calculates score/clear/DJ LEVEL nor interprets original AFP.
"""
from __future__ import annotations

from dataclasses import dataclass
import json

CONTRACT = "oms-skin-pages.v1"
PAGE_FILE = "skin-pages.json"
RESULT_PACKAGE = "contents/data/graphic/1/result.ifs"
RESULT_SPRITES = "packages/contents/data/graphic/1/result/sprites/"
ART_PREFIX = "pages/results33/"
CANVAS = (1920, 1080)
NAME = "Lighting Model Skin Omni Until 33"


@dataclass(frozen=True)
class Asset:
    identifier: str
    name: str
    width: int
    height: int
    role: str

    @property
    def source(self):
        return RESULT_SPRITES + self.name

    @property
    def destination(self):
        return ART_PREFIX + self.name


ASSETS = (
    Asset("neutral", "c_bg.png", 46, 360, "Opaque main-result texture, black multiplier supplies a neutral backing only"),
    Asset("clear-bg", "33re_bg_clear.png", 1920, 1080, "Actual ruleset clear outcome background"),
    Asset("failed-bg", "33re_bg_failed.png", 1920, 1080, "Actual ruleset failed-clear outcome background"),
    Asset("outer-shadow", "33re_bg_shadow_outer.png", 1920, 1080, "Original main-screen outer shadow"),
    Asset("liquid", "33re_liquid.png", 1920, 1080, "Original main-screen liquid bitmap, static low-opacity adaptation"),
    Asset("frame-left", "33re_sparkle_waku_01.png", 590, 430, "Original clear frame corner"),
    Asset("frame-left-failed", "33re_sparkle_waku_01failed.png", 590, 430, "Original failed frame corner"),
    Asset("frame-right", "33re_sparkle_waku_02.png", 416, 672, "Original clear frame side"),
    Asset("frame-right-failed", "33re_sparkle_waku_02failed.png", 416, 672, "Original failed frame side"),
    Asset("clear-title", "txt_clear.png", 788, 126, "Original main-result CLEAR word art, true clear only"),
    Asset("failed-title", "txt_failed.png", 654, 102, "Original main-result FAILED word art, false clear only"),
    Asset("brand", "33re_tx_ver33.png", 200, 90, "Original Until33 main-result branding"),
    Asset("score-frame", "waku_score_1p.png", 548, 74, "Original score frame"),
    Asset("score-heading", "score_panel_s.png", 220, 40, "Original generic SCORE word art, no EX-SCORE assumption"),
    Asset("judge-shadow", "waku_judge_shadow.png", 284, 154, "Original judgement-list shadow, uniformly enlarged"),
)

HIT_RESULTS = ("perfect", "great", "good", "ok", "meh", "miss", "combo_break")
TEXT_SOURCES = frozenset(("beatmap.title", "beatmap.artist", "beatmap.version", "score.value", "score.label",
                          "score.accuracy", "score.accuracy.label", "score.max-combo", "score.max-combo.label",
                          *(source for result in HIT_RESULTS for source in ("hit." + result, "hit." + result + ".label"))))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def rectangle(identifier, kind, x, y, width, height, **fields):
    return {"id": identifier, "type": kind, "x": x, "y": y, "width": width, "height": height, **fields}


def sprite(identifier, resource, x, y, width, height, *, cleared=None, opacity=1, colour="ffffffff"):
    result = rectangle(identifier, "sprite", x, y, width, height, resource=resource)
    if cleared is not None:
        result["visibleWhen"] = {"source": "score.cleared", "value": cleared}
    if opacity != 1:
        result["opacity"] = opacity
    if colour != "ffffffff":
        result["colour"] = colour
    return result


def text(identifier, source, x, y, width, height, size, *, align="left", colour="ffffffff"):
    return rectangle(identifier, "text", x, y, width, height, source=source, fontSize=size, align=align, colour=colour)


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "A page JSON object contains a duplicate key")
        result[key] = value
    return result


def _not_json_number(value):
    raise ValueError("A page declaration contains a non-JSON number: " + value)


def _page_members(encoded):
    """Locate root values for an exact byte-preserving splice, not a second semantic page codec."""
    value = encoded.decode("utf-8", errors="strict")
    decoder = json.JSONDecoder(object_pairs_hook=_unique_object, parse_constant=_not_json_number)

    def whitespace(index):
        while index < len(value) and value[index] in " \t\r\n":
            index += 1
        return index

    index = whitespace(0)
    require(index < len(value) and value[index] == "{", "The existing page must be a JSON object")
    index = whitespace(index + 1)
    members = {}
    while index < len(value) and value[index] != "}":
        require(value[index] == '"', "A page property name must be a JSON string")
        key, index = decoder.raw_decode(value, index)
        require(key not in members, "The page root contains a duplicate key")
        index = whitespace(index)
        require(index < len(value) and value[index] == ":", "Missing page property separator")
        start = whitespace(index + 1)
        parsed, end = decoder.raw_decode(value, start)
        members[key] = (start, end, parsed)
        index = whitespace(end)
        require(index < len(value) and value[index] in ",}", "The page root is incomplete")
        if value[index] == "}":
            break
        index = whitespace(index + 1)
        require(index < len(value) and value[index] != "}", "A page root has a trailing comma")
    require(index < len(value) and value[index] == "}" and whitespace(index + 1) == len(value),
            "The page must be one complete JSON object")
    require("contract" in members and members["contract"][2] == CONTRACT, "Use the public page contract")
    return value, members, index


def page_member_bytes(encoded):
    value, members, _ = _page_members(encoded)
    return {key: value[start:end].encode("utf-8") for key, (start, end, _) in members.items()}


def replace_results_page(original, results):
    """Replace only Results; retain every byte outside its root value, including SongSelect JSON."""
    replacement = json.dumps(results, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    if original is None:
        return ('{"contract":' + json.dumps(CONTRACT) + ',"results":' + replacement + '}\n').encode("utf-8")
    value, members, closing = _page_members(original)
    if "results" in members:
        require(isinstance(members["results"][2], dict), "The existing Results property must be an object")
        start, end, _ = members["results"]
        return (value[:start] + replacement + value[end:]).encode("utf-8")
    return (value[:closing] + ("," if members else "") + '"results":' + replacement + value[closing:]).encode("utf-8")


def results_page():
    """Build only the initial public page document; gameplay and user choices have no inputs here."""
    elements = [
        sprite("neutral-background", "neutral", 0, 0, *CANVAS, colour="000000ff"),
        sprite("clear-background", "clear-bg", 0, 0, *CANVAS, cleared=True, colour="bebebeff"),
        sprite("failed-background", "failed-bg", 0, 0, *CANVAS, cleared=False, colour="6c6c6cff"),
        sprite("outer-shadow", "outer-shadow", 0, 0, *CANVAS, opacity=.45),
        sprite("liquid-bitmap", "liquid", 0, 0, *CANVAS, opacity=.06),
        sprite("clear-left-frame", "frame-left", 0, 650, 590, 430, cleared=True),
        sprite("failed-left-frame", "frame-left-failed", 0, 650, 590, 430, cleared=False),
        sprite("clear-right-frame", "frame-right", 1504, 205, 416, 672, cleared=True),
        sprite("failed-right-frame", "frame-right-failed", 1504, 205, 416, 672, cleared=False),
        sprite("left-reading-panel", "neutral", 66, 158, 665, 862, colour="000000ff", opacity=.68),
        text("song-title", "beatmap.title", 82, 40, 1450, 68, 46),
        text("song-artist", "beatmap.artist", 84, 108, 1000, 40, 25, colour="dce8ffff"),
        text("chart-name", "beatmap.version", 1110, 112, 430, 36, 24, align="right", colour="dce8ffff"),
        sprite("version-brand", "brand", 1640, 42, 200, 90),
        sprite("score-heading", "score-heading", 88, 167, 220, 40),
        sprite("score-frame", "score-frame", 88, 220, 548, 74),
        text("native-score-label", "score.label", 108, 224, 210, 25, 20),
        text("native-score-value", "score.value", 108, 248, 505, 44, 40, align="right"),
        text("accuracy-heading", "score.accuracy.label", 110, 320, 246, 40, 24),
        text("maximum-combo-heading", "score.max-combo.label", 390, 320, 240, 40, 24, align="right"),
        text("accuracy-value", "score.accuracy", 110, 368, 246, 56, 38),
        text("maximum-combo-value", "score.max-combo", 390, 368, 240, 56, 38, align="right"),
        sprite("judge-list-shadow", "judge-shadow", 84, 462, 596.4, 323.4, opacity=.85),
        sprite("clear-outcome-word", "clear-title", 1050, 146, 500, 500 * 126 / 788, cleared=True),
        sprite("failed-outcome-word", "failed-title", 1025, 142, 550, 550 * 102 / 654, cleared=False),
    ]
    for index, result in enumerate(HIT_RESULTS):
        y = 470 + index * 48
        elements.extend((text("judge-label-" + result, "hit." + result + ".label", 112, y, 290, 42, 27),
                         text("judge-count-" + result, "hit." + result, 425, y, 202, 42, 29, align="right")))
    elements.append(rectangle("native-ruleset-statistics", "native-statistics", 800, 235, 1035, 790))
    document = {"contract": CONTRACT, "results": {"width": CANVAS[0], "height": CANVAS[1],
        "resources": [{"id": asset.identifier, "path": asset.destination, "filtering": "linear"} for asset in ASSETS],
        "elements": elements}}
    validate_generated_page(document)
    return document


def validate_generated_page(document):
    """Check this generator's fixed output; the shared C# codec remains the author contract authority."""
    require(document.get("contract") == CONTRACT, "Use the public results page contract")
    page = document["results"]
    require((page["width"], page["height"]) == CANVAS, "The first results adaptation uses a1920x1080 canvas")
    resources = {resource["id"] for resource in page["resources"]}
    require(len(resources) == len(ASSETS) <= 32, "Each copied original bitmap must have one resource")
    elements = page["elements"]
    require(len(elements) <= 128 and len({element["id"] for element in elements}) == len(elements), "Page element identities must be unique and bounded")
    require({element["resource"] for element in elements if element["type"] == "sprite"} == resources, "Do not ship unused original art")
    texts = [element for element in elements if element["type"] == "text"]
    require(len(texts) <= 32 and {element["source"] for element in texts} == TEXT_SOURCES, "Only true read-only score sources belong on the page")
    require(sum(256 * (element["fontSize"] + 16) ** 2 for element in texts) <= 64 * 1024 * 1024, "The public text budget must remain unchanged")
    require(len([element for element in elements if element["type"] == "native-statistics"]) == 1, "DJ/Clear/Gauge use one real native statistics host")
    for element in elements:
        require(element["x"] >= 0 and element["y"] >= 0 and element["width"] >= 1 and element["height"] >= 1
                and element["x"] + element["width"] <= CANVAS[0] and element["y"] + element["height"] <= CANVAS[1],
                "A result element leaves its authored canvas")
        if "visibleWhen" in element:
            require(element["visibleWhen"]["source"] == "score.cleared" and type(element["visibleWhen"]["value"]) is bool,
                    "Completion must never substitute for an authoritative clear outcome")
    require(len(json.dumps(document, separators=(",", ":")).encode("utf-8")) <= 64 * 1024, "Page JSON exceeds its public budget")


def adaptation_receipt():
    return {
        "canvas": list(CANVAS), "placement": "OMS initial readable results adaptation; not original AFP geometry or timing",
        "source": "Only original contents/data/graphic/1/result.ifs sprites; no screenshot or sub-premium art",
        "defaultPage": "The existing client always opens its original page; this optional declaration offers its normal page switch",
        "clear": "score.cleared from Ruleset.GetResultsClearState; true/false pick original matching backgrounds, frames and word art",
        "unknownClear": "Neither clear/failed art is shown; the black multiplier backing remains and native statistics retains unavailable state",
        "score": "score.value is the native ScoreManager display value; score.label comes from that score's ruleset; no static EX-SCORE label",
        "judgements": "Both labels and counts use hit.* from that score's ruleset, including its historical compatibility policy",
        "accuracyAndCombo": "Actual score.accuracy and score.max-combo with their neutral .label sources; no MISS COUNT aggregate is invented",
        "outcomeWords": "Original main-result txt_clear/txt_failed English bitmaps at their true aspect ratios; clear-state branches unchanged",
        "rankAndGauge": "One native-statistics host; score.rank is not used and no percentage-derived DJ LEVEL is made",
        "numbers": "Current public native text only; original score digit bitmaps and djt ordinal images are not used",
        "backgroundMultipliers": {"neutral": "000000ff", "clear-bg": "bebebeff", "failed-bg": "6c6c6cff"},
        "bitmapBytes": "Copied byte-for-byte; static composition, tint, opacity and placements are explicit author adaptation",
        "pbOpponentAndCharts": "No fabricated PB, opponent, ghost, rank change or standalone chart",
        "nativeOperations": "Existing original score list, replay, retry and collection controls are owned by ResultsScreen",
        "codecValidation": "pending", "nativeValidation": "pending", "archive": "not-created",
    }
