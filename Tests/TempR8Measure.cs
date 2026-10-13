using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.SystemCenters.Dev;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// TEMP（第八轮取证用，跑完即删）：离线量出「人员中心 · 测试数据满值」的内容高 vs 视口高，
    /// 覆盖 1920×1080 与 1600×900 两组，并按项目「计高 ≡ 绘制同源」逐个分解贡献项。
    /// </summary>
    [TestFixture]
    public class TempR8Measure
    {
        private const string Root =
            @"E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统";

        private static readonly string Out = Root + @"\_verify_tmp\r8_measure.txt";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static HarmonyLib.Harmony harmony;
        private static int screenW = 1920;
        private static int screenH = 1080;

        private static readonly Dictionary<string, string> Keyed = LoadKeyed();

        [OneTimeSetUp]
        public void SetUp()
        {
            harmony = new HarmonyLib.Harmony("personalchronicle.r8.tempmeasure");

            // ---- 引擎缺口短接（见第七轮取证：离线宿主无 Unity 运行时）----
            Patch(typeof(Verse.Translator), "Translate", new[] { typeof(string) }, "TranslateKeyPrefix");
            Patch(typeof(Verse.Translator), "Translate", new[] { typeof(string), typeof(object[]) },
                "TranslateArgsPrefix");
            Patch(typeof(Verse.GrammarResolverSimple), "Formatted",
                new[] { typeof(Verse.TaggedString), typeof(List<string>), typeof(List<object>) },
                "FormattedPrefix");
            MethodInfo tick = typeof(Verse.Find).GetProperty("TickManager",
                BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
            if (tick != null)
            {
                harmony.Patch(tick, prefix: new HarmonyLib.HarmonyMethod(
                    typeof(TempR8Measure).GetMethod("TickPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            }
            // ⚠ Screen 是 native ECall（Harmony 无法包），故**不 patch**：窗口尺寸按
            //   `MasterControlTabWindow.SetInitialSizeAndPosition`（:121-130）与实际 InitialSize
            //   的夹取规则在测试里直接算出（见 Measure_Content_vs_Viewport）。
            File.WriteAllText(Root + @"\_verify_tmp\r8_shim_diag.txt", "Keyed=" + Keyed.Count);
        }

        private static void Patch(Type target, string name, Type[] signature, string prefixName)
        {
            MethodInfo original = target.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, signature, null);
            Assert.IsNotNull(original, "未找到 " + target.Name + "." + name);
            harmony.Patch(original, prefix: new HarmonyLib.HarmonyMethod(
                typeof(TempR8Measure).GetMethod(prefixName, BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static bool ScreenWidthPrefix(ref int __result)
        {
            __result = screenW;
            return false;
        }

        private static bool ScreenHeightPrefix(ref int __result)
        {
            __result = screenH;
            return false;
        }

        private static bool TickPrefix(ref Verse.TickManager __result)
        {
            __result = null;
            return false;
        }

        // ---------------------------------------------------------------- 译文注入

        private static Dictionary<string, string> LoadKeyed()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in new[]
            {
                Root + @"\Languages\ChineseSimplified\Keyed\Archive.xml",
                Root + @"\Languages\ChineseSimplified\Keyed\UI.xml"
            })
            {
                if (!File.Exists(file))
                {
                    continue;
                }
                XDocument doc = XDocument.Load(file);
                if (doc.Root == null)
                {
                    continue;
                }
                foreach (XElement element in doc.Root.Elements())
                {
                    map[element.Name.LocalName] = element.Value;
                }
            }
            return map;
        }

        private static string Resolve(string key, object[] args)
        {
            string text;
            if (!Keyed.TryGetValue(key, out text))
            {
                text = key;
            }
            if (args == null)
            {
                return text;
            }
            for (int i = 0; i < args.Length; i++)
            {
                text = text.Replace("{" + i + "}", args[i] == null ? string.Empty : args[i].ToString());
            }
            return text;
        }

        private static bool TranslateKeyPrefix(string key, ref Verse.TaggedString __result)
        {
            __result = Resolve(key, null);
            return false;
        }

        private static bool TranslateArgsPrefix(string key, object[] args, ref string __result)
        {
            __result = Resolve(key, args);
            return false;
        }

        private static bool FormattedPrefix(Verse.TaggedString str, List<string> argsLabelsArg,
            List<object> argsObjectsArg, ref Verse.TaggedString __result)
        {
            string text = str.ToString();
            if (argsObjectsArg != null)
            {
                for (int i = 0; i < argsObjectsArg.Count; i++)
                {
                    string v = argsObjectsArg[i] == null ? string.Empty : argsObjectsArg[i].ToString();
                    text = text.Replace("{" + i + "}", v);
                    if (argsLabelsArg != null && i < argsLabelsArg.Count
                        && !string.IsNullOrEmpty(argsLabelsArg[i]))
                    {
                        text = text.Replace("{" + argsLabelsArg[i] + "}", v);
                    }
                }
            }
            __result = text;
            return false;
        }

        // ---------------------------------------------------------------- 度量

        private static float Const(string name)
        {
            FieldInfo f = typeof(MasterControl.MasterControlTabWindow).GetField(name,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                return Convert.ToSingle(f.GetRawConstantValue(), Inv);
            }
            FieldInfo t = typeof(MasterControl.MasterControlTabWindow).Assembly
                .GetType("PersonalChronicle.Archive.UI.UITheme")
                ?.GetField(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(t, "常量未找到：" + name);
            return Convert.ToSingle(t.GetRawConstantValue(), Inv);
        }

        private static object Field(object instance, string name)
        {
            return instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(instance);
        }

        // ⚠ 离线测试宿主无 Unity 运行时：`Activator.CreateInstance(MasterControlTabWindow)` 会触发
        //   `RimWorld.SoundDefOf` 静态构造 ⇒ SecurityException「ECall 方法必须打包到系统模块中」
        //   （堆栈位置 = 本方法第 206 行，与业务代码无关）。本文件是**取证工具**，加 Ignore 以免
        //   遮蔽真实回归；在**游戏进程内**取证时移除此行即可。
        [Ignore("离线宿主无引擎 ECall；在游戏进程内取证时移除此行")]
        [Test]
        public void Measure_Content_vs_Viewport()
        {
            StringBuilder sb = new StringBuilder();
            float headerHeight = Const("HeaderHeight");
            float gap = Const("Gap");
            float panelPadding = Const("PanelPadding");
            float scrollbar = Const("ScrollbarThickness");

            foreach (int[] screen in new[] { new[] { 1920, 1080 }, new[] { 1600, 900 } })
            {
                foreach (bool manifest in new[] { true, false })
                {
                    screenW = screen[0];
                    screenH = screen[1];

                    object window = Activator.CreateInstance(
                        typeof(MasterControl.MasterControlTabWindow), true);
                    // 窗口实际尺寸（复刻 SetInitialSizeAndPosition :121-130 的夹取 + 居中）：
                    //   size = InitialSize（= RequestedTabSize 经原版按屏幕夹取：宽 ≤ 屏宽、高 ≤ 屏高−35）
                    //   canvasHeight = screenH − 35
                    //   宽/高按屏幕夹取后即为 windowRect 的 w/h（x/y 只影响位置）。
                    Vector2 requested = (Vector2)typeof(MasterControl.MasterControlTabWindow)
                        .GetProperty("RequestedTabSize", BindingFlags.Public | BindingFlags.Instance)
                        .GetValue(window);
                    float canvasHeight = screenH - 35f;
                    float sizeX = Mathf.Min(requested.x, screenW);
                    float sizeY = Mathf.Min(requested.y, canvasHeight);
                    Rect windowRect = new Rect(
                        Mathf.Round((screenW - sizeX) / 2f),
                        Mathf.Round((canvasHeight - sizeY) * 0.5f),
                        sizeX, sizeY);

                    float margin = (float)typeof(Verse.Window).GetProperty("Margin",
                        BindingFlags.Public | BindingFlags.Instance).GetValue(window);
                    Rect inRect = windowRect.ContractedBy(margin);

                    PopulationOverviewSnapshot snapshot = DevPopulationFixtures.BuildRandom(12345);
                    SystemCenterDetailSnapshot detail = new SystemCenterDetailSnapshot
                    {
                        ShowManifest = manifest,
                        IsEmpty = false,
                        PopulationOverview = snapshot
                    };
                    typeof(MasterControl.MasterControlTabWindow)
                        .GetField("cachedDetail", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(window, detail);

                    // 窗口内容区（DoWindowContents 的 contentRect；chrome 为空 ⇒ ticker = 0）
                    Rect contentRect = new Rect(0f, headerHeight + gap, inRect.width,
                        inRect.height - headerHeight - gap);
                    Rect inner = contentRect.ContractedBy(panelPadding);
                    float blockWidth = inner.width - scrollbar;

                    MethodInfo compute = typeof(MasterControl.MasterControlTabWindow).GetMethod(
                        "ComputeDetailHeight", BindingFlags.NonPublic | BindingFlags.Instance);
                    float contentHeight = (float)compute.Invoke(window, new object[] { blockWidth });

                    MethodInfo block = typeof(MasterControl.MasterControlTabWindow).GetMethod(
                        "PopulationBlockHeight", BindingFlags.NonPublic | BindingFlags.Static);
                    float popBlock = (float)block.Invoke(null, new object[] { blockWidth, snapshot });

                    s(sb, "==== screen " + screen[0] + "x" + screen[1] + " · ShowManifest=" + manifest + " ====");
                    s(sb, "windowRect      = " + r(windowRect));
                    s(sb, "inRect          = " + r(inRect));
                    s(sb, "contentRect     = " + r(contentRect));
                    s(sb, "inner(视口)     = " + r(inner));
                    s(sb, "blockWidth      = " + blockWidth.ToString("F1", Inv));
                    s(sb, "视口高 viewport = " + inner.height.ToString("F1", Inv));
                    s(sb, "内容高 content  = " + contentHeight.ToString("F1", Inv));
                    s(sb, "差额 Δ          = " + (contentHeight - inner.height).ToString("F1", Inv)
                        + (contentHeight > inner.height ? "  ⇒ 出滚动条" : "  ⇒ 零滚动条"));
                    s(sb, "其中 人口区块    = " + popBlock.ToString("F1", Inv));
                    s(sb, "  尾部 Notice+40 = " + (Const("NoticeBoxHeight") + 40f).ToString("F1", Inv));
                    s(sb, "  SectionGap     = " + Const("SectionGap").ToString("F1", Inv));
                    s(sb, "  PanelPadY      = " + Const("PopulationPanelPadY").ToString("F1", Inv));
                    s(sb, "  PanelPadX      = " + Const("PopulationPanelPadX").ToString("F1", Inv));
                    s(sb, "  PanelGap       = " + Const("PopulationPanelGap").ToString("F1", Inv));
                    s(sb, "  ChipRowHeight  = " + Const("ChipRowHeight").ToString("F1", Inv));
                    s(sb, "");
                }
            }
            File.WriteAllText(Out, sb.ToString(), Encoding.UTF8);
        }

        private static string r(Rect rect)
        {
            return string.Format(Inv, "x={0:F1} y={1:F1} w={2:F1} h={3:F1}",
                rect.x, rect.y, rect.width, rect.height);
        }

        private static void s(StringBuilder sb, string line)
        {
            sb.AppendLine(line);
        }
    }
}
