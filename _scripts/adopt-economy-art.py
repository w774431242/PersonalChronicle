"""Copy a reviewed generator output without modifying image pixels."""
from pathlib import Path
import sys,json,hashlib,shutil
root=Path(__file__).resolve().parents[1]
base=root/'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08'
kind,direction,source,prompt_path=sys.argv[1:]
name=f'PC_PEA_{kind}'
target=root/f'Textures/Things/Building/PawnEconomy/{name}_{direction}.png'
record=base/'生成记录'/f'{name}_{direction}.json'
prompt_target=base/f'{name}_{direction}.prompt.txt'
draft=base/'草稿'/'首轮'
draft.mkdir(parents=True,exist_ok=True)
for old in (target,record,prompt_target):
    dest=draft/old.name
    if old.exists() and not dest.exists(): shutil.copy2(old,dest)
target.parent.mkdir(parents=True,exist_ok=True)
record.parent.mkdir(parents=True,exist_ok=True)
shutil.copy2(source,target)
if Path(prompt_path).resolve()!=prompt_target.resolve(): shutil.copy2(prompt_path,prompt_target)
north=next(a for a in json.loads((base/'北向母图清单.json').read_text(encoding='utf-8-sig'))['assets'] if a['kind']==kind)
entry=dict(kind=kind,defName=name,direction=direction,path=target.relative_to(root).as_posix(),generatedSource=source,
           northReference=north['path'],northReferenceSha256=north['sha256'],promptFile=prompt_target.name,
           sha256=hashlib.sha256(target.read_bytes()).hexdigest(),generator='built-in image_gen',
           generationMode='north-reference generation or targeted reference edit',externalPixelEditsPerformed=False)
record.write_text(json.dumps(entry,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(entry,ensure_ascii=False))
