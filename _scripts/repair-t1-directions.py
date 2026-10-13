"""修订获授权的 T1 四向 PNG，保留素材原件；不使用图片模型。"""
from pathlib import Path
from datetime import datetime
import json
import hashlib
import shutil
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/设计文档/功能模块/资产规范/02-T1生产设备/T1生产设备_四向修订_2026-10-05'
APPROVED = ROOT / 'docs/设计文档/功能模块/资产规范/02-T1生产设备/智能冶炼台_试修_2026-10-05'
DIRS = [('北','north'),('东','east'),('南','south'),('西','west')]
TRANSPOSE = {90:Image.Transpose.ROTATE_270,180:Image.Transpose.ROTATE_180,270:Image.Transpose.ROTATE_90}
PLAN = {
    'PC_T1_Machining': {},
    'PC_T1_Tailoring': {'南':('北',180)},
    'PC_T1_Smithy': {'南':('北',180)},
    'PC_T1_Cooking': {'南':('北',180),'西':('东',180)},
    'PC_T1_DrugLab': {'东':('北',90),'南':('北',180),'西':('北',270)},
    'PC_T1_Stonecutting': {'东':('北',90),'南':('北',180),'西':('北',270)},
    'PC_T1_Sculpting': {'东':('北',90),'南':('北',180),'西':('北',270)},
    'PC_T1_Butchery': {'东':('北',90),'南':('北',180),'西':('北',270)},
    'PC_T1_Crafting': {'东':('北',90),'南':('北',180),'西':('北',270)},
}

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def properties(p):
    im=Image.open(p)
    alpha=im.getchannel('A')
    return {'size':list(im.size),'mode':im.mode,'alpha_extrema':list(alpha.getextrema()),
            'transparent_corners':all(alpha.getpixel(c)==0 for c in [(0,0),(im.width-1,0),(0,im.height-1),(im.width-1,im.height-1)]),
            'visible_bbox':list(alpha.point(lambda v:255 if v>16 else 0).getbbox()),'bytes':p.stat().st_size,'sha256':sha(p)}

def comparison(label,before,after,rotated=False):
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',27)
    small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',21)
    sheet=Image.new('RGB',(1280,1840),'#242a31'); draw=ImageDraw.Draw(sheet)
    draw.text((20,12),label+'｜'+('北向结构复核' if rotated else '实际四向前后对照'),font=font,fill='white')
    draw.text((20,55),'左：原素材　右：修订图。灰色仅为预览底色，PNG 保留透明背景。',font=small,fill='#b8c3cf')
    reverse=[None,Image.Transpose.ROTATE_90,Image.Transpose.ROTATE_180,Image.Transpose.ROTATE_270]
    for i,(cn,en) in enumerate(DIRS):
        y=100+i*430
        draw.text((20,y),cn+'向',font=font,fill='#7bbfec')
        for col,collection in enumerate([before,after]):
            im=collection[cn].copy()
            if rotated and reverse[i] is not None: im=im.transpose(reverse[i])
            im.thumbnail((600,365),Image.Resampling.LANCZOS)
            sheet.paste(im,(col*640+(640-im.width)//2,y+39+(365-im.height)//2),im)
        draw.line((15,y+420,1265,y+420),fill='#55616c',width=2)
    directory=OUT/('北向结构复核' if rotated else '实际四向对照')
    sheet.save(directory/(label+'.png'))

def overview(groups,after):
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
    small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
    for page in [0,1]:
        sheet=Image.new('RGB',(1600,1850),'#242a31'); draw=ImageDraw.Draw(sheet)
        draw.text((20,12),'T1 生产设备｜修订后的实际四向总览 '+str(page+1)+'/2',font=font,fill='white')
        for col,(cn,en) in enumerate(DIRS): draw.text((col*400+175,52),cn+'向',font=small,fill='#7bbfec')
        for row,g in enumerate(groups[page*5:page*5+5]):
            label=g['label']; y=85+row*350
            draw.text((18,y),label,font=font,fill='white')
            for col,(cn,en) in enumerate(DIRS):
                im=after[label][cn].copy(); im.thumbnail((375,300),Image.Resampling.LANCZOS)
                sheet.paste(im,(col*400+(400-im.width)//2,y+38+(300-im.height)//2),im)
            draw.line((12,y+346,1588,y+346),fill='#53606c')
        sheet.save(OUT/('实际四向总览_'+str(page+1)+'.png'))

def main():
    manifest=json.loads((ROOT/'_verify_tmp/T1-assets-latest.json').read_text(encoding='utf-8-sig'))
    # A later import points the live manifest at these outputs. Repeat from the
    # preserved input manifest instead of copying a file over itself.
    if manifest.get('processing')=='directional_revision' and manifest.get('revision_manifest')==str(OUT/'修订清单.json'):
        manifest=json.loads(Path(manifest['previous_manifest']).read_text(encoding='utf-8-sig'))
    groups=[]
    for i in range(0,len(manifest['assets']),4):
        assets=manifest['assets'][i:i+4]
        label=Path(assets[0]['source']).stem[:-1]
        identity=assets[0]['target'].removesuffix('_north.png')
        groups.append({'label':label,'identity':identity,'assets':assets})
    # Validate ALL source files before producing any assets.
    for a in manifest['assets']:
        if sha(Path(a['source']))!=a['source_sha256']: raise ValueError('原素材变化，需重新审查：'+a['source'])
    for folder in ['修订母图','游戏尺寸','实际四向对照','北向结构复核']:
        (OUT/folder).mkdir(parents=True,exist_ok=True)
    records=[]; all_after={}; changed=0
    for group in groups:
        label=group['label']; identity=group['identity']
        before={cn:Image.open(a['source']).convert('RGBA') for (cn,en),a in zip(DIRS,group['assets'])}
        after={}
        for (cn,en),a in zip(DIRS,group['assets']):
            mother=OUT/'修订母图'/(label+cn+'.png')
            game=OUT/'游戏尺寸'/a['target']
            source=Path(a['source'])
            if identity=='PC_T1_Smelter':
                shutil.copy2(APPROVED/'修订母图'/mother.name,mother)
                shutil.copy2(APPROVED/'游戏尺寸'/game.name,game)
                operation='保留原图' if cn in ['北','东'] else '沿用用户认可的冶炼台试修版'
                donor=cn; angle=None
            elif cn in PLAN[identity]:
                donor,angle=PLAN[identity][cn]
                im=before[donor].transpose(TRANSPOSE[angle])
                im.save(mother)
                size=(448,192) if en in ['north','south'] else (192,448)
                im.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA').save(game)
                operation=donor+'向完整机体顺时针转向 '+str(angle)+'°'
            else:
                shutil.copy2(source,mother)
                original_game=Path(manifest['prepared_directory'])/a['target']
                if sha(original_game)!=a['target_sha256']: raise ValueError('原导入备份不一致')
                shutil.copy2(original_game,game)
                operation='保留原图'; donor=cn; angle=0
            after[cn]=Image.open(mother).convert('RGBA')
            props=properties(game)
            expected=[448,192] if cn in ['北','南'] else [192,448]
            if props['mode']!='RGBA' or props['size']!=expected or props['alpha_extrema']!=[0,255] or not props['transparent_corners']:
                raise ValueError('游戏 PNG 校验失败：'+game.name)
            is_changed=props['sha256']!=a['target_sha256']
            changed+=int(is_changed)
            records.append({'device':label,'identity':identity,'direction':en,'direction_chinese':cn,
                            'original':a['source'],'original_sha256':a['source_sha256'],
                            'operation':operation,'donor_direction':donor,'clockwise_angle':angle,
                            'changed':is_changed,'mother':properties(mother),'game':props})
        comparison(label,before,after,False)
        comparison(label,before,after,True)
        all_after[label]=after
    overview(groups,all_after)
    summary={'created_at':datetime.now().isoformat(timespec='seconds'),'devices_reviewed':10,
             'remaining_devices_reviewed':9,'remaining_devices_modified':8,'remaining_directions_modified':19,
             'game_files':40,'game_files_modified':changed,'game_files_unchanged':40-changed,
             'north_baselines_unchanged':all(not r['changed'] for r in records if r['direction']=='north'),
             'source_files_unchanged':all(sha(Path(a['source']))==a['source_sha256'] for a in manifest['assets']),
             'original_archive':manifest['original_archive'],'image_models_called':False,
             'visual_review_completed':False,'runtime_gameplay_verified':False,'deployed_to_mods':False,
             'lighting_note':'沿用用户认可的结构转向方式；像素旋转会同时旋转原图光照和投影，未独立重绘光照。',
             'files':records}
    (OUT/'修订清单.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(OUT)
    print(json.dumps({k:v for k,v in summary.items() if k!='files'},ensure_ascii=False))

if __name__=='__main__': main()
