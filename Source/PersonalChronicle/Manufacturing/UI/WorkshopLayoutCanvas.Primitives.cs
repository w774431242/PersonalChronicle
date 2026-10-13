using System;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Automation;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 画布原语：文本度量、翻译取值、网格与坐标换算、灯色映射（无状态依赖）。
    ///
    /// <para>纯物理切片（CHG-MFG-ALIGN-001 · GATE-ARCH ARC-013 处置）：自 WorkshopLayoutCanvas.cs
    /// 原样搬移，**零契约改动** —— 类名、成员可见性、方法签名、存档 schema、翻译键均不变。</para>
    /// </summary>
    internal static partial class WorkshopLayoutCanvas
    {

        internal static Color LampColorOf(string stateKey)
        {
            if (stateKey == EquipmentStateResolver.KeyRun)
            {
                return UITheme.Alive;
            }
            if (stateKey == EquipmentStateResolver.KeyFault)
            {
                return UITheme.Dead;
            }
            return stateKey == EquipmentStateResolver.KeyOff ? UITheme.Dim : UITheme.Threat;
        }

        /// <summary>坐标网格（预览「坐标网格」开关；细线每 50 设计单位）。</summary>
        private static void DrawGrid(Rect rect, float k, Vector2 center)
        {
            Color line = UITheme.WithAlpha(UITheme.BorderSoft, 0.35f);
            const float step = 50f;
            for (float d = -DesignExtent; d <= DesignExtent + 0.1f; d += step)
            {
                float sx = center.x + d * k;
                if (sx > rect.x && sx < rect.xMax)
                {
                    Widgets.DrawLine(new Vector2(sx, rect.y), new Vector2(sx, rect.yMax), line, 1f);
                }
                float sy = center.y + d * k;
                if (sy > rect.y && sy < rect.yMax)
                {
                    Widgets.DrawLine(new Vector2(rect.x, sy), new Vector2(rect.xMax, sy), line, 1f);
                }
            }
        }

        /// <summary>
        /// 缩放系数：设计单位 → 屏幕像素（与只读俯视同式，便于两视图读数一致）。
        /// <c>internal</c>：② 外壳 `#zoom-bar` 的「光标绝对坐标」读数复用同一换算。
        /// </summary>
        internal static float Scale(Rect viewport, CanvasState state)
        {
            return viewport.width * state.Zoom / (DesignExtent * 2f);
        }

        /// <summary>
        /// 视口内「设计原点 (0,0)」的屏幕位置 = 视口中心 + Pan（原点居中约定的唯一换算点）。
        /// </summary>
        internal static Vector2 OriginScreenPos(Rect viewport, CanvasState state)
        {
            return new Vector2(viewport.center.x + state.Pan.x, viewport.center.y + state.Pan.y);
        }

        private static float Measure(string text)
        {
            GameFont previous = Text.Font;
            Text.Font = GameFont.Tiny;
            float width = Text.CalcSize(text).x;
            Text.Font = previous;
            return width;
        }

        /// <summary>
        /// **无参** Keyed 文案（走 <see cref="UIComponents.Loc"/> 的语言级缓存）。
        ///
        /// <para>⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：本类 13 处 <c>T()/Translate()</c>
        /// 全在 <c>Draw*</c> 内（审计登记「绘画期翻译」的重灾区之一）。原实现每帧
        /// <c>.Translate().ToString()</c> 分配字符串；改走缓存后**一处改动覆盖本类全部无参点**。</para>
        ///
        /// <para>⚠ 带参版本（<see cref="Translate1"/> / <see cref="Translate2"/>）**有意不缓存** ——
        /// 参数逐次不同。它们若在绘制期高频调用，需在调用方按 revision + 语言另建缓存
        /// （本类已有正规范式：<c>BuildDeviceEntries</c> 的节流缓存）。</para>
        /// </summary>
        private static string Translate(string key)
        {
            return UIComponents.Loc(key);
        }

        /// <summary>
        /// 两参格式化翻译。**必须用 <see cref="NamedArgument"/> 重载** —— 传 <c>object</c> 会落到
        /// 过时的 `params object[]` 版本（CS0618），这是本工程已踩过的坑（1.6 翻译 API）。
        /// </summary>
        private static string Translate2(string key, int arg0, int arg1)
        {
            return key.Translate((NamedArgument)arg0, (NamedArgument)arg1).ToString();
        }

        /// <summary>单参格式化翻译（同上，走 <see cref="NamedArgument"/> 重载）。</summary>
        private static string Translate1(string key, int arg)
        {
            return key.Translate((NamedArgument)arg).ToString();
        }
    }
}
