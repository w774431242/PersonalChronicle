"""T1 十二台设备单向贴图生成入口。

用法：
    python gen_t1_single.py                # 生成全部 12 台 + 拼版预览 + 验收
    python gen_t1_single.py --only 3       # 只生成第 3 台（用于调参迭代）
    python gen_t1_single.py --scale 3      # 预览拼版放大倍数

输出：
    out/PC_<Function>_T1_east.png     128x128 RGBA 单向贴图（east 为基准向）
    out/_sheet_t1_east.png            拼版预览（含算法标注）
    out/_vs_benchmark.png             与风格基准件同缩放并排对比
    out/_report.json                  技术红线 + 密度/明度指标自检结果

验收口径（与基准件 PC_AutoFarm_M 同口径实测，T1 密度按 01 文件为基准的 60%）
--------------------------------------------------------------------------------
    指标                  基准件      T1 合格区间     说明
    median_lum (>0.5α)    44.0        32-58          明度关系一致
    dark_ratio (<0x50)    0.746       >=0.55         规范 §4 整体压暗
    bright_ratio (>112)   0.156       0.04-0.22      亮部（金属高光/亮边）
    detail_ratio (grad>25)0.220       0.075-0.24     细节密度（60% ≈ 0.13）
    opaque_ratio          0.518       >=0.34         画布利用率
    warm_ratio (饱和暖色) 0.235       >=0.09         第二机体色（黄铜旧件）
"""

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import devices_t1 as D                                      # noqa: E402

OUT_DIR = os.path.join(HERE, "out")
BENCHMARK = os.path.join(
    HERE, "..", "..",
    "Textures", "Things", "Building", "AutoFarm", "PC_AutoFarm_M.png")

# T1 验收合格区间（与基准件同口径）
LIMITS = {
    "median_lum": (32.0, 58.0),
    "dark_ratio": (0.55, 1.0),
    "bright_ratio": (0.04, 0.22),
    "detail_ratio": (0.075, 0.24),
    "opaque_ratio": (0.34, 0.95),
    "warm_ratio": (0.09, 1.0),
}


# ----------------------------------------------------------------------
# 统一指标：基准件与生成件走同一段代码，保证同口径
# ----------------------------------------------------------------------

def measure_rgba(arr):
    """从 RGBA float 数组算全套指标。"""
    al = arr[:, :, 3]
    op = al > 127
    if not op.any():
        return None
    lum = 0.299 * arr[:, :, 0] + 0.587 * arr[:, :, 1] + 0.114 * arr[:, :, 2]
    L = lum[op]
    gy, gx = np.gradient(lum)
    gm = np.hypot(gx, gy)
    ys, xs = np.nonzero(op)
    r, g, b = arr[:, :, 0][op], arr[:, :, 1][op], arr[:, :, 2][op]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1.0), 0.0)
    colorful = sat > 0.30
    warm = colorful & (r >= g) & (g > b)
    return {
        "median_lum": round(float(np.median(L)), 1),
        "mean_lum": round(float(L.mean()), 1),
        "dark_ratio": round(float((L < 0x50).mean()), 3),
        "bright_ratio": round(float((L > 112).mean()), 3),
        "detail_ratio": round(float((gm > 25).mean()), 3),
        "detail_ratio_12": round(float((gm > 12).mean()), 3),
        "warm_ratio": round(float(warm.mean()), 3),
        "color_ratio": round(float(colorful.mean()), 3),
        "opaque_ratio": round(float(op.mean()), 3),
        "bbox": (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1),
    }


def benchmark_stats():
    if not os.path.exists(BENCHMARK):
        return None
    arr = np.array(Image.open(BENCHMARK).convert("RGBA")).astype(np.float32)
    m = measure_rgba(arr)
    if m:
        m["path"] = os.path.normpath(BENCHMARK)
        m["size"] = [int(arr.shape[1]), int(arr.shape[0])]
    return m


# ----------------------------------------------------------------------
# 单台生成
# ----------------------------------------------------------------------

def build_one(spec):
    """按算法配方绘制一台设备，返回成品 Sprite（收尾在内部完成）。"""
    s = D.build(spec)
    s.pixel_snap()
    # strength 0.35：只做轻校准。给太高的力度会把刚做出来的高光一起压掉，
    # 亮部（lum>112）与细节梯度会同步缩水。
    s.flatten_luminance(pivot=44.0 / 255.0, strength=0.35)
    # 对比拉伸：以中位数为不动点把亮部抬起来。诊断显示生成件的 p90 只有 91-121，
    # 而基准件是 135 —— 中间调压缩才是"观感发灰"的真正原因。
    # 它不动中位明度，所以 median_lum 仍落在规范区间内。
    s.contrast_stretch(amount=0.72)
    # 收尾：官方要求的粗黑描边（画在最后，不被任何明度操作冲淡）
    s.outline(px=2)
    return s


# ----------------------------------------------------------------------
# 技术红线 + 指标验收
# ----------------------------------------------------------------------

def audit(path, sprite, spec):
    st = sprite.stats()
    kb = os.path.getsize(path) / 1024.0
    img = Image.open(path)
    arr = np.array(img)
    opaque = arr[:, :, 3] > 0
    halo = bool(arr[~opaque][:, :3].any()) if (~opaque).any() else False
    m = measure_rgba(np.array(img).astype(np.float32)) or {}

    checks = {
        # ---- 规范 §7 六条技术红线 ----
        "png32_rgba": img.mode == "RGBA",
        "not_palette": img.mode != "P",
        "size_1to1": img.size == (128, 128),
        "no_halo": not halo,
        "size_ok_kb": kb < 80.0,
        "has_transparent": st.get("opaque_ratio", 1.0) < 0.95,
        "alpha_clean": st.get("stray_alpha_px", 0) <= 150,
    }
    # ---- 观感指标（与基准件同口径）----
    for k, (lo, hi) in LIMITS.items():
        v = m.get(k)
        checks[f"metric_{k}"] = bool(v is not None and lo <= v <= hi)

    return {
        "name": spec["name"], "cn": spec["cn"],
        "file": os.path.basename(path), "kb": round(kb, 1),
        "shape_algo": spec["shape_algo"], "mat_algo": spec["mat_algo"],
        "metrics": m, "checks": checks, "pass": all(checks.values()),
        "failed": [k for k, v in checks.items() if not v],
    }


# ----------------------------------------------------------------------
# 拼版预览
# ----------------------------------------------------------------------

def load_font(size=12):
    for p in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf",
              r"C:\Windows\Fonts\arial.ttf"):
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                continue
    return ImageFont.load_default()


def make_sheet(images, specs, cell=256, cols=4, pad=10, label_h=30,
               bg=(0x2C, 0x2C, 0x2C)):
    rows = int(np.ceil(len(images) / cols))
    w = cols * cell + (cols + 1) * pad
    h = rows * (cell + label_h) + (rows + 1) * pad
    sheet = Image.new("RGB", (w, h), bg)
    draw = ImageDraw.Draw(sheet)
    font = load_font(13)

    for i, (img, spec) in enumerate(zip(images, specs)):
        r, c = divmod(i, cols)
        x = pad + c * (cell + pad)
        y = pad + r * (cell + label_h + pad)
        big = img.resize((cell, cell), Image.NEAREST)
        sheet.paste(big, (x, y), big)
        draw.text((x + 2, y + cell + 6), f'{spec["cn"]}  {spec["name"]}',
                  fill=(0xD0, 0xD0, 0xD0), font=font)
        draw.text((x + 2, y + cell + 18), spec["mat_algo"],
                  fill=(0x80, 0x88, 0x90), font=font)
    return sheet


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", type=int, default=0, help="只生成第 N 台（1 起）")
    ap.add_argument("--scale", type=int, default=2, help="拼版放大倍数")
    args = ap.parse_args()

    os.makedirs(OUT_DIR, exist_ok=True)
    bench = benchmark_stats()
    if bench:
        print("基准件 PC_AutoFarm_M 实测 "
              f"lum={bench['median_lum']}  亮部={bench['bright_ratio']}  "
              f"细节={bench['detail_ratio']}  占空={bench['opaque_ratio']}  "
              f"暖色={bench['warm_ratio']}")

    specs = D.DEVICES
    if args.only:
        specs = [D.DEVICES[args.only - 1]]

    images, reports = [], []
    for spec in specs:
        s = build_one(spec)
        path = os.path.join(OUT_DIR, f'{spec["name"]}_east.png')
        s.save(path)
        rep = audit(path, s, spec)
        rep["sprite"] = s
        reports.append(rep)
        m = rep["metrics"]
        flag = "PASS" if rep["pass"] else "FAIL"
        print(f'[{flag}] {spec["cn"]:<11} {rep["kb"]:>5.1f}KB  '
              f'lum={m["median_lum"]:>5.1f}  暗={m["dark_ratio"]:.2f}  '
              f'亮={m["bright_ratio"]:.3f}  细节={m["detail_ratio"]:.3f}  '
              f'占空={m["opaque_ratio"]:.3f}  暖={m["warm_ratio"]:.3f}'
              + (f'   未过: {rep["failed"]}' if rep["failed"] else ""))

    for rep in reports:
        images.append(rep.pop("sprite").to_image())

    if len(images) > 1:
        sheet = make_sheet(images, specs, cell=128 * args.scale)
        sheet_path = os.path.join(OUT_DIR, "_sheet_t1_east.png")
        sheet.save(sheet_path)
        print(f"\n拼版预览 -> {sheet_path}")

        if os.path.exists(BENCHMARK):
            bench_img = Image.open(BENCHMARK).convert("RGBA")
            bspec = dict(cn="基准件", name="PC_AutoFarm_M", mat_algo="项目风格基准 (T3)")
            sheet2 = make_sheet([bench_img] + images, [bspec] + specs,
                                cell=128 * args.scale, cols=5)
            cmp_path = os.path.join(OUT_DIR, "_vs_benchmark.png")
            sheet2.save(cmp_path)
            print(f"基准件对比 -> {cmp_path}")

    with open(os.path.join(OUT_DIR, "_report.json"), "w", encoding="utf-8") as f:
        json.dump({"benchmark": bench, "limits": LIMITS, "items": reports},
                  f, ensure_ascii=False, indent=2)

    bad = [r for r in reports if not r["pass"]]
    print(f"\n合计 {len(reports)} 台，通过 {len(reports) - len(bad)} 台"
          + (f"，未通过：{[r['name'] for r in bad]}" if bad else ""))


if __name__ == "__main__":
    main()
