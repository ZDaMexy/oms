"""Lossless regular grids for the native Slider/HiddenCover/LiftCover APIs.

The host owns movement and clipping. A read-only TimerProperty selects an exact
grid cell; the original gameplay.attach clock still selects the source frame.
Only one source PNG and one file-backed destination page are decoded/mapped at
a time. Cached results belong to one AssetSink, never another family or build.
"""

from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass, replace
import hashlib
import io
import json
import math
import mmap
from pathlib import Path
import stat
import tempfile

from PIL import Image

from beatoraja_author import source_path
from beatoraja_native import Lua, lua


@dataclass(frozen=True)
class GridPage:
    order: int
    blend: str
    offset: tuple[int, int]
    size: tuple[int, int]
    columns: int
    rows: int
    crops: tuple[tuple[int, int, int, int, int], ...]
    frames: tuple[tuple[int, int], ...]
    clock: str
    source: str = ""

    @property
    def dimensions(self):
        return self.columns * self.size[0], self.rows * self.size[1]

    @property
    def draw(self):
        ranges = []
        for frame, _ in self.frames:
            if ranges and frame == ranges[-1][1] + 1:
                ranges[-1][1] = frame
            else:
                ranges.append([frame, frame])
        condition = " or ".join(f"frame == {a}" if a == b else f"(frame >= {a} and frame <= {b})"
                                for a, b in ranges)
        return Lua("function() local frame=" + self.clock + ";return " + condition + " end")

    @property
    def timer(self):
        # SkinHidden and SkinSourceImage divide TimerProperty microseconds by
        # 1000 using signed integer division. Return an exact multiple of 1000,
        # including when the synthetic start timestamp is negative.
        return Lua("(function() local cells=" + lua(dict(self.frames)) + ";return function() local cell=cells["
                   + self.clock + "];if cell == nil then return main_state.timer_off_value end;return "
                   + "(math.floor(main_state.time()/1000)-(cell*100+50))*1000 end end)()")

    def image(self):
        """A fresh specifier: callers may add their own ID without changing cache."""
        width, height = self.dimensions
        return {"src": self.source, "x": 0, "y": 0, "w": width, "h": height,
                "divx": self.columns, "divy": self.rows,
                "cycle": self.columns * self.rows * 100, "timer": self.timer}


def _integer(value):
    return isinstance(value, int) and not isinstance(value, bool)


def plan_cover_atlas(index, page_limit=2048):
    """Retain every source run, prefix, loop boundary and empty terminal frame."""
    if (index.get("contract") != "oms-iidx-baked-animation.v1"
            or not index.get("verification", {}).get("passed") or not index.get("selectionComplete")):
        raise ValueError("Cover grid requires a complete verified bake")
    frames, source_count = index["frames"], index["sourceFrameCount"]
    if (not _integer(source_count) or source_count < 1
            or [frame["sourceFrame"] for frame in frames] != list(range(source_count))):
        raise ValueError("Cover grid requires the entire ordered source frame sequence")
    if index.get("heldLoop") or index.get("trigger") != "gameplay.attach":
        raise ValueError("Cover grid requires its gameplay.attach clock")
    fps = index["framesPerSecond"]
    count = source_count - int(bool(frames[-1].get("terminal")))
    loop_start = index.get("loopStartFrame", 0) if index["loop"] else -1
    if (not math.isfinite(fps) or fps <= 0 or count <= 0
            or index["loop"] and (not _integer(loop_start) or not 0 <= loop_start < count)):
        raise ValueError("Invalid source animation rate or loop bounds")
    if not _integer(page_limit) or not 1 <= page_limit <= 2048:
        raise ValueError("Cover grid page limit must be an integer in 1..2048")
    clock = f"animation_frame(0,{fps},{count},{loop_start},0)"
    pages = {page["id"]: page for page in index["pages"]}
    if len(pages) != len(index["pages"]):
        raise ValueError("Duplicate source atlas page identity")
    groups = {}
    for frame in frames:
        if frame.get("terminal") and (frame is not frames[-1] or frame["runs"]):
            raise ValueError("The terminal cover frame must be last and empty")
        previous = -1
        for run in frame["runs"]:
            order, blend = run["order"], run["blend"]
            if not _integer(order) or order <= previous or blend not in ("normal", "additive"):
                raise ValueError("Baked run ordering/blend is not qualified")
            previous = order
            x, y, w, h = run["rect"]
            if (not all(_integer(v) for v in (x, y, w, h, *run["offset"]))
                    or len(run["offset"]) != 2 or min(x, y) < 0 or min(w, h) <= 0):
                raise ValueError("Cover crop and offset must be exact integer pixels")
            page = pages[run["page"]]
            if x + w > page["width"] or y + h > page["height"]:
                raise ValueError("Cover crop is outside its declared source page")
            if max(w, h) > page_limit:
                raise ValueError("A cover cell exceeds the qualified grid page limit")
            key = (order, blend, *run["offset"], w, h)
            cells, membership = groups.setdefault(key, ({}, {}))
            crop = (run["page"], x, y, w, h)
            membership[frame["sourceFrame"]] = cells.setdefault(crop, len(cells))
    result = []
    for (order, blend, ox, oy, w, h), (cells, membership) in sorted(groups.items()):
        crops = tuple(cells)
        max_columns, max_rows = page_limit // w, page_limit // h
        capacity = max_columns * max_rows
        for start in range(0, len(crops), capacity):
            selected = crops[start:start + capacity]
            # Minimum padding, then minimum longest edge, then near-square.
            columns, rows = min(((c, math.ceil(len(selected) / c)) for c in range(1, max_columns + 1)
                                 if math.ceil(len(selected) / c) <= max_rows),
                                key=lambda cr: (cr[0] * cr[1], max(cr[0] * w, cr[1] * h), abs(cr[0] * w - cr[1] * h)))
            result.append(GridPage(order, blend, (ox, oy), (w, h), columns, rows, selected,
                                   tuple((frame, cell - start) for frame, cell in membership.items()
                                         if start <= cell < start + len(selected)), clock))
    return tuple(result)


def _page_key(index_path, page):
    return (source_path(index_path.parent, page["file"]), page["sha256"], page["width"], page["height"])


def _page_bytes(key):
    path, digest, width, height = key
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != digest:
        raise ValueError("Cover source atlas hash does not match verified index")
    with Image.open(io.BytesIO(data)) as image:
        if image.format != "PNG" or image.size != (width, height):
            raise ValueError("Cover source atlas dimensions or format changed")
    return data


def _owned(path, root):
    """Never delete or map a redirected temporary path, even after a failure."""
    if not path.resolve(strict=True).is_relative_to(root) or path == root:
        raise ValueError("Cover grid temporary path escaped its output")
    current = path
    while current != root:
        details = current.lstat()
        if current.is_symlink() or getattr(details, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
            raise ValueError("Cover grid temporary path contains a reparse point")
        current = current.parent
    return path


def cover_atlas(sink, index_path, index, page_limit=2048):
    """Return immutable grid assets shared by cover, Lift and Graph Area aliases.

    Every input qualifies its own paths before using an equal pixel/frame-map
    cache entry. The cache retains grid assets, never the full input index, so
    read_bakes can release an index before loading another selection.
    """
    index_path = Path(index_path).resolve()
    grids = plan_cover_atlas(index, page_limit)
    pages = {page["id"]: page for page in index["pages"]}
    used_ids = {crop[0] for grid in grids for crop in grid.crops}
    page_keys = {pid: _page_key(index_path, pages[pid]) for pid in used_ids}
    # Paths, manifest labels and page IDs are deliberately excluded. Strict
    # Lift aliases may rename them while preserving all pixels and frame maps.
    digest = hashlib.sha256()
    digest.update(json.dumps([index["canvas"], page_limit], separators=(",", ":")).encode())
    for grid in grids:
        cells = [(page_keys[p][1:], x, y, w, h) for p, x, y, w, h in grid.crops]
        digest.update(json.dumps([grid.order, grid.blend, grid.offset, grid.size, grid.columns, grid.rows,
                                 cells, grid.frames, grid.clock], separators=(",", ":")).encode())
    identity = digest.hexdigest()
    if identity in sink._cover_atlases:
        for key in page_keys.values():
            if key not in sink._cover_verified_pages:
                _page_bytes(key)
                sink._cover_verified_pages.add(key)
        result = sink._cover_atlases[identity]
        return result
    if not grids:
        sink._cover_atlases[identity] = grids
        return grids

    work = Path(tempfile.mkdtemp(prefix=".cover-grid-", dir=sink.output))
    raw_paths = []
    try:
        transfers = defaultdict(lambda: defaultdict(list))
        for number, grid in enumerate(grids):
            path = work / f"{number:04d}.rgba"
            with path.open("xb") as stream:
                stream.truncate(math.prod(grid.dimensions) * 4)
            raw_paths.append(path)
            for cell, (pid, x, y, w, h) in enumerate(grid.crops):
                transfers[pid][number].append((cell, x, y, w, h))
        for pid in sorted(transfers):
            key = page_keys[pid]
            data = _page_bytes(key)
            with Image.open(io.BytesIO(data)) as source:
                source.load()
                rgba = source if source.mode == "RGBA" else source.convert("RGBA")
                try:
                    for number, copies in transfers[pid].items():
                        grid = grids[number]
                        width, _ = grid.dimensions
                        with _owned(raw_paths[number], sink.output).open("r+b") as stream:
                            with mmap.mmap(stream.fileno(), 0) as target:
                                for cell, x, y, w, h in copies:
                                    left, top = cell % grid.columns * w, cell // grid.columns * h
                                    with rgba.crop((x, y, x + w, y + h)) as crop:
                                        pixels = crop.tobytes()
                                    for row in range(h):
                                        start = ((top + row) * width + left) * 4
                                        target[start:start + w * 4] = pixels[row * w * 4:(row + 1) * w * 4]
                finally:
                    if rgba is not source:
                        rgba.close()
                    source.close()
            sink._cover_verified_pages.add(key)
            del data
        result = []
        for grid, path in zip(grids, raw_paths):
            with _owned(path, sink.output).open("r+b") as stream:
                with mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as pixels:
                    image = Image.frombuffer("RGBA", grid.dimensions, pixels, "raw", "RGBA", 0, 1)
                    try:
                        source_id = sink.add_raster(image)
                    finally:
                        # PIL's context-manager exit only closes file handles;
                        # close() releases the exported mmap pixel pointer too.
                        image.close()
            result.append(replace(grid, source=source_id))
            _owned(path, sink.output).unlink()
        _owned(work, sink.output).rmdir()
    except BaseException as error:
        (work / "failure.json.txt").write_text(json.dumps({"complete": False, "index": str(index_path),
            "identity": identity, "error": f"{type(error).__name__}: {error}",
            "note": "Only this fresh grid work directory is temporary; source bakes were not changed."}, indent=2), encoding="utf8")
        raise
    result = tuple(result)
    sink._cover_atlases[identity] = result
    return result
