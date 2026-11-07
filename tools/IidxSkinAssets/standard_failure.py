"""Export both retained ordinary failure movies, retaining all original frames."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from standard_components import StandardComponents


def validate_failure_source(source):
    if (source["movie"] != "miss" or source["stage"] != [0, 0, 1280, 720]
            or source["fps"] != 60 or source["sourceFrames"] != 180
            or source["labels"] or source["translatedRootControl"]):
        raise ValueError("The qualified failure movie requires its original full canvas and180 frames")


def export_failure(library, output, bemani_root, image_tools):
    exporter = StandardComponents(library, output, bemani_root, image_tools)
    families = {}
    for family in exporter.source_families:
        identifier = {"0": "sf.failed", "1": "sf.failed.legacy"}.get(family, "sf.failed." + family)
        clip = exporter.clip(f"{family}/gameparts", "miss", identifier, loop=False, trigger="gameplay.fail")
        validate_failure_source(clip["source"])
        receipt = {"source": f"26 HDD/{family}/gameparts/miss,180 frames at60Hz,1280x720",
            "hostContract": "Only actual gameplay.fail may trigger this nonlooping program. Retain the original180 frames on the OMS3000ms failure presentation without changing authoritative failure or inventing a low-Gauge trigger.",
            "sourcePNGBytesPreserved": True, "sourceFramesPreserved": True}
        if family == "0":
            receipt.update({
            "nativeLoad": "35388f/353d96 load the shared gameplay miss movie, not the unrelated boss failed_ prefixes.",
            "nativeTrigger": "354190 first handles ordinary chart completion;353960 then checks both participating-side failure/retirement states. On this failure branch354247 starts the miss movie.",
            "nativeExit": "353f90 keeps the failure state for at least152 native ticks before353ee0 exit fading. The full180-frame asset is retained; all180 frames being visible in the original caller is not asserted."})
        elif family == "1":
            receipt.update({
                "selection": "Retained family1 artwork accompanies the CANNON BALLERS frame in this author work.",
                "sourceContent": "The independent miss movie includes its original x_font/x_noise_bg children, laser/noise and ENGINE BLOW-OUT artwork.",
                "nativeBoundary": "This26 executable does not select family1. The25 executable/caller and its exit timing have not been supplied; the original asset is mapped to the established OMS failure event/outro, not asserted to reproduce25 software timing."})
        elif family == "s22":
            receipt.update(source="22 HDD/gameparts/miss,180 frames at60Hz,1280x720",
                nativeLoad="The supplied22 executable loads the shared gameparts miss movie for its play scene. The original22 clock/shield failure artwork and full source program are retained.",
                nativeBoundary="Only actual OMS gameplay.fail starts this source movie. The OMS3000ms failure presentation is an explicit host adaptation; the original22 scene exit duration and all180 frames being visible in its caller are not asserted.")
        else:
            receipt.update(source=f"24 HDD/{'0/' if family == 's24' else ''}gameparts/miss,180 frames at60Hz,1280x720",
                nativeBoundary="Original 24 HDD artwork and full source timeline; the OMS failure event/outro is an explicit host adaptation. This does not assert original 23 executable timing.")
        families[family] = {"clip": clip, "receipt": receipt}
    result = {"contract": "oms-standard-failure.v2", "librarySHA256": exporter.source_library_sha,
              "families": families, "files": list(exporter.files.values())}
    (output / "failure.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n", "utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ("library", "output", "bemani-root", "image-tools"):
        parser.add_argument("--" + field, type=Path, required=True)
    args = parser.parse_args()
    result = export_failure(args.library, args.output, args.bemani_root, args.image_tools)
    print(json.dumps({"output": str(args.output), "files": len(result["files"]),
        "families": {family: {"resources": len(value["clip"]["resources"]),
            "animations": len(value["clip"]["animations"]), "source": value["clip"]["source"]}
            for family, value in result["families"].items()}}))
