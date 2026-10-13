"""测量贴图轮廓描边的宽度与暗度（官方规范：每 64px 用 2px 粗黑描边）。

做法：对不透明区域做形态学腐蚀，被腐蚀掉的环带就是"描边层"。
统计该环带的明度分布，并测出"从外缘向内多少像素内明度仍 < 阈值"。
"""

import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
BENCH = os.path.normpath(os.path.join(
    HERE, "..", "..", "Textures", "Things", "Building", "AutoFarm",
    "PC_AutoFarm_M.png"))
OUT = os.path.join(HERE, "out")


def erode(m, it=1):
    o = m.copy()
    for _ in range(it):
        e = o.copy()
        e[1:, :] &= o[:-1, :]
        e[:-1, :] &= o[1:, :]
        e[:, 1:] &= o[:, :-1]
        e[:, :-1] &= o[:, 1:]
        o = e
    return o


def dilate(m, it=1):
    o = m.copy()
    for _ in range(it):
        d = o.copy()
        d[1:, :] |= o[:-1, :]
        d[:-1, :] |= o[1:, :]
        d[:, 1:] |= o[:, :-1]
        d[:, :-1] |= o[:, 1:]
        o = d
    return o


def probe(arr, name):
    op = arr[:, :, 3] > 127
    lum = 0.299 * arr[:, :, 0] + 0.587 * arr[:, :, 1] + 0.114 * arr[:, :, 2]
    if op.sum() == 0:
        return
    # 外缘 1..6 像素环带上的明度中位数
    prev = op
    print(f"{name:<30} 外缘明度（第1..6px 环带中位）: ", end="")
    rings = []
    for i in range(1, 7):
        e = erode(op, i)
        ring = prev & ~e
        if ring.sum() == 0:
            break
        rings.append(float(np.median(lum[ring])))
        prev = e
    print("  ".join(f"{v:5.0f}" for v in rings))
    # 边缘连续暗像素厚度：从轮廓向内，明度<0x38 的连续深度（取中位）
    inner = erode(op, 1)
    dark_ok = op & (lum < 0x38)
    depths = []
    cur = op
    for i in range(1, 8):
        ring = cur & ~erode(op, i)
        if ring.sum() == 0:
            break
        if dark_ok[ring].mean() > 0.5:
            depths.append(i)
        cur = cur
    print(f"{'':<30} 连续暗描边深度 ≈ {max(depths) if depths else 0}px "
          f"（128px 贴图，官方口径应为 2px/64px = 4px）")


if __name__ == "__main__":
    b = np.array(Image.open(BENCH).convert("RGBA")).astype(np.float32)
    probe(b, "基准件 PC_AutoFarm_M")
    print("-" * 100)
    for f in sorted(os.listdir(OUT)):
        if f.endswith("_east.png"):
            a = np.array(Image.open(os.path.join(OUT, f)).convert("RGBA")).astype(np.float32)
            probe(a, f.replace("_east.png", ""))
