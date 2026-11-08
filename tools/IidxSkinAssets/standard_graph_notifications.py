"""Export the two retained Graph notification families without changing their art.

The native level selector replaces one named child's image in lv_pass. It does
not select between three children or require a new animation. The shared update
movie is exported separately so a single score change can restart it only once.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path

from build_fullcombo_catalog import all_tags
from standard_components import StandardComponents


MOVIES = ("mybest", "target", "update", "lv_pass")
RANK_TEXTURES = {"aaa": "pass_aaa", "aa": "pass_aa", "a": "pass_a"}
TRIGGER = "score.crossing"


def validate_notification_source(source):
    if (source["movie"] not in MOVIES or source["stage"] != [0, 0, 280, 720]
            or source["fps"] != 60 or source["sourceFrames"] != 150
            or source["durationMs"] != 2500 or source["labels"] or source["translatedRootControl"]):
        raise ValueError("Graph notifications require the full original280x720,150-frame60Hz movie")


def validate_notification_program(program):
    if (program["width"] != 280 or program["height"] != 720 or program["fps"] != 60
            or program["loop"] is not False or program["trigger"] != TRIGGER
            or len(program["frames"]) != 151 or program["frames"][-1]["nodes"]
            or any(key in program for key in ("heldLoop", "releaseFrame", "loopStartFrame"))):
        raise ValueError("Graph notifications must retain150 source frames and their empty terminal boundary")


def rank_child_contract(source, root, tools):
    """Trace the actual root's named child through its original sprite chain.

    Unused sprite definitions can contain another placement with the same name;
    searching all definitions for that string would mistake them for live peers.
    """
    movie = source["movies"]["lv_pass"]
    placements = [tag for tag in movie.tags if isinstance(tag, tools.AP2PlaceObjectTag)
                  and tag.movie_name == "lv_pass" and not tag.update]
    if len(placements) != 1:
        raise ValueError("The source must have one root placement named lv_pass")
    placement = placements[0]
    definitions = [tag for tag in all_tags(movie.tags, tools.AP2DefineSpriteTag)
                   if getattr(tag, "id", None) is not None]
    reference, chain = placement.source_tag_id, []
    while True:
        if reference in chain:
            raise ValueError("The named rank child contains a cyclic source reference")
        chain.append(reference)
        matches = [tag for tag in definitions if tag.id == reference]
        if len(matches) != 1:
            raise ValueError("The named rank child has an ambiguous or absent source definition")
        definition = matches[0]
        if not isinstance(definition, tools.AP2DefineSpriteTag):
            shape = getattr(definition, "reference", None)
            if shape not in source["mapping"]:
                raise ValueError("The named rank child does not end in a qualified source shape")
            break
        children = [tag for tag in definition.tags
                    if isinstance(tag, tools.AP2PlaceObjectTag) and not tag.update]
        if len(children) != 1:
            raise ValueError("The named rank child must contain exactly one image subtree")
        reference = children[0].source_tag_id

    geometry_index = source["mapping"][shape]
    geometry = source["geometry"][geometry_index]
    if len(geometry["draws"]) != 1 or geometry["draws"][0]["texture"] is None:
        raise ValueError("The named rank child must contain one textured draw")
    draw = geometry["draws"][0]
    row = source["rows"][draw["texture"]]
    if (Path(row["name"]).stem != "pass_aaa" or [row["width"], row["height"]] != [276, 36]
            or geometry["vertices"] != [[0, 0, 0], [276, 0, 0], [0, 36, 0], [276, 36, 0]]
            or draw["uv"] != [[0, 0], [1, 0], [0, 1], [1, 1]]
            or draw["indices"] != [0, 1, 2, 2, 1, 3] or draw["flags"] != 3
            or draw["sourceColour"] is not None or geometry["colours"]):
        raise ValueError("The original rank quad must exactly cover its276x36 source image")

    placed = [node for node in root["nodes"] if node["sourceObject"] == placement.object_id
              and node["sourceTag"] == placement.source_tag_id and node["depth"] == placement.depth
              and node["parent"] == 0]
    if len(placed) != 1:
        raise ValueError("The named source child is not unique in the recorded live hierarchy")
    target = placed[0]["id"]
    descendants = {target}
    for node in root["nodes"]:
        if node["parent"] in descendants:
            descendants.add(node["id"])
    meshes = [node for node in root["nodes"] if node["id"] in descendants and node["geometry"] is not None]
    matching = [node for node in root["nodes"] if node["geometry"] == geometry_index]
    if len(meshes) != 1 or meshes != matching:
        raise ValueError("The replacement image must belong only to the one named child")
    return {"namedChild": "lv_pass", "sourceObject": placement.object_id,
            "sourceDepth": placement.depth, "sourceTagChain": chain, "sourceShape": shape,
            "sourceGeometry": geometry_index, "recordedMeshNode": meshes[0]["id"],
            "sourceTexture": "pass_aaa", "sourceSize": [276, 36],
            "vertices": geometry["vertices"], "uv": draw["uv"], "triangles": draw["indices"]}


def replace_rank_texture(program, source_resource, replacement_resource, source_row, replacement_row, child):
    """Change just the named image reference; retain every other program field."""
    validate_notification_program(program)
    if (Path(source_row["name"]).stem != "pass_aaa"
            or Path(replacement_row["name"]).stem not in RANK_TEXTURES.values()
            or [source_row["width"], source_row["height"]] != child["sourceSize"]
            or [replacement_row["width"], replacement_row["height"]] != child["sourceSize"]
            or source_row["sampler"] != replacement_row["sampler"]):
        raise ValueError("Rank replacement must retain the qualified original image dimensions and sampler")
    for row in (source_row, replacement_row):
        left, right, top, bottom = row["uvRect"]
        if [right - left, bottom - top] != child["sourceSize"]:
            raise ValueError("Rank image UV rectangle does not describe the complete source image")
    matches = [index for index, geometry in enumerate(program["geometries"])
               if geometry["resource"] == source_resource]
    if len(matches) != 1:
        raise ValueError("The named rank image must have one unique runtime geometry")
    index = matches[0]
    expected = {"resource": source_resource,
                "vertices": [[*point, *uv] for point, uv in zip(child["vertices"], child["uv"], strict=True)],
                "triangles": child["triangles"]}
    if program["geometries"][index] != expected:
        raise ValueError("Runtime rank geometry no longer matches the named original source quad")
    result = copy.deepcopy(program)
    result["geometries"][index]["resource"] = replacement_resource
    restored = copy.deepcopy(result)
    restored["geometries"][index]["resource"] = source_resource
    if restored != program:
        raise ValueError("Rank replacement changed original geometry, state, frame or playback data")
    return result, {"sourceTexture": "pass_aaa", "replacementTexture": Path(replacement_row["name"]).stem,
        "runtimeGeometry": index, "sourceSize": child["sourceSize"],
        "sourceImageRect": source_row["imageRect"], "sourceUVRect": source_row["uvRect"],
        "replacementImageRect": replacement_row["imageRect"], "replacementUVRect": replacement_row["uvRect"],
        "sourcePNG_SHA256": source_row["sha256"], "replacementPNG_SHA256": replacement_row["sha256"],
        "allOtherProgramFieldsUnchanged": True, "sourcePNGBytesPreserved": True}


def _write_program(exporter, prefix, program):
    import brotli
    encoded = json.dumps(program, separators=(",", ":"), allow_nan=False).encode("utf-8")
    compressed = brotli.compress(encoded, quality=5)
    if len(compressed) > 16 * 1024**2 or len(encoded) > 128 * 1024**2:
        raise ValueError("Graph notification exceeds the public recorded-clip byte limit")
    relative = f"custom/{prefix}/clip.json.br"
    path = exporter.output / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as destination:
        destination.write(compressed)
    exporter.files[relative] = {"path": relative, "sha256": hashlib.sha256(compressed).hexdigest(), "bytes": len(compressed)}
    return relative


def export_graph_notifications(library, output, bemani_root, image_tools):
    import brotli
    exporter = StandardComponents(library, output, bemani_root, image_tools)
    families = {}
    for family in exporter.source_families:
        package = family + "/graph"
        clips, programs = {}, {}
        for movie in MOVIES:
            key = "aaa" if movie == "lv_pass" else movie
            clip = exporter.clip(package, movie, f"sgn.f{family}.{key}", loop=False, trigger=TRIGGER)
            validate_notification_source(clip["source"])
            program = json.loads(brotli.decompress((exporter.output / clip["animations"][0]["path"]).read_bytes()))
            validate_notification_program(program)
            clips[key], programs[key] = clip, program

        source = exporter._package(package)
        root = json.loads((exporter.output / f"research/sgn.f{family}.aaa/source-root.json").read_text("utf-8"))
        child = rank_child_contract(source, root, exporter.tools)
        source_declaration, source_row = exporter.asset("sgn", package, "pass_aaa")
        rank_proofs = {}
        for rank, texture in RANK_TEXTURES.items():
            declaration, row = exporter.asset("sgn", package, texture)
            program, proof = replace_rank_texture(programs["aaa"], source_declaration["id"], declaration["id"],
                                                  source_row, row, child)
            rank_proofs[rank] = proof
            if rank == "aaa":
                clips[rank] = copy.deepcopy(clips[rank])
            else:
                prefix = f"sgn.f{family}.{rank}"
                relative = _write_program(exporter, prefix, program)
                clip = copy.deepcopy(clips["aaa"])
                resources = {resource["id"]: resource for resource in clip["resources"]
                             if resource["id"] != source_declaration["id"]}
                resources[declaration["id"]] = declaration
                clip["resources"] = list(resources.values())
                clip["animations"] = [{"id": prefix + ".clip", "path": relative, "resources": sorted(resources)}]
                clip["node"].update(id=prefix + ".node", animation=prefix + ".clip")
                clips[rank] = clip
            clips[rank]["receipt"]["rankTextureReplacement"] = proof

        receipt = {"sourceFamily": family, "sourceFramesPreserved": True, "sourcePNGBytesPreserved": True,
            "lvPassNamedChild": child, "rankTextureReplacements": rank_proofs,
            "sharedUpdate": "One independent150-frame movie. Restart once per genuine score update that crosses any eligible threshold; never stack a copy for each simultaneously crossed threshold.",
            "sourceAnchor": {"x": "1000+Graph offset", "y": 0},
            "sourcePriorities": {"mybest": 67, "target": 67, "lv_pass": 67, "update": 68},
            "sourceBlend": "Internal AFP/GEO blend is preserved. The native update setting0x80000001 is recorded without guessing a blend-mode meaning.",
            "nativeEligibility": {"mybest": "Once: actual currentEX strictly exceeds an available positive PB.",
                "target": "Once: actual currentEX strictly exceeds an available positive frozen target.",
                "rank": "A/AA/AAA use>=; when one score change reaches several levels, only the highest newly reached rank plays.",
                "disabled": "Native preference suppresses drawing while its one-time crossing eligibility is still consumed."},
            "sourceSelectionBoundary": "0/1 identify the retained26 library; s23/s24 explicitly alias the24 HDD root/0 artwork. All native priority, anchor and eligibility references in this receipt were audited in26 and are OMS author adaptations for the earlier families, not claims about their historical executables.",
            "excluded": "achieve_pass has source art but no verified ordinary-play activation and is not exported."}
        if family == "s22":
            receipt.update({
                "sourcePriorities": {"mybest": 57, "target": 57, "lv_pass": 57, "update": 58},
                "sourceSelectionBoundary": "s22 identifies the original supplied22 graph package, including its own PNGs. Its caller loads these four movies, replaces the named lv_pass child with pass_aaa/pass_aa/pass_a, and places one shared update behind the notifications. Source shape qualification and rank replacement are checked from this package, never borrowed from a later family.",
                "hostEligibilityBoundary": "Actual OMS score-crossing events drive the retained programs. The22 caller independently has one-time PB/target crossings and a highest-rank selector; this author retains the existing OMS positive/available threshold gates and does not reconstruct22 per-note ghosts, preference storage or events missed before attach."})
        elif family == "s21":
            receipt.update({
                "sourceSelectionBoundary": "s21 selects the supplied original21 graph package and its own PNG/GEO resources. The four complete AFP programs and named rank-child replacement are qualified from that package. Matching22 AFP bytes do not establish matching native callers.",
                "hostEligibilityBoundary": "Actual OMS score-crossing events use the existing positive/available gates and highest-rank rule. The67/68 ordering and1000+Graph-offset placement recorded here are retained OMS author adaptations from the26 reference, not independently qualified21 native priority/anchor/threshold rules."})
        families[family] = {"clips": clips, "receipt": receipt}
        research = exporter.output / f"research/sgn.f{family}"
        research.mkdir(parents=True, exist_ok=True)
        (research / "rank-replacement-proof.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", "utf-8")

    result = {"contract": "oms-standard-graph-notifications.v1", "librarySHA256": exporter.source_library_sha,
              "families": families, "files": list(exporter.files.values())}
    (exporter.output / "graph-notifications.json").write_text(
        json.dumps(result, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n", "utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ("library", "output", "bemani-root", "image-tools"):
        parser.add_argument("--" + field, type=Path, required=True)
    args = parser.parse_args()
    result = export_graph_notifications(args.library, args.output, args.bemani_root, args.image_tools)
    print(json.dumps({"output": str(args.output), "files": len(result["files"]),
                      "families": {family: list(value["clips"]) for family, value in result["families"].items()}}))
