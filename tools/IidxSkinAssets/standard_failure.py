"""Export the26 ordinary STAGE FAILED movie, retaining all original frames."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from standard_components import StandardComponents


def validate_failure_source(source):
    if (source["movie"] != "miss" or source["stage"] != [0, 0, 1280, 720]
            or source["fps"] != 60 or source["sourceFrames"] != 180
            or source["labels"] or source["translatedRootControl"]):
        raise ValueError("The qualified26 failure movie requires its original full canvas and180 frames")


def export_failure(library, output, bemani_root, image_tools):
    exporter = StandardComponents(library, output, bemani_root, image_tools)
    clip = exporter.clip("0/gameparts", "miss", "sf.failed", loop=False, trigger="gameplay.fail")
    validate_failure_source(clip["source"])
    result = {"contract": "oms-standard-failure.v1", "librarySHA256": exporter.source_library_sha,
        "clip": clip, "files": list(exporter.files.values()),
        "receipt": {"source": "26/0/gameparts/miss,180 frames at60Hz,1280x720",
            "nativeLoad": "35388f/353d96 load the shared gameplay miss movie, not the unrelated boss failed_ prefixes.",
            "nativeTrigger": "354190 first handles ordinary chart completion;353960 then checks both participating-side failure/retirement states. On this failure branch354247 starts the miss movie.",
            "nativeExit": "353f90 keeps the failure state for at least152 native ticks before353ee0 exit fading. The full180-frame asset is retained; all180 frames being visible in the original caller is not asserted.",
            "hostContract": "Only actual gameplay.fail may trigger this nonlooping program. The author/host must retain and advance the failure presentation without changing the authoritative failure or inventing a low-Gauge trigger.",
            "sourcePNGBytesPreserved": True, "sourceFramesPreserved": True}}
    (output / "failure.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n", "utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ("library", "output", "bemani-root", "image-tools"):
        parser.add_argument("--" + field, type=Path, required=True)
    args = parser.parse_args()
    result = export_failure(args.library, args.output, args.bemani_root, args.image_tools)
    print(json.dumps({"output": str(args.output), "resources": len(result["clip"]["resources"]),
        "animations": len(result["clip"]["animations"]), "files": len(result["files"]), "source": result["clip"]["source"]}))
