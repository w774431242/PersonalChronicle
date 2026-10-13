"""诊断：基准件与生成件的"暗缝/亮边配对"结构差异。

假设：观感差距不在"纹理密度"，而在"部件之间有没有暗缝把体块切开"。
基准件的表面被大量**深色沟槽**切成小体块，生成件是一整块灰面盖了层网格。

指标定义：
  gutter  = 明度 < 42 且 5x5 邻域均值 > 72 的像素（= 亮面里的暗沟）
  rim     = 明度 > 112 且 5x5 邻域均值 < 90 的像素（= 暗面里的亮边）
  pair    = gutter 与 rim 在 1px 内相邻的像素数（中心差分真正吃得下的配对）
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


def box_mean(a, r=2):
    """5x5 均值（用积分图，快）。"""
    p = np.pad(a, r + 1, mode="edge")
    ii = p.cumsum(0).cumsum(1)
    k = 2 * r + 1
    ii = np.pad(ii, ((1, 0), (1, 0)))
    H, W = a.shape
    s = (ii[k:k + H, k:k + W] - ii[0:H, k:k + W]
         - ii[k:k + H, 0:W] + ii[0:H, 0:W])
    return s / float(k * k)


def dilate1(m):
    o = m.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            o |= np.roll(np.roll(m, dy, 0), dx, 1)
    return o


def probe(arr, name):
    op = arr[:, :, 3] > 127
    lum = 0.299 * arr[:, :, 0] + 0.587 * arr[:, :, 1] + 0.114 * arr[:, :, 2]
    lm = box_mean(lum, 2)

    gutter = op & (lum < 42) & (lm > 72)
    rim = op & (lum > 112) & (lm < 90)
    pair = gutter & dilate1(rim)
    verydark = op & (lum < 26)

    L = lum[op]
    # 规范 §2 面积配比要求的三个明度档：深灰暗面(#10-#20) / 主灰(#30-#40) / 亮灰(#50-#70+)
    t_dark = float((L < 0x28).mean())
    t_mid = float(((L >= 0x28) & (L < 0x60)).mean())
    t_hi = float((L >= 0x60).mean())
    t_blaze = float((L >= 0x80).mean())
    print(f"{name:<34} 暗沟={gutter.sum()/op.sum():.4f}  "
          f"亮边={rim.sum()/op.sum():.4f}  "
          f"紧邻配对={pair.sum()/op.sum():.4f}  "
          f"lum_p10={np.percentile(L,10):.0f}  p90={np.percentile(L,90):.0f}  "
          f"跨度={np.percentile(L,90)-np.percentile(L,10):.0f}  "
          f"| 深暗={t_dark:.2f} 主灰={t_mid:.2f} 亮灰={t_hi:.2f} 辉光={t_blaze:.2f}")
    return dict(gutter=float(gutter.sum() / op.sum()),
                rim=float(rim.sum() / op.sum()),
                pair=float(pair.sum() / op.sum()))


if __name__ == "__main__":
    print(f"{'':<34} {'占不透明像素比例':<10}")
    print("-" * 118)
    b = np.array(Image.open(BENCH).convert("RGBA")).astype(np.float32)
    probe(b, "基准件 PC_AutoFarm_M")
    print("-" * 118)
    for f in sorted(os.listdir(OUT)):
        if f.endswith("_east.png"):
            a = np.array(Image.open(os.path.join(OUT, f)).convert("RGBA")).astype(np.float32)
            probe(a, f.replace("_east.png", ""))
