"""Publish original AUTO SCRATCH art in a fresh private Omni sibling author."""
from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import uuid

from build_lighting_details import store_json
from build_lighting_gameplay_controls import audit_shared_root
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_persistent import _independent
import lighting_auto_assist as assist


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def upgrade(source, output, library):
    source = _independent(source)
    output = _independent(output, output=True, exists=False)
    library = _independent(library)
    assist.require(output.parent == source.parent and output != source and not output.exists()
                   and not output.with_suffix('.osk').exists(), 'Create a fresh sibling author and archive')
    archive = source.with_suffix('.osk')
    generation = read_json(exact_file(source, 'generation.json'))
    identity = {'generationSha256': sha(source / 'generation.json'), 'archiveSha256': sha(archive)}
    assist.require(generation['archive']['sha256'] == identity['archiveSha256'], 'The immutable parent archive changed')
    source_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    original_catalog = read_json(exact_file(source, 'gameplay/content.json'))
    assist.require(len(original_catalog['modules']) == 13
                   and sum(len(module['choices']) for module in original_catalog['modules']) == 1285,
                   'Use the complete v31 timing/NEXT parent')
    specs = assist.contribution_specs(original_catalog)
    reference = assist.source_reference(library, specs)
    staging = output.parent / (output.name + '.staging-' + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    catalog = copy.deepcopy(original_catalog)
    media, new_configs, receipts = {}, set(), []
    for spec in specs:
        manifest, scene, measurements = assist.read_contribution(source, spec)
        updated_manifest, updated_scene, receipt = assist.transform_contribution(manifest, scene, spec, reference, measurements)
        assist.material_ownership(updated_scene)
        for relative, item in receipt['newFiles'].items():
            source_image = Path(item['source']).resolve(strict=True)
            assist.require(source_image.is_relative_to(library), 'AUTO SCRATCH source must remain in the independent library')
            if relative in media:
                assist.require(media[relative] == item['sha256'], 'Conflicting original AUTO SCRATCH pixels')
            elif relative in source_files:
                assist.require(hashes[relative] == item['sha256'], 'A new asset path conflicts with parent media')
                media[relative] = item['sha256']
            else:
                copy_exact(source_image, staging / relative)
                assist.require(sha(staging / relative) == item['sha256'], 'AUTO SCRATCH PNG bytes changed while copying')
                media[relative] = item['sha256']
        module, = [entry for entry in catalog['modules'] if entry['id'] == spec['module']]
        choice, = [entry for entry in module['choices'] if entry['value'] == spec['choice']]
        part = choice if not spec['when'] else next(entry for entry in choice['variants'] if entry['when'] == spec['when'])
        assist.require((part['manifest'], part['scene']) == (spec['manifest'], spec['scene']), 'Frozen contribution selection changed')
        part.update({'manifest': store_json(staging, 'm', updated_manifest), 'scene': store_json(staging, 's', updated_scene)})
        new_configs.update((part['manifest'], part['scene']))
        receipts.append({**receipt, 'newManifest': part['manifest'], 'newScene': part['scene']})
    write_json(staging / 'gameplay/content.json', catalog)
    files = package_closure(staging)
    assist.require(set(files) <= set(source_files) | set(media) | new_configs,
                   'Only reviewed AUTO SCRATCH resources and contribution configurations may enter the closure')
    assist.require(preview_files(catalog) == preview_files(original_catalog), 'Preserve every existing preview and choice')
    for original, changed in zip(original_catalog['modules'], catalog['modules']):
        if original['id'] not in ('notes', 'charge-notes'):
            assist.require(original == changed, 'An unrelated functional/art module changed')
    assist.require(all(sha(exact_file(staging, relative)) == hashes[relative]
                       for relative in set(source_files) & set(files) if relative != 'gameplay/content.json'),
                   'A retained parent document, art, font or animation changed')
    audit = audit_package(staging, files)
    audit_shared_root(staging, catalog, audit)
    audit['authorDirectory'] = author_directory_budget(files)
    parent_audit = read_json(exact_file(source, 'package-audit.json'))
    assist.require(audit['options'] == 32 and audit['selectedUpperBound']['fonts'] == parent_audit['selectedUpperBound']['fonts']
                   and not audit['aggregateViolations'], 'Keep the existing option, font and selected resource budgets')
    for item in reference['readInputs']:
        assist.require(sha(item['path']) == item['sha256'], 'A source library input changed during authoring')
    assist.require(all(sha(exact_file(source, relative)) == expected for relative, expected in hashes.items())
                   and sha(source / 'generation.json') == identity['generationSha256'] and sha(archive) == identity['archiveSha256'],
                   'The immutable parent author/archive changed')
    receipt = {'name': NAME, 'status': 'prepared-awaiting-current-runtime-validation', 'repackagedFrom': str(source),
        'sourceIdentity': identity, 'autoScratch': {'art': reference, 'contributions': receipts,
            'source': assist.SOURCE, 'rulesets': ['bms'], 'autoNoteAndManiaArtChanged': False, 'fontsAdded': 0, 'optionsAdded': 0},
        'sourcePreservation': {'originalSourceClosureUnchanged': True, 'originalArchiveUnchanged': True,
            'changedExistingMedia': [], 'previewFiles': len(preview_files(catalog)), 'unrelatedModulesUnchanged': 11},
        'codecValidation': {'status': 'pending'}, 'runtimeValidation': {'status': 'pending'},
        'budgets': audit['selectedUpperBound'], 'unifiedOptionCount': audit['options'],
        'closure': {'files': len(files), 'uncompressedBytes': audit['uncompressedBytes'], 'configurationBytes': audit['configurationBytes']},
        'authorDirectory': audit['authorDirectory']}
    write_json(staging / 'package-audit.json', audit)
    write_json(staging / 'generation.json', receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    print({'author': str(output), 'contributions': len(receipts), 'originalAutoTextures': len(media),
           'budgets': receipt['budgets'], 'closure': receipt['closure']})
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--library', type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.library)


if __name__ == '__main__':
    main()
