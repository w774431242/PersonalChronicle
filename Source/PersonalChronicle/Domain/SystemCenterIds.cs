namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 系统中心的稳定 ID（= <see cref="SystemCenterDef.defName"/>）。
    ///
    /// <para>GOV-006 硬编码登记：分类 = <b>数据键</b>；来源 = <c>Defs/SystemCenterDefs.xml</c>
    /// 与《系统中心标准化规范书》§2.1；生命周期 = 随规范书中心 ID 变更而同步。</para>
    ///
    /// <para>这里<b>只登记被内置数据源绑定的中心 ID</b>，不登记中心全集 —— 中心全集
    /// 的唯一来源是 Def（新增中心不需要改 C#）。启动期由
    /// <c>ChronicleStartup.ValidateSystemCenterProviders</c> 校验「Provider 绑定的 ID
    /// 都能在注册表里找到」，防止 C# 常量与 XML 漂移后静默无数据。</para>
    /// </summary>
    public static class SystemCenterIds
    {
        public const string Manufacturing = "manufacturing_center";
        public const string Economy = "economy_center";

        // ⚠ CHG-20261006-CENTER-RESERVE（2026-10-06 玩家裁决）：`logistics_center` 与 `medical_center`
        //   已降级为**预留中心**（`Defs/SystemCenterDefs.xml` 的 `reserved` 改为 true）。预留期不注册内置
        //   数据源 ⇒ 按本类自述不变量（「只登记被内置数据源绑定的中心 ID」）**停止登记这两个 ID**；
        //   继续留着常量就与本类注释互相矛盾（常量指向一个没有数据源的中心）。
        //   复活路径（**必须成对**，只做一半会触发启动期不一致告警）：
        //     ① 拷回 `_backups/centers_retire_20261006/` 下的 Provider 原件；
        //     ② 在 `SystemCenterDataRegistry.CreateWithBuiltins` 恢复对应 Register 一行；
        //     ③ 把 Def 的 `reserved` 改回 false；
        //     ④ 恢复下方两行常量。
        //   原行：
        //     public const string Logistics = "logistics_center";
        //     public const string Medical = "medical_center";

        /// <summary>人员系统中心：模块（殖民地档案馆）已接入，详情页承载档案馆主界面本体。</summary>
        public const string Population = "population_center";

    }
}
