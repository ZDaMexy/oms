"""Compose verified 24/26 art libraries without overwriting either source.

Only explicit logical package aliases change. PNG, AFP, GEO, archive and other
payload bytes are copied unchanged. Original game paths/hashes remain in each
package's origin and the two exact input manifests. No Lightning input exists.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
from urllib.parse import quote, unquote

from standard_source_catalog import BASE_STYLES, COMPOSITION, EARLIER_ALIASES


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
    copied_files = []
    for original, alias, package in imports:
        source, logical = package['source'], 'contents/data/graphic/' + alias + '.ifs'
        if any(p['source'] == logical for p in result['packages']):
            raise ValueError('Earlier package alias collides with an existing source')
        target = output / 'source-copies' / logical
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(inside(library24, 'source-copies/' + source), target)
        old_prefix = 'packages/data/graphic/' + original
        new_prefix = 'packages/contents/data/graphic/' + alias
        for file in sorted((library24 / old_prefix).rglob('*')):
            if not file.is_file(): continue
            checked = inside(library24, file.relative_to(library24).as_posix())
            relative = new_prefix + '/' + file.relative_to(library24 / old_prefix).as_posix()
            dest = output / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(checked, dest)
            checksum = digest(checked)
            if digest(dest) != checksum: raise ValueError('Imported source payload changed')
            copied_files.append({'path': relative, 'origin': file.relative_to(library24).as_posix(), 'sha256': checksum})
        row = copy.deepcopy(package)
        row.update(source=logical, candidateStyle=None,
                   origin={'library': 'iidx24', 'path': source, 'archiveSHA256': package['sha256']})
        result['packages'].append(row)
        for texture in earlier['textures']:
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
            result['textures'].append(row)
    for label, library in (('iidx26', library26), ('iidx24', library24)):
        target = output / 'source-imports' / (label + '-library.json')
        target.parent.mkdir(exist_ok=True)
        shutil.copy2(library / 'library.json', target)
        if digest(target) != input_hashes[label]: raise ValueError('Input manifest changed')
    result['sourceComposition'] = {'contract': COMPOSITION, 'aliases24': EARLIER_ALIASES, 'inputManifestSHA256': input_hashes,
        'scope': '23 copula retained artwork and 24 SINOBUZ from the 24 HDD; 25/26 and eight ARC SCORE revivals from the 26 HDD. Aliases are author identities, not original paths or complete executable coverage.',
        'importedFiles': copied_files}
    result['uniqueSpriteContents'] = len({t['sha256'] for t in result['textures']})
    result['sourceFileCount'] = base['sourceFileCount'] + earlier['sourceFileCount']
    result['note'] = 'Logical package aliases; original source paths and hashes are preserved in origin and source-imports.'
    template = Path(__file__).with_name('browser.html').read_text(encoding='utf-8')
    embedded = json.dumps(result, ensure_ascii=False).replace('<', '\\u003c').replace('>', '\\u003e')
    (output / 'packages.json').write_text(json.dumps(result['packages'], ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'index.html').write_text(template.replace('__LIBRARY_DATA__', embedded), encoding='utf-8')
    (output / 'library.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'run.json').write_text(json.dumps({'status': 'complete', 'contract': COMPOSITION, 'inputManifestSHA256': input_hashes}, indent=2) + '\n')
    return {'packages': len(result['packages']), 'textures': len(result['textures']), 'importedPackages': len(imports), 'payloadBytesChanged': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('library26', 'library24', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(compose(args.library26, args.library24, args.output)))
