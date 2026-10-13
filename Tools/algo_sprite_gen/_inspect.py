"""5 倍最近邻放大对比：基准件 vs 若干生成件。

用法：
    python _inspect.py 01 03 08            # 放大指定序号
    python _inspect.py all 5               # 全部 + 基准件，5 倍
输出：
    out/_zoom_<...>.png
"""

import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import devices_t1 as D  # noqa: E402

OUT = os.path.join(HERE, "out")
BENCH = os.path.normpath(os.path.join(
    HERE, "..", "..", "Textures", "Things", "Building", "AutoFarm",
    "PC_AutoFarm_M.png"))


def font(sz=13):
    for p in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default()


def main():
    args = sys.argv[1:]
    scale = 5
    if args and args[-1].isdigit():
        scale = int(args.pop())
    if not args or args[0] == "all":
        picks = list(range(1, 13))
    else:
        picks = [int(a) for a in args]

    cells = [("基准件 PC_AutoFarm_M", Image.open(BENCH).convert("RGBA"))]
    for i in picks:
        sp = D.DEVICES[i - 1]
        p = os.path.join(OUT, f'{sp["name"]}_east.png')
        if os.path.exists(p):
            cells.append((f'{sp["cn"]} {sp["name"]}', Image.open(p).convert("RGBA")))

    cell = 128 * scale
    lab = 26
    pad = 10
    cols = min(4, len(cells))
    rows = int(np.ceil(len(cells) / cols))
    W = cols * cell + (cols + 1) * pad
    H = rows * (cell + lab) + (rows + 1) * pad
    sheet = Image.new("RGB", (W, H), (0x2C, 0x2C, 0x2C))
    d = ImageDraw.Draw(sheet)
    f = font(15)

    for i, (name, im) in enumerate(cells):
        r, c = divmod(i, cols)
        x = pad + c * (cell + pad)
        y = pad + r * (cell + lab + pad)
        big = im.resize((cell, cell), Image.NEAREST)
        sheet.paste(big, (x, y), big)
        d.text((x + 2, y + cell + 4), name, fill=(0xD8, 0xD8, 0xD8), font=f)

    path = os.path.join(OUT, f"_zoom_{'all' if len(picks) > 6 else '_'.join(map(str, picks))}"
                                 f"_x{scale}.png")
    sheet.save(path)
    print(path, sheet.size)


if __name__ == "__main__":
    main()
