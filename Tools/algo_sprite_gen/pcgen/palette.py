"""T1 设备贴图色板。

唯一真源：docs/设计文档/功能模块/资产规范/00-风格与提示词规范/自动化设备_美术风格规范_v1.0.md §2 实测色板。
本模块只登记颜色常量，不做任何计算。规范 §2 硬规则 4：不得引入色板之外的颜色。

面积配比约束（§2 硬规则 3）：灰机体 70% / 深灰暗面 20% / 功能色+警示 <= 10%。
"""

# ---- §2.1 机体基色（占 70-85% 面积）----
BODY_MID = (0x30, 0x30, 0x30)      # 机体主灰
BODY_HI = (0x40, 0x40, 0x40)       # 机体次灰
DARK = (0x10, 0x10, 0x10)          # 深灰/阴影
DARK2 = (0x20, 0x20, 0x20)         # 次级暗面
LIGHT = (0x50, 0x50, 0x50)         # 亮灰/高光
LIGHT2 = (0x70, 0x70, 0x70)        # 金属亮边
RUST = (0x54, 0x30, 0x00)          # 做旧/锈迹
RUST2 = (0x84, 0x48, 0x0C)         # 黄铜旧件

# ---- §2.2 功能色语义系统（每设备只取 1 个主功能色 + 黄色警示）----
ENERGY_BLUE = (0x24, 0x48, 0x6C)   # 能源 / 水
ENERGY_BLUE2 = (0x18, 0x3C, 0x6C)  # 能源 / 水（深）
BIO_GREEN = (0x54, 0xB4, 0x00)     # 生化 / 生长
BIO_GREEN2 = (0x60, 0xA8, 0x00)    # 生化 / 生长（深）
MOTION_ORANGE = (0xF0, 0x78, 0x00)  # 运动 / 作业机构
MOTION_ORANGE2 = (0xB4, 0x60, 0x0C)  # 运动 / 作业机构（深）

# ---- 警示 / 导向（全设备通用辅助色，不算入主功能色）----
HAZARD = (0xFC, 0xA8, 0x24)
HAZARD2 = (0xC0, 0x84, 0x18)

# ---- 语义名 -> 主色映射（供设备配方按名称取色）----
SEMANTIC = {
    "energy": ENERGY_BLUE,
    "energy_dark": ENERGY_BLUE2,
    "bio": BIO_GREEN,
    "bio_dark": BIO_GREEN2,
    "motion": MOTION_ORANGE,
    "motion_dark": MOTION_ORANGE2,
    "hazard": HAZARD,
    "hazard_dark": HAZARD2,
}

# ---- 屏幕专用：深灰底 + 功能色细线（01 文件本级统一备注 1 条）----
SCREEN_BG = (0x18, 0x18, 0x1A)


def mix(a, b, t):
    """按 t（0-1 的浮点数组或标量）线性混合两个 RGB 元组，返回 (H, W, 3) 浮点数组。"""
    import numpy as np

    ca = np.array(a, dtype=np.float32)
    cb = np.array(b, dtype=np.float32)
    t = np.asarray(t, dtype=np.float32)
    if t.ndim == 2:
        t = t[:, :, None]
    return ca * (1.0 - t) + cb * t


def scale(c, k):
    """整体缩放颜色明度，k 可为标量或 (H, W) 数组。"""
    import numpy as np

    arr = np.array(c, dtype=np.float32)
    k = np.asarray(k, dtype=np.float32)
    if k.ndim == 2:
        k = k[:, :, None]
    return arr * k
