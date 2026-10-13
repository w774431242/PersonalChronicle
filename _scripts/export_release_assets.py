"""Export the generated cover for distribution and render the code-defined UI icon."""
from pathlib import Path
import shutil
import sys
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent
source = Path(sys.argv[1]).resolve()
design = root / 'docs' / '开发参考' / '发布素材' / '0.1'
design.mkdir(parents=True, exist_ok=True)
shutil.copy2(source, design / '封面原图.png')
# Palette encoding keeps the title, layout and dimensions unchanged. The full
# generated artwork remains in the development area, outside the release.
with Image.open(source) as cover:
    preview = cover.convert('RGB').quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    preview.save(root / 'About' / 'Preview.png', optimize=True)
if (root / 'About' / 'Preview.png').stat().st_size >= 1_000_000:
    raise RuntimeError('Preview exceeds the conservative 1 MB export budget')

# A repo-native geometric dashboard glyph, independent of the old archive icon.
svg = '''<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">
<rect x="10" y="10" width="108" height="108" rx="20" fill="#102536" stroke="#7ed9e9" stroke-width="5"/>
<path d="M26 39H102" stroke="#7ed9e9" stroke-width="4"/>
<rect x="25" y="24" width="24" height="6" rx="3" fill="#edbd65"/>
<rect x="26" y="52" width="29" height="20" rx="4" fill="#7ed9e9"/>
<rect x="72" y="52" width="29" height="20" rx="4" fill="#edbd65"/>
<rect x="26" y="86" width="29" height="20" rx="4" fill="#edbd65"/>
<rect x="72" y="86" width="29" height="20" rx="4" fill="#7ed9e9"/>
<path d="M55 62H72M40 72V86M86 72V86M55 96H72" stroke="#7ed9e9" stroke-width="4"/>
</svg>'''
(design / '管理系统图标.svg').write_text(svg, encoding='utf-8')
scale = 4
icon = Image.new('RGBA', (128 * scale, 128 * scale), (0, 0, 0, 0))
draw = ImageDraw.Draw(icon)
def rect(box, fill, radius, outline=None, width=0):
    draw.rounded_rectangle(tuple(int(v * scale) for v in box), radius=radius * scale,
                           fill=fill, outline=outline, width=width * scale)
rect((10, 10, 118, 118), '#102536', 20, '#7ed9e9', 5)
draw.line((26*scale, 39*scale, 102*scale, 39*scale), fill='#7ed9e9', width=4*scale)
rect((25, 24, 49, 30), '#edbd65', 3)
for box, color in [((26,52,55,72),'#7ed9e9'), ((72,52,101,72),'#edbd65'),
                   ((26,86,55,106),'#edbd65'), ((72,86,101,106),'#7ed9e9')]:
    rect(box, color, 4)
for points in [(55,62,72,62), (40,72,40,86), (86,72,86,86), (55,96,72,96)]:
    draw.line(tuple(v*scale for v in points), fill='#7ed9e9', width=4*scale)
destination = root / 'Textures' / 'UI' / 'ManagementIcon.png'
icon.resize((128, 128), Image.Resampling.LANCZOS).save(destination, optimize=True)
print(f'PREVIEW_BYTES={(root / "About" / "Preview.png").stat().st_size}')
print(f'ICON_BYTES={destination.stat().st_size}')
