"""把实机截图按面板区域裁切放大（含提亮提对比），用于逐面板核对 1:1 还原。

用法：
    python _scripts/crop_shot.py <截图路径> [输出目录]

产出：
    <输出目录>/crop_<区域名>.png    —— 默认输出目录 _verify_tmp/

为什么需要它：实机截图常被缩放到 1080 宽，8.5px 的字在图上只有 ~5px ⇒ 肉眼无法判定
「数值列到底有没有画出来」。放 6~8 倍 + 提亮后可直接读像素级证据（本工具已两次定位
到「列宽算错导致整列落在画布外」这类静态审查看不出的缺陷）。
"""
import os
import sys

from PIL import Image, ImageEnhance

# 区域表：(名称, x0, y0, x1, y1, 放大倍数)  —— 坐标为整图比例，按 16:9 窗口布局取
REGIONS = [
    ("head_kpi", 0.00, 0.02, 1.00, 0.17, 3),
    ("p1_society", 0.00, 0.13, 0.29, 0.62, 4),
    ("p2_status", 0.26, 0.13, 0.56, 0.62, 4),
    ("p3_market", 0.52, 0.13, 0.80, 0.62, 4),
    ("p4_select", 0.76, 0.13, 1.00, 0.90, 4),
    ("p5_inventory", 0.00, 0.58, 0.80, 0.90, 3),
    ("zoom_market_head", 0.525, 0.30, 0.785, 0.40, 8),
    ("zoom_market_rows", 0.525, 0.365, 0.785, 0.50, 8),
    ("zoom_select_rows", 0.76, 0.20, 1.00, 0.42, 7),
    ("zoom_chain", 0.00, 0.62, 0.52, 0.80, 6),
    ("zoom_invside", 0.50, 0.60, 0.80, 0.78, 7),
    ("zoom_kpi", 0.00, 0.055, 0.55, 0.135, 8),
]


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    src = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "_verify_tmp")
    os.makedirs(out, exist_ok=True)

    im = Image.open(src).convert("RGB")
    w, h = im.size
    print("SIZE", w, h)
    for name, x0, y0, x1, y1, scale in REGIONS:
        box = (int(w * x0), int(h * y0), int(w * x1), int(h * y1))
        tile = im.crop(box).resize(
            ((box[2] - box[0]) * scale, (box[3] - box[1]) * scale), Image.LANCZOS)
        tile = ImageEnhance.Contrast(tile).enhance(1.6)
        tile = ImageEnhance.Brightness(tile).enhance(1.35)
        path = os.path.join(out, "crop_" + name + ".png")
        tile.save(path)
        print("crop_" + name, box, "->", tile.size)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
