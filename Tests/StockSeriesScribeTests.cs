using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 库存序列的 **Scribe 往返**（`SDD-LCE-003` §3；GATE-SAVE 未验证项 U1–U5 的自动化兜底）。
    ///
    /// <para><b>为什么必须有</b>：该段是**本仓首个私有嵌套 `IExposable` + `LookMode.Deep` 字典**，
    /// 且失败模式是**静默**的（读档后永远空态、不报错、不崩）—— 只靠实机目视极难发现。
    /// 本用例用真实 Scribe 管线（`ScribeSaver` / `ScribeLoader`，签名已 ilspycmd 取证）走：
    /// <c>Saving → InitSaving/ExposeData/FinalizeSaving → LoadingVars → FinalizeLoading</c>
    /// （含 `ResolveAllCrossReferences` + `DoAllPostLoadInits` 第二遍，见 `08c` §12.3）。</para>
    ///
    /// <para><b>覆盖</b>：①多品种 · 六档环 + 进行中桶**逐根全等**（含派生涨跌幅）；
    /// ②空集合（Scribe 惯例：空集合不写档 ⇒ 读回 null ⇒ 我们的 `??=` 归一）；
    /// ③旧档缺段 ⇒ 目标保持 null 且**不抛**（Data 层负责归一为空集合，契约钉死）。</para>
    ///
    /// <para><b>环境</b>：离线宿主（无 Unity 运行时）。浮点按 `1e-3` 容差比较（写入文本格式不影响判定）；
    /// 用例值取 .5/.25 这类十进制可精确表示的样本。</para>
    /// </summary>
    [TestFixture]
    public sealed class StockSeriesScribeTests
    {
        /// <summary>
        /// 离线日志垫片（Harmony）：Scribe 管线在 Warning/Error 分支会走 `Verse.Log` ⇒
        /// 底层是 Unity 的 native ECall，离线必抛「ECall 方法必须打包到系统模块中」。
        /// 这里把三类日志**短接为捕获**（不丢弃：留在 <see cref="logCapture"/> 里供诊断），
        /// 与 `TempR8Measure` 的离线宿主短接同规。
        /// </summary>
        private static HarmonyLib.Harmony logShim;

        private static readonly List<string> logCapture = new List<string>();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            logShim = new HarmonyLib.Harmony("personalchronicle.tests.stockscribesilencer");
            PatchLog("Error");
            PatchLog("Warning");
            PatchLog("Message");
            // `ScribeLoader.FinalizeLoading()` 内会调 `Verse.DeepProfiler.Start(label)`，
            // 该处离线 NRE（Unity/宿主缺口）⇒ 一并短接（Start/End 均为静态 void）。
            PatchVoid(typeof(DeepProfiler), "Start", typeof(string));
            PatchVoid(typeof(DeepProfiler), "End");
        }

        /// <summary>把静态 void 方法短接为 no-op（离线宿主缺口）。</summary>
        private static void PatchVoid(Type type, string name, params Type[] args)
        {
            MethodInfo original = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (original == null)
            {
                return;
            }
            logShim.Patch(original, prefix: new HarmonyLib.HarmonyMethod(
                typeof(StockSeriesScribeTests).GetMethod(nameof(VoidPrefix),
                    BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static bool VoidPrefix()
        {
            return false;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (logShim != null)
            {
                logShim.UnpatchAll(logShim.Id);
                logShim = null;
            }
        }

        private static void PatchLog(string name)
        {
            MethodInfo original = typeof(Log).GetMethod(name,
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (original == null)
            {
                return;
            }
            logShim.Patch(original, prefix: new HarmonyLib.HarmonyMethod(
                typeof(StockSeriesScribeTests).GetMethod(nameof(LogPrefix),
                    BindingFlags.NonPublic | BindingFlags.Static)));
        }

        /// <summary>前缀返回 false ⇒ 跳过原方法（不触 ECall）；文本进捕获表。</summary>
        private static bool LogPrefix(string text)
        {
            logCapture.Add(text ?? string.Empty);
            return false;
        }

        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), "pc_stock_scribe_" + Guid.NewGuid().ToString("N") + ".xml");
        }

        [TearDown]
        public void TearDown()
        {
            // 复位 Scribe 全局状态（避免污染其它用例）；临时文件尽力清理。
            Scribe.ForceStop();
            Scribe.mode = LoadSaveMode.Inactive;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // 清理失败不影响用例判定（临时目录）
            }
        }

        // ---------------- ① 全保真往返 ----------------

        [Test]
        public void RoundTrip_PreservesStoreSeriesAndCandles()
        {
            StockSeriesStore original = new StockSeriesStore();

            // Steel：跨 5 个日桶（前 4 桶已封 + 末桶进行中）⇒ 同时命中 环 / 进行中桶 / 上卷（周/月/季/年）
            StockSeries steel = original.GetOrCreate("Steel");
            for (int day = 0; day < 5; day++)
            {
                for (int k = 0; k < 6; k++)
                {
                    steel.AppendSample(day * EconomyPeriodTier.TicksPerDay + k * 250L,
                        100.5f + day * 1.25f + k * 0.25f);
                }
            }

            // Wood：单一进行中桶（无已封蜡烛）
            StockSeries wood = original.GetOrCreate("Wood");
            for (int i = 0; i < 5; i++)
            {
                wood.AppendSample(i * 250L, 7.25f);
            }

            StockSeriesStore loaded = RoundTrip(original);

            if (loaded == null)
            {
                Assert.Fail("往返后对象为 null（管线未产出对象）；捕获日志：" + JoinLogs());
            }
            Assert.AreEqual(original.Count, loaded.Count, "品种数");
            Assert.AreEqual(original.HasAnyData, loaded.HasAnyData, "HasAnyData");
            Assert.AreEqual(1, loaded.StockSeriesSchema, "段内版本位 v 往返");

            AssertTierEqual(original.Get("Steel"), loaded.Get("Steel"), "Steel");
            AssertTierEqual(original.Get("Wood"), loaded.Get("Wood"), "Wood");
        }

        // ---------------- ② 空集合归一（空集合不写档 ⇒ 读回 null ⇒ ??=） ----------------

        [Test]
        public void RoundTrip_EmptyStoreYieldsEmptyNotCrash()
        {
            StockSeriesStore original = new StockSeriesStore();
            StockSeriesStore loaded = RoundTrip(original);

            if (loaded == null)
            {
                Assert.Fail("空集合往返后为 null；捕获日志：" + JoinLogs());
            }
            Assert.AreEqual(0, loaded.Count, "空集合不得凭空多出条目");
            Assert.IsFalse(loaded.HasAnyData);
        }

        // ---------------- ③ 旧档缺段：目标保持 null 且不抛 ----------------

        [Test]
        public void Load_MissingSegment_LeavesTargetNullWithoutThrow()
        {
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<savegame><other>x</other></savegame>",
                System.Text.Encoding.UTF8);

            StockSeriesStore target = null;
            Scribe.loader.InitLoading(path);   // 同纪律：不预置 mode
            Scribe_Deep.Look(ref target, "stockSeries");
            Scribe.loader.FinalizeLoading();

            Assert.IsNull(target,
                "旧档缺段 ⇒ 目标保持 null（`ExposeStockSeries` 负责归一为空集合；此处只钉死不抛）");
        }

        // ---------------- ④ 读档语义（无上限保留 + 退役槽位清空 + 槽位对齐） ----------------

        /// <summary>
        /// ⚠ 读取超大段（2026-09-21 裁决后语义）：
        /// · **Day 档 = 无上限** ⇒ 存档里的 60 根必须**完整保留**（一根不裁、最老仍在）；
        /// · **退役 60分 槽位** ⇒ 存档里残留的旧小时蜡烛必须被**清空**（Trim(0) 迁移语义）；
        /// · 槽位对齐 ⇒ Day 数据仍在槽位 1（旧档形状升级后不位移）。
        /// </summary>
        [Test]
        public void Load_UnboundedDayRing_IsKept_AndRetiredHourSlotCleared()
        {
            StockSeriesStore seed = new StockSeriesStore();
            StockSeries series = seed.GetOrCreate("Steel");
            series.AppendSample(0L, 1f);
            series.AppendSample(EconomyPeriodTier.TicksPerDay, 2f);   // 日桶 0 封桶（环内 1 根）
            SaveTo(seed);

            const int Total = 60;
            XDocument doc = XDocument.Load(path);
            List<XElement> slots = doc.Descendants("rings").First().Elements().ToList();
            Assert.AreEqual(EconomyPeriodTier.Count, slots.Count, "存档形状 = 6 槽位（含退役槽）");
            FillRing(slots[EconomyPeriodTier.Hour60], 12, 900);      // 退役槽：种 12 根旧小时蜡烛
            FillRing(slots[EconomyPeriodTier.Day], Total, 0);         // 基准档：种 60 根
            doc.Descendants("forming").Remove();   // 去掉进行中桶 ⇒ 档内蜡烛数 = 环内长度（断言更锐）
            doc.Save(path);

            StockSeriesStore loaded = LoadFrom();
            Assert.IsNotNull(loaded, "读取超大段不得失败（静默 null 是最大风险）");
            StockSeries read = loaded.Get("Steel");
            Assert.IsNotNull(read, "品种键应保真");
            Assert.AreEqual(0, read.CountOf(EconomyPeriodTier.Hour60), "退役槽位：旧小时蜡烛必须被清空");
            Assert.AreEqual(Total, read.CountOf(EconomyPeriodTier.Day), "无上限：60 根一根不裁");
            Assert.AreEqual(1f, read.CandleAt(EconomyPeriodTier.Day, 0).Open, "最老一根仍在（未被裁剪）");
            Assert.AreEqual((float)Total, read.CandleAt(EconomyPeriodTier.Day, Total - 1).Close, "最新一根在");
        }

        /// <summary>
        /// **旧档形状双向兼容**（保留退役槽位索引位的核心理由）：给每个槽位种一根"开 = 槽位号"的蜡烛
        /// ⇒ 读档后必须**各就各位**（槽位 i 的蜡烛仍在 i），槽位 0 被清空。
        /// 同时钉死"升级无需迁移、降级回旧版亦不误读"。
        /// </summary>
        [Test]
        public void Load_LegacyShape_KeepsSlotAlignment_AndClearsRetiredHourSlot()
        {
            StockSeriesStore seed = new StockSeriesStore();
            StockSeries series = seed.GetOrCreate("Steel");
            series.AppendSample(0L, 1f);
            series.AppendSample(EconomyPeriodTier.TicksPerDay, 2f);
            SaveTo(seed);

            XDocument doc = XDocument.Load(path);
            List<XElement> slots = doc.Descendants("rings").First().Elements().ToList();
            for (int slot = 0; slot < slots.Count; slot++)
            {
                FillRing(slots[slot], 1, slot - 1);   // 值 = 槽位号（槽位 0 得 0 亦无妨，它会被清空）
            }
            doc.Descendants("forming").Remove();
            doc.Save(path);

            StockSeriesStore loaded = LoadFrom();
            Assert.IsNotNull(loaded, "读取不得失败");
            StockSeries read = loaded.Get("Steel");
            Assert.IsNotNull(read, "品种键应保真");
            Assert.AreEqual(0, read.CountOf(EconomyPeriodTier.Hour60), "槽位 0（60分）退役 ⇒ 清空");
            for (int slot = EconomyPeriodTier.Day; slot < EconomyPeriodTier.Count; slot++)
            {
                Assert.AreEqual(1, read.CountOf(slot), "槽位 " + slot + " 应有 1 根");
                Assert.AreEqual((float)slot, read.CandleAt(slot, 0).Open, "槽位 " + slot + " 的蜡烛必须留在原位");
            }
        }

        /// <summary>把某档的已封环替换为 <paramref name="count"/> 根"开 = baseValue + i + 1"的蜡烛。</summary>
        private static void FillRing(XElement slot, int count, int baseValue)
        {
            Assert.IsNotNull(slot, "槽位元素");
            XElement k = slot.Element("k");
            if (k == null)
            {
                k = new XElement("k");
                slot.Add(k);
            }
            k.RemoveNodes();
            for (int i = 0; i < count; i++)
            {
                int v = baseValue + i + 1;
                k.Add(new XElement("li",
                    new XElement("o", v), new XElement("h", v),
                    new XElement("l", v), new XElement("c", v), new XElement("n", 1)));
            }
        }

        // ---------------- 工具 ----------------

        /// <summary>
        /// 真实 Scribe 管线往返。⚠ 故意**不 new** `ScribeSaver/ScribeLoader`：
        /// 二者是 `Scribe` 的静态字段且**内联初始化**（`08c` §12.1 实证），游戏内也是复用同一实例，
        /// `ForceStop()` 负责复位 —— 与官方用法一致。
        /// </summary>
        private StockSeriesStore RoundTrip(StockSeriesStore source)
        {
            SaveTo(source);
            return LoadFrom();
        }

        /// <summary>
        /// ⚠ 纪律（2026-09-21 取证修正）：**不得预置 `Scribe.mode`** ——
        /// `ScribeSaver.InitSaving` / `ScribeLoader.InitLoading` 开头即有守卫
        /// `if (Scribe.mode != Inactive) { Log.Error(...); Scribe.ForceStop(); }`，
        /// 且二者**自行**把 mode 切到 Saving / LoadingVars。预置会被守卫打成 ForceStop。
        /// </summary>
        private void SaveTo(StockSeriesStore source)
        {
            Scribe.saver.InitSaving(path, "savegame");
            StockSeriesStore toSave = source;
            Scribe_Deep.Look(ref toSave, "stockSeries");
            Scribe.saver.FinalizeSaving();
            Scribe.mode = LoadSaveMode.Inactive;   // 防御性归位（FinalizeSaving 后本应已 Inactive）
        }

        /// <summary>LoadingVars + FinalizeLoading（含 PostLoadInit 第 2 遍，见 08c §12.3）。</summary>
        private StockSeriesStore LoadFrom()
        {
            StockSeriesStore loaded = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded, "stockSeries");
            Scribe.loader.FinalizeLoading();
            return loaded;
        }

        private static string JoinLogs()
        {
            return logCapture.Count == 0 ? "(无捕获日志)" : string.Join(" | ", logCapture);
        }

        /// <summary>槽位逐根全等（开/高/低/收/样本数）+ 派生涨跌幅全等；退役槽位必须为空。</summary>
        private static void AssertTierEqual(StockSeries a, StockSeries b, string name)
        {
            Assert.IsNotNull(a, name + " 源序列");
            Assert.IsNotNull(b, name + " 读回序列");
            Assert.AreEqual(0, b.CountOf(EconomyPeriodTier.Hour60), name + " 退役 60分 槽位读档后必须为空");

            for (int tier = 0; tier < EconomyPeriodTier.Count; tier++)
            {
                int countA = a.CountOf(tier);
                int countB = b.CountOf(tier);
                Assert.AreEqual(countA, countB, name + " 档 " + tier + " 蜡烛数");
                for (int i = 0; i < countA; i++)
                {
                    StockCandle x = a.CandleAt(tier, i);
                    StockCandle y = b.CandleAt(tier, i);
                    string tag = name + " 档 " + tier + " 第 " + i + " 根";
                    Assert.IsNotNull(y, tag + " 读回不得为 null");
                    Assert.AreEqual(x.Open, y.Open, 1e-3, tag + " 开");
                    Assert.AreEqual(x.High, y.High, 1e-3, tag + " 高");
                    Assert.AreEqual(x.Low, y.Low, 1e-3, tag + " 低");
                    Assert.AreEqual(x.Close, y.Close, 1e-3, tag + " 收");
                    Assert.AreEqual(x.Samples, y.Samples, tag + " 样本数");
                }

                float pctA, pctB;
                bool hasA = a.TryDeltaPct(tier, out pctA);
                bool hasB = b.TryDeltaPct(tier, out pctB);
                Assert.AreEqual(hasA, hasB, name + " 档 " + tier + " 涨跌幅可算性");
                if (hasA && hasB)
                {
                    Assert.AreEqual(pctA, pctB, 1e-3, name + " 档 " + tier + " 涨跌幅");
                }
            }
        }
    }
}
