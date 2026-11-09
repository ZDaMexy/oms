"""Map qualified cabinet lane planes to the shared OMS gameplay domain.

The earlier cabinet art has unnamed lane meshes. SINOBUZ compresses these
decorative meshes vertically; that animation is preserved independently of the
host's gameplay coordinates. The audited 22/24 native note scissor is 482px;
for21, only the separate cover-number clip has been independently qualified at
482px. This author retains its484px source-plane component convention without
claiming native21 note-coordinate parity.
"""
from standard_source_catalog import (EARLIER_STYLES, SOURCE20_STYLES, SOURCE21_STYLES, SOURCE22_STYLES,
                                     layout_family, package_key, source_style)


def lane_movie(style, layout):
    style = source_style(style)
    if layout not in ('p1', 'p2', 'dp'):
        raise ValueError('Unqualified Standard lane context')
    if layout == 'dp':
        return 'center_frame_dp' if style in SOURCE20_STYLES else 'center_frame_dp_1p'
    return '2p_frame' if layout == 'p2' else '1p_frame'


def _domains(layout):
    return ((304, 596), (684, 976)) if layout == 'dp' else (((940, 1232),) if layout == 'p2' else ((48, 340),))


def _vertices(width, height):
    return [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (width, 0), (0, height), (width, height))]


def _earlier_regions(reference, style, layout, draws):
    """Retain the original fixed plane, separately from its painted surround.

    Original21, four PENDUAL source packages and LINCLE DP have their own 292x484
    meshes. LINCLE SP and TRICORO bake the plane into a larger original raster;
    their measured local rectangles are projected through that raster's GEO.
    Original20 retains its own TRICORO panel and single DP-centre movie. Its
    local 292x484 component plane is an OMS convention, not a native scissor.
    No frame from the later ARC SCORE set supplies these coordinates or pixels.
    """
    package = package_key(style)
    if reference['package'] != package or reference['common_family'] != layout_family(style):
        raise ValueError('22 lane reference belongs to a different source package/family')
    expected = _domains(layout)
    baked = style in SOURCE20_STYLES or style == 'source22-gmframe9' or style == 'source22-gmframe8' and layout != 'dp'
    if not baked:
        lanes = sorted((d for d in draws if d['texture'] in ('sp_lane_1p', 'sp_lane_2p')),
                       key=lambda d: d['bounds']['left'])
        if len(lanes) != len(expected):
            raise ValueError('22 source must retain each original lane mesh')
        for draw, (left, right) in zip(lanes, expected):
            asset, matrix, bounds = draw['asset'], draw['matrix'], draw['bounds']
            if (asset['package'] != package or (asset['width'], asset['height']) != (292, 484)
                    or draw['source_vertices'] != _vertices(292, 484)
                    or any(abs(matrix[k] - v) > 1e-9 for k, v in (('a', 1), ('b', 0), ('c', 0), ('tx', left)))
                    or not 0 < matrix['d'] <= 1
                    or abs(matrix['ty'] + 484 * matrix['d'] - 484) > .01
                    or any(abs(bounds[k] - v) > .01 for k, v in (('left', left), ('right', right), ('bottom', 484)))):
                raise ValueError('22 source lane geometry or fixed bottom anchor changed')
    else:
        for group, (left, right) in enumerate(expected):
            is_right = group == 1 if layout == 'dp' else layout == 'p2'
            if style == 'source22-gmframe8':
                name, size, local_left = 'sp_frame_' + ('2p' if is_right else '1p'), (356, 720), 16 if is_right else 48
            else:
                name, size, local_left = 'frame_lane_' + ('2p' if is_right else '1p'), (354, 609), 5 if is_right else 57
            matches = [d for d in draws if d['texture'] == name]
            if len(matches) != 1:
                raise ValueError('22 baked lane requires its exact original panel draw')
            draw = matches[0]
            asset, matrix, bounds = draw['asset'], draw['matrix'], draw['bounds']
            translation = left - local_left
            if (asset['package'] != package or (asset['width'], asset['height']) != size
                    or draw['source_vertices'] != _vertices(*size)
                    or any(abs(matrix[k] - v) > 1e-9 for k, v in
                           (('a', 1), ('b', 0), ('c', 0), ('d', 1), ('tx', translation), ('ty', 0)))
                    or any(abs(bounds[k] - v) > .01 for k, v in
                           (('left', translation), ('right', translation + size[0]), ('top', 0), ('bottom', size[1])))):
                raise ValueError('22 baked lane panel identity or original GEO projection changed')
    return [{'left': left, 'right': right, 'top': 0, 'bottom': 484} for left, right in expected]


def source_lane_regions(reference, style, layout):
    style = source_style(style)
    if layout not in ('p1', 'p2', 'dp'):
        raise ValueError('Unqualified Standard lane context')
    movie = lane_movie(style, layout)
    draws = reference['layouts'][movie]['draws']
    if style in SOURCE20_STYLES or style in SOURCE21_STYLES or style in SOURCE22_STYLES:
        return _earlier_regions(reference, style, layout, draws)
    if style in EARLIER_STYLES:
        lanes = sorted((d for d in draws if d['texture'] in ('sp_lane_1p', 'sp_lane_2p')),
                       key=lambda d: d['bounds']['left'])
        expected = _domains(layout)
        if len(lanes) != len(expected):
            raise ValueError('Earlier frame must contain its exact source lane planes')
        vertices = _vertices(292, 484)
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
