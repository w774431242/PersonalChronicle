from pathlib import Path
import json,subprocess,sys
root=Path(__file__).resolve().parents[1]
kind,direction=sys.argv[1:3]
prefix=sys.argv[3] if len(sys.argv)>3 else 'candidate'
candidate=root/'_verify_tmp'/f'{prefix}_{kind}_{direction}.json'
data=json.loads(candidate.read_text(encoding='utf-8-sig'))
prompt=candidate.with_suffix('.prompt.txt')
prompt.write_text(data['prompt']+'\n',encoding='utf-8')
subprocess.run([sys.executable,str(root/'_scripts/adopt-economy-art.py'),kind,direction,data['generatedSource'],str(prompt)],check=True,cwd=root)
record=root/'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08/生成记录'/f'PC_PEA_{kind}_{direction}.json'
entry=json.loads(record.read_text(encoding='utf-8'))
for key in ('inputReferences','repairTargetSha256','priorPrompt'):
    if key in data: entry[key]=data[key]
record.write_text(json.dumps(entry,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
