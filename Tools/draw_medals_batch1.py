#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PersonalChronicle 勋章算法绘制 · 批次1（劳动系 9 枚）
绘制规范见 docs/参考清单/勋章AI生图规范.md：
  - 透明背景 RGBA
  - 正面正交、居中、对称
  - 金属浮雕 + 绶带 + 中文立体标题
  - 金/银/铜三档仅改材质色
输出：generated-images/medals/batch1/*.png + contact_sheet.png
"""
import math
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SIZE = 512
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "generated-images", "medals", "batch1")
os.makedirs(OUT, exist_ok=True)
FONT = os.path.join(ROOT, "Fonts", "ZCOOLKuaiLe-Regular.ttf")

TIERS = {
    "bronze": {"base": (184, 115, 60), "light": (228, 174, 112), "dark": (108, 60, 26)},
    "silver": {"base": (198, 204, 212), "light": (248, 250, 253), "dark": (118, 126, 138)},
    "gold":   {"base": (212, 175, 55), "light": (255, 230, 152), "dark": (150, 110, 22)},
}
RIBBON = {
    "Labor_Model": (196, 30, 32),
    "Labor_Worker": (214, 140, 40),
    "Labor_TechAce": (38, 92, 180),
}
SERIES_TITLE = {
    "Labor_Model": "劳动模范",
    "Labor_Worker": "先进工作者",
    "Labor_TechAce": "生产技术能手",
}


def grad_layer(cx, cy, r, color, max_alpha, blur, steps=40):
    layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(steps, 0, -1):
        rr = r * i / steps
        a = int(max_alpha * (1 - i / steps))
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=color + (a,))
    return layer.filter(ImageFilter.GaussianBlur(blur))


def metal_disc(cx, cy, R, tier):
    base, light, dark = tier["base"], tier["light"], tier["dark"]
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([cx - R, cy - R, cx + R, cy + R], fill=base + (255,))
    sh = grad_layer(cx + R * 0.25, cy + R * 0.28, R * 0.96, (0, 0, 0), 90, 18)
    img = Image.alpha_composite(img, sh)
    hi = grad_layer(cx - R * 0.28, cy - R * 0.30, R * 0.82, (255, 255, 255), 70, 14)
    img = Image.alpha_composite(img, hi)
    d = ImageDraw.Draw(img)
    # bevel rim stays inside the disc radius R
    d.ellipse([cx - R + 6, cy - R + 6, cx + R - 6, cy + R - 6], outline=dark + (255,), width=12)
    d.ellipse([cx - R + 14, cy - R + 14, cx + R - 14, cy + R - 14], outline=light + (210,), width=4)
    ir = R - 26
    d.ellipse([cx - ir, cy - ir, cx + ir, cy + ir], outline=dark + (235,), width=5)
    # clip everything to the medal circle so no halo leaks outside
    mask = Image.new("L", (SIZE, SIZE), 0)
    dm = ImageDraw.Draw(mask)
    dm.ellipse([cx - R, cy - R, cx + R, cy + R], fill=255)
    transparent = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    img = Image.composite(img, transparent, mask)
    return img


def draw_ribbon(canvas, cx, top_y, color, metal):
    d = ImageDraw.Draw(canvas)
    length = 150
    gold = metal["light"]
    for s in (-1, 1):
        top_x = cx + s * 20
        bot_x = cx + s * 74
        poly = [(top_x - 15, top_y), (top_x + 15, top_y),
                (bot_x + 24, top_y + length), (bot_x - 24, top_y + length)]
        d.polygon(poly, fill=color + (255,))
        # gold edge
        d.line([poly[0], poly[3]], fill=gold + (230,), width=4)
        d.line([poly[1], poly[2]], fill=gold + (230,), width=4)
    # suspension bar
    d.rounded_rectangle([cx - 30, top_y - 16, cx + 30, top_y + 8], radius=5,
                        fill=metal["base"] + (255,), outline=metal["dark"] + (255,), width=4)


def gear(d, cx, cy, r, teeth, color, light):
    # highlight copy
    d.ellipse([cx - r - 2, cy - r - 2, cx + r - 2, cy + r - 2], fill=light + (210,))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color + (255,))
    for k in range(teeth):
        ang = math.pi * 2 * k / teeth
        tx = cx + math.cos(ang) * r
        ty = cy + math.sin(ang) * r
        d.rectangle([tx - 5, ty - 5, tx + 5, ty + 5], fill=color + (255,))
    d.ellipse([cx - r * 0.45, cy - r * 0.45, cx + r * 0.45, cy + r * 0.45], fill=light + (180,))


def emboss_text(d, text, cx, cy, font, base, hi, sh):
    bb = d.textbbox((0, 0), text, font=font)
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    x = cx - w / 2 - bb[0]
    y = cy - h / 2 - bb[1]
    d.text((x + 3, y + 3), text, font=font, fill=sh + (235,))
    d.text((x - 2, y - 2), text, font=font, fill=hi + (235,))
    d.text((x, y), text, font=font, fill=base + (255,))


def draw_emblem(d, series, cx, cy, color, light):
    if series == "Labor_Model":
        # 工人剪影 + 双侧麦穗 + 上方齿轮
        gear(d, cx + 36, cy - 36, 18, 10, color, light)
        d.polygon([(cx - 34, cy + 34), (cx + 34, cy + 34),
                   (cx + 22, cy - 4), (cx - 22, cy - 4)], fill=color + (255,))   # shoulders
        d.ellipse([cx - 18, cy - 46, cx + 18, cy - 10], fill=color + (255,))     # head
        d.ellipse([cx - 12, cy - 42, cx + 2, cy - 30], fill=light + (110,))      # relief highlight
        for s in (-1, 1):
            bx = cx + s * 54
            d.line([bx, cy + 30, bx, cy - 34], fill=color + (255,), width=4)
            for j in range(4):
                yy = cy - 32 + j * 15
                d.ellipse([bx - 6, yy, bx + 6, yy + 12], fill=color + (255,))    # grains
    elif series == "Labor_Worker":
        # 三层工件堆叠 + 扳手 + 齿轮
        gear(d, cx + 38, cy - 32, 20, 10, color, light)
        for j in range(3):
            yy = cy - 26 + j * 22
            d.rounded_rectangle([cx - 48, yy, cx + 20, yy + 16], radius=4, fill=color + (255,))
            d.line([cx - 44, yy + 3, cx + 16, yy + 3], fill=light + (90,), width=2)
        d.ellipse([cx - 12, cy - 28, cx + 12, cy - 4], fill=color + (255,))      # wrench jaw
        d.ellipse([cx - 4, cy - 22, cx + 4, cy - 12], fill=light + (160,))       # jaw opening
        d.rounded_rectangle([cx - 6, cy - 12, cx + 6, cy + 34], radius=3, fill=color + (255,))
    elif series == "Labor_TechAce":
        # 仪表盘 + 指针 + 小齿轮 + 电路短线
        d.ellipse([cx - 38, cy - 42, cx + 40, cy + 36], fill=color + (255,))
        d.ellipse([cx - 30, cy - 34, cx + 32, cy + 28], outline=light + (150,), width=3)
        for k in range(5):
            ang = -math.pi / 2 + (k - 2) * (math.pi / 6)
            x1 = cx + 1 + math.cos(ang) * 25
            y1 = cy - 3 + math.sin(ang) * 25
            x2 = cx + 1 + math.cos(ang) * 31
            y2 = cy - 3 + math.sin(ang) * 31
            d.line([x1, y1, x2, y2], fill=light + (210,), width=3)
        d.line([cx + 1, cy - 3, cx + 25, cy - 31], fill=light + (255,), width=5)  # needle
        gear(d, cx - 50, cy + 24, 15, 8, color, light)
        for k in range(3):
            yy = cy + 6 + k * 9
            d.line([cx + 46, yy, cx + 66, yy], fill=light + (190,), width=3)
            d.ellipse([cx + 64, yy - 3, cx + 71, yy + 4], fill=light + (190,))


def draw_medal(series, tier_name):
    tier = TIERS[tier_name]
    color, light, dark = tier["base"], tier["light"], tier["dark"]
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    cx, cy, R = 256, 322, 150
    draw_ribbon(canvas, cx, 78, RIBBON[series], tier)
    disc = metal_disc(cx, cy, R, tier)
    canvas = Image.alpha_composite(canvas, disc)
    d = ImageDraw.Draw(canvas)
    emboss_color = dark
    draw_emblem(d, series, cx, cy, emboss_color, light)
    title = SERIES_TITLE[series]
    fsize = min(44, int(190 / max(1, len(title))))
    font = ImageFont.truetype(FONT, fsize)
    emboss_text(d, title, cx, cy + R - 58, font, dark, light, (0, 0, 0))
    return canvas


def main():
    cells = []
    for series in ("Labor_Model", "Labor_Worker", "Labor_TechAce"):
        for tier in ("bronze", "silver", "gold"):
            key = f"{series}_{tier}"
            img = draw_medal(series, tier)
            img.save(os.path.join(OUT, f"Medal_{key}.png"))
            cells.append((key, img))
            print("saved", key)
    # contact sheet 3x3
    cols = 3
    rows = 3
    pad = 12
    cs = Image.new("RGBA", (cols * SIZE + (cols + 1) * pad, rows * SIZE + (rows + 1) * pad), (0, 0, 0, 0))
    order = ["Labor_Model", "Labor_Worker", "Labor_TechAce"]
    idx = 0
    for r in range(rows):
        for c in range(cols):
            series = order[r]
            tier = ("bronze", "silver", "gold")[c]
            key = f"{series}_{tier}"
            im = dict((k, v) for k, v in cells)[key]
            x = pad + c * (SIZE + pad)
            y = pad + r * (SIZE + pad)
            cs.alpha_composite(im, (x, y))
    cs.save(os.path.join(OUT, "contact_sheet.png"))
    print("saved contact_sheet.png")


if __name__ == "__main__":
    main()
