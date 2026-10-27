"""Original PNG art for native SongSelect hosts, with no list, data or actions.

This is an explicit OMS geometry adaptation, not a reconstruction of IIDX AFP layout.
"""
from __future__ import annotations

from dataclasses import dataclass
import json

CONTRACT = "oms-skin-pages.v1"
PAGE_FILE = "skin-pages.json"
NAME = "Lighting Model Skin Omni Until 33"
PACKAGE = "contents/data/graphic/1/mselect.ifs"
PACKAGE_SHA256 = "cd63c4fd79ed52625adcf1479225795511724d3346b7c13dd47eaa95e5e8dac8"
SOURCE_PREFIX = "packages/contents/data/graphic/1/mselect/sprites/"
ART_PREFIX = "pages/songselect33/"


@dataclass(frozen=True)
class Asset:
    identifier: str
    name: str
    width: int
    height: int
    sha256: str
    role: str

    @property
    def source(self):
        return SOURCE_PREFIX + self.name

    @property
    def destination(self):
        return ART_PREFIX + self.name


ASSETS = (
    Asset("ss-background", "m00_grada_blk.png", 8, 370,
          "465c1b8a9a000b78817afc005233c404e8f01af251ecd3c0e630cabd24daf0d3", "Native mainContent backdrop; no native layout replacement"),
    Asset("ss-edge", "mlist_bar_1.png", 30, 648,
          "cb269d52073384f7e2aa27a9eb1d40159a6cdb0125ceaaf3912b7eee95f58839", "Actual native carousel viewport edge below the filter"),
    Asset("ss-row", "listb_lightning.png", 750, 50,
          "91ccb87ea1485dd432a2e4c494c330496546691c92f14b5acdc9a54d1a4ee8bb", "Native unselected row Content chrome, retaining lamp and actual background"),
    Asset("ss-selected", "listr.png", 820, 120,
          "e95abd44ea7da0d6bc2a77b5d654a7f3b86674dabd705e121b52510916d9c324", "Native Selected or Expanded row Content chrome, no new selection authority"),
    Asset("ss-brand", "ssm_33th.png", 220, 110,
          "c9e064146b0398996198525b041ba9a6ae1d61e839bcdc1da3ed261f27ffdb7e", "Static theme watermark below real title/artist/controls"),
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def song_select_page():
    return {
        "resources": [{"id": asset.identifier, "path": asset.destination, "filtering": "linear"} for asset in ASSETS],
        "background": "ss-background", "listEdge": "ss-edge", "row": "ss-row",
        "selectedRow": "ss-selected", "brand": "ss-brand",
    }


def append_song_select(original: bytes):
    """Append one root property, retaining every existing Results JSON byte verbatim."""
    text = original.decode("utf-8", errors="strict")
    document = json.loads(text)
    require(isinstance(document, dict) and document.get("contract") == CONTRACT
            and set(document) == {"contract", "results"}, "Use the existing Results-only parent declaration")
    require(isinstance(document["results"], dict), "The existing Results declaration must remain intact")
    stripped = text.rstrip()
    require(stripped.endswith("}"), "The parent page is not one complete JSON object")
    encoded = (stripped[:-1] + ',"songSelect":' + json.dumps(song_select_page(), separators=(",", ":")) + "}\n").encode("utf-8")
    upgraded = json.loads(encoded)
    require(upgraded["results"] == document["results"] and upgraded["songSelect"] == song_select_page(), "Results changed during the native chrome append")
    require(encoded.startswith(stripped[:-1].encode("utf-8")), "The original Results bytes must be retained")
    resources = [resource for page in (upgraded["results"], upgraded["songSelect"]) for resource in page["resources"]]
    require(len(resources) <= 32 and len({item["id"] for item in resources}) == len(resources)
            and len({item["path"].lower() for item in resources}) == len(resources), "Both pages share the existing32-resource budget and identity domain")
    require(len(encoded) <= 64 * 1024, "The page declaration exceeds its unchanged64KiB budget")
    return encoded


def adaptation_receipt():
    return {
        "layoutBasis": "OMS native SongSelect host geometry adaptation; original IIDX AFP positions/animation not claimed",
        "nativeRows": "45px difficulty rows,72px standalone/set rows; actual Content size and Selected||Expanded state",
        "default": "Original native interface; explicit footer switch preserves all existing controls and selection",
        "scope": "Actual ruleset/settled WorkingBeatmap/mods/preferences; no ApplySkinScope/global skin mutation",
        "lifetime": "Single exact capsule owner, synchronous detach of pooled/live chrome before textures/lease release; mounted chrome blocks unstaged current refresh",
        "preserves": ["Carousel and all filters", "BMS lamp and real PanelSetBackground", "native metadata and score paths",
                      "MusicController preview/pause", "Mods/Random/Options/collections/Back", "SelectAndRun/PlayerLoader", "background reveal"],
        "newFonts": 0, "gameplayOptionsChanged": 0, "newDataSources": 0, "newActionsOrScripts": 0,
    }
