"""Read-only local DLC inventory. Python 3 stdlib + PowerShell/Mono.Cecil.

Raw XML indexes are NOT patched/resolved Def databases. Only output is written.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
from xml.parsers import expat

PACKS = ('Core', 'Royalty', 'Ideology', 'Biotech', 'Anomaly', 'Odyssey')
SOURCE_FILES = (
    'Source/PersonalChronicle/Domain/DlcStatus.cs',
    'Source/PersonalChronicle/Compatibility/Dlc/IdeologyAdapter.cs',
    'Source/PersonalChronicle/Compatibility/Dlc/PawnLifeBiotechAdapter.cs',
)
GATES = {'MayRequire', 'MayRequireAnyOf', 'IfModActive', 'IfModActiveAll', 'IfModNotActive'}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def raw_index(path, relative):
    """Use XML parser line numbers, not text matching inside comments."""
    data = path.read_bytes()
    parser = expat.ParserCreate()
    starts = []
    gates = []
    depth = 0

    def start(name, attrs):
        nonlocal depth
        depth += 1
        if depth == 2:
            starts.append(parser.CurrentLineNumber)
        for attr, value in attrs.items():
            if attr in GATES:
                gates.append({'line': parser.CurrentLineNumber, 'node': name,
                              'attribute': attr, 'value': value, 'depth': depth})

    def end(name):
        nonlocal depth
        depth -= 1

    parser.StartElementHandler = start
    parser.EndElementHandler = end
    parser.Parse(data, True)
    root = ET.fromstring(data)
    definitions = []
    if root.tag == 'Defs':
        for line, node in zip(starts, root):
            definitions.append({'line': line, 'type': node.tag, 'defName': node.findtext('defName'),
                'templateName': node.get('Name'), 'parentName': node.get('ParentName'),
                'abstract': (node.get('Abstract') or '').lower() == 'true',
                'class': node.get('Class'), 'topLevelFields': [child.tag for child in node],
                'requirements': {key: val for key, val in node.attrib.items() if key in GATES}})
    patches = []
    if root.tag == 'Patch':
        for node in root.iter():
            if node.get('Class'):
                patches.append({'class': node.get('Class'), 'xpath': node.findtext('xpath'),
                                'requirements': {key: val for key, val in node.attrib.items() if key in GATES}})
    return {'path': relative, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data),
            'root': root.tag, 'definitions': definitions, 'gates': gates, 'patchOperations': patches}


def official_config(path):
    if path is None or not path.is_file():
        return {'state': 'Unavailable', 'officialActiveEntries': [], 'runtimeLoaded': 'Unknown'}
    root = ET.parse(path).getroot()
    entries = []
    for position, node in enumerate(root.findall('./activeMods/li')):
        package = (node.text or '').strip().lower()
        if package == 'ludeon.rimworld' or package.startswith('ludeon.rimworld.'):
            entries.append({'packageId': package, 'loadOrderPosition': position})
    return {'state': 'ConfigRead', 'path': str(path), 'sha256': digest(path),
            'officialActiveEntries': entries, 'runtimeLoaded': 'Unknown'}


def collect(game, repo, output, cecil, config, shell):
    output.mkdir(parents=True, exist_ok=True)
    runtime_path = output / 'runtime-api-and-il.json'
    script = Path(__file__).with_name('inspect-runtime.ps1')
    subprocess.run([shell, '-NoProfile', '-File', str(script), '-GameRoot', str(game),
                    '-CecilPath', str(cecil), '-OutputPath', str(runtime_path)], check=True)
    runtime = json.loads(runtime_path.read_text(encoding='utf-8-sig'))
    api_lines = ['# Installed runtime API index', '', 'Generated signatures; public does not imply a supported mod extension point.', '']
    for item in runtime['types']:
        api_lines += ['## ' + item['name'], '', '```text']
        api_lines += [method['signature'] for method in item['methods'] if method['public']]
        api_lines += ['```', '']
    (output / 'runtime-api-index.md').write_text('\n'.join(api_lines), encoding='utf-8')
    initial_assembly_hash = runtime['assemblySha256'].lower()
    config_info = official_config(config)
    active = {entry['packageId'] for entry in config_info['officialActiveEntries']}
    errors, packs = [], []
    for name in PACKS:
        folder = game / 'Data' / name
        about = folder / 'About' / 'About.xml'
        pack = {'folder': name, 'root': str(folder), 'diskPresent': folder.is_dir(),
                'runtimeLoaded': 'Unknown', 'files': []}
        if about.is_file():
            metadata = ET.parse(about).getroot()
            package = (metadata.findtext('packageId') or '').strip().lower()
            pack.update(packageId=package, aboutSha256=digest(about),
                configuredActive=(package in active) if config_info['state'] == 'ConfigRead' else None,
                supportedVersions=[node.text for node in metadata.findall('supportedVersions/li')],
                forceLoadBefore=[node.text for node in metadata.findall('forceLoadBefore/li')],
                forceLoadAfter=[node.text for node in metadata.findall('forceLoadAfter/li')],
                hasLoadFoldersXml=(folder / 'LoadFolders.xml').is_file())
            files = sorted({path for sub in ('Defs', 'Patches') for path in (folder / sub).rglob('*.xml')})
            if (folder / 'LoadFolders.xml').is_file():
                files.append(folder / 'LoadFolders.xml')
            for path in files:
                try:
                    pack['files'].append(raw_index(path, path.relative_to(game).as_posix()))
                except (ET.ParseError, expat.ExpatError) as error:
                    errors.append({'path': str(path), 'error': str(error)})
            counts = Counter(record['type'] for file in pack['files'] for record in file['definitions'])
            pack.update(xmlFiles=len(pack['files']), rawDefinitionNodes=sum(counts.values()),
                        definitionTypeCounts=dict(sorted(counts.items())),
                        requirementNodes=sum(len(file['gates']) for file in pack['files']))
        else:
            pack.update(packageId=None, configuredActive=None, reason='AboutMissing')
        packs.append(pack)
    source_dir = output / 'project-source'
    source_dir.mkdir(exist_ok=True)
    source_copies = []
    for relative in SOURCE_FILES:
        source = repo / relative
        destination = source_dir / (source.name + '.txt')
        shutil.copyfile(source, destination)
        source_copies.append({'path': relative, 'snapshot': destination.relative_to(output).as_posix(),
                             'sha256': digest(source), 'copySha256': digest(destination)})
    hits = []
    pattern = re.compile(r'ModsConfig\.|IdeologyAdapter|PawnLifeBiotechAdapter|DlcStatus\.|IVanillaLifeExtension|extension\.(RegisterNeeds|VisiblePregnancy)|MayRequire')
    for base, suffix in (('Source', '.cs'), ('Defs', '.xml'), ('Patches', '.xml')):
        for path in sorted((repo / base).rglob('*' + suffix)):
            if any(part in {'bin', 'obj'} for part in path.parts):
                continue
            lines = path.read_text(encoding='utf-8-sig').splitlines()
            matching = [{'line': number, 'text': text.strip()} for number, text in enumerate(lines, 1) if pattern.search(text)]
            if matching:
                hits.append({'path': path.relative_to(repo).as_posix(), 'sha256': digest(path), 'hits': matching})
    snapshot = {'schemaVersion': 1, 'createdUtc': datetime.now(timezone.utc).isoformat(),
        'gameRoot': str(game), 'repoRoot': str(repo),
        'gameVersion': (game / 'Version.txt').read_text(encoding='utf-8-sig').strip(),
        'assemblySha256': initial_assembly_hash, 'config': config_info, 'packs': packs,
        'sourceCopies': source_copies, 'sourceCallSites': hits, 'parseErrors': errors,
        'scope': 'Raw official Def/Patch XML structure; no merging, patches, inheritance, runtime or ownership inference'}
    write_json(output / 'inventory.json', snapshot)
    # A TSV makes thousands of raw Def locations searchable without proprietary XML copies.
    rows = ['pack\ttype\tdefName\ttemplateName\tparentName\tabstract\tpath\tline']
    for pack in packs:
        for file in pack['files']:
            for record in file['definitions']:
                rows.append('\t'.join(str(value or '') for value in (pack['folder'], record['type'], record['defName'],
                    record['templateName'], record['parentName'], str(record['abstract']), file['path'], record['line'])))
    (output / 'raw-def-index.tsv').write_text('\n'.join(rows) + '\n', encoding='utf-8')
    if digest(game / 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll') != initial_assembly_hash:
        raise RuntimeError('Assembly changed during collection; snapshot rejected')
    if errors:
        raise RuntimeError(f'{len(errors)} XML parse errors; inspect inventory.json; no complete-scan claim permitted')
    for item in source_copies:
        if item['sha256'] != item['copySha256'] or digest(repo / item['path']) != item['sha256']:
            raise RuntimeError('Source changed during collection; snapshot rejected')
    print(json.dumps({'packs': len(packs), 'xmlFiles': sum(p.get('xmlFiles', 0) for p in packs),
                      'rawDefinitionNodes': sum(p.get('rawDefinitionNodes', 0) for p in packs), 'parseErrors': len(errors)}))


if __name__ == '__main__':
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument('--game-root', required=True, type=Path)
    args.add_argument('--repo-root', required=True, type=Path)
    args.add_argument('--cecil-path', required=True, type=Path)
    args.add_argument('--mods-config', type=Path)
    args.add_argument('--output', required=True, type=Path)
    args.add_argument('--powershell', default='pwsh')
    options = args.parse_args()
    collect(options.game_root.resolve(), options.repo_root.resolve(), options.output.resolve(),
            options.cecil_path.resolve(), options.mods_config.resolve() if options.mods_config else None,
            options.powershell)
