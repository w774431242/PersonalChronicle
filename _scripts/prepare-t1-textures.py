"""Prepare user-supplied T1 PNGs; no image model calls and no edits to source files."""
import argparse
from datetime import datetime
from pathlib import Path
import hashlib
import json
import shutil
import xml.etree.ElementTree as ET
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DIRECTIONS = {'北': 'north', '东': 'east', '南': 'south', '西': 'west'}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def main():
    parser = argparse.ArgumentParser(description='将中文命名的 T1 原图留档并导出标准游戏贴图，不生成图像。')
    parser.add_argument('source', type=Path)
    args = parser.parse_args()
    source = args.source.resolve(strict=True)
    if not source.is_dir(): raise ValueError('Source must be a directory')
    specs = ET.parse(ROOT / 'Defs/T1_Production.xml').getroot().findall('ThingDef')
    found = {}
    for file in source.rglob('*'):
        if file.is_file() and file.suffix.lower() == '.png':
            if file.stem in found: raise ValueError(f'Duplicate Chinese asset name: {file.stem}')
            found[file.stem] = file
    jobs = []
    for spec in specs:
        label = spec.findtext('label').replace('（T1）', '').strip()
        identity = spec.findtext('defName')
        for chinese, suffix in DIRECTIONS.items():
            stem = label + chinese
            file = found.get(stem)
            if file is None: raise FileNotFoundError(f'Missing {stem}.png')
            target_size = (448, 192) if suffix in ('north', 'south') else (192, 448)
            with Image.open(file) as im:
                im.load()
                if im.mode != 'RGBA': raise ValueError(f'{file.name}: RGBA required')
                if im.width < target_size[0] or im.height < target_size[1]:
                    raise ValueError(f'{file.name}: source is smaller than target')
                # Generated canvases may differ from 7:3 by pixel rounding, but wrong rotations must not pass.
                if abs(im.width / im.height / (target_size[0] / target_size[1]) - 1) > .005:
                    raise ValueError(f'{file.name}: unexpected aspect or orientation {im.size}')
                alpha = im.getchannel('A')
                if alpha.getextrema() != (0, 255): raise ValueError(f'{file.name}: nonempty transparent RGBA required')
                if any(im.getpixel(c)[3] != 0 for c in ((0, 0), (im.width-1, 0), (0, im.height-1), (im.width-1, im.height-1))):
                    raise ValueError(f'{file.name}: corners must be transparent')
                bbox = alpha.getbbox()
                edge_touch = bbox[0] == 0 or bbox[1] == 0 or bbox[2] == im.width or bbox[3] == im.height
                jobs.append({'source': str(file), 'source_name': file.name, 'source_sha256': sha(file),
                             'source_size': list(im.size), 'source_bytes': file.stat().st_size,
                             'target': identity + '_' + suffix + '.png', 'target_size': list(target_size),
                             'alpha_bbox_source': list(bbox), 'source_alpha_touches_edge': edge_touch})
    stamp = datetime.now().strftime('%Y%m%d_%H%M%S_%f')
    archive = ROOT / '_backups' / ('T1-assets-original_' + stamp)
    output = ROOT / '_verify_tmp' / ('T1-assets_' + stamp)
    archive.mkdir(parents=True); output.mkdir(parents=True)
    for job in jobs:
        src = Path(job['source']); backup = archive / src.name
        shutil.copy2(src, backup)
        if sha(backup) != job['source_sha256']: raise RuntimeError('Original archive hash mismatch')
        with Image.open(src) as im:
            # Premultiplied alpha avoids dark fringes around transparent edges during downsampling.
            small = im.convert('RGBa').resize(tuple(job['target_size']), Image.Resampling.LANCZOS).convert('RGBA')
            small.save(output / job['target'], format='PNG', optimize=True)
        target = output / job['target']
        job['target_sha256'] = sha(target)
        job['target_bytes'] = target.stat().st_size
        if sha(src) != job['source_sha256']: raise RuntimeError('Source was modified during processing')
    report = {'source': str(source), 'original_archive': str(archive), 'prepared_directory': str(output),
              'image_models_called': False, 'source_files_unchanged': True, 'files': len(jobs),
              'source_bytes': sum(x['source_bytes'] for x in jobs),
              'prepared_bytes': sum(x['target_bytes'] for x in jobs), 'assets': jobs}
    (output / '素材处理记录.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    (ROOT / '_verify_tmp/T1-assets-latest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k != 'assets'}, ensure_ascii=False, indent=2))

if __name__ == '__main__': main()
