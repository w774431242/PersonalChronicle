"""Verify this reference snapshot and optionally freeze a ZIP. No game execution."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import zipfile

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('collector', Path(__file__).with_name('collect-reference.py'))
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


def validate(bundle, game=None, repo=None, archive=None):
    snapshot = bundle / 'snapshot'
    inventory = json.loads((snapshot / 'inventory.json').read_text(encoding='utf-8'))
    runtime = json.loads((snapshot / 'runtime-api-and-il.json').read_text(encoding='utf-8-sig'))
    checks = []

    def check(name, condition):
        if not condition:
            raise AssertionError(name)
        checks.append({'name': name, 'result': 'PASS'})

    check('Six official pack rows recorded', tuple(pack['folder'] for pack in inventory['packs']) == collector.PACKS)
    check('Zero XML parse errors', inventory['parseErrors'] == [])
    check('Runtime state is not inferred from config', inventory['config']['runtimeLoaded'] == 'Unknown'
          and all(pack['runtimeLoaded'] == 'Unknown' for pack in inventory['packs']))
    check('Assembly identities agree across reports', runtime['assemblySha256'].lower() == inventory['assemblySha256'])
    types = {item['name']: item for item in runtime['types']}
    check('21 requested runtime types present', len(types) == 21)

    def method(type_name, name):
        matches = [m for m in types[type_name]['methods'] if m['name'] == name]
        check(type_name + '.' + name + ' found', len(matches) == 1)
        return matches[0]

    load = method('Verse.LoadedModManager', 'LoadAllActiveMods')
    direct_calls = [call['target'].split('::')[1].split('(')[0] for call in load['calls']
                    if 'Verse.LoadedModManager::' in call['target']]
    check('Original loading stage call order verified', direct_calls == [
        'InitializeMods', 'LoadModContent', 'CreateModClasses', 'LoadModXML',
        'CombineIntoUnifiedXML', 'ErrorCheckPatches', 'ApplyPatches', 'ParseAndProcessXML', 'ClearCachedPatches'])
    parse = method('Verse.LoadedModManager', 'ParseAndProcessXML')
    check('New and legacy parser branches both present',
          any('::DefFromNodeNew(' in call['target'] for call in parse['calls']) and
          any('Verse.DirectXmlLoader::DefFromNode(' in call['target'] for call in parse['calls']) and
          'legacy-xml-deserializer' in parse['strings'])
    check('Top-level all/any requirements recorded', {'MayRequire', 'MayRequireAnyOf'} <= set(parse['strings']))
    gate = method('Verse.DirectXmlToObjectNew', 'ValidateMayRequires')
    check('New parser uses all/any activity queries',
          any('::AllModsActiveNoSuffix(' in c['target'] for c in gate['calls']) and
          any('::AnyModActiveNoSuffix(' in c['target'] for c in gate['calls']))
    for name in collector.PACKS[1:]:
        method('Verse.ModsConfig', 'get_' + name + 'Active')
        method('Verse.ModLister', 'get_' + name + 'Installed')
    check('Absent API probes preserved', all(not probe['present'] for probe in runtime['absentProbes']))
    for copy in inventory['sourceCopies']:
        check('Copied source hash: ' + copy['path'], copy['sha256'] == collector.digest(snapshot / copy['snapshot']))
    raw_count = sum(pack['rawDefinitionNodes'] for pack in inventory['packs'])
    check('TSV row count matches raw XML nodes', sum(1 for _ in (snapshot / 'raw-def-index.tsv').open(encoding='utf-8')) - 1 == raw_count)
    check('All pack counts consistent', all(pack['xmlFiles'] == len(pack['files']) and
        pack['rawDefinitionNodes'] == sum(len(file['definitions']) for file in pack['files'])
        for pack in inventory['packs']))
    with tempfile.TemporaryDirectory(prefix='dlc-reference-fixture-') as directory:
        xml = Path(directory) / 'fixture.xml'
        xml.write_text('<Defs>\n<!-- <Fake MayRequire="bad"/> -->\n<NeedDef Name="Base" Abstract="true">\n'
                       '<defName>CaseSensitive</defName>\n<li MayRequireAnyOf="a,b">x</li>\n</NeedDef>\n</Defs>', encoding='utf-8')
        result = collector.raw_index(xml, 'fixture.xml')
        check('Comments excluded and XML line numbers correct', len(result['definitions']) == 1 and
              result['definitions'][0]['line'] == 3 and result['gates'][0]['line'] == 5)
        check('Abstract/template/identity kept separate', result['definitions'][0]['abstract'] and
              result['definitions'][0]['templateName'] == 'Base' and result['definitions'][0]['defName'] == 'CaseSensitive')
        xml.write_text('<Defs><Broken></Defs>', encoding='utf-8')
        failed = False
        try:
            collector.raw_index(xml, 'fixture.xml')
        except collector.expat.ExpatError:
            failed = True
        check('Malformed XML raises an error', failed)
        check('Missing config stays Unknown', collector.official_config(Path(directory) / 'missing.xml')['state'] == 'Unavailable')
    if game:
        check('Live game assembly hash unchanged', collector.digest(game / 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll') == inventory['assemblySha256'])
        count = 0
        for pack in inventory['packs']:
            root = game / 'Data' / pack['folder']
            live = {p.relative_to(game).as_posix() for sub in ('Defs', 'Patches') for p in (root / sub).rglob('*.xml')}
            if (root / 'LoadFolders.xml').is_file():
                live.add((root / 'LoadFolders.xml').relative_to(game).as_posix())
            check('Live file set: ' + pack['folder'], live == {file['path'] for file in pack['files']})
            check('Live About hash: ' + pack['folder'], collector.digest(root / 'About/About.xml') == pack['aboutSha256'])
            for file in pack['files']:
                if collector.digest(game / file['path']) != file['sha256']:
                    raise AssertionError('Live XML changed: ' + file['path'])
                count += 1
        check('All live XML hashes match (' + str(count) + ')', count == sum(p['xmlFiles'] for p in inventory['packs']))
    if repo:
        for source in inventory['sourceCallSites']:
            check('Live call-site source hash: ' + source['path'], collector.digest(repo / source['path']) == source['sha256'])
    report = {'createdUtc': datetime.now(timezone.utc).isoformat(), 'verification': 'MetadataAndToolVerified',
              'gameVerified': False, 'productionIntegrated': False, 'checks': checks, 'passed': len(checks),
              'scope': 'Tool fixtures, snapshot consistency, and requested live hashes; no behavioral gameplay tests'}
    collector.write_json(snapshot / 'validation.json', report)
    if archive:
        if bundle == archive.resolve() or bundle in archive.resolve().parents:
            raise ValueError('ZIP must be outside the bundle')
        files = [path for path in sorted(bundle.rglob('*')) if path.is_file() and path.name != 'manifest.json']
        if any(path.suffix.lower() in {'.dll', '.exe', '.png', '.ogg', '.xml'} for path in files):
            raise AssertionError('Unexpected game payload in bundle')
        manifest = {'schemaVersion': 1, 'files': [{'path': path.relative_to(bundle).as_posix(),
                     'sha256': collector.digest(path), 'bytes': path.stat().st_size} for path in files]}
        collector.write_json(bundle / 'manifest.json', manifest)
        archive.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as zipped:
            for path in files + [bundle / 'manifest.json']:
                zipped.write(path, bundle.name + '/' + path.relative_to(bundle).as_posix())
        with zipfile.ZipFile(archive) as zipped:
            if zipped.testzip() is not None:
                raise AssertionError('ZIP CRC failed')
            for entry in manifest['files']:
                data = zipped.read(bundle.name + '/' + entry['path'])
                if collector.hashlib.sha256(data).hexdigest() != entry['sha256']:
                    raise AssertionError('ZIP manifest mismatch: ' + entry['path'])
        print(json.dumps({'validationChecksPassed': len(checks), 'zipEntries': len(files) + 1,
                          'zipBytes': archive.stat().st_size, 'zipSha256': collector.digest(archive)}))
    else:
        print(json.dumps({'validationChecksPassed': len(checks)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', required=True, type=Path)
    parser.add_argument('--game-root', type=Path)
    parser.add_argument('--repo-root', type=Path)
    parser.add_argument('--zip', type=Path)
    args = parser.parse_args()
    validate(args.bundle.resolve(), args.game_root.resolve() if args.game_root else None,
             args.repo_root.resolve() if args.repo_root else None, args.zip)
