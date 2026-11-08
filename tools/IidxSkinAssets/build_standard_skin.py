#!/usr/bin/env python3
"""Build the independent, local Standard Model Until 26 author package.

The only artwork inputs are its verified library, cabinet research and component
exports. Lightning author directories and packages are never inputs. This emits
ordinary OMS author files; public CLI and real Player verification remain gates.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
from standard_source_catalog import (frame_token, frame_id, package_key, source_style,
                                     SOURCE21_STYLES, SOURCE22_STYLES, SOURCE22_AVAILABLE_STYLES)
import shutil
import sys
from urllib.parse import unquote
from standard_source_catalog import (styles as source_styles, families as source_families,
                                     retained_styles, layout_family, presentation_family, art_identity, centre_translation)

sys.dont_write_bytecode = True

NAME = 'Standard Model Skin Omni Until 26'
# These are gmframe theme IDs inside Rootage, not independently sourced game versions.
# 03/08/12/14/16/17/19/21 are Rootage ARC SCORE revivals; 26 is its base theme.
# 25 is retained HDD artwork, not an entry in this Rootage binary's theme selector.
# Keep IDs stable for existing author selections; source scope is documented separately.
FRAMES = {3: '3rd style', 8: '8th style', 12: 'HAPPY SKY', 14: 'GOLD',
          16: 'EMPRESS', 17: 'SIRIUS', 19: 'Lincle', 21: 'SPADA',
          25: 'CANNON BALLERS', 26: 'Rootage'}
EARLIER_FRAME_LABELS = {23: 'copula', 24: 'SINOBUZ'}
SOURCE22_FRAME_LABELS = {
    'source22-gmframe0': 'SPADA (22 HDD retained)',
    'source22-gmframe0-00': '22 PENDUAL PRESENT',
    'source22-gmframe0-01': '22 PENDUAL FUTURE',
    'source22-gmframe8': 'LINCLE (22 CUSTOM)',
    'source22-gmframe9': 'tricoro (22 CUSTOM)',
    'source22-gmframe10': 'SPADA (22 CUSTOM)',
}


def frame_label(style):
    if style in SOURCE21_STYLES:
        return '21 SPADA (Original)'
    if style in SOURCE22_FRAME_LABELS:
        return SOURCE22_FRAME_LABELS[style]
    return f'{style:02} {FRAMES.get(style, EARLIER_FRAME_LABELS.get(style))}'

CANVAS = (1280, 720)
LOGICAL = 768 / 720
MANIFEST = 'oms-gameplay-skin-manifest.v1'
SCENE = 'oms-gameplay-skin-scene.v1'
EVENT = 'oms-gameplay-skin-event.v1'
COMMON = '[GameplaySkin.Common:1]'


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n', encoding='utf-8')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def contained(root, relative):
    path = (root / unquote(relative)).resolve(strict=True)
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError(f'Input leaves its declared root: {relative}')
    return path


def node(identifier, kind='container', properties=None, resource=None, slot=None, children=()):
    value = {'id': identifier, 'type': kind, 'target': {'kind': 'global'}, 'blend': 'inherit',
             'properties': properties or {}, 'effects': [], 'children': list(children)}
    if resource is not None:
        value['resource'] = resource
    if slot:
        value['slot'] = slot
    return value


def document(identifier):
    return {'contract': SCENE, 'root': node(identifier), 'tracks': [], 'stateMachines': [],
            'bindings': [], 'variants': [], 'templates': [], 'instances': []}


def rect(bounds, surface=(0, 0, 1280, 720), dx=0):
    x, y, width, height = surface
    return {'x': (bounds['left'] + dx - x) / width, 'y': (bounds['top'] - y) / height,
            'width': (bounds['right'] - bounds['left']) / width,
            'height': (bounds['bottom'] - bounds['top']) / height}


def box(x, y, width, height):
    return {'left': x, 'top': y, 'right': x + width, 'bottom': y + height}


def union(draws):
    bounds = [d['bounds'] for d in draws]
    if not bounds:
        raise ValueError('A required original binding is absent')
    return {'left': min(b['left'] for b in bounds), 'top': min(b['top'] for b in bounds),
            'right': max(b['right'] for b in bounds), 'bottom': max(b['bottom'] for b in bounds)}


def largest_clear_rectangle(image, x0, x1, y0=0, y1=None):
    """Largest completely transparent rectangle in one actual cabinet opening.

    This is a safe OMS BGA aperture, not a reconstruction of the game decoder's
    hidden crop/fit policy. The unmodified cabinet still overlays its exact edge.
    """
    alpha = image.getchannel('A')
    pixels = alpha.load()
    heights = [0] * (x1 - x0)
    best = (0, None)
    for y in range(y0, image.height if y1 is None else y1):
        for x in range(x0, x1):
            heights[x - x0] = heights[x - x0] + 1 if pixels[x, y] == 0 else 0
        stack = []
        for i, height in enumerate([*heights, 0]):
            start = i
            while stack and stack[-1][1] > height:
                left, h = stack.pop()
                area = (i - left) * h
                if i - left >= 80 and h >= 80 and area > best[0]:
                    best = area, box(x0 + left, y + 1 - h, i - left, h)
                start = left
            if not stack or stack[-1][1] < height:
                stack.append((start, height))
    if best[1] is None:
        raise ValueError('No verified transparent BGA aperture in the source cabinet')
    return best[1]


def recorded_branch(branch):
    """Read an existing source-qualified program, without copying its files."""
    import brotli
    animation, = branch['animations']
    source, = (item for item in branch['files'] if item['path'] == animation['path'])
    if branch['node']['animation'] != animation['id'] or sha(source['source']) != source['sha256']:
        raise ValueError('Cabinet recorded program differs from its source receipt')
    return json.loads(brotli.decompress(Path(source['source']).read_bytes()))


def recorded_frame_draws(program, frame_index):
    """Canonical draw order, including every original ancestor and source UV.

    A single-frame export renumbers its tables. Compare the referenced data,
    not those incidental indices, and retain camera/mask/colour/blend state.
    """
    frame = program['frames'][frame_index]
    chains, draws = {}, []
    for node_index, state_index in frame['nodes']:
        item = program['nodes'][node_index]
        chain = ([] if item['parent'] == -1 else chains[item['parent']]) + [
            {'state': program['states'][state_index], 'blend': item['blend']}]
        chains[node_index] = chain
        if item['geometry'] >= 0:
            draws.append({'geometry': program['geometries'][item['geometry']], 'ancestors': chain})
    return {'canvas': [program['width'], program['height']], 'fps': program['fps'],
            'camera': frame['camera'], 'draws': draws}


def steady_program_reuse(original, exported, split):
    """Prove each old static layer is the same source frame of its motion clip."""
    sample, fps = original['source']['steadyFrameZeroBased'], original['source']['fps']
    motion = exported['motion']['layers'] if split else [exported['motion']]
    steady = exported['steady']['layers'] if split else [exported['steady']]
    kinds = [branch.get('kind', 'frame') for branch in motion]
    if kinds != [branch.get('kind', 'frame') for branch in steady]:
        raise ValueError('Cabinet steady/motion layer order differs; cannot share programs')
    proofs = []
    for dynamic, static in zip(motion, steady, strict=True):
        moving, held = recorded_branch(dynamic), recorded_branch(static)
        if (type(sample) is not int or not 0 <= sample < len(moving['frames']) - 1
                or moving['fps'] != fps or held['fps'] != fps or len(held['frames']) != 2
                or moving['frames'][-1]['nodes'] or held['frames'][-1]['nodes']):
            raise ValueError('Cabinet steady frame is outside its complete original motion')
        bindings = lambda branch: [{key: value for key, value in binding.items() if key not in ('id', 'target')}
                                   for binding in branch.get('bindings', [])]
        selected, previous = recorded_frame_draws(moving, sample), recorded_frame_draws(held, 0)
        if (selected != previous or dynamic['node']['blend'] != static['node']['blend']
                or bindings(dynamic) != bindings(static)):
            raise ValueError('Cabinet steady frame geometry, hierarchy or brightness differs from motion')
        signature = json.dumps(selected, sort_keys=True, separators=(',', ':'), allow_nan=False).encode('utf-8')
        proofs.append({'kind': dynamic.get('kind', 'frame'), 'motionAnimation': dynamic['animations'][0]['id'],
                       'retiredSteadyAnimation': static['animations'][0]['id'], 'draws': len(selected['draws']),
                       'sourceFrameDrawSHA256': hashlib.sha256(signature).hexdigest()})
    return motion, {'movie': original['source']['movie'], 'steadyFrameZeroBased': sample,
                    'fps': fps, 'fixedClockMs': sample * 1000 / fps, 'layers': proofs,
                    'oldPrograms': len(motion) + len(steady), 'newPrograms': len(motion),
                    'equivalence': 'Exact source-frame camera, geometry/UV, ordered ancestor transforms, masks, colours and blends; lane brightness bindings retained. No PNG or motion-phase changes.'}


def select_gauge_assets(gauge, source_data):
    """Keep retained archive evidence outside the usable frame's runtime closure."""
    available = {f'f{frame_token(style)}' for style in source_styles(source_data)}
    retained = {f'f{frame_token(style)}' for style in retained_styles(source_data)}
    if not available <= gauge['frames'].keys() <= retained:
        raise ValueError('Gauge exports must cover every usable frame and only qualified retained sources')
    frames = {key: value for key, value in gauge['frames'].items() if key in available}
    required = {item['path'] for layouts in frames.values() for clip in layouts.values()
                for field in ('resources', 'animations') for item in clip[field]}
    files = [row for row in gauge['files'] if row['path'] in required]
    if {row['path'] for row in files} != required:
        raise ValueError('Selected Gauge animation/resource file closure is incomplete')
    return frames, files


class Package:
    def __init__(self, library, output, image_tools):
        self.library, self.output = library.resolve(strict=True), output.resolve()
        data = read(self.library / 'library.json')
        self.source_data = data
        self.frame_styles = source_styles(data)
        if data['status'] != 'complete' or data['sourceMetadataUnchanged'] is not True:
            raise ValueError('Use the complete independently verified source library')
        if self.output.exists() or self.output.drive.casefold() == 'c:':
            raise ValueError('Use a new non-system author directory')
        if self.output.is_relative_to(self.library) or self.library.is_relative_to(self.output):
            raise ValueError('Keep the author directory separate from the source library')
        self.output.mkdir(parents=True)
        sys.path.insert(0, str(image_tools.resolve(strict=True)))
        from PIL import Image
        self.Image = Image
        self.index = {(row['package'].removeprefix('contents/data/graphic/').removesuffix('.ifs'),
                       Path(row['name']).stem): row for row in data['textures']}
        self.files, self.sources, self.receipts = {}, {}, []
        self.additional_options, self.reading_labels = {}, {}
        self.exclusions = data.get('excludedSources', [])
        self.empty_path = 'standard/empty.png'
        target = self.output / self.empty_path
        target.parent.mkdir(parents=True)
        Image.new('RGBA', (1, 1)).save(target)
        self.files[self.empty_path] = sha(target)
        self.solid_path = 'standard/author-solid.png'
        Image.new('RGBA', (1, 1), 'white').save(self.output / self.solid_path)
        self.files[self.solid_path] = sha(self.output / self.solid_path)

    def copy(self, source, relative, expected=None):
        source = Path(source)
        expected = expected or sha(source)
        if sha(source) != expected:
            raise ValueError(f'Source changed: {source}')
        destination = (self.output / relative).resolve()
        if not destination.is_relative_to(self.output) or destination == self.output:
            raise ValueError('Output path escapes the author directory')
        if destination.exists():
            if sha(destination) != expected:
                raise ValueError(f'Conflicting author content: {relative}')
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)
        self.files[relative] = expected

    def asset(self, package, name):
        row = self.index[package, name]
        # Direct sprite declarations and recorded frame exports own different IDs.
        # copula shares one exact PNG between those owners; keep their file paths
        # separate so a selected manifest never declares two IDs for one path.
        relative = f"standard/direct-art/{row['sha256']}.png"
        self.copy(contained(self.library, row['sprite']), relative, row['sha256'])
        self.sources[package, name] = {'package': package, 'name': name, 'source': row['sprite'],
                                     'path': relative, 'sha256': row['sha256'],
                                     'imageRect': row['imageRect'], 'uvRect': row['uvRect']}
        # Same source art may be referenced by different frame branches; IDs remain exact.
        identifier = 'std.t.h' + row['sha256'][:24]
        return {'id': identifier, 'type': 'texture', 'path': relative, 'filter': 'nearest'}, row


class Frame:
    def __init__(self, owner, research, style, layout, *, wide=False):
        self.owner, self.research, self.style, self.layout = owner, research, style, layout
        self.common_family = presentation_family(style)
        if wide and layout == 'dp':
            raise ValueError('The evidenced wide movies belong to single-player layouts')
        self.wide = wide
        self.key, self.prefix = package_key(style), f'std.f{frame_token(style)}.{layout}'
        if wide:
            self.prefix += '.wide'
        self.map = read(research / 'skin-reference-map.json')
        if self.map['stage'] != {'width': 1280, 'height': 720, 'fps': 60.0}:
            raise ValueError('This source needs a different explicit canvas projection')
        self.dp, self.p2 = layout == 'dp', layout == 'p2'
        self.player = '2p' if self.p2 else '1p'
        self.side = 'dp_frame_1p' if self.dp else '2p_frame' if self.p2 else '1p_frame'
        self.centre = ('center_frame_dp_1p' if self.dp else
                       'center_frame_wide_' + self.player if wide else 'center_frame_sp')
        # SINOBUZ's ordinary SP/Graph captures prove +35/-35 against independent
        # centre and side-frame anchors. copula retained art uses that 24 caller
        # as an explicit adaptation. Existing 26-based themes retain their +36
        # placement and mirrored counterpart. Wide and DP use their source movies.
        self.dx = centre_translation(style, layout, wide)
        self.scene = document(self.prefix + '.root')
        self.resources, self.fonts, self.animations = {}, {}, {}
        self.ini, self.info, self.evidence = [], [], []
        self.material_targets = {}
        self.empty = self.resource({'id': 'std.empty', 'type': 'texture', 'path': owner.empty_path})

    def resource(self, declaration):
        key = declaration['id']
        if key in self.resources and self.resources[key] != declaration:
            raise ValueError(f'Conflicting exact resource {key}')
        self.resources[key] = declaration
        return key

    def asset(self, package, name):
        declaration, row = self.owner.asset(package, name)
        return self.resource(declaration), row

    def bind(self, target, prop, source):
        self.scene['bindings'].append({'id': target + '.b.' + prop, 'target': target, 'property': prop, 'source': source})

    def steps(self, target, prop, source, values):
        identifier = target + '.step.' + prop
        self.scene['tracks'].append({'id': identifier, 'type': 'tween', 'target': target,
            'property': prop, 'clock': source, 'loop': False, 'easing': 'step',
            'keyframes': [{'id': identifier + f'.n{i}', 'time': time, 'value': value}
                          for i, (time, value) in enumerate(values)]})

    def variant(self, target, prop, source, default, cases):
        if prop not in ('visible', 'resource'):
            raise ValueError('This builder emits only the public visibility and resource variants')
        self.scene['variants'].append({'id': target + '.v.' + prop, 'target': target, 'property': prop,
            'source': source, 'default': default, 'cases': [
                {'id': target + f'.c.n{i}', 'key': key, 'resource' if prop == 'resource' else 'value': value}
                for i, (key, value) in enumerate(cases.items())]})

    def template(self, name, root, slot):
        identifier = self.prefix + '.t.' + name
        material = (f'markers/frame-{self.layout}-information' if name in ('information', 'graph')
                    else f'standard/markers/f{frame_token(self.style)}-{self.layout}-{name}')
        self.owner.copy(self.owner.output / self.owner.empty_path, material + '.png')
        root['slot'] = slot
        self.scene['templates'].append({'id': identifier, 'root': root})
        self.scene['instances'].append({'id': identifier + '.i', 'template': identifier,
                                       'material': {'slot': slot, 'resource': material}})
        return material

    def draws(self, name, movie=None):
        movies = [movie] if movie else [self.side, self.centre]
        return [d for m in movies for d in self.map['layouts'][m]['draws'] if name in d['game_bindings']]

    def font(self, suffix, package, names, advance=None, *, uniform_height=True):
        identifier = f'std.font.f{frame_token(self.style)}.{suffix}'
        glyphs = []
        height = None
        for character, name in names.items():
            resource, row = self.asset(package, name)
            height = height or row['height']
            if uniform_height and height != row['height']:
                raise ValueError(f'Font glyph height differs in {package}/{suffix}')
            height = max(height, row['height'])
            glyphs.append({'character': character, 'resource': resource,
                           'advance': advance if advance is not None else row['width']})
        self.fonts[identifier] = {'id': identifier, 'lineHeight': height, 'glyphs': glyphs}
        return identifier, height

    def number(self, name, source, bounds, font, height, *, fmt='integer', dx=0, alignment='right'):
        identifier = self.prefix + '.' + name
        result = node(identifier, 'text', {**rect(bounds, dx=dx), 'text': '', 'bitmap-font': font,
                      'font-size': height * LOGICAL, 'format': fmt, 'alignment': alignment,
                      'colour': '#ffffffff'})
        self.bind(identifier, 'text', source)
        return result

    def field_rects(self):
        from standard_lane_geometry import source_lane_regions
        values = source_lane_regions(self.map, self.style, self.layout)
        if self.style in (23, 24):
            self.evidence.append({'earlierLaneDomain': {
                'sourceTextureSize': [292, 484],
                'omsGameplayDomain': values,
                'adaptation': 'The complete source-local lane height is anchored at the original bottom484. The24 art retains its original vertical compression. Its audited native note scissor is482px; the OMS author retains its existing484px component convention and hit offset. This is not historical native note-coordinate parity.'}})
        if self.style in SOURCE22_STYLES:
            self.evidence.append({'source22LaneDomain': {'sourcePackage': self.key,
                'omsGameplayDomain': values, 'nativeNoteClipHeight': 482,
                'adaptation': 'Own original lane GEO or the qualified lane region of a combined cabinet mesh. OMS retains its484px component domain and hit offset, without claiming native note-coordinate parity.'}})
        if self.style in SOURCE21_STYLES:
            self.evidence.append({'source21LaneDomain': {'sourcePackage': self.key,
                'omsGameplayDomain': values, 'nativeCoverClipHeight': 482,
                'adaptation': 'Own original484px lane GEO. The independently checked21 cover renderer clips at482px; OMS retains its484px component domain and hit offset, without claiming native note-coordinate parity.'}})
        return values

    def profiles(self):
        stages = self.field_rects()
        effect = union(self.draws('effect', self.centre))
        mods_rect = [(effect['left'] + self.dx) / 1280, effect['top'] / 720,
                     (effect['right'] - effect['left']) / 1280,
                     (min(effect['bottom'], 720) - effect['top']) / 720]
        self.evidence.append({'mods': 'Native OMS Mod controls inside the original effect-area hook; author adaptation.',
                              'sourceBounds': effect, 'normalizedBounds': mods_rect})
        result = []
        for ruleset, modes in (('bms', (5, 7, 14)), ('mania', (5, 6, 7, 8, 14, 16))):
            for mode in modes:
                if (mode > 8) != self.dp:
                    continue
                count = (8 if ruleset == 'bms' else mode // 2) if self.dp else mode + 1 if ruleset == 'bms' else mode
                keymode = f'{count}k-{count}k' if ruleset == 'mania' and self.dp else f'{mode}k'
                output_stages = []
                for group, bounds in enumerate(stages):
                    right = group == 1 if self.dp else self.p2
                    widths = [34, 26, 34, 26, 34, 26, 34, 60] if right else [60, 34, 26, 34, 26, 34, 26, 34]
                    starts, cursor = [], 2
                    for width in widths:
                        starts.append(cursor)
                        cursor += width + 2
                    if ruleset == 'bms':
                        slots = list(range(8)) if self.dp else ([7, *range(2 if mode == 5 else 0, 7)] if right else list(range(count)))
                    elif count in (5, 7):
                        slots = list(range(7 - count, 7)) if right else list(range(1, count + 1))
                    else:
                        slots = list(range(8 - count, 8)) if right else list(range(count))
                    output_stages.append({'rect': [bounds['left']/1280, bounds['top']/720, 292/1280,
                                                   (bounds['bottom']-bounds['top'])/720],
                                          'lanes': [[starts[i]/292, widths[i]/292] for i in slots],
                                          'keys': [[starts[i]/292, widths[i]/292] for i in slots]})
                profile = {'id': self.prefix + f'.{ruleset}.k{mode}', 'ruleset': ruleset, 'keymode': keymode,
                               'presentation': 'any' if self.dp else self.layout, 'stages': output_stages,
                               'keyAreaHeight': 80/720, 'gaugeHeight': 74/720, 'hitPosition': 1,
                               'modsRect': mods_rect}
                if ruleset == 'bms' and layout_family(self.style) == '0':
                    profile['normalSpeedPreviewRect'] = [{'p1':304, 'p2':942, 'dp':686}[self.layout]/1280, 0, 34/1280, 477/720]
                result.append(profile)
        return result

    def background(self):
        children = []
        preparation = 0
        for movie, dx in ((self.centre, self.dx), (self.side, 0)):
            source_export = self.research / 'recorded' / movie / 'export.json'
            original = read(source_export)
            preparation = max(preparation, original['motion']['receipt']['loopStartFrame'] * 1000 / self.map['stage']['fps'])
            split = movie in ('1p_frame', '2p_frame', 'center_frame_dp_1p')
            if split:
                exported = read(self.owner.presentation_assets / self.key / movie / 'export.json')
                if (exported['contract'] != 'oms-standard-lane-layers.v1'
                        or exported['source'] != original['source']
                        or exported['receipt']['sourceExportSHA256'] != sha(source_export)):
                    raise ValueError('Lane layers differ from the selected original cabinet export')
                self.evidence.append({'laneBrightness': exported['receipt']})
            else:
                exported = original
            layers, proof = steady_program_reuse(original, exported, split)
            proof['sourceExportSHA256'] = sha(source_export)
            if split:
                source_root = source_export.parent / 'source-root.json'
                if exported['receipt']['sourceRootSHA256'] != sha(source_root):
                    raise ValueError('Lane split no longer matches its original source frame graph')
                proof['sourceRootSHA256'] = exported['receipt']['sourceRootSHA256']
            self.evidence.append({'steadyProgramReuse': proof})
            fixed_clock = self.prefix + '.m-' + movie.replace('_', '-') + '.steady-time'
            self.scene.setdefault('numericSources', []).append(
                {'id': fixed_clock, 'operation': 'add', 'left': proof['fixedClockMs'], 'right': 0})
            # Both branches use the complete original motion. Only the inactive
            # entry choice changes; its steady branch samples an immutable time.
            for branch in layers:
                for file in branch['files']:
                    self.owner.copy(file['source'], file['path'], file['sha256'])
                for resource in branch['resources']:
                    self.resource(resource)
                for animation in branch['animations']:
                    if animation['id'] in self.animations and self.animations[animation['id']] != animation:
                        raise ValueError('Animation ID collision')
                    self.animations[animation['id']] = animation
            variants = []
            for mode in ('motion', 'steady'):
                layer_nodes = []
                branch_id = self.prefix + '.m-' + movie.replace('_', '-') + '.' + mode
                for index, branch in enumerate(layers):
                    animation_node = copy.deepcopy(branch['node'])
                    source_id = animation_node['id']
                    animation_node['id'] = branch_id + f'.animation{index}'
                    animation_node['properties'] = {'x': dx/1280, 'y': 0, 'width': 1, 'height': 1}
                    animation_node['clock'] = 'presentation.elapsed' if mode == 'motion' else 'value.' + fixed_clock
                    for binding in branch.get('bindings', []):
                        if binding['target'] != source_id:
                            raise ValueError('Lane brightness must bind its own original layer')
                        self.bind(animation_node['id'], binding['property'], binding['source'])
                    layer_nodes.append(animation_node)
                parent = node(branch_id + '.gate', children=layer_nodes)
                self.variant(parent['id'], 'visible', 'option.frame-opening', mode == 'steady',
                             {'true': mode == 'motion', 'false': mode == 'steady'})
                variants.append(parent)
            children.extend(variants)
        self.scene['preparationDuration'] = preparation
        from standard_reading import add_preview_background
        if layout_family(self.style) == '0':
            add_preview_background(self, children)
        return self.template('background', node(self.prefix + '.background', children=children), 'stage.background')

    def apertures(self, *, graph_visible=True):
        if not graph_visible and (self.style not in SOURCE21_STYLES or not self.dp):
            raise ValueError('Four Graph-OFF openings are qualified only for original21 DP')
        image = self.owner.Image.open(self.research / self.map['layouts'][self.centre]['static_frame']).convert('RGBA')
        if self.dx:
            shifted = self.owner.Image.new('RGBA', CANVAS)
            shifted.alpha_composite(image, (self.dx, 0))
            image = shifted
        side = self.owner.Image.open(self.research / self.map['layouts'][self.side]['static_frame']).convert('RGBA')
        image.alpha_composite(side)
        stage = self.field_rects()[0]
        if self.dp:
            monitor = self.draws('qpro_bg')
            if len(monitor) != 1:
                raise ValueError('DP requires its original lower-monitor hook')
            split = round(monitor[0]['bounds']['top'])
            rectangles = [largest_clear_rectangle(image, 0, 300, 0, split),
                          largest_clear_rectangle(image, 0, 300, split)]
            if not graph_visible:
                rectangles.extend([largest_clear_rectangle(image, 980, 1280, 0, split),
                                   largest_clear_rectangle(image, 980, 1280, split)])
            original_openings = copy.deepcopy(rectangles)
            # The public BMS layout reserves its bottom 8% for the HUD even
            # outside the lanes. Stay below that contract's 662.4px boundary.
            for bounds in rectangles:
                bounds['bottom'] = min(bounds['bottom'], 662)
            self.evidence.append({'dpOriginalMonitorOpenings': original_openings,
                                  'publicHudBottomLimit': 662})
        else:
            rectangles = ([largest_clear_rectangle(image, 0, int(stage['left']))] if self.p2
                          else [largest_clear_rectangle(image, int(stage['right']), 1280)])
            for bounds in rectangles:
                bounds['bottom'] = min(bounds['bottom'], 640)
        self.evidence.append({'bga': rectangles, 'centreTranslation': self.dx,
                              'scope': ('Original21 DP Graph-OFF uses all four independently measured clear openings; fit and the public lower-HUD clip remain OMS adaptations.' if not graph_visible else
                                        'Original21 DP Graph-ON uses the two left clear openings; the separate Graph-OFF BGA contribution uses all four. Fit and the public lower-HUD clip remain OMS adaptations.' if self.dp and self.style in SOURCE21_STYLES else
                                        'Original DP left upper/lower openings, clipped at the public HUD boundary; mirroring the BGA into the Qpro opening and retaining both when Graph is hidden are OMS adaptations.' if self.dp else
                                        'Verified fully transparent opening after composing both original movies; OMS fit and lower-HUD clipping are author adaptations.')})
        return rectangles

    def score_panel(self):
        if self.style not in (*SOURCE21_STYLES, *SOURCE22_AVAILABLE_STYLES) or self.dp or self.wide:
            return
        # These named, one-frame source panels are removed from recorded art
        # together with game-controlled placeholders. The original caller keeps
        # only the participating side; restore that side before its live digits.
        hook = 'sco_waku_' + self.player
        draws = self.draws(hook, self.centre)
        if len(draws) != 1:
            raise ValueError(f'{self.key}/{self.layout} requires one original {hook}')
        draw = draws[0]
        asset, bounds = draw['asset'], draw['bounds']
        width, height = asset['width'], asset['height']
        vertices = [{'x': x, 'y': y, 'z': 0} for x, y in
                    ((0, 0), (width, 0), (0, height), (width, height))]
        projected = [{**v, 'x': v['x'] + bounds['left'], 'y': v['y'] + bounds['top']} for v in vertices]
        if (asset['package'] != self.key or draw['texture'] != hook or draw['game_bindings'] != [hook]
                or draw['source_vertices'] != vertices or draw['projected_vertices'] != projected
                or bounds['right'] - bounds['left'] != width or bounds['bottom'] - bounds['top'] != height
                or draw['mask_present'] or draw['blend'] != 0
                or draw['mult_color'] != dict.fromkeys(('r', 'g', 'b', 'a'), 1)
                or draw['add_color'] != dict.fromkeys(('r', 'g', 'b', 'a'), 0)
                or draw['draw_params'] != [{'flags': 3, 'region': hook, 'vertexes': [0, 1, 2, 2, 1, 3], 'blend': None}]):
            raise ValueError(f'{self.key}/{self.layout} score panel is not the qualified original rectangle')
        resource, _ = self.asset(self.key, hook)
        self.info.append(node(self.prefix + '.score-panel', 'sprite', rect(bounds, dx=self.dx), resource))
        self.evidence.append({'scorePanel': {'sourceHook': hook, 'sourcePackage': self.key,
            'sourceBounds': bounds, 'centreTranslation': self.dx,
            'scope': 'Own original single-frame panel and source rectangle; only the participating SP side is restored below its live digits. Wide/DP use their separate original cabinet art.'}})

    def bpm_range_panel(self):
        if self.style not in (*SOURCE21_STYLES, *SOURCE22_AVAILABLE_STYLES):
            return
        # Both qualified21/22 callers hide this layer and extrema at min == max.
        # This author already displays the actual extrema continuously; restore
        # their own labels with that host behavior, below the live readouts.
        draws = self.draws('bpm_waku', self.centre)
        if len(draws) != 1:
            raise ValueError(f'{self.key}/{self.layout} requires one original bpm_waku')
        draw = draws[0]
        asset, bounds = draw['asset'], draw['bounds']
        source21 = self.style in SOURCE21_STYLES
        width, height = (266, 54) if source21 else (280, 64)
        vertices = [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (width, 0), (0, height), (width, height))]
        projected = [{**v, 'x': v['x'] + bounds['left'], 'y': v['y'] + bounds['top']} for v in vertices]
        if (asset['package'] != self.key or asset['width'] != width or asset['height'] != height
                or draw['texture'] != 'pm_bpm2' or draw['game_bindings'] != ['bpm_waku']
                or draw['source_vertices'] != vertices or draw['projected_vertices'] != projected
                or bounds['right'] - bounds['left'] != width or bounds['bottom'] - bounds['top'] != height
                or draw['mask_present'] or draw['blend'] != 0
                or draw['mult_color'] != dict.fromkeys(('r', 'g', 'b', 'a'), 1)
                or draw['add_color'] != dict.fromkeys(('r', 'g', 'b', 'a'), 0)
                or draw['draw_params'] != [{'flags': 3, 'region': 'pm_bpm2', 'vertexes': [0, 1, 2, 2, 1, 3], 'blend': None}]):
            raise ValueError(f'{self.key}/{self.layout} BPM range panel is not the qualified original rectangle')
        resource, _ = self.asset(self.key, 'pm_bpm2')
        self.info.append(node(self.prefix + '.bpm-range-panel', 'sprite', rect(bounds, dx=self.dx), resource))
        scope = 'Own full-size MIN/BPM/MAX art at the final source rectangle, below the existing actual extrema. The host continuously displays both extrema and their labels; native22 hides these when min equals max. Original DP ancestor entry motion is not restored by this direct HUD layer.'
        if source21:
            scope = scope.replace('native22', 'native21')
        self.evidence.append({'bpmRangePanel': {'sourceHook': 'bpm_waku', 'sourcePackage': self.key,
            'sourceTexture': 'pm_bpm2', 'sourceBounds': bounds, 'centreTranslation': self.dx,
            'scope': scope}})

    def numbers(self):
        self.wide_score_labels()
        self.score_panel()
        self.bpm_range_panel()
        source21 = self.style in SOURCE21_STYLES
        hud_package = 's21/gameparts' if source21 else self.key
        digits, h = self.font('numbers', hud_package, {str(i):f'playm_sco{i:02}' for i in range(10)}, 27 if source21 else 26)
        bpm, bh = self.font('bpm', hud_package, {str(i):f'playm_bpm{i:02}' for i in range(10)}, 27 if source21 else 26)
        small, sh = self.font('small-bpm', layout_family(self.style) + '/gameparts', {str(i):f'playm_bpm_m{i:02}' for i in range(10)}, 18 if source21 else 17)
        score_width, combo_width, small_width = (163, 109, 55) if source21 else (160, 112, 53)
        for name, source, hook, font, height, width in (
                ('score','score.value',f'score_{self.player}_0',digits,h,score_width),
                ('combo','combo.maximum',f'maxcombo_{self.player}_0',digits,h,combo_width),
                ('bpm','timing.bpm','bpm_0',bpm,bh,82),
                ('min-bpm','timing.bpm-min','bpm_min_0',small,sh,small_width),
                ('max-bpm','timing.bpm-max','bpm_max_0',small,sh,small_width)):
            draws = self.draws(hook)
            if not draws:
                raise ValueError(f'{self.key}/{self.layout} is missing real {hook}')
            d = draws[-1]
            bounds = {**d['bounds'], 'right': d['bounds']['left'] + width}
            movie = next(m for m in (self.side,self.centre) if d in self.map['layouts'][m]['draws'])
            readout = self.number(name, source, bounds, font, height, dx=self.dx if movie==self.centre else 0)
            if name == 'score':
                # OMS can reach seven digits. Keep the native six-digit field
                # inside its source bezel and shrink only that additional digit.
                self.steps(readout['id'], 'font-size', source,
                           [(0, height * LOGICAL), (1_000_000, height * LOGICAL * score_width / (190 if source21 else 186))])
            self.info.append(readout)
            self.evidence.append({'readout':name,'sourceHook':hook,'sourceBounds':d['bounds'],'bounds':bounds,
                                  'advance':self.fonts[font]['glyphs'][0]['advance'],'scope':'Original glyph and hook; multi-digit width is an OMS adaptation.'})
        hsdraws = [d for i in range(4) for d in self.draws(f'hs_{self.player}_{i}',self.side)]
        bounds = union(hsdraws)
        anchors = sorted(d['bounds']['left'] for d in hsdraws)
        advance = anchors[1]-anchors[0]
        hs, hh = self.font('hispeed-'+self.layout,hud_package,{str(i):f'playm_hs_{i:02}' for i in range(10)},advance)
        self.fonts[hs]['glyphs'].append({'character':'.','resource':self.empty,'advance':anchors[2]-anchors[1]-advance})
        hi_speed = self.number('hispeed','scroll.hi-speed',bounds,hs,hh,fmt='fixed-2')
        self.bind(hi_speed['id'], 'visible', 'scroll.hi-speed-available')
        self.info.append(hi_speed)
        for i,d in enumerate(self.map['layouts'][self.side]['draws']):
            if d['texture'] in ('playm_hs_dot','playm_red_line'):
                resource,_=self.asset(d['asset']['package'],d['texture'])
                self.info.append(node(self.prefix+f'.fixed.n{i}','sprite',rect(d['bounds']),resource))
        return digits,h

    def wide_score_labels(self):
        if self.style not in SOURCE21_STYLES or not self.wide:
            return
        # Original21 wide movies carry separate named labels; filtering all
        # named placeholders removes these along with the live digit anchors.
        for name, texture, width in (('score', 'playm_score', 52), ('maxcombo', 'playm_maxcombo', 90)):
            hook = name + '_' + self.player
            draws = self.draws(hook, self.centre)
            if len(draws) != 1:
                raise ValueError(f'{self.key}/{self.layout} requires one original wide {hook}')
            draw = draws[0]
            bounds, asset = draw['bounds'], draw['asset']
            vertices = [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (width, 0), (0, 15), (width, 15))]
            projected = [{**v, 'x': v['x'] + bounds['left'], 'y': v['y'] + bounds['top']} for v in vertices]
            if (asset['package'] != self.key or (asset['width'], asset['height']) != (width, 15)
                    or draw['texture'] != texture or draw['game_bindings'] != [hook]
                    or draw['source_vertices'] != vertices or draw['projected_vertices'] != projected
                    or bounds['right'] - bounds['left'] != width or bounds['bottom'] - bounds['top'] != 15
                    or draw['mask_present'] or draw['blend'] != 0
                    or draw['mult_color'] != dict.fromkeys(('r', 'g', 'b', 'a'), 1)
                    or draw['add_color'] != dict.fromkeys(('r', 'g', 'b', 'a'), 0)
                    or draw['draw_params'] != [{'flags': 3, 'region': texture, 'vertexes': [0, 1, 2, 2, 1, 3], 'blend': None}]):
                raise ValueError(f'{self.key}/{self.layout} wide score label is not the qualified original rectangle')
            resource, _ = self.asset(self.key, texture)
            self.info.append(node(self.prefix + '.label.' + name, 'sprite', rect(bounds), resource))
            self.evidence.append({'wideScoreLabel': {'sourceHook': hook, 'sourcePackage': self.key,
                'sourceBounds': bounds, 'scope': 'Own original wide-SP named label, restored below live digits without moving or replacing its artwork.'}})

    def header(self):
        difficulty = self.draws('diff_'+self.player,self.side)
        level = self.draws('lvlnum_'+self.player,self.side)
        if not difficulty or not level:
            self.evidence.append({'header':'No original difficulty/level hooks in this source; omitted rather than transplanting another frame.'})
            return
        for field,draws,mapping in (
            ('difficulty',difficulty,{'BEGINNER':'playm_beginner','NORMAL':'playm_normal','HYPER':'playm_hyper',
                                     'ANOTHER':'playm_another','LEGGENDARIA':'playm_leggendaria'}),
            ('level',level,{str(i):f'lvlnum_{i:02}' for i in range(1,13)})):
            draw=draws[-1];bounds=draw['bounds'];values={}
            hud_package = 's21/gameparts' if self.style in SOURCE21_STYLES else self.key
            for key,name in mapping.items():
                if self.style in SOURCE21_STYLES and self.dp and field == 'difficulty':
                    name += '_dp'
                if (hud_package,name) in self.owner.index:
                    values[key]=self.asset(hud_package,name)[0]
            identifier=self.prefix+'.header.'+field
            image=node(identifier,'sprite',rect(bounds),self.empty)
            self.variant(identifier,'resource','song.'+field,self.empty,values)
            fallback=node(identifier+'.text','text',{**rect(bounds),'text':'','font-size':12*LOGICAL,
                          'colour':'#ffffffff','alignment':'centre','text-overflow':'ellipsis'})
            self.bind(fallback['id'],'text','song.'+field)
            self.variant(fallback['id'],'visible','song.'+field,True,{key:False for key in values})
            self.info.extend((image,fallback))
        for i,d in enumerate(self.draws('lv_'+self.player,self.side)):
            resource,_=self.asset(d['asset']['package'],d['texture'])
            label = node(self.prefix+f'.header.lv.n{i}','sprite',rect(d['bounds']),resource)
            self.variant(label['id'], 'visible', 'song.level', True, {'': False})
            self.info.append(label)

    def gauge(self):
        d=self.draws('gg_bar_'+self.player,self.side)[-1]
        resource,_=self.asset(d['asset']['package'],d['texture'])
        mode_names = [('hard','playm_gauge_hard'),('ex-hard','playm_gauge_exhard')]
        if self.style not in (*SOURCE21_STYLES, *SOURCE22_STYLES):
            mode_names.append(('assist-easy','playm_gauge_aeasy_'+self.player))
        modes={key:self.asset(layout_family(self.style) + '/gameparts',name)[0] for key,name in mode_names}
        bounds=d['bounds'];prefix=self.prefix+'.gauge'
        fill=node(prefix+'.fill','sprite',{},resource)
        reveal=node(prefix+'.reveal','clip',{'clip-mode':'bounds','reveal-x':0},children=[fill])
        if self.p2:
            reveal['properties'].update({'x':1,'y':1,'rotation':180})
            fill['properties'].update({'x':1,'y':1,'rotation':180})
        self.bind(reveal['id'],'reveal-x','gauge.value')
        self.variant(fill['id'],'resource','gauge.type',resource,modes)
        dark=node(prefix+'.dark','sprite',{'opacity':.2},resource)
        self.variant(dark['id'],'resource','gauge.type',resource,modes)
        solid=self.resource({'id':'std.author-solid','type':'texture','path':self.owner.solid_path})
        threshold=node(prefix+'.threshold','sprite',{'x':0,'y':-.10,'width':2/300,'height':1.2,
                       'colour':'#999999ff'},solid)
        self.bind(threshold['id'],'x','gauge.clear-threshold')
        cleared=copy.deepcopy(threshold)
        cleared['id']=prefix+'.cleared'
        cleared['properties']['colour']='#ffcc33ff'
        self.bind(cleared['id'],'x','gauge.clear-threshold')
        self.variant(cleared['id'],'visible','gauge.clear-state',False,{'clear':True})
        threshold_direction=node(prefix+'.threshold-direction',
            properties={'x':1,'y':1,'rotation':180} if self.p2 else {},children=[threshold,cleared])
        self.variant(threshold_direction['id'],'visible','gauge.type',False,
                     {'normal':True,'easy':True,'assist-easy':True})
        shown = node(prefix+'.shown', children=[reveal,threshold_direction])
        self.variant(shown['id'], 'visible', 'option.gauge-display', False, {'a':True})
        self.info.append(node(prefix+'.body',properties=rect(bounds),children=[dark,shown]))
        anchors=[self.draws(f'percent_{self.player}_{i}',self.side)[-1] for i in range(3)]
        advance=anchors[0]['bounds']['left']-anchors[1]['bounds']['left']
        hud_package = 's21/gameparts' if self.style in SOURCE21_STYLES else self.key
        font,height=self.font('gauge',hud_package,{str(i):f'playm_gg_{i:02}' for i in range(10)},advance,
                              uniform_height=self.style not in SOURCE22_STYLES)
        self.fonts[font]['glyphs'].append({'character':'%','resource':self.empty,'advance':1})
        percent={**union(anchors[:2]),'right':anchors[0]['bounds']['left']+advance+1}
        readout=self.number('gauge.percent','gauge.value',percent,font,height,fmt='percent')
        self.steps(readout['id'],'opacity','gauge.value',[(0,1),(.995,0)])
        hundred=[]
        for i,anchor in enumerate(anchors):
            resource,_=self.asset(hud_package,f'playm_gg_{1 if i==2 else 0:02}')
            hundred.append(node(prefix+f'.hundred.n{i}','sprite',rect(anchor['bounds']),resource))
        group=node(prefix+'.hundred',children=hundred)
        self.steps(group['id'],'opacity','gauge.value',[(0,0),(.995,1)])
        percentages = node(prefix+'.percentages', children=[readout,group])
        self.variant(percentages['id'], 'visible', 'option.gauge-display', True, {'c':False})
        self.info.append(percentages)
        for index, hyphen in enumerate(self.draws('percent_'+self.player+'_hypen', self.side)):
            hyphen_resource, _ = self.asset(hyphen['asset']['package'], hyphen['texture'])
            dash = node(prefix+f'.hyphen.n{index}', 'sprite', rect(hyphen['bounds']), hyphen_resource)
            self.variant(dash['id'], 'visible', 'option.gauge-display', False, {'c':True})
            self.info.append(dash)
        gauge_evidence = {'gaugePercent':'Original ones/tens/hundreds hooks; native compressed hundred position retained.',
                          'advance':advance,'roundedHundredThreshold':.995,
                          'visibility':'26 original A=bar+percent, B=dark bar+percent, C=dark bar+source hyphen if present;25 has no hyphen hook.',
                          'darkUnderlayOpacity':.2}
        if self.style in SOURCE22_STYLES:
            gauge_evidence.update(
                gaugePercent='Own22 ones/tens/hundreds artwork and original source-hook positions.',
                visibility='Existing OMS A/B/C display policy, dark underlay and rounded100 threshold are author adaptations on22 art; this does not assert the22 executable gauge-option table.',
                assistEasyArtwork='The22 source has no Assist Easy gauge sprite. The OMS mode retains the original normal gauge artwork with its actual host value/60% threshold; no later-source artwork is substituted.',
                glyphHeightPolicy='Original glyph PNG sizes are retained, top-aligned in a line whose height is the largest source glyph. This represents Lincle digit2 at its original28px inside the30px line without rescaling its pixels; original movie-cell parity is not claimed.',
                sourceFamily='s22')
        if self.style in SOURCE21_STYLES:
            gauge_evidence.update(
                gaugePercent='Own21 gameparts ones/tens/hundreds artwork and original source-hook positions.',
                visibility='Existing OMS A/B/C display policy, dark underlay and rounded100 threshold are author adaptations on21 art; this does not assert the21 executable gauge-option table.',
                assistEasyArtwork='The21 source has no Assist Easy gauge sprite. The OMS mode retains its own original normal gauge artwork with its actual host value/60% threshold; no later-source artwork is substituted.',
                sourceFamily='s21')
        self.evidence.append(gauge_evidence)
        hazard=node(prefix+'.hazard','text',{**rect(box(bounds['left'],bounds['bottom']+1,150,12)),
                    'text':'HAZARD / SURVIVAL','font-size':10*LOGICAL,'colour':'#ffffffff'})
        self.variant(hazard['id'],'visible','gauge.type',False,{'hazard':True})
        self.info.append(hazard)

    def progress(self):
        for player in (('1p','2p') if self.dp else (self.player,)):
            draws=self.draws('music_point_'+player,self.side)
            if not draws and self.style == 'source22-gmframe9' and not self.dp:
                self.evidence.append({'musicProgressUnavailable':
                    'The retained22 Tricoro SP source has no music_point placement in either side or any centre movie. No cursor or travel geometry is borrowed; its DP source retains both original hooks.'})
                continue
            d=draws[-1]
            resource,_=self.asset(d['asset']['package'],d['texture'])
            prefix=self.prefix+'.progress.p'+player[0]
            bounds=d['bounds']
            height=bounds['bottom']-bounds['top']
            travel=474-height-bounds['top']
            cursor=node(prefix+'.cursor','sprite',{'x':0,'y':0,'width':1,'height':height/travel},resource)
            cursor['blend']='additive'
            track=node(prefix+'.travel',properties=rect(box(bounds['left'],bounds['top'],
                       bounds['right']-bounds['left'],travel)),children=[cursor])
            self.bind(cursor['id'],'y','timing.progress')
            self.info.append(track)
        if self.dp or draws:
            self.evidence.append({'musicProgress':'Original cursor and initial anchor; endpoint aligned to source lower E area is an author adaptation, not the original progress formula.'})

    def graph(self):
        from standard_graph import build_graph
        exported = build_graph(self.owner.library, self.layout, self.owner.feedback_font,
                               common_family=self.common_family)
        for file in exported['files']:
            self.owner.copy(file['source'], file['path'], file['sha256'])
        for declaration in exported['resources']:
            self.resource(declaration)
        for declaration in exported['bitmapFonts']:
            if declaration['id'] in self.fonts:
                raise ValueError('Graph font ID collision')
            self.fonts[declaration['id']] = declaration
        for field in ('templates', 'instances', 'bindings', 'variants', 'tracks', 'numericSources'):
            self.scene.setdefault(field, []).extend(exported.get(field, []))
        self.evidence.append({'graph': exported['receipt']})

    def feedback(self):
        from customization_modules import material_contexts
        from standard_feedback import build_feedback
        exported = build_feedback(self.owner.library, self.research / 'skin-reference-map.json',
            style=self.style, layout=self.layout, centre_movie=self.centre, centre_dx=self.dx,
            contexts=[c for c in material_contexts() if c['layout'] == self.layout],
            font_path=self.owner.source_font, output=self.owner.output,
            judgement_position_option='judgement-position', prepared_font=self.owner.feedback_font)
        for file in exported['files']:
            self.owner.copy(file['source'], file['path'], file['sha256'])
        for declaration in exported['resources']:
            self.resource(declaration)
        for declaration in exported['bitmapFonts']:
            self.fonts[declaration['id']] = declaration
        for field in ('templates', 'instances', 'bindings', 'variants', 'tracks', 'numericSources'):
            self.scene.setdefault(field, []).extend(exported.get(field, []))
        for target in exported['targets']:
            key = target['scope'], target['target'], target['slot']
            if key in self.material_targets:
                raise ValueError('Competing feedback material ownership')
            self.material_targets[key] = target['material']
        for option in exported['options']:
            old = self.owner.additional_options.setdefault(option['id'], option)
            if old != option:
                raise ValueError('Frame feedback option contracts differ')
        self.evidence.append({'feedback': exported['receipt']})
        self.failure_feedback()

    def failure_feedback(self):
        source = self.owner.failure_families[self.common_family]
        clip = source['clip']
        for resource in clip['resources']:
            self.resource(resource)
        for animation in clip['animations']:
            self.animations[animation['id']] = animation
        # A direct explicit Global child retains SafeBounds, while a material-selected
        # template would project every child into hud.text's native footer surface.
        # hud.text shares HudForeground with judgements; decoration's Overlay layer
        # cannot cover them, regardless of its local z. Keep the failure independent
        # of both ruleset JUDGE templates and their preference/preparation gates.
        self.scene['root']['children'].append(node(self.prefix + '.failure',
            properties={'z': -100}, slot='hud.text', children=[copy.deepcopy(clip['node'])]))
        self.scene['failureOutro'] = 3000
        self.evidence.append({'failure': {**source['receipt'],
            'mount': 'One direct Global hud.text foreground owner with an explicit slotless Global child using frozen SafeBounds; independent of JUDGE preferences and preparation. Original gameplay.fail clip and 3000 ms outro are unchanged.'}})

    def gauge_feedback(self):
        source = self.owner.gauge_feedback[f'f{frame_token(self.style)}'][self.layout]
        for resource in source['resources']:
            self.resource(resource)
        for animation in source['animations']:
            self.animations[animation['id']] = animation
        once, loop, initial = [copy.deepcopy(source[key]) for key in ('oneShot', 'loop', 'initial')]
        uncleared = node(self.prefix + '.gauge-feedback.uncleared', children=[once])
        self.variant(uncleared['id'], 'visible', 'gauge.clear-state', False, {'uncleared': True})
        after_increase = node(self.prefix + '.gauge-feedback.increased', children=[loop])
        self.bind(after_increase['id'], 'visible', 'gauge.increase-available')
        before_increase = node(self.prefix + '.gauge-feedback.initial', children=[initial])
        self.variant(before_increase['id'], 'visible', 'gauge.increase-available', True, {'true': False})
        cleared = node(self.prefix + '.gauge-feedback.clear', children=[before_increase, after_increase])
        self.variant(cleared['id'], 'visible', 'gauge.clear-state', False, {'clear': True})
        self.info.extend((uncleared, cleared))
        self.evidence.append({'gaugeFeedback': {'source': source['source'],
            'scope': 'Exact per-cabinet original canvas; independent of Gauge A/B/C and centre Graph displacement.',
            'lifecycle': 'Real OMS health increases restart the one-shot below clear and the loop at/above clear. Initially clear with no observed increase uses gameplay.attach; it does not fabricate an increase event.',
            'adaptation': 'OMS actual health increases replace the original displayed two-percent quantisation; actual OMS gauge clear-state supplies the threshold.'}})

    def controls(self):
        from customization_modules import material_contexts
        from standard_controls import build_controls
        exported=build_controls(self.owner.library,self.research/'skin-reference-map.json',
            style=self.style,layout=self.layout,
            contexts=[c for c in material_contexts() if c['layout']==self.layout])
        for file in exported['files']:
            self.owner.copy(file['source'],file['path'],file['sha256'])
        for declaration in exported['resources']:
            self.resource(declaration)
        for declaration in exported['animations']:
            if declaration['id'] in self.animations:
                raise ValueError('Control animation ID collision')
            self.animations[declaration['id']]=declaration
        for field in ('templates','instances','bindings','variants','tracks'):
            self.scene[field].extend(exported[field])
        self.evidence.append({'controls':exported['receipt']})
        return {target['target']:target['material'] for target in exported['targets']}

    def material_ini(self, background, information, controls):
        from customization_modules import material_contexts
        profiles=self.profiles()
        for context in material_contexts():
            if context['layout']!=self.layout:
                continue
            if context['group']==0:
                self.ini += [COMMON,'Target: Global '+context['suffix'],
                    f'stage.background: resource Provide "{background}"',
                    self.feedback_material('Global', context['suffix'], 'decoration', f'Provide "{information}"'),
                    f'hud.text: resource Provide "{self.owner.empty_path[:-4]}"']
                if context['ruleset']=='bms':
                    self.ini.append('bga.frame: resource Suppress')
            self.ini += [COMMON,'Target: Stage '+context['stage'],
                f'stage.background: resource Provide "{self.owner.empty_path[:-4]}"',
                'decoration: resource Suppress',
                'hud.gauge: resource Suppress','hud.combo: resource Suppress','hud.text: resource Suppress']
            for slot in ('stage.foreground','playfield.backdrop','playfield.baseplate','playfield.judgement-line'):
                self.ini.append(self.feedback_material('Stage', context['stage'], slot,
                    f'Provide "{self.owner.empty_path[:-4]}"'))
            for lane in context['lanes']:
                self.ini += [COMMON,'Target: Lane '+lane['target']]
                self.ini.append(f'playfield.key: resource Provide "{controls[lane["target"]]}"')
                for slot in ('playfield.lane-divider','playfield.hit-target'):
                    self.ini.append(f'{slot}: resource Provide "{self.owner.empty_path[:-4]}"')
                self.ini.append('effect.key-flash: resource Suppress')
        return profiles

    def feedback_material(self, scope, target, slot, fallback):
        resource = self.material_targets.get((scope, target, slot))
        return f'{slot}: resource Provide "{resource}"' if resource else f'{slot}: resource {fallback}'

    def build(self):
        background=self.background()
        self.numbers();self.header();self.gauge();self.progress();self.graph()
        self.gauge_feedback()
        self.feedback()
        self.reading()
        information=self.template('information',node(self.prefix+'.information',children=self.info),'decoration')
        controls=self.controls()
        profiles=self.material_ini(background,information,controls)
        apertures=self.apertures()
        bga_context = {'style':self.style,'layout':self.layout,'wide':self.wide,'apertures':apertures}
        if self.style in SOURCE21_STYLES and self.dp:
            bga_context['dpGraphOffApertures'] = self.apertures(graph_visible=False)
        self.owner.bga_contexts.append(bga_context)
        for mode in (14,) if self.dp else (5,7):
            fields={'PlayfieldWidth':672/1280 if self.dp else 292/1280,'PlayfieldHeight':484/720,
                'PlayfieldSideInset':48/1280,'LaneSidePadding':2/292,'NormalLaneWidth':1,
                'BlackLaneWidth':26/34,'ScratchLaneWidth':60/34,'NormalLaneSpacing':2/34,
                'ScratchLaneSpacing':2/34,'LongNoteBodyWidth':1,'KeyAreaHeight':80/720,
                'GaugeHeight':74/720,'BgaInformationHeight':0,'BarLineHeight':1,'HitTargetLineHeight':5*LOGICAL}
            if self.dp:fields['DualStageGap']=88/672
            self.ini += ['[Bms]',f'Keymode: {mode}K',*[f'{k}: {v:.12g}' for k,v in fields.items()],'']
        relative=f'standard/frames/f{frame_token(self.style)}/{self.layout}'+('-wide' if self.wide else '')
        manifest={'contract':MANIFEST,'scene':'gameplay-skin.scene.json','sceneContract':SCENE,'eventContract':EVENT,
                  'resources':list(self.resources.values()),'bitmapFonts':list(self.fonts.values()),
                  'animations':list(self.animations.values()),'layouts':profiles}
        write(self.owner.output/(relative+'/manifest.json'),manifest)
        write(self.owner.output/(relative+'/scene.json'),self.scene)
        (self.owner.output/(relative+'/materials.ini')).write_text('\n'.join(self.ini)+'\n',encoding='utf-8')
        self.owner.receipts.append({'frame':frame_id(self.style),'layout':self.layout,'wide':self.wide,
                                   'centreMovie':self.centre,'research':str(self.research),
                                   'stage':self.map['stage'],'sourceLibraryOnly':True,'adaptations':self.evidence})
        return {field:relative+'/'+name for field,name in (('manifest','manifest.json'),('scene','scene.json'),('ini','materials.ini'))}

    def reading(self):
        from standard_reading import add_reading, add_arrival
        from standard_cover_readout import add_cover_readouts
        add_reading(self, self.owner.reading_families[layout_family(self.style)], self.owner.feedback_font, self.owner.source_font)
        add_cover_readouts(self, self.owner.feedback_font, source_family=layout_family(self.style))
        if layout_family(self.style) == '0':
            add_arrival(self)
        else:
            self.evidence.append({'readingSourceCapabilities': {
                'dedicatedNormalSpeedPreviewArt': False, 'firstRowCueArt': False,
                'nativePreStartLanePreview': 'Existing OMS fallback; not a source-game capability claim.',
                'reason': ('No corresponding artwork or movies in the qualified21 gameparts; no26 artwork substituted.' if self.style in SOURCE21_STYLES else 'No corresponding artwork or movies in the qualified22 gameparts; no26 artwork substituted.' if self.style in SOURCE22_STYLES else 'No corresponding artwork or movies in the readable 24 HDD graphic archives; no 26 artwork substituted.')}})


def build(args):
    owner=Package(args.library,args.output,args.image_tools)
    from standard_feedback import prepare_feedback_font
    from standard_reading import OPTIONS as READING_OPTIONS
    from standard_presentation_options import BRIGHTNESS_OPTION
    owner.presentation_assets = args.presentation_assets.resolve(strict=True)
    owner.source_font = args.source_font.resolve(strict=True)
    owner.feedback_font = prepare_feedback_font(owner.source_font, owner.output)
    reading = read(args.reading_assets / 'reading.json')
    if (reading.get('contract') != 'oms-standard-reading-assets.v1'
            or reading.get('librarySHA256') != sha(owner.library / 'library.json')):
        raise ValueError('READY export must belong to this exact Standard source library')
    owner.reading_families = reading.get('families', {'0': reading['clips']})
    for row in reading['files']:
        owner.copy(contained(args.reading_assets.resolve(), row['path']), row['path'], row['sha256'])
    gauge = read(args.gauge_assets / 'gauge-feedback.json')
    failure = read(args.failure_assets / 'failure.json')
    notifications = read(args.graph_notifications / 'graph-notifications.json')
    for catalog, contract, directory in ((gauge, 'oms-standard-gauge-feedback.v1', args.gauge_assets),
                                        (failure, 'oms-standard-failure.v2', args.failure_assets),
                                        (notifications, 'oms-standard-graph-notifications.v1', args.graph_notifications)):
        if catalog.get('contract') != contract or catalog.get('librarySHA256') != sha(owner.library / 'library.json'):
            raise ValueError('Stage feedback must belong to this exact Standard source library')
        selected_files = catalog['files']
        if catalog is gauge:
            owner.gauge_feedback, selected_files = select_gauge_assets(gauge, owner.source_data)
        for row in selected_files:
            owner.copy(contained(directory.resolve(), row['path']), row['path'], row['sha256'])
    if set(failure['families']) != set(source_families(owner.source_data)):
        raise ValueError('Failure export must retain every qualified source family')
    owner.failure_families = failure['families']
    import brotli
    for layouts in owner.gauge_feedback.values():
        for clip in layouts.values():
            animation = copy.deepcopy(clip['animations'][1])
            program = json.loads(brotli.decompress((owner.output / animation['path']).read_bytes()))
            if program['trigger'] != 'gauge.increase' or not program['loop']:
                raise ValueError('Initial clear-loop must derive from the exact increase loop')
            program['trigger'] = 'gameplay.attach'
            animation['id'] += '.initial'
            animation['path'] = str(Path(animation['path']).with_name('initial.json.br')).replace('\\', '/')
            path = owner.output / animation['path']
            path.write_bytes(brotli.compress(json.dumps(program, separators=(',', ':'), allow_nan=False).encode(), quality=5))
            owner.files[animation['path']] = sha(path)
            clip['animations'].append(animation)
            clip['initial'] = copy.deepcopy(clip['loop'])
            clip['initial']['id'] += '.initial'
            clip['initial']['animation'] = animation['id']
    choices=[]
    owner.bga_contexts=[]
    for style in owner.frame_styles:
        parts={layout:Frame(owner,args.research/package_key(style),style,layout).build() for layout in ('p1','p2','dp')}
        wide={layout:Frame(owner,args.research/package_key(style),style,layout,wide=True).build() for layout in ('p1','p2')}
        choice={'value':frame_id(style),'label':frame_label(style),**parts['dp'],
                'variants':[{'when':{'layout':layout,'graph-visible':shown},**(parts if shown else wide)[layout]}
                            for layout in ('p1','p2') for shown in (True,False)]}
        reference=read(args.research/package_key(style)/'skin-reference-map.json')
        preview=owner.Image.new('RGBA',CANVAS,(5,7,12,255))
        for movie,dx in (('center_frame_sp',centre_translation(style, 'p1')),('1p_frame',0)):
            art=owner.Image.open(args.research/package_key(style)/reference['layouts'][movie]['static_frame']).convert('RGBA')
            preview.alpha_composite(art,(dx,0))
        preview.thumbnail((512,288),owner.Image.Resampling.LANCZOS)
        relative=f'standard/previews/frame{frame_token(style)}.png'
        target=owner.output/relative
        target.parent.mkdir(parents=True,exist_ok=True)
        preview.save(target,optimize=True)
        if target.stat().st_size>256*1024:
            raise ValueError('Frame preview exceeds the public preview budget')
        choice['preview']=relative
        choices.append(choice)
    components=read(args.components/'modules.json')
    if components['source']['artIdentity']!=art_identity(owner.source_data) or components['source']['librarySHA256']!=sha(owner.library/'library.json'):
        raise ValueError('Component exports must belong to this exact Standard source library')
    files={row['path']:(args.components.resolve(),row) for row in components['files']}
    for override in args.component_overrides:
        replacement=read(override/'modules.json')
        if replacement['source']!=components['source']:
            raise ValueError('A component revision cannot change its source identity')
        positions={module['id']:i for i,module in enumerate(components['modules'])}
        for module in replacement['modules']:
            if module['id'] in positions:
                components['modules'][positions[module['id']]]=module
            else:
                components['modules'].append(module)
        option_positions = {option['id']:i for i,option in enumerate(components['options'])}
        for option in replacement['options']:
            if option['id'] in option_positions:
                components['options'][option_positions[option['id']]] = option
            else:
                components['options'].append(option)
        for row in replacement['files']:
            files[row['path']]=(override.resolve(),row)
    for root,row in files.values():
        owner.copy(contained(root,row['path']),row['path'],row['sha256'])
    from standard_graph_targets import export_graph_target_module
    graph_targets = export_graph_target_module(owner.library, owner.output, owner.feedback_font,
                                               notifications=notifications['families'])
    from standard_bga import export_bga_module
    bga = export_bga_module(owner.library, owner.output, owner.bga_contexts, source_geometry=args.bga_geometry)
    for contribution in (graph_targets, bga):
        for row in contribution['files']:
            owner.copy(row['source'], row['path'], row['sha256'])
    root={'contract':MANIFEST,'scene':'gameplay-skin.scene.json','sceneContract':SCENE,'eventContract':EVENT,
          'resources':[],'contentCatalog':'gameplay/content.json','options':[
              {'id':'layout','type':'choice','label':'游玩区域样式','default':'p1','effect':'presentation',
               'keymodes':['5k','6k','7k','8k'],'choices':[{'value':'p1','label':'1P（左侧）'},{'value':'p2','label':'2P（右侧）'}]},
              {'id':'frame-opening','type':'toggle','label':'框体入场与持续动画','default':True},
              {'id':'graph-visible','type':'toggle','label':'Graph Area 显示统计','default':True},
              {'id':'gauge-display','type':'choice','label':'Gauge 显示','default':'a',
               'choices':[{'value':'a','label':'Type A（血槽与百分比）'},
                          {'value':'b','label':'Type B（百分比）'},
                          {'value':'c','label':'Type C（隐藏数值）'}]},
              BRIGHTNESS_OPTION, *READING_OPTIONS, *graph_targets['options'], *owner.additional_options.values(), *components['options']]}
    if len({option['id'] for option in root['options']}) != len(root['options']):
        raise ValueError('The final independent package has duplicate option ownership')
    write(owner.output/'gameplay-skin.json',root)
    write(owner.output/'gameplay-skin.scene.json',document('std.root'))
    write(owner.output/'gameplay/content.json',{'contract':'oms-gameplay-skin-content.v1','modules':[
        {'id':'frame','label':'Game Frame','default':'iidx26','choices':choices}, graph_targets['module'], bga['module'], *components['modules']]})
    (owner.output/'skin.ini').write_text(f'[General]\nName: {NAME}\nAuthor: OMS local author\nVersion: 2.7\n',encoding='utf-8')
    write(owner.output.with_name(owner.output.name+'.source-receipt.json'),{'name':NAME,'status':'generated-pending-author-cli-and-player',
        'frames':[frame_id(x) for x in owner.frame_styles],'sourceLibrary':str(owner.library),'sourceExclusions':owner.exclusions,
        'frameReceipts':owner.receipts,'frameSourceAssets':list(owner.sources.values()),
        'componentsSource':str(args.components),'componentOverrides':[str(p) for p in args.component_overrides],
        'graphTargets':graph_targets['receipts'],
        'graphNotifications':{'source':str(args.graph_notifications),
                              'families':{family:value['receipt'] for family,value in notifications['families'].items()}},
        'bga':{'receipts':bga['receipts'],'sourceBoundary':bga['sourceBoundary']},
        'noLightningArtworkInputs':True,
        'scope':'Private independent gameplay skin; native OMS song selection and results; original runtime parity is a separate evidence boundary.'})
    print(json.dumps({'author':str(owner.output),'frames':len(choices),'modules':len(components['modules'])+3}))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for field in ('library','research','components','output','image-tools','reading-assets','source-font','presentation-assets','gauge-assets','failure-assets','graph-notifications','bga-geometry'):
        parser.add_argument('--'+field,type=Path,required=True)
    parser.add_argument('--component-overrides',type=Path,nargs='*',default=[])
    build(parser.parse_args())
