"""Deterministic, reversible Smelter texture trial; never writes runtime assets."""
from pathlib import Path
from datetime import datetime
import json
import hashlib
import shutil
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/设计文档/功能模块/资产规范/02-T1生产设备/智能冶炼台_试修_2026-10-05'
SOURCE = Path('D:/素材')
DIRS = [('北','north'),('东','east'),('南','south'),('西','west')]

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def png_checks(p):
    im = Image.open(p)
    a = im.getchannel('A')
    return {'mode':im.mode, 'size':list(im.size), 'alpha_extrema':list(a.getextrema()),
            'transparent_corners': all(a.getpixel(c)==0 for c in [(0,0),(im.width-1,0),(0,im.height-1),(im.width-1,im.height-1)]),
            'visible_bbox':list(a.point(lambda v:255 if v>16 else 0).getbbox()), 'sha256':sha(p)}

def make_sheet(before, after, rotated=False):
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 23)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 19)
    sheet = Image.new('RGB', (1280,1200), '#242a31')
    d = ImageDraw.Draw(sheet)
    d.text((20,10),'智能冶炼台｜试修前后对照' + ('（统一转回北向）' if rotated else '（实际四向）'),font=font,fill='white')
    d.text((20,47),'左：原图　右：试修版；北、东保持原图。透明背景仅在预览中垫灰。',font=small,fill='#b9c5d0')
    rotations = [None,Image.Transpose.ROTATE_90,Image.Transpose.ROTATE_180,Image.Transpose.ROTATE_270]
    for i, (cn,en) in enumerate(DIRS):
        y = 85+i*275
        d.text((20,y),cn+'向',font=font,fill='#77bde9')
        for j, collection in enumerate([before,after]):
            im = collection[cn].copy()
            if rotated and rotations[i] is not None: im = im.transpose(rotations[i])
            im.thumbnail((600,238),Image.Resampling.LANCZOS)
            sheet.paste(im,(j*640+(640-im.width)//2,y+29+(238-im.height)//2),im)
        d.line((15,y+270,1265,y+270),fill='#505b66')
    sheet.save(OUT / ('北向结构对照.png' if rotated else '实际四向对照.png'))

def main():
    for name in ['原图备份','修订母图','游戏尺寸']:
        (OUT/name).mkdir(parents=True,exist_ok=True)
    originals = {cn:SOURCE/('智能冶炼台'+cn+'.PNG') for cn,en in DIRS}
    hashes = {cn:sha(p) for cn,p in originals.items()}
    before = {cn:Image.open(p).convert('RGBA') for cn,p in originals.items()}
    for cn,p in originals.items():
        backup = OUT/'原图备份'/p.name
        if backup.exists() and sha(backup)!=hashes[cn]:
            raise RuntimeError('Previous backup differs; do not overwrite it')
        if not backup.exists(): shutil.copy2(p,backup)
    after = {cn:im.copy() for cn,im in before.items()}

    # South already has a rear-facing furnace hood. Reorder ends while retaining
    # its existing vertical projection; fix the complete control panel separately.
    south = before['南'].transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    south_panel = (244,290,566,563)
    south.paste(south.crop(south_panel).transpose(Image.Transpose.FLIP_TOP_BOTTOM),south_panel[:2])
    after['南'] = south

    # First stitched West trial had visible seams and was rejected on inspection.
    # Use the coherent East view for a structural West trial. This rotates baked
    # lighting/projection as well, so it MUST NOT be labeled release-ready.
    after['西'] = before['东'].transpose(Image.Transpose.ROTATE_180)

    results = {}
    for cn,en in DIRS:
        mother = OUT/'修订母图'/('智能冶炼台'+cn+'.png')
        game = OUT/'游戏尺寸'/('PC_T1_Smelter_'+en+'.png')
        if cn in ['北','东']:
            shutil.copy2(originals[cn],mother)
            shutil.copy2(ROOT/'Textures/Things/Building/T1Production'/game.name,game)
        else:
            after[cn].save(mother)
            size=(448,192) if cn=='南' else (192,448)
            after[cn].convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA').save(game)
        results[en] = {'original':str(originals[cn]),'original_sha256':hashes[cn],
                       'mother':png_checks(mother),'game':png_checks(game)}
    make_sheet(before,after,False)
    make_sheet(before,after,True)
    summary = {'created_at':datetime.now().isoformat(timespec='seconds'), 'trial_only':True,
               'source_unchanged':all(sha(originals[cn])==hashes[cn] for cn,en in DIRS),
               'north_east_mothers_byte_identical':all(sha(OUT/'修订母图'/('智能冶炼台'+cn+'.png'))==hashes[cn] for cn in ['北','东']),
               'image_models_called':False,'runtime_assets_written':False,'files':results,
               'operations':{'south':'Mirror overall X, flip control-panel Y; preserve existing South furnace view',
                             'west':'Rotate coherent East source 180 degrees; structural trial only, baked lighting and projection also rotate'},
               'limitations':['South lateral highlights were mirrored and need lighting review',
                              'West direction is corrected but baked lighting/projection remain rotated; not release-ready',
                              'No in-game runtime verification']}
    (OUT/'自动检查.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(OUT)
    print(json.dumps({k:v for k,v in summary.items() if k not in ['files','operations']},ensure_ascii=False))

if __name__=='__main__': main()
