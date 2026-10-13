"""CHG-20261011-LABOR-VISUAL：生成实体工作合同纸贴图（程序化绘制，无第三方素材，无烧录文字）。

产出：Textures/Things/Item/PawnEconomy/PC_PEA_WorkContract.png
规格：128x128、RGBA、透明背景（ASSET 规范：物品 128x128）。
用途：ThingDef.PC_PEA_WorkContractDocument 的 graphicData.texPath / uiIconPath。
许可：本仓自绘（无外部素材），与工程其余程序化贴图同源。
"""
import os

from PIL import Image, ImageDraw

SIZE = 128
OUT = r"E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Textures\Things\Item\PawnEconomy\PC_PEA_WorkContract.png"

PAPER = (243, 238, 222, 255)
PAPER_SHADE = (226, 218, 198, 255)
EDGE = (140, 122, 92, 255)
INK = (108, 112, 124, 255)
SEAL = (172, 58, 52, 235)

img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# 纸张主体（略偏冷白，右/下留出纸张厚度）
left, top, right, bottom = 22, 12, 106, 116
d.rectangle([left, top, right, bottom], fill=PAPER, outline=EDGE, width=2)
# 纸张厚度（右侧与底部阴影带）
d.rectangle([right, top + 3, right + 3, bottom + 3], fill=PAPER_SHADE)
d.rectangle([left + 3, bottom, right + 3, bottom + 3], fill=PAPER_SHADE)
# 折角
d.polygon([(right - 18, top), (right, top), (right, top + 18)], fill=PAPER_SHADE, outline=EDGE)

# 正文行（抽象线条，不烧录文字）
y = top + 26
for i in range(6):
    width = 66 if i % 3 else 46
    d.rectangle([left + 10, y, left + 10 + width, y + 4], fill=INK)
    y += 12

# 标题行（较粗）
d.rectangle([left + 10, top + 10, left + 58, top + 17], fill=INK)

# 印章（红色圆环 + 内圈）
cx, cy, r = right - 20, bottom - 20, 13
d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=SEAL, width=3)
d.ellipse([cx - r + 5, cy - r + 5, cx + r - 5, cy + r - 5], outline=SEAL, width=2)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("wrote", OUT, img.size, img.mode)
