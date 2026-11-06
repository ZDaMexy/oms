"""Map qualified cabinet lane planes to the shared OMS gameplay domain.

The earlier cabinet art has unnamed lane meshes. SINOBUZ compresses these
decorative meshes vertically; that animation is preserved independently of the
host's gameplay coordinates. Its native note scissor is 482px, while this
author retains the existing Standard 484px source-plane component convention.
"""
from standard_source_catalog import BASE_STYLES, EARLIER_STYLES


def source_lane_regions(reference, style, layout):
    if style not in (*BASE_STYLES, *EARLIER_STYLES) or layout not in ('p1', 'p2', 'dp'):
        raise ValueError('Unqualified Standard lane context')
    movie = 'center_frame_dp_1p' if layout == 'dp' else ('2p_frame' if layout == 'p2' else '1p_frame')
    draws = reference['layouts'][movie]['draws']
    if style in EARLIER_STYLES:
        lanes = sorted((d for d in draws if d['texture'] in ('sp_lane_1p', 'sp_lane_2p')),
                       key=lambda d: d['bounds']['left'])
        expected = ((304, 596), (684, 976)) if layout == 'dp' else (((940, 1232),) if layout == 'p2' else ((48, 340),))
        if len(lanes) != len(expected):
            raise ValueError('Earlier frame must contain its exact source lane planes')
        vertices = [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (292, 0), (0, 484), (292, 484))]
        result = []
        for draw, (left, right) in zip(lanes, expected):
            bounds, asset = draw['bounds'], draw['asset']
            if (asset['package'] != f'gmframe{style:02}' or (asset['width'], asset['height']) != (292, 484)
                    or draw['source_vertices'] != vertices
                    or any(abs(bounds[key] - value) > .01 for key, value in (('left', left), ('right', right), ('bottom', 484)))):
                raise ValueError('Earlier source lane dimensions or bottom anchor changed')
            result.append({'left': left, 'right': right, 'top': 0, 'bottom': 484})
        return result
    result = []
    for player in (('1p', '2p') if layout == 'dp' else (('2p',) if layout == 'p2' else ('1p',))):
        lanes = [d for d in draws if 'lane_' + player in d['game_bindings']]
        if not lanes:
            raise ValueError('Missing qualified named source lane plane')
        bounds = {key: (min if key in ('left', 'top') else max)(d['bounds'][key] for d in lanes)
                  for key in ('left', 'top', 'right', 'bottom')}
        if abs(bounds['right'] - bounds['left'] - 292) > .01 or abs(bounds['bottom'] - 484) > .01:
            raise ValueError('Unqualified source lane footprint')
        result.append(bounds)
    return result
