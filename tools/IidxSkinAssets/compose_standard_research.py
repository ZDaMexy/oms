"""Reuse unchanged 26 AFP exports and join separately evaluated 23/24 exports.

Every original input in each static and recorded source closure is rehashed
against the composed library. Existing evidence and payloads are not rewritten.
"""
import argparse
import json
from pathlib import Path
import shutil

from compose_standard_sources import digest, inside
from standard_source_catalog import BASE_STYLES, expanded, layout_family, metadata


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def compose(library, base_research, research23, research24, output):
    library = library.resolve(strict=True)
    data = metadata(library)
    if not expanded(data): raise ValueError('Use the qualified 24/26 composition')
    output = output.resolve()
    inputs = [(base_research.resolve(strict=True), BASE_STYLES), (research23.resolve(strict=True), (23,)),
              (research24.resolve(strict=True), (24,))]
    if output.exists() or output.drive.lower() == 'c:' or not output.parent.is_dir():
        raise ValueError('Use a new research directory under an existing non-system parent')
    if any(output.is_relative_to(p) or p.is_relative_to(output) for p in (library, *(p for p, _ in inputs))):
        raise ValueError('Research output must not overlap an input')
    expected_base = data['sourceComposition']['inputManifestSHA256']['iidx26']
    receipts, plans = [], []
    for source, styles in inputs:
        source_identity = read(source / 'source.json')
        expected = expected_base if styles == BASE_STYLES else digest(library / 'library.json')
        if source_identity['librarySha256'] != expected:
            raise ValueError('Research source manifest does not match its original verified library')
        for style in styles:
            folder = source / f'gmframe{style:02}'
            reference = read(folder / 'skin-reference-map.json')
            if reference['package'] != folder.name or reference['common_family'] != layout_family(style):
                raise ValueError('Research common texture family does not match its explicit source')
            if any(read(folder / name)['status'] != 'complete' for name in ('run.json', 'recorded/run.json')):
                raise ValueError('Both static and complete recorded exports are required')
            sources = [source / f'sources-{style:02}.json', folder / 'recorded/sources.json']
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
            plans.append((folder, source / f'sources-{style:02}.json'))
    output.mkdir()
    for folder, source_list in plans:
        shutil.copytree(folder, output / folder.name)
        shutil.copy2(source_list, output / source_list.name)
        for manifest in (output / folder.name / 'recorded').glob('*/export.json'):
            value = read(manifest)
            for mode in ('motion', 'steady'):
                for row in value[mode]['files']:
                    old = Path(row['source']).resolve(strict=True)
                    if old.is_relative_to(folder):
                        new = output / folder.name / old.relative_to(folder)
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
        folder = output / f'gmframe{receipt["style"]:02}'
        receipt['composedResearchFiles'] = [{'path': p.relative_to(folder).as_posix(), 'sha256': digest(p)}
                                           for p in sorted(folder.rglob('*')) if p.is_file()]
    composition = {'contract': 'oms-standard-research-composition.v1',
                   'librarySha256': digest(library / 'library.json'), 'frames': receipts}
    (output / 'source-composition.json').write_text(json.dumps(composition, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    mappings = {f'iidx{r["style"]:02}': str(output / f'gmframe{r["style"]:02}/skin-reference-map.json') for r in receipts}
    (output / 'frame-maps.json').write_text(json.dumps(mappings, indent=2) + '\n')
    return {'frames': len(receipts), 'reverifiedSourceFiles': sum(len(r['sourceFilesReverified']) for r in receipts)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ('library', 'base-research', 'research23', 'research24', 'output'):
        parser.add_argument('--' + field, type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(compose(args.library, args.base_research, args.research23, args.research24, args.output)))
