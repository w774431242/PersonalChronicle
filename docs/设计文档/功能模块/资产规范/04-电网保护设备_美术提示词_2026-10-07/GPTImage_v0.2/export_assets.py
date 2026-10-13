"""Technical export only: uniform downsampling, transparent padding, inspection and contact sheets.
No object rotation, mirroring, nonuniform stretching, repainting or alpha fabrication.
Run: python -X utf8 export_assets.py
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops
import hashlib, json, math, zipfile
ROOT = Path(__file__).resolve().parent
manifest = json.loads((ROOT / "generation_manifest.json").read_text(encoding="utf-8"))
records = {a["asset"]: a for a in manifest["assets"]}
dirs = ("north", "east", "south", "west")
expected = {f"{stem}_{d}.png" for stem in ("PC_PowerCircuitBreaker", "PC_PowerIsolator", "PC_PowerIsolator_Off") for d in dirs}
assert set(records) == expected
originals = {name: Image.open(ROOT / rec["selected_source"]).convert("RGBA") for name, rec in records.items()}
def solid_bbox(im):
    box = im.getchannel("A").point(lambda a: 255 if a > 128 else 0).getbbox()
    assert box is not None
    return box
# Closed/open pairs use ONE common transform so state switching cannot be recentered by export.
groups = [[f"PC_PowerCircuitBreaker_{d}.png"] for d in dirs] + [[f"PC_PowerIsolator_{d}.png", f"PC_PowerIsolator_Off_{d}.png"] for d in dirs]
qa = []
for group in groups:
    boxes = [solid_bbox(originals[n]) for n in group]
    box = (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))
    # Scale full source canvas, retaining original alpha; never crop or synthesize transparency.
    scale = 256 / max(box[2]-box[0], box[3]-box[1])
    cx, cy = (box[0]+box[2])/2, (box[1]+box[3])/2
    for name in group:
        src = originals[name]
        assert src.width == src.height and src.width >= 384
        w, h = round(src.width*scale), round(src.height*scale)
        reduced = src.resize((w, h), Image.Resampling.LANCZOS)
        master = Image.new("RGBA", (384,384))
        master.paste(reduced, (round(192-cx*scale), round(192-cy*scale)))
        game = master.resize((192,192), Image.Resampling.LANCZOS)
        for size, im, folder in [(384,master,ROOT/"母图384"),(192,game,ROOT/"游戏尺寸192"/"Textures"/"Things"/"Building")]:
            folder.mkdir(parents=True,exist_ok=True)
            dest=folder/name
            im.save(dest)
            check=Image.open(dest)
            assert check.format=="PNG" and check.mode=="RGBA" and check.size==(size,size)
            a=check.getchannel("A")
            corners=[a.getpixel(p) for p in [(0,0),(size-1,0),(0,size-1),(size-1,size-1)]]
            assert corners==[0,0,0,0], (name,corners)
            qa.append({"file":str(dest.relative_to(ROOT)).replace("\\","/"),"size":[size,size],"mode":check.mode,"corner_alpha":corners,"alpha_min_max":list(a.getextrema()),"solid_bbox":list(solid_bbox(check)),"source":records[name]["selected_source"],"sha256":hashlib.sha256(dest.read_bytes()).hexdigest(),"uniform_scale_from_source":scale*(size/384),"pair_transform_group":group})
# A measurable static-frame comparison, diagnostic only; no arbitrary pass threshold.
pair_stats=[]
for d in dirs:
    a=Image.open(ROOT/"游戏尺寸192"/"Textures"/"Things"/"Building"/f"PC_PowerIsolator_{d}.png")
    b=Image.open(ROOT/"游戏尺寸192"/"Textures"/"Things"/"Building"/f"PC_PowerIsolator_Off_{d}.png")
    ba=solid_bbox(a); bb=solid_bbox(b)
    samples=[]
    for y in range(192):
        for x in range(192):
            # Corner frame/border zones exclude moving central mechanisms.
            u=(x-ba[0])/max(1,ba[2]-ba[0]);v=(y-ba[1])/max(1,ba[3]-ba[1])
            if (u<.19 or u>.81) and (v<.19 or v>.81):
                pa=a.getpixel((x,y));pb=b.getpixel((x,y))
                if pa[3]>240 and pb[3]>240:samples.append(sum(abs(pa[i]-pb[i]) for i in range(3))/3)
    pair_stats.append({"direction":d,"static_corner_rgb_mean_absolute_difference_0_255":round(sum(samples)/len(samples),3) if samples else None,"static_corner_sample_pixels":len(samples),"exact_pixel_identity":ImageChops.difference(a,b).getbbox() is None,"interpretation":"Diagnostic only. Model edits are not guaranteed pixel-identical outside the mechanism."})
report={"date":"2026-10-07","delivery_status":"GENERATED_CANDIDATES_GAMEPLAY_PENDING","assets":12,"exports":24,"checks":{"exact_expected_names":True,"ascii_png_names":all(n.isascii() for n in expected),"no_circuit_breaker_off":not any("CircuitBreaker_Off" in n for n in expected),"rgba_and_dimensions":True,"transparent_corners":True,"user_approved_north_masters":True,"four_direction_part_counts":"VISUALLY_REVIEWED","off_disconnection_gap":"VISUALLY_REVIEWED","off_static_pixels_identical":"NOT_GUARANTEED","in_game_placement_and_graphic_switch":"NOT_RUN","defs_imported":"NO"},"pair_diagnostics":pair_stats,"files":qa}
(ROOT/"验收数据.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
fontpath=Path("C:/Windows/Fonts/msyh.ttc")
font=ImageFont.truetype(str(fontpath),18)
small=ImageFont.truetype(str(fontpath),14)
for bg,label in [((225,225,219),"浅底"),((46,48,50),"深底")]:
    sheet=Image.new("RGB",(1000,850),bg)
    draw=ImageDraw.Draw(sheet)
    fg=(30,30,30) if label=="浅底" else (230,230,230)
    draw.text((24,14),"电网保护设备 · GPT Image · 192×192 实际尺寸预览",font=font,fill=fg)
    for row,(stem,title) in enumerate([("PC_PowerCircuitBreaker","断路器 · 正常"),("PC_PowerIsolator","隔离开关 · 闭合"),("PC_PowerIsolator_Off","隔离开关 · 断开")]):
        for col,d in enumerate(dirs):
            x=20+col*245;y=56+row*258
            draw.rectangle((x,y,x+236,y+246),outline=(145,145,140),width=1)
            draw.text((x+12,y+10),title+" / "+d,font=small,fill=fg)
            sprite=Image.open(ROOT/"游戏尺寸192"/"Textures"/"Things"/"Building"/f"{stem}_{d}.png")
            sheet.paste(sprite,(x+22,y+43),sprite)
    sheet.save(ROOT/f"资产预览_{label}.png")
# Copyable combined prompts; actual submitted prompts remain individually available.
parts=[]
for stem in ("PC_PowerCircuitBreaker","PC_PowerIsolator","PC_PowerIsolator_Off"):
    for d in dirs:
        name=f"{stem}_{d}"
        parts.append(name+"\n"+(ROOT/"提示词"/f"{name}.txt").read_text(encoding="utf-8"))
(ROOT/"全部生成提示词.txt").write_text("\n\n".join(parts),encoding="utf-8")
print(json.dumps({"assets":12,"exports":24,"pair_diagnostics":pair_stats},ensure_ascii=False,indent=2))

