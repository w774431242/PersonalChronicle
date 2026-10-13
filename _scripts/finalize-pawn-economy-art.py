"""Record art-only verification evidence; never modifies PNGs or game state."""
from pathlib import Path
import json,hashlib,csv
root=Path(__file__).resolve().parents[1]
base=root/'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
images=read(base/'图像检查.json')
review=read(base/'逐向复核.json')
deployment=read(base/'部署验证.json')
checks=read(base/'校验输出.json')['checks']
assert images['checksPassed'] and len(images['files'])==20
assert len(review['assets'])==20 and all(x['result']=='PASS' for x in review['assets'])
assert all(x['exitCode']==0 for x in checks)
assert deployment['deployed'] and deployment['unchangedLogicDll'] and deployment['workshopIdentityPreserved']
assert deployment['targetFileCount']==232 and not deployment['gameVerified']
for item in images['files']:
    src=root/item['path']
    dst=Path(deployment['target'])/item['path']
    assert hashlib.sha256(src.read_bytes()).hexdigest()==item['sha256']
    assert src.read_bytes()==dst.read_bytes()
rows=list(csv.DictReader((base/'四向实机结果.csv').open(encoding='utf-8-sig',newline='')))
assert len(rows)==20 and len({(r['DefName'],r['Direction']) for r in rows})==20
assert all(r['Preview']=='未测' and r['ComponentHealth']=='未测' for r in rows)
report=dict(changeId='CHG-20261008-ECON-ART',state='DeployedPendingGameAcceptance',imageCount=20,
    nativeResolution=[1254,1254],northMasterCount=5,imageChecksPassed=True,
    generatedImageVisualReviewPassed=True,visualReviewer='assistant',gameVerified=False,
    logicalTestsRerun=False,buildRerun=False,globalGateAvailable=False,
    checks=checks,deployment='部署验证.json',manualGameRows=20,
    borderAlphaMaximum=max(x['borderMaxAlpha'] for x in images['files']),
    strictZeroBorderCount=sum(x['strictZeroAlphaBorder'] for x in images['files']),
    note='Independent north-reference redraws and image_gen repairs. Visual review permits documented minor line/placement differences, not pixel identity or guaranteed gameplay quality. Border alpha <=1/255; strict-zero status remains explicit.')
(base/'实装验证.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
paths=[root/'Defs/PawnEconomy_Institutions.xml']
paths += sorted((root/'Textures/Things/Building/PawnEconomy').glob('*.png'))
paths += sorted(base.rglob('*'))
paths += [root/'_scripts'/name for name in ('adopt-economy-art.py','adopt-economy-art-candidate.py',
    'assemble-economy-art-manifest.py','inspect-pawn-economy-art.py','verify-pawn-economy-art.ps1',
    'deploy-pawn-economy-art.ps1','finalize-pawn-economy-art.py')]
paths += [root/'docs/changelog/v1.4.x_2026-10-08_经济机构四向美术.md',
    root/'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/实施批次状态.json',
    root/'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/09-B2.4.2_实机验收清单_2026-10-08.md']
lines=['---','change_id: CHG-20261008-ECON-ART','state: DeployedPendingGameAcceptance','scope: five institutions, twenty directional PNGs, visual Def binding and art-only deployment','---','',
    '# 经济机构四向美术变更标注','',
    '每机构北向为母图，三向独立引用北向；修订目标可同时引用自身候选，未链式引用其他方向。未对原始PNG做程序旋转、镜像、缩放、裁切或透明度改写。目录草稿为开发参考，不进入游戏Mods。','',
    '全部20图结构/尺寸检查及生成图逐张复核通过；独立重绘保留微小描线/位置差异说明。12图边缘Alpha=1/255，8图严格全零，检查与诊断分开记录。原版字段/路径/非视觉Def深比较及经济Def/ThinkTree/RUIF检查通过。','',
    '已同步1个Def+20PNG，逐文件哈希与源目录一致，其余210运行文件和逻辑DLL保持，工坊ID保留。232文件、48,912,947字节（46.647MiB）；备份和源/目标哈希见部署验证。未启动游戏、未编辑存档、未上传工坊；经济能力门禁不变。全局gate缺失，游戏验收未完成，不标Released。','',
    '| 文件 | 定位 | SHA-256 |','| --- | --- | --- |']
for path in sorted(set(p for p in paths if p.is_file())):
    rel=path.relative_to(root).as_posix()
    if path.suffix.lower()=='.png':
        anchor='二进制PNG'
    else:
        content=path.read_text(encoding='utf-8-sig').splitlines()
        if path.suffix.lower()=='.xml':
            matches=[f'{i+1}: {line.strip()}' for i,line in enumerate(content) if any(k in line for k in ('Graphic_Multi','drawRotated','drawSize','uiIconPath'))]
        elif path.suffix.lower() in ('.py','.ps1'):
            matches=[f'{i+1}: {line.strip()}' for i,line in enumerate(content) if line.startswith(('def ','param(','$ErrorActionPreference','manifest =','report =','read=','$paths =','for item in'))]
        else:
            matches=[]
        anchor='<br>'.join(matches[:8]) if matches else ('1: '+(content[0].strip() if content else 'empty'))
        anchor=anchor.replace('|','\\|')
    lines.append(f'| {rel} | {anchor} | {hashlib.sha256(path.read_bytes()).hexdigest().upper()} |')
# 变更标注落点 = docs/设计文档/架构/变更标注/（DOC-001；2026-10-09 由根目录迁入，勿写回仓库根）
annot_dir=root/'docs'/'设计文档'/'架构'/'变更标注'
annot_dir.mkdir(parents=True,exist_ok=True)
(annot_dir/'变更标注_CHG-20261008-ECON-ART.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('PASS: final art verification, 20 untested game rows, deployed hashes and change annotation recorded; game acceptance remains pending.')
