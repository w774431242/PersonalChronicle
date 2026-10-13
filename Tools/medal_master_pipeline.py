#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
勋章母图管线：白底母图 -> 透明 RGBA -> HSV 材质派生(银/金) -> 512 母版
用法: python medal_master_pipeline.py <src.png> <out_prefix>
- 抠底: 从四边泛洪 near-white 连通域 -> alpha 0, MinFilter 收边去白边
- 派生: 仅替换金属色域(H 9~50, S>=40)的 H/S, 保留 V(浮雕明度) => 结构同源
  红色绶带(H~0/255)不在色域内, 三档均保持红绶带
"""
import sys
import os
from collections import deque
from PIL import Image, ImageChops, ImageFilter

SIZE = 512
NEAR_WHITE = 232          # min(r,g,b) >= 该值视为背景候选
H_LO, H_HI, S_MIN = 9, 50, 40   # 金属色域 (PIL HSV 0-255)
GOLD = dict(h=32, s=190, v_gain=1.00)    # ~45°
SILVER = dict(h=150, s=20, v_gain=1.12)  # 低饱和 + 提亮


def key_out_background(src):
    img = Image.open(src).convert("RGB")
    w, h = img.size
    px = img.load()
    visited = bytearray(w * h)
    q = deque()

    def is_bg(x, y):
        r, g, b = px[x, y]
        return r >= NEAR_WHITE and g >= NEAR_WHITE and b >= NEAR_WHITE

    for x in range(w):
        for y in (0, h - 1):
            if is_bg(x, y) and not visited[y * w + x]:
                visited[y * w + x] = 1
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if is_bg(x, y) and not visited[y * w + x]:
                visited[y * w + x] = 1
                q.append((x, y))
    while q:
        x, y = q.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h:
                i = ny * w + nx
                if not visited[i] and is_bg(nx, ny):
                    visited[i] = 1
                    q.append((nx, ny))

    # 第二遍：封闭的近纯白大块区域(如绶带环内空洞)也视为背景
    PURE = 248
    MIN_AREA = 2500
    seen2 = bytearray(w * h)
    for start in range(w * h):
        if visited[start] or seen2[start]:
            continue
        x0, y0 = start % w, start // w
        r, g, b = px[x0, y0]
        if not (r >= PURE and g >= PURE and b >= PURE):
            continue
        comp = []
        q2 = deque([(x0, y0)])
        seen2[start] = 1
        while q2:
            x, y = q2.popleft()
            comp.append((x, y))
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < w and 0 <= ny < h:
                    i = ny * w + nx
                    if not visited[i] and not seen2[i]:
                        rr, gg, bb = px[nx, ny]
                        if rr >= PURE and gg >= PURE and bb >= PURE:
                            seen2[i] = 1
                            q2.append((nx, ny))
        if len(comp) >= MIN_AREA:
            for x, y in comp:
                visited[y * w + x] = 1

    alpha = Image.new("L", (w, h), 255)
    ap = alpha.load()
    for y in range(h):
        base = y * w
        for x in range(w):
            if visited[base + x]:
                ap[x, y] = 0
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    out = img.convert("RGBA")
    out.putalpha(alpha)
    return out


def retint(rgba, spec):
    rgb = rgba.convert("RGB")
    hsv = rgb.convert("HSV")
    H, S, V = hsv.split()
    hmask = H.point(lambda x: 255 if H_LO <= x <= H_HI else 0)
    smask = S.point(lambda x: 255 if x >= S_MIN else 0)
    sel = ImageChops.multiply(hmask, smask).filter(ImageFilter.GaussianBlur(1))
    newH = Image.new("L", H.size, spec["h"])
    newS = Image.new("L", S.size, spec["s"])
    newV = V.point(lambda x: min(255, int(x * spec["v_gain"])))
    new_rgb = Image.merge("HSV", (newH, newS, newV)).convert("RGB")
    out_rgb = Image.composite(new_rgb, rgb, sel)
    out = out_rgb.convert("RGBA")
    out.putalpha(rgba.split()[3])
    return out


def crop_square(rgba, size=SIZE):
    bbox = rgba.split()[3].getbbox()
    if not bbox:
        return rgba.resize((size, size), Image.LANCZOS)
    l, t, r, b = bbox
    m = int(max(r - l, b - t) * 0.04)
    l, t = max(0, l - m), max(0, t - m)
    r, b = min(rgba.width, r + m), min(rgba.height, b + m)
    im = rgba.crop((l, t, r, b))
    side = max(im.size)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.alpha_composite(im, ((side - im.width) // 2, (side - im.height) // 2))
    return canvas.resize((size, size), Image.LANCZOS)


def main():
    src, prefix = sys.argv[1], sys.argv[2]
    os.makedirs(os.path.dirname(prefix), exist_ok=True)
    keyed = key_out_background(src)
    tiers = {"bronze": keyed, "silver": retint(keyed, SILVER), "gold": retint(keyed, GOLD)}
    finals = {}
    for name, im in tiers.items():
        f = crop_square(im)
        p = f"{prefix}_{name}.png"
        f.save(p)
        finals[name] = f
        print("saved", p)
    pad = 12
    sheet = Image.new("RGBA", (3 * SIZE + 4 * pad, SIZE + 2 * pad), (0, 0, 0, 0))
    for i, name in enumerate(("bronze", "silver", "gold")):
        sheet.alpha_composite(finals[name], (pad + i * (SIZE + pad), pad))
    sheet.save(prefix + "_contact.png")
    print("saved", prefix + "_contact.png")


if __name__ == "__main__":
    main()
