"""Read-only PNG/manifest checks; never draws, crops, rescales or edits assets."""
from pathlib import Path
import hashlib
import json
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08'
manifest = json.loads((BASE / '生成清单.json').read_text(encoding='utf-8-sig'))
files = []
for entry in manifest['assets']:
    path = ROOT / entry['path']
    with Image.open(path) as image:
        image.load()
        w, h = image.size
        if image.mode != 'RGBA':
            raise RuntimeError(f'{path}: expected real RGBA')
        data = np.asarray(image)
        alpha = data[:, :, 3]
        visible = alpha > 128  # Cutout uses the meaningful opaque silhouette, not tiny alpha noise.
        ys, xs = np.nonzero(visible)
        if not len(xs):
            raise RuntimeError(f'{path}: empty image')
        box = [int(xs.min()), int(ys.min()), int(xs.max()+1), int(ys.max()+1)]
        bw, bh = box[2]-box[0], box[3]-box[1]
        center_error = [abs((box[0]+box[2])/2/w-.5), abs((box[1]+box[3])/2/h-.5)]
        border = np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]])
        rgb = data[visible,:3]
        checks = {
            'squareCanvas': w == h,
            'sufficientResolution': 512 <= w <= 2048,
            # Keep strict alpha-zero status below. One 8-bit alpha LSB is reported
            # as encoder residue, not confused with an opaque/cropped silhouette.
            'nearZeroTransparentBorder': int(border.max()) <= 1,
            'substantialTransparency': float((alpha==0).mean()) > .4,
            'notBlankWhitePlaceholder': float(rgb.std(axis=0).mean()) > 15,
            'centered': max(center_error) <= .04,
            'directionalCounterRatio': .38 <= (bh/bw if entry['direction'] in ('east','west') else bw/bh) <= .60,
            'insideOneByTwoFootprint': (2*bh/h <= 1.0 and 2*bw/w <= 1.9) if entry['direction'] in ('east','west') else (2*bw/w <= 1.0 and 2*bh/h <= 1.9),
            'margin': min(box[0]/w,box[1]/h,(w-box[2])/w,(h-box[3])/h) >= .035,
        }
        files.append({
            **entry, 'width':w, 'height':h, 'mode':'RGBA', 'bytes':path.stat().st_size,
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest(), 'opaqueBounds':box,
            'silhouetteRatio':round(bw/bh,4), 'mapSilhouetteSize':[round(2*bw/w,4),round(2*bh/h,4)],
            'zeroAlphaFraction':round(float((alpha==0).mean()),4), 'borderMaxAlpha':int(border.max()),
            'strictZeroAlphaBorder':int(border.max())==0,
            'lowAlphaNoisePixels':int(((alpha>0)&(alpha<=16)).sum()), 'checks':checks,
            'checksPassed':all(checks.values()),
        })
groups = []
for name in sorted(set(f['defName'] for f in files)):
    group = [f for f in files if f['defName'] == name]
    north = next(f for f in group if f['direction'] == 'north')
    nw, nh = north['mapSilhouetteSize']
    comparisons = []
    for f in group:
        fw, fh = f['mapSilhouetteSize']
        short, long = (fh,fw) if f['direction'] in ('east','west') else (fw,fh)
        stable = abs(short/nw-1) <= .10 and abs(long/nh-1) <= .10
        comparisons.append({'direction':f['direction'], 'shortScaleVsNorth':round(short/nw,4),
                            'longScaleVsNorth':round(long/nh,4), 'withinTenPercent':stable})
    groups.append({'defName':name, 'fourUniqueDirections':sorted(f['direction'] for f in group)==['east','north','south','west'],
                   'comparisons':comparisons, 'scaleStable':all(c['withinTenPercent'] for c in comparisons)})
report = {
    'changeId':'CHG-20261008-ECON-ART', 'files':files,
    'groups':groups,
    'checksPassed':len(files)==20 and len(groups)==5 and all(f['checksPassed'] for f in files) and all(g['scaleStable'] and g['fourUniqueDirections'] for g in groups),
    'pixelEditsPerformed':False,
    'note':'Meaningful bounds use alpha > 128. Border gate allows only 0/255 or 1/255; strict zero status and low-alpha speckles are reported separately and not erased. No opaque background or clipped silhouette is accepted. Visual review and actual Cutout rendering remain separate.',
    'gameVerified':False,
}
(BASE / '图像检查.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for f in files:
    print(f"{f['defName']}: {f['width']}x{f['height']} ratio={f['silhouetteRatio']} map={f['mapSilhouetteSize']} PASS={f['checksPassed']}")
if not report['checksPassed']:
    raise SystemExit('Image structural gate failed; see per-file checks')
