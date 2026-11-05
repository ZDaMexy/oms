"""26 held-Start cover readouts using the actual OMS reading snapshot.

Native3523f0 draws font2 white units and centred green numbers, with a font5
max-min range. Source3520f7 gates each cover independently. The native26
white scale0..999 is deliberately not substituted for OMS's actual0..1000
configuration. No cover or speed value is calculated from key presses.
"""
from __future__ import annotations

from build_standard_skin import LOGICAL, node


CANVAS_WIDTH = 1280
SOURCE_HEIGHT = 484
SOURCE_CLIP = 482
WHITE_ANCHORS = {"p1": (129,), "p2": (1021,), "dp": (397, 767)}
# Numeric tween times are bounded by the public format. Beyond seven digits
# reserve the ten-cell width of a nonnegative Int32 endpoint, as Start does.
NUMERIC_WIDTHS = ((0, 1), (10, 2), (100, 3), (1000, 4), (10000, 5),
                  (100000, 6), (1000000, 7), (10000000, 10))


def add_cover_readouts(frame, prepared_font):
    """Append one full-canvas contribution to the existing Global owner.

    The caller has already declared ``prepared_font['fonts']`` and its atlases.
    This adds no fonts, resources, recorded animations or material owners.
    Relative children of the source482px clip use that height, not720px.
    """
    prefix = frame.prefix + ".cover-readout"

    def text(identifier, value, *, x=0, y=0, size="normal", source=None, colour="#ffffffff"):
        height = 20 if size == "normal" else 18
        item = node(identifier, "text", {"x": x / CANVAS_WIDTH, "y": y / SOURCE_CLIP,
            "width": 120 / CANVAS_WIDTH, "height": height / SOURCE_CLIP,
            "bitmap-font": prepared_font["fonts"][size], "font-size": height * LOGICAL,
            "text": value, "format": "integer", "alignment": "left", "colour": colour})
        if source:
            frame.bind(identifier, "text", source)
        return item

    def linear(identifier, source, first, last, maximum):
        frame.scene["tracks"].append({"id": identifier + ".position", "type": "tween",
            "target": identifier, "property": "y", "clock": source, "loop": False, "easing": "linear",
            "keyframes": [{"id": identifier + ".position.begin", "time": 0, "value": first / SOURCE_CLIP},
                          {"id": identifier + ".position.end", "time": maximum, "value": last / SOURCE_CLIP}]})

    def padded(identifier, source, *, centre=False, colour="#ffffffff"):
        """Source %03d minimum width with one actual-value text binding.

        Separate literal zeros preserve the source cells without demanding a
        new public integer formatter or reserving three dynamic-value nodes.
        """
        value = text(identifier + ".value", "", source=source, colour=colour)
        two = text(identifier + ".zeros.two", "00", x=-18 if centre else 0, colour=colour)
        one = text(identifier + ".zeros.one", "0", x=-18 if centre else 0, colour=colour)
        frame.steps(two["id"], "opacity", source, [(0, 1), (10, 0)])
        frame.steps(one["id"], "opacity", source, [(0, 0), (10, 1), (100, 0)])
        if centre:
            offsets = [(0, 6), (10, -6)] + [(threshold, -digits * 6)
                for threshold, digits in NUMERIC_WIDTHS if threshold >= 100]
        else:
            offsets = [(0, 24), (10, 12), (100, 0)]
        frame.steps(value["id"], "x", source, [(threshold, offset / CANVAS_WIDTH) for threshold, offset in offsets])
        return node(identifier, children=[two, one, value])

    def green_range(identifier, y):
        # Split the total-width translation across the two real endpoint
        # clocks. This centres max + hyphen + min without derived font maths
        # or guessing the range from the already-rounded current green value.
        high = text(identifier + ".max", "", source="scroll.green-number-max", size="small", colour="#00ff00ff")
        dash = text(identifier + ".hyphen", "-", size="small", colour="#00ff00ff")
        low = text(identifier + ".min", "", x=8, source="scroll.green-number-min", size="small", colour="#00ff00ff")
        tail = node(identifier + ".tail", children=[dash, low])
        head = node(identifier + ".head", children=[high, tail])
        width = node(identifier + ".width", children=[head])
        frame.steps(tail["id"], "x", "scroll.green-number-max",
                    [(threshold, digits * 8 / CANVAS_WIDTH) for threshold, digits in NUMERIC_WIDTHS])
        frame.steps(head["id"], "x", "scroll.green-number-max",
                    [(threshold, -digits * 4 / CANVAS_WIDTH) for threshold, digits in NUMERIC_WIDTHS])
        frame.steps(width["id"], "x", "scroll.green-number-min",
                    [(threshold, -(digits * 4 + 4) / CANVAS_WIDTH) for threshold, digits in NUMERIC_WIDTHS])
        result = node(identifier, properties={"x": 104 / CANVAS_WIDTH, "y": y / SOURCE_CLIP}, children=[width])
        frame.bind(identifier, "visible", "scroll.green-number-range-available")
        return result

    roots = []
    for side, x in enumerate(WHITE_ANCHORS[frame.layout], 1):
        for kind, source, available in (("sudden", "cover.sudden-units", "cover.sudden-active"),
                                         ("hidden", "cover.hidden-units", "cover.hidden-active"),
                                         ("lift", "lift.value", "lift.available")):
            identifier = prefix + f".p{side}." + kind
            # Original26 is downscroll. Lift reversal is a deliberate OMS
            # geometry projection; it uses the actual applied offset ratio.
            orientations = (False, True) if kind == "lift" else (False,)
            for reversed_lift in orientations:
                root_id = identifier + (".reversed" if reversed_lift else ".normal")
                top = kind == "sudden" or reversed_lift
                white = padded(root_id + ".white", source)
                green = padded(root_id + ".green", "scroll.green-number", centre=True, colour="#00ff00ff")
                green["properties"]["x"] = 104 / CANVAS_WIDTH
                row = node(root_id, properties={"x": x / CANVAS_WIDTH},
                           children=[white, green, green_range(root_id + ".range", -16 if top else 22)])
                if kind == "lift":
                    linear(root_id, "lift.offset", -26 if top else SOURCE_HEIGHT,
                           SOURCE_HEIGHT - 26 if top else 0, 1)
                    frame.variant(root_id, "visible", "lift.reversed", False,
                                  {"true" if reversed_lift else "false": True})
                else:
                    linear(root_id, source, -26 if top else SOURCE_HEIGHT,
                           SOURCE_HEIGHT - 26 if top else 0, 1000)
                gate = node(root_id + ".active", children=[row])
                frame.bind(gate["id"], "visible", available)
                roots.append(gate)

    root = node(prefix + ".source-clip", "clip", {"height": SOURCE_CLIP / 720, "clip-mode": "bounds"}, children=roots)
    for name, source in (("adjusting", "scroll.adjusting"), ("available", "scroll.available"),
                         ("version", "scroll.hi-speed-available")):
        root = node(prefix + "." + name, children=[root])
        frame.bind(root["id"], "visible", source)
    frame.info.append(root)
    frame.evidence.append({"coverReadouts": {
        "sourceCallerRVA": "3520f7/3523f0; source font2/5; original fixed software table535060",
        "whiteAnchors": list(WHITE_ANCHORS[frame.layout]), "greenOffset": 104,
        "sourceGeometry": {"laneHeight": SOURCE_HEIGHT, "bottomClip": SOURCE_CLIP,
            "sudden": "pixels-26", "hiddenAndLift": "484-pixels",
            "greenRangeY": {"sudden": -16, "hiddenAndLift": 22}},
        "sourceFormat": "White %03d left; current green %03d or %04d centred; font5 unpadded max-min centred. Exact public values drive each text.",
        "actualUnitsAdaptation": "OMS0..1000 configured/applied units are displayed without replacing them by native26 floor(pixels*999/482). Full1000 uses four original cells. Cover edges use the actual484px authored lane fraction; Lift uses its actual applied offset, not configured white units.",
        "visibility": "Actual Start adjusting plus available hi-speed state; each actual cover-active or Lift-available gate. Independent from Start panel preference. Inactive configured cover values cannot reveal readouts; zero Lift lies beyond the original482px clip.",
        "reversedLiftAdaptation": "Actual reversed Lift uses its top edge and the evidenced top-edge text offsets; native26 downscroll layout does not claim this reversed mode.",
        "largeEndpointAdaptation": "Beyond seven digits, reserve ten original cells within the public numeric-track time ceiling, as the Start panel does.",
        "scope": "Fixed source software coordinates are independent of historical frame and centre Graph translation. No new image, font, recorded-animation, scoring or input state."}})
