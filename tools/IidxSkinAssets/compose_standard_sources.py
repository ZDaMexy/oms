"""Compose verified Standard libraries without overwriting any source.

Only explicit logical package aliases change. PNG, AFP, GEO, archive and other
payload bytes are copied unchanged. Original game paths/hashes remain in each
package's origin and exact input manifests. The 22 extension retains the whole
24/26 manifest so existing AFP research keeps its original identity. No
Lightning input exists.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
from urllib.parse import quote, unquote

from standard_source_catalog import (BASE_STYLES, COMPOSITION, COMPOSITION22, COMPOSITION21,
                                     EARLIER_ALIASES, SOURCE22_ALIASES, SOURCE21_ALIASES,
                                     SOURCE21_CUSTOM_PACKAGES, expanded)


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def inside(root, relative):
    path = (root / unquote(relative)).resolve(strict=True)
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError('Source file leaves its verified library')
    return path


def load(root):
    data = json.loads((root / 'library.json').read_text(encoding='utf-8-sig'))
    if data['status'] != 'complete' or data['sourceMetadataUnchanged'] is not True or 'sourceComposition' in data:
        raise ValueError('Use two complete, original independently extracted libraries')
    return data


def import_packages(output, library, textures, data, imports, identity):
    """Copy explicit aliases while retaining original archive and sprite bytes."""
    copied_files = []
    for original, alias, package in imports:
        source, logical = package['source'], 'contents/data/graphic/' + alias + '.ifs'
        if any(p['source'] == logical for p in data['packages']):
            raise ValueError('Earlier package alias collides with an existing source')
        target = output / 'source-copies' / logical
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(inside(library, 'source-copies/' + source), target)
        if digest(target) != package['sha256']:
            raise ValueError('Imported archive changed')
        old_prefix = 'packages/data/graphic/' + original
        new_prefix = 'packages/contents/data/graphic/' + alias
        for file in sorted((library / old_prefix).rglob('*')):
            if not file.is_file(): continue
            checked = inside(library, file.relative_to(library).as_posix())
            relative = new_prefix + '/' + file.relative_to(library / old_prefix).as_posix()
            dest = output / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(checked, dest)
            checksum = digest(checked)
            if digest(dest) != checksum: raise ValueError('Imported source payload changed')
            row = {'path': relative, 'origin': file.relative_to(library).as_posix(), 'sha256': checksum}
            if identity != 'iidx24':
                row['library'] = identity
            copied_files.append(row)
        row = copy.deepcopy(package)
        row.update(source=logical, candidateStyle=None,
                   origin={'library': identity, 'path': source, 'archiveSHA256': package['sha256']})
        data['packages'].append(row)
        for texture in textures:
            if texture['package'] != source: continue
            row = copy.deepcopy(texture)
            row['originPackage'] = source
            row['package'], row['candidateStyle'] = logical, None
            for field in ('sprite', 'raw'):
                original_path = unquote(row[field])
                if not original_path.startswith(old_prefix + '/'):
                    raise ValueError('Texture does not belong to its declared source package')
                row[field] = quote(new_prefix + original_path[len(old_prefix):], safe='/')
            if digest(inside(output, row['sprite'])) != row['sha256']:
                raise ValueError('Imported sprite changed')
            data['textures'].append(row)
    return copied_files


def publish(output, result, input_hashes, contract):
    result['uniqueSpriteContents'] = len({t['sha256'] for t in result['textures']})
    result['note'] = 'Logical package aliases; original source paths and hashes are preserved in origin and source-imports.'
    template = Path(__file__).with_name('browser.html').read_text(encoding='utf-8')
    embedded = json.dumps(result, ensure_ascii=False).replace('<', '\\u003c').replace('>', '\\u003e')
    (output / 'packages.json').write_text(json.dumps(result['packages'], ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'index.html').write_text(template.replace('__LIBRARY_DATA__', embedded), encoding='utf-8')
    (output / 'library.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'run.json').write_text(json.dumps({'status': 'complete', 'contract': contract, 'inputManifestSHA256': input_hashes}, indent=2) + '\n')


def compose(library26, library24, output):
    library26, library24 = (Path(p).resolve(strict=True) for p in (library26, library24))
    output = Path(output).resolve()
    if output.exists() or output.drive.lower() == 'c:' or not output.parent.is_dir():
        raise ValueError('Use a new library under an existing dedicated non-system parent')
    if any(output.is_relative_to(p) or p.is_relative_to(output) for p in (library26, library24)):
        raise ValueError('Composed library must not overlap either source')
    base, earlier = load(library26), load(library24)
    if base.get('graphicRoot', 'contents/data/graphic') != 'contents/data/graphic' or earlier.get('graphicRoot') != 'data/graphic':
        raise ValueError('Expected the separately verified 26 and 24 source layouts')
    base_frames = {int(Path(p['source']).stem[7:]) for p in base['packages'] if Path(p['source']).stem.startswith('gmframe')}
    if base_frames != set(BASE_STYLES):
        raise ValueError('The base must contain the existing independent Standard ten-theme source selection')
    for package in base['packages']:
        relative = PurePosixPath(package['source']).relative_to('contents/data/graphic')
        if len(relative.parts) > 1 and relative.parts[0] not in ('0', '1'):
            raise ValueError('The base contains an unqualified Standard source family')
    for file in library26.rglob('*'):
        if file.is_symlink() or (hasattr(file, 'is_junction') and file.is_junction()):
            raise ValueError('The base library must not contain reparse paths')
        if file.is_file():
            inside(library26, file.relative_to(library26).as_posix())
    for package in base['packages']:
        archive = inside(library26, 'source-copies/' + package['source'])
        if digest(archive) != package['sha256'] or not package['sourceUnchanged']:
            raise ValueError('Base archive differs from its verified source manifest')
    for texture in base['textures']:
        if digest(inside(library26, texture['sprite'])) != texture['sha256']:
            raise ValueError('Base sprite differs from its verified source manifest')
    earlier_packages = {p['source']: p for p in earlier['packages']}
    imports = []
    for original, alias in EARLIER_ALIASES.items():
        source = 'data/graphic/' + original + '.ifs'
        package = earlier_packages[source]
        if not package['sourceUnchanged'] or digest(inside(library24, 'source-copies/' + source)) != package['sha256']:
            raise ValueError('Earlier archive differs from its verified source manifest')
        imports.append((original, alias, package))
    required = sum(p.stat().st_size for p in library26.rglob('*') if p.is_file())
    required += sum(p.stat().st_size for p in library24.rglob('*') if p.is_file())
    if shutil.disk_usage(output.parent).free < required + 256 * 1024 * 1024:
        raise ValueError('Insufficient working space for the bounded library composition')
    # A partial composition must not inherit the original library's success
    # receipts or browser entry. Only root metadata is replaced; payload files
    # with these names remain untouched.
    metadata_entries = {'library.json', 'packages.json', 'index.html', 'run.json'}

    def ignore_original_metadata(directory, names):
        return metadata_entries.intersection(names) if Path(directory) == library26 else ()

    shutil.copytree(library26, output, ignore=ignore_original_metadata)
    result = copy.deepcopy(base)
    input_hashes = {'iidx26': digest(library26 / 'library.json'), 'iidx24': digest(library24 / 'library.json')}
    for package in result['packages']:
        archive = inside(output, 'source-copies/' + package['source'])
        if digest(archive) != package['sha256'] or not package['sourceUnchanged']:
            raise ValueError('Base archive differs from its verified source manifest')
        package['origin'] = {'library': 'iidx26', 'path': package['source'], 'archiveSHA256': package['sha256']}
    copied_files = import_packages(output, library24, earlier['textures'], result, imports, 'iidx24')
    for label, library in (('iidx26', library26), ('iidx24', library24)):
        target = output / 'source-imports' / (label + '-library.json')
        target.parent.mkdir(exist_ok=True)
        shutil.copy2(library / 'library.json', target)
        if digest(target) != input_hashes[label]: raise ValueError('Input manifest changed')
    result['sourceComposition'] = {'contract': COMPOSITION, 'aliases24': EARLIER_ALIASES, 'inputManifestSHA256': input_hashes,
        'scope': '23 copula retained artwork and 24 SINOBUZ from the 24 HDD; 25/26 and eight ARC SCORE revivals from the 26 HDD. Aliases are author identities, not original paths or complete executable coverage.',
        'importedFiles': copied_files}
    result['sourceFileCount'] = base['sourceFileCount'] + earlier['sourceFileCount']
    publish(output, result, input_hashes, COMPOSITION)
    return {'packages': len(result['packages']), 'textures': len(result['textures']), 'importedPackages': len(imports), 'payloadBytesChanged': False}


def retained_base(library, data):
    """Verify the exact predecessor manifest before accepting its research ID."""
    composition = data['sourceComposition']
    receipt = composition['previousComposition']
    if receipt['path'] != 'source-imports/standard24-26-library.json':
        raise ValueError('Unexpected retained Standard composition path')
    manifest = inside(library, receipt['path'])
    if digest(manifest) != receipt['sha256']:
        raise ValueError('Retained Standard composition manifest changed')
    base = json.loads(manifest.read_text(encoding='utf-8-sig'))
    if not expanded(base) or base['sourceComposition']['contract'] != COMPOSITION:
        raise ValueError('Retained Standard composition is not the original 24/26 library')
    original = base['sourceComposition']
    if (base['status'] != 'complete' or base['sourceMetadataUnchanged'] is not True or
            original['inputManifestSHA256'] != {key: composition['inputManifestSHA256'][key] for key in ('iidx26', 'iidx24')} or
            data['packages'][:len(base['packages'])] != base['packages'] or
            data['textures'][:len(base['textures'])] != base['textures'] or
            composition['importedFiles'][:len(original['importedFiles'])] != original['importedFiles']):
        raise ValueError('Retained Standard composition does not match the unchanged base')
    return base


def extend(base_library, library22, output):
    """Append the exact bounded 22 selection to an existing 24/26 composition."""
    base_library, library22 = (Path(p).resolve(strict=True) for p in (base_library, library22))
    output = Path(output).resolve()
    if output.exists() or output.drive.lower() == 'c:' or not output.parent.is_dir():
        raise ValueError('Use a new library under an existing dedicated non-system parent')
    if any(output.is_relative_to(p) or p.is_relative_to(output) for p in (base_library, library22)):
        raise ValueError('Composed library must not overlap either source')
    base = json.loads((base_library / 'library.json').read_text(encoding='utf-8-sig'))
    if (not expanded(base) or base['sourceComposition']['contract'] != COMPOSITION or
            base['status'] != 'complete' or base['sourceMetadataUnchanged'] is not True or
            base.get('graphicRoot', 'contents/data/graphic') != 'contents/data/graphic'):
        raise ValueError('Extend the complete existing 24/26 Standard composition exactly once')
    earlier = load(library22)
    if earlier.get('graphicRoot') != 'data/graphic':
        raise ValueError('Expected the independently verified 22 source layout')
    expected = {'data/graphic/' + key + '.ifs' for key in SOURCE22_ALIASES}
    packages = {p['source']: p for p in earlier['packages']}
    if set(packages) != expected or len(packages) != len(earlier['packages']):
        raise ValueError('Use exactly the qualified 22 package selection; unsupported or missing source package')
    if any(t['package'] not in expected for t in earlier['textures']):
        raise ValueError('Source22 texture refers to an unsupported package')
    for library in (base_library, library22):
        for file in library.rglob('*'):
            if file.is_symlink() or (hasattr(file, 'is_junction') and file.is_junction()):
                raise ValueError('Source libraries must not contain reparse paths')
    input_hashes = dict(base['sourceComposition']['inputManifestSHA256'])
    for identity in ('iidx26', 'iidx24'):
        if digest(inside(base_library, 'source-imports/' + identity + '-library.json')) != input_hashes[identity]:
            raise ValueError('Retained original source manifest changed')
    for package in base['packages']:
        if not package['sourceUnchanged'] or digest(inside(base_library, 'source-copies/' + package['source'])) != package['sha256']:
            raise ValueError('Base archive differs from its verified source manifest')
    for texture in base['textures']:
        if digest(inside(base_library, texture['sprite'])) != texture['sha256']:
            raise ValueError('Base sprite differs from its verified source manifest')
    for row in base['sourceComposition']['importedFiles']:
        if digest(inside(base_library, row['path'])) != row['sha256']:
            raise ValueError('Existing imported source payload changed')
    imports = []
    for original, alias in SOURCE22_ALIASES.items():
        package = packages['data/graphic/' + original + '.ifs']
        if not package['sourceUnchanged'] or digest(inside(library22, 'source-copies/' + package['source'])) != package['sha256']:
            raise ValueError('Source22 archive differs from its verified source manifest')
        imports.append((original, alias, package))
    retained_path = 'source-imports/standard24-26-library.json'
    if (base_library / retained_path).exists() or (base_library / 'source-imports/iidx22-library.json').exists():
        raise ValueError('New source manifest identity already exists in the base')
    required = sum(p.stat().st_size for library in (base_library, library22) for p in library.rglob('*') if p.is_file())
    if shutil.disk_usage(output.parent).free < required + 256 * 1024 * 1024:
        raise ValueError('Insufficient working space for the bounded library composition')
    base_hash = digest(base_library / 'library.json')
    input_hashes['iidx22'] = digest(library22 / 'library.json')
    metadata_entries = {'library.json', 'packages.json', 'index.html', 'run.json'}

    def ignore_base_metadata(directory, names):
        return metadata_entries.intersection(names) if Path(directory) == base_library else ()

    shutil.copytree(base_library, output, ignore=ignore_base_metadata)
    result = copy.deepcopy(base)
    copied_files = import_packages(output, library22, earlier['textures'], result, imports, 'iidx22')
    for source, relative, checksum in ((base_library / 'library.json', retained_path, base_hash),
                                       (library22 / 'library.json', 'source-imports/iidx22-library.json', input_hashes['iidx22'])):
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if digest(target) != checksum:
            raise ValueError('Input manifest changed')
    composition = result['sourceComposition']
    composition.update(contract=COMPOSITION22, aliases22=SOURCE22_ALIASES,
                       inputManifestSHA256=input_hashes,
                       previousComposition={'path': retained_path, 'sha256': base_hash})
    composition['importedFiles'].extend(copied_files)
    composition['scope'] += (' Source22 keys preserve six supplied frame archives independently; they are not six native menu choices. '
                             'Bare gmframe0 is retained artwork without a proved native menu route. All eight original common packages '
                             'are retained separately; their aliases alone do not select an era or region. Only the eight selected '
                             'lane archives and lnote00/notes_cn00 are added as source22 materials.')
    retained_base(output, result)
    result['sourceFileCount'] = base['sourceFileCount'] + earlier['sourceFileCount']
    publish(output, result, input_hashes, COMPOSITION22)
    return {'packages': len(result['packages']), 'textures': len(result['textures']),
            'importedPackages': len(imports), 'payloadBytesChanged': False}


def retained_base22(library, data):
    """Verify the unchanged 22/24/26 predecessor of the original21 extension."""
    composition = data['sourceComposition']
    receipt = composition['previousComposition22']
    if receipt['path'] != 'source-imports/standard22-24-26-library.json':
        raise ValueError('Unexpected retained 22 Standard composition path')
    manifest = inside(library, receipt['path'])
    if digest(manifest) != receipt['sha256']:
        raise ValueError('Retained 22 Standard composition manifest changed')
    base = json.loads(manifest.read_text(encoding='utf-8-sig'))
    if (not expanded(base) or base['sourceComposition']['contract'] != COMPOSITION22 or
            base['status'] != 'complete' or base['sourceMetadataUnchanged'] is not True):
        raise ValueError('Retained predecessor must be the complete 22/24/26 composition')
    original = base['sourceComposition']
    if (original['inputManifestSHA256'] != {key: composition['inputManifestSHA256'][key] for key in ('iidx26', 'iidx24', 'iidx22')} or
            data['packages'][:len(base['packages'])] != base['packages'] or
            data['textures'][:len(base['textures'])] != base['textures'] or
            composition['importedFiles'][:len(original['importedFiles'])] != original['importedFiles']):
        raise ValueError('Retained 22 Standard composition does not match the unchanged base')
    retained_base(library, base)
    return base


def extend21(base_library, library21, output):
    """Add six original21 core archives; qualify, but do not duplicate, CUSTOM."""
    base_library, library21 = (Path(p).resolve(strict=True) for p in (base_library, library21))
    output = Path(output).resolve()
    if output.exists() or output.drive.lower() == 'c:' or not output.parent.is_dir():
        raise ValueError('Use a new library under an existing dedicated non-system parent')
    if any(output.is_relative_to(p) or p.is_relative_to(output) for p in (base_library, library21)):
        raise ValueError('Composed library must not overlap either source')
    base = json.loads((base_library / 'library.json').read_text(encoding='utf-8-sig'))
    if (not expanded(base) or base['sourceComposition']['contract'] != COMPOSITION22 or
            base['status'] != 'complete' or base['sourceMetadataUnchanged'] is not True):
        raise ValueError('Extend the complete existing 22/24/26 Standard composition exactly once')
    retained_base(base_library, base)
    earlier = load(library21)
    expected = {'data/graphic/' + key + '.ifs' for key in (*SOURCE21_ALIASES, *SOURCE21_CUSTOM_PACKAGES)}
    packages = {package['source']: package for package in earlier['packages']}
    if (earlier.get('graphicRoot') != 'data/graphic' or set(packages) != expected or
            len(packages) != len(earlier['packages']) or any(t['package'] not in expected for t in earlier['textures'])):
        raise ValueError('Use exactly the independently verified 155 original21 packages')
    input_hashes = dict(base['sourceComposition']['inputManifestSHA256'])
    for identity in ('iidx26', 'iidx24', 'iidx22'):
        if digest(inside(base_library, 'source-imports/' + identity + '-library.json')) != input_hashes[identity]:
            raise ValueError('Retained original source manifest changed')
    for library, data in ((base_library, base), (library21, earlier)):
        for path in library.rglob('*'):
            if path.is_symlink() or (hasattr(path, 'is_junction') and path.is_junction()):
                raise ValueError('Source libraries must not contain reparse paths')
        for package in data['packages']:
            if not package['sourceUnchanged'] or digest(inside(library, 'source-copies/' + package['source'])) != package['sha256']:
                raise ValueError('Source archive differs from its independently verified manifest')
        for texture in data['textures']:
            if digest(inside(library, texture['sprite'])) != texture['sha256']:
                raise ValueError('Source sprite differs from its independently verified manifest')
    for row in base['sourceComposition']['importedFiles']:
        if digest(inside(base_library, row['path'])) != row['sha256']:
            raise ValueError('Existing imported source payload changed')
    reused = []
    for original in SOURCE21_CUSTOM_PACKAGES:
        package = packages['data/graphic/' + original + '.ifs']
        matches = [p for p in base['packages'] if p['sha256'] == package['sha256']]
        if not matches:
            raise ValueError('Original21 CUSTOM archive has no exact retained source: ' + original)
        reused.append({'source': package['source'], 'archiveSHA256': package['sha256'],
                       'bytes': inside(library21, 'source-copies/' + package['source']).stat().st_size,
                       'retainedPackages': [{'source': p['source'], 'origin': p['origin'], 'archiveSHA256': p['sha256']} for p in matches],
                       'newChoice': False, 'payloadCopied': False})
    imports = [(original, alias, packages['data/graphic/' + original + '.ifs'])
               for original, alias in SOURCE21_ALIASES.items()]
    retained_path = 'source-imports/standard22-24-26-library.json'
    if (base_library / retained_path).exists() or (base_library / 'source-imports/iidx21-library.json').exists():
        raise ValueError('Original21 identity already exists in the base')
    required = sum(p.stat().st_size for library in (base_library, library21) for p in library.rglob('*') if p.is_file())
    if shutil.disk_usage(output.parent).free < required + 256 * 1024 * 1024:
        raise ValueError('Insufficient working space for the bounded library composition')
    base_hash = digest(base_library / 'library.json')
    input_hashes['iidx21'] = digest(library21 / 'library.json')
    metadata_entries = {'library.json', 'packages.json', 'index.html', 'run.json'}

    def ignore_base_metadata(directory, names):
        return metadata_entries.intersection(names) if Path(directory) == base_library else ()

    shutil.copytree(base_library, output, ignore=ignore_base_metadata)
    result = copy.deepcopy(base)
    copied_files = import_packages(output, library21, earlier['textures'], result, imports, 'iidx21')
    for source, relative, checksum in ((base_library / 'library.json', retained_path, base_hash),
                                       (library21 / 'library.json', 'source-imports/iidx21-library.json', input_hashes['iidx21'])):
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if digest(target) != checksum:
            raise ValueError('Input manifest changed')
    composition = result['sourceComposition']
    composition.update(contract=COMPOSITION21, aliases21=SOURCE21_ALIASES, inputManifestSHA256=input_hashes,
                       previousComposition22={'path': retained_path, 'sha256': base_hash},
                       source21ReusedPackages=reused)
    composition['importedFiles'].extend(copied_files)
    composition['scope'] += (' Original21 SPADA retains its own frame and five shared packages under source21/s21. '
                             'Its149 CUSTOM archives are independently SHA-qualified against retained original archives; '
                             'their provenance is recorded without new menu choices or duplicate payload copies. '
                             'The original21 identity remains separate from22 CUSTOM and26 ARC SCORE SPADA.')
    result['sourceFileCount'] = base['sourceFileCount'] + earlier['sourceFileCount']
    retained_base22(output, result)
    publish(output, result, input_hashes, COMPOSITION21)
    return {'packages': len(result['packages']), 'textures': len(result['textures']),
            'importedPackages': len(imports), 'reusedCustomPackages': len(reused), 'payloadBytesChanged': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('library26', 'library24', 'base-library', 'library22', 'library21'):
        parser.add_argument('--' + name, type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.library21 is not None:
        if args.base_library is None or any(value is not None for value in (args.library22, args.library26, args.library24)):
            parser.error('Use --base-library with --library21 for the original21 extension')
        print(json.dumps(extend21(args.base_library, args.library21, args.output)))
    elif args.base_library is not None or args.library22 is not None:
        if args.base_library is None or args.library22 is None or args.library26 is not None or args.library24 is not None:
            parser.error('Use --base-library with --library22, or --library26 with --library24')
        print(json.dumps(extend(args.base_library, args.library22, args.output)))
    else:
        if args.library26 is None or args.library24 is None:
            parser.error('Both --library26 and --library24 are required for the original composition')
        print(json.dumps(compose(args.library26, args.library24, args.output)))
