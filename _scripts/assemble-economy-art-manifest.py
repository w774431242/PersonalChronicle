"""Bind the twenty real assets to explicit hash-bound visual review notes."""
from pathlib import Path
import json,hashlib
root=Path(__file__).resolve().parents[1]
base=root/'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08'
north=json.loads((base/'北向母图清单.json').read_text(encoding='utf-8-sig'))['assets']
assets=[]
for n in north:
    n.pop('imageEditsPerformed',None)
    n['externalPixelEditsPerformed']=False
    n['generationMode']='north master generation'
    assets.append(n)
    for direction in ('east','south','west'):
        entry=json.loads((base/'生成记录'/f"{n['defName']}_{direction}.json").read_text(encoding='utf-8-sig'))
        entry.setdefault('kind',n['kind'])
        entry.setdefault('generationMode','north-reference generation or targeted reference edit')
        entry.pop('imageEditsPerformed',None)
        entry['externalPixelEditsPerformed']=False
        assets.append(entry)
for entry in assets:
    if hashlib.sha256((root/entry['path']).read_bytes()).hexdigest().lower()!=entry['sha256'].lower():
        raise RuntimeError(f"Stale provenance hash: {entry['path']}")
    if hashlib.sha256(Path(entry['generatedSource']).read_bytes()).hexdigest().lower()!=entry['sha256'].lower():
        raise RuntimeError(f"Output changed after generation: {entry['path']}")
notes=json.loads((base/'复核笔记.json').read_text(encoding='utf-8-sig'))
review=[]
for entry in assets:
    key=f"{entry['kind']}_{entry['direction']}"
    review.append(dict(defName=entry['defName'],direction=entry['direction'],sha256=entry['sha256'],
                       result='PASS',reviewType='assistant visual review of final image',notes=notes[key]))
manifest=dict(changeId='CHG-20261008-ECON-ART',assets=assets,generator='built-in image_gen',
              externalPixelEditsPerformed=False,gameVerified=False,
              note='Reference-guided generation and image_gen repairs; no external raster rotation, mirror, crop, resize or alpha edits. Minor generated line/placement differences are recorded rather than claiming pixel identity.')
(base/'生成清单.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(base/'逐向复核.json').write_text(json.dumps(dict(changeId=manifest['changeId'],assets=review,gameVerified=False),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Assembled 20 image provenance records and 20 explicit visual reviews; game not verified.')
