using System;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>
    /// 《09-经验反馈与失败恢复》§4 的七类失败。**每一类的处置不同**，把它们混成一个"失败"布尔
    /// 会导致：证据不足被当成供应方不可靠、走错模块的对账被重试、制度闭锁被无限重试。
    /// </summary>
    internal enum FailureClass
    {
        /// <summary>ProtectedAction/OutOfScope：让路，控制/范围变化时复核，**不计服务失败**。</summary>
        ProtectedAction = 0,
        /// <summary>StaleVersion/ReservationLost：释放本票据，读新版本，有界重新比较。</summary>
        StaleVersion = 1,
        /// <summary>MoneyShortfall/StockShortage/NoStaff：等工资/补货/排班等**相关事件**，慢速兜底。</summary>
        ResourceShortage = 2,
        /// <summary>Qualification/DependencyCycle：明确前置或制度闭锁；寻找合法替代，没有就**结构性受阻**。</summary>
        Qualification = 3,
        /// <summary>DeliveryMismatch/TransactionUnknown：交 M7/M11 对账，**不重放付款**。</summary>
        Reconciliation = 4,
        /// <summary>ActualDefault/RepeatedCancellation：更新相应可靠性并允许合法换岗/换服务，保留已履约应付。</summary>
        ActualDefault = 5,
        /// <summary>GetterError/Unsupported：部分未知/适配问题，不虚构需求为零，**不处罚供应方**。</summary>
        GetterError = 6
    }

    /// <summary>某一类失败的**规定处置**。</summary>
    internal readonly struct FailureDisposition
    {
        /// <summary>是否进入退避（冷却是为了不再无意义地重复，而不是惩罚）。</summary>
        public bool Backoff { get; }
        /// <summary>是否计入"服务失败"并因此更新可靠性 —— §4 只对 ActualDefault 开启。</summary>
        public bool CountsAsServiceFailure { get; }
        /// <summary>是否结构性受阻（制度闭锁/资格不满足 ⇒ 换时机也没用，须等真实变化）。</summary>
        public bool Structural { get; }
        /// <summary>是否必须交 M7/M11 对账（不得由本模块自行重试或重放付款）。</summary>
        public bool RouteToReconciliation { get; }
        /// <summary>是否"等事件"而非只等冷却到点。</summary>
        public bool AwaitsEvent { get; }
        public FailureDisposition(bool backoff, bool countsAsServiceFailure, bool structural,
            bool routeToReconciliation, bool awaitsEvent)
        {
            Backoff = backoff; CountsAsServiceFailure = countsAsServiceFailure; Structural = structural;
            RouteToReconciliation = routeToReconciliation; AwaitsEvent = awaitsEvent;
        }
    }

    /// <summary>
    /// §4 失败分类的**纯规则**（离线可证伪）。分类**只依据负责模块给出的原因码**，不猜因果：
    /// "不裁决原版隐性因果"是 M9 的边界（§1）。
    ///
    /// 未知原因一律归 <see cref="FailureClass.GetterError"/> —— 与 §4 "GetterError/Unsupported：
    /// 部分未知/适配问题……**不处罚供应方**"同向：**宁可少记失败，不要错记失败**。
    /// </summary>
    internal static class FailureClassRules
    {
        /// <summary>
        /// 由**生产方引用**的原因码常量（防字面量漂移：未知码会静默归 GetterError ⇒ 不记失败）。
        /// 表本身（Classify 的 switch）保持字面量；InterruptionProtocolTests 断言这些常量在表内命中正确的类。
        /// </summary>
        public const string ReasonReservationLost = "ReservationLost";
        public const string ReasonProtectedTimetable = "ProtectedTimetable";

        /// <summary>按原因码定类。未知码 ⇒ GetterError（少记而非错记）。</summary>
        public static FailureClass Classify(string reasonCode)
        {
            switch (reasonCode)
            {
                // 让路：控制权/范围/时机不在本模块手里，都不是供应方的错。
                case "NativeControlOrSafety":
                case "OutOfScope":
                case "ProtectedTimetable":
                case "NativeFoodFirst":
                case "OutsideHours":
                case "NotAtCounter":
                case "PawnUnknown":
                    return FailureClass.ProtectedAction;
                case "StaleRevision":
                case "ReservationLost":
                    return FailureClass.StaleVersion;
                case "InsufficientFunds":
                case "NoFoodInStock":
                case "CounterUnavailable":
                case "NoStaff":
                    return FailureClass.ResourceShortage;
                case "EmploymentNotPermitted":
                case "Qualification":
                case "DependencyCycle":
                case "NoManagedWorkType":
                    return FailureClass.Qualification;
                case "HandOverFailed":
                case "PaymentConflict":
                case "PaymentUnavailable":
                case "TransactionUnknown":
                    return FailureClass.Reconciliation;
                case "ActualDefault":
                case "RepeatedCancellation":
                case "ContractExpired":
                    return FailureClass.ActualDefault;
                // 证据不足：显示未知，既不记失败也不记满信誉。
                case "NoPublishedPrice":
                case "PriceUnavailable":
                case "CostEstimateUnavailable":
                case "GetterError":
                case "Unsupported":
                default:
                    return FailureClass.GetterError;
            }
        }

        /// <summary>该类的规定处置，逐条对应 §4 表格右侧一列。</summary>
        public static FailureDisposition DispositionOf(FailureClass failureClass)
        {
            switch (failureClass)
            {
                // 让路：复核由控制/范围变化触发，不进退避账。
                case FailureClass.ProtectedAction:
                    return new FailureDisposition(false, false, false, false, false);
                // 释放票据、读新版本、有界重比较 ⇒ 退避但短暂（由调用方按类给不同上限）。
                case FailureClass.StaleVersion:
                    return new FailureDisposition(true, false, false, false, false);
                // 等工资/补货/排班等**相关事件**，慢速兜底；不每 Tick 扫相同条件。
                case FailureClass.ResourceShortage:
                    return new FailureDisposition(true, false, false, false, true);
                // 制度闭锁/资格不满足：换时机无用 ⇒ 结构性受阻。
                case FailureClass.Qualification:
                    return new FailureDisposition(true, false, true, false, true);
                // 交对账，隔离相关新增承诺，**不重放付款**。
                case FailureClass.Reconciliation:
                    return new FailureDisposition(true, false, false, true, true);
                // 真实违约/反复取消：允许合法换岗换服务，并更新可靠性；已履约应付保留。
                case FailureClass.ActualDefault:
                    return new FailureDisposition(true, true, false, false, true);
                // 适配/未知：不处罚供应方。
                case FailureClass.GetterError:
                    return new FailureDisposition(false, false, false, false, false);
                default:
                    return DispositionOf(FailureClass.GetterError);
            }
        }

        /// <summary>便捷判定：该原因码是否进入退避账（等价于处置里的 Backoff）。</summary>
        public static bool IsBackoff(string reasonCode) => DispositionOf(Classify(reasonCode)).Backoff;

        /// <summary>便捷判定：该原因码是否应计入服务失败（更新可靠性）。**只有 ActualDefault 为真**。</summary>
        public static bool IsServiceFailure(string reasonCode) => DispositionOf(Classify(reasonCode)).CountsAsServiceFailure;
    }
}
