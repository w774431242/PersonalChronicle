"""pcgen —— 纯算法驱动的 RimWorld 设备贴图生成库。

不依赖任何神经网络或训练模型：所有像素由 SDF 几何、噪声、分形、
元胞自动机、计算几何等确定性算法算出，同一随机种子永远得到同一张图。

配色与结构语汇严格对齐：
    docs/设计文档/功能模块/资产规范/00-风格与提示词规范/自动化设备_美术风格规范_v1.0.md
    docs/设计文档/功能模块/资产规范/01-智能化生产设备_T1T3/资产图集提示词/00_公共风格基底与使用说明.md
    docs/设计文档/功能模块/资产规范/01-智能化生产设备_T1T3/资产图集提示词/01_T1_智能化生产设备.md
"""

from . import palette
from . import sdf
from . import noise
from . import fractal
from . import cellular
from . import geometry
from . import render

__all__ = ["palette", "sdf", "noise", "fractal", "cellular", "geometry", "render"]
