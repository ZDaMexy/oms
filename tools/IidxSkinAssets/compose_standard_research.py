"""Reuse unchanged AFP exports and join separately evaluated Standard sources.

Every original input in each static and recorded source closure is rehashed
against the composed library. Existing evidence and payloads are not rewritten.
"""
import argparse
import json
from pathlib import Path
import shutil

from compose_standard_sources import digest, inside, retained_base
from standard_source_catalog import (BASE_STYLES, SOURCE22_STYLES, expanded,
                                     frame_id, frame_token, includes22,
                                     layout_family, metadata, package_key)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def compose(library, base_research, research23, research24, output, research22=None):
    library = library.resolve(strict=True)
    data = metadata(library)
    if not expanded(data): raise ValueError('Use the qualified 24/26 composition')
    output = output.resolve()
    inputs = [(base_research.resolve(strict=True), BASE_STYLES), (research23.resolve(strict=True), (23,)),
              (research24.resolve(strict=True), (24,))]
    source22 = includes22(data)
    if source22 != (research22 is not None):
        raise ValueError('The 22 source composition requires its own separate research22 input')
    previous_identity = None
    if source22:
        retained_base(library, data)
        previous_identity = data['sourceComposition']['previousComposition']['sha256']
        inputs.append((research22.resolve(strict=True), SOURCE22_STYLES))
    if output.exists() or output.drive.lower() == 'c:' or not output.parent.is_dir():
        raise ValueError('Use a new research directory under an existing non-system parent')
    if any(output.is_relative_to(p) or p.is_relative_to(output) for p in (library, *(p for p, _ in inputs))):
        raise ValueError('Research output must not overlap an input')
    expected_base = data['sourceComposition']['inputManifestSHA256']['iidx26']
    current_identity = digest(library / 'library.json')
    receipts, plans = [], []
    for source, styles in inputs:
        source_identity = read(source / 'source.json')
        if styles == BASE_STYLES:
            expected = {expected_base}
        elif styles == SOURCE22_STYLES:
            expected = {current_identity}
        else:
            expected = {current_identity}
            if previous_identity is not None:
                expected.add(previous_identity)
        if source_identity['librarySha256'] not in expected:
            raise ValueError('Research source manifest does not match its original verified library')
        for style in styles:
            relative_folder = package_key(style)
            folder = source / relative_folder
            reference = read(folder / 'skin-reference-map.json')
            if reference['package'] != relative_folder or reference['common_family'] != layout_family(style):
                raise ValueError('Research common texture family does not match its explicit source')
            if any(read(folder / name)['status'] != 'complete' for name in ('run.json', 'recorded/run.json')):
                raise ValueError('Both static and complete recorded exports are required')
            source_list = source / f'sources-{frame_token(style)}.json'
            sources = [source_list, folder / 'recorded/sources.json']
            checked = {}
            for manifest in sources:
                for row in read(manifest)['files']:
                    actual = inside(library, row['path'])
                    if actual.stat().st_size != row['bytes'] or digest(actual) != row['sha256']:
                        raise ValueError('Source bytes changed since AFP export: ' + row['path'])
                    checked[row['path']] = row['sha256']
            files = [{'path': p.relative_to(folder).as_posix(), 'sha256': digest(p)}
                     for p in sorted(folder.rglob('*')) if p.is_file()]
            receipts.append({'style': style, 'originalResearch': str(source), 'originalSourceIdentity': source_identity,
                             'sourceFilesReverified': checked, 'researchFiles': files})
            plans.append((folder, source_list, relative_folder))
    output.mkdir()
    for folder, source_list, relative_folder in plans:
        destination = output / relative_folder
        shutil.copytree(folder, destination)
        shutil.copy2(source_list, output / source_list.name)
        for manifest in (destination / 'recorded').glob('*/export.json'):
            value = read(manifest)
            for mode in ('motion', 'steady'):
                for row in value[mode]['files']:
                    old = Path(row['source']).resolve(strict=True)
                    if old.is_relative_to(folder):
                        new = destination / old.relative_to(folder)
                    else:
                        parts = old.parts
                        if 'packages' not in parts:
                            raise ValueError('Unexpected recorded source location')
                        new = inside(library, Path(*parts[parts.index('packages'):]).as_posix())
                    if digest(old) != row['sha256'] or digest(new) != row['sha256']:
                        raise ValueError('Rebound source differs from the original recorded export')
                    row['source'] = str(new)
            manifest.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for receipt in receipts:
        folder = output / package_key(receipt['style'])
        receipt['composedResearchFiles'] = [{'path': p.relative_to(folder).as_posix(), 'sha256': digest(p)}
                                           for p in sorted(folder.rglob('*')) if p.is_file()]
    composition = {'contract': 'oms-standard-research-composition.v1',
                   'librarySha256': digest(library / 'library.json'), 'frames': receipts}
    (output / 'source-composition.json').write_text(json.dumps(composition, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    mappings = {frame_id(r['style']): str(output / package_key(r['style']) / 'skin-reference-map.json') for r in receipts}
    (output / 'frame-maps.json').write_text(json.dumps(mappings, indent=2) + '\n')
    return {'frames': len(receipts), 'reverifiedSourceFiles': sum(len(r['sourceFilesReverified']) for r in receipts)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ('library', 'base-research', 'research23', 'research24', 'output'):
        parser.add_argument('--' + field, type=Path, required=True)
    parser.add_argument('--research22', type=Path)
    args = parser.parse_args()
    print(json.dumps(compose(args.library, args.base_research, args.research23, args.research24, args.output, args.research22)))
