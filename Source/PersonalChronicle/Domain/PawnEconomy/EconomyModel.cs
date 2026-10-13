using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.PawnEconomy
{
    public enum InstitutionKind { LaborOffice, WorkshopOffice, SettlementOffice, Shop, ExamCenter }
    public enum EconomyOrderKind { Employment, Purchase, Examination }
    public enum EconomyOrderStatus { Prepared, Active, Committed, Cancelled }
    public enum EconomicGoalStatus { None, WaitingIncome, WaitingSupply, WaitingExamination, Executing, AcquiredPendingUse, Satisfied, Blocked }
    public enum EconomyRecordKind { Account, Person, Institution, Position, Contract, Order, Receipt, Report, Ownership }

    public abstract class EconomyRecord
    {
        public long Id;
        public abstract EconomyRecordKind Kind { get; }
    }
    public sealed class EconomyAccount : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Account;
        public string Key;
        public long Balance, Reserved, Income, Expense;
        public long Available => Balance - Reserved;
    }
    public sealed class EconomyPerson : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Person;
        public string PawnKey;
        public bool Autonomy = true;
        public EconomicGoalStatus Goal;
        public int NextDecisionTick;
        public string AcquiredAsset, Reason;
    }
    public sealed class EconomyInstitution : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Institution;
        public string AnchorKey, DoctorKey;
        public int MapId, ZoneId = -1;
        public InstitutionKind InstitutionKind;
        public bool Open = true, RequiresExam;
        public int StartHour = 6, EndHour = 18, Capacity = 1;
        public long Price = 6000, Wage = 2000;
        public int Revision;
        public string AccountKey => InstitutionKind == InstitutionKind.ExamCenter ? "institution:" + AnchorKey : EconomyState.PublicAccount;
    }
    public sealed class EconomyPosition : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Position;
        public long InstitutionId;
        public int MapId, ZoneId, StartHour, EndHour, Capacity = 1;
        public long Wage;
        public bool Open = true, RequiresExam;
        public int Revision;
    }
    public sealed class EconomyContract : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Contract;
        public long PositionId;
        public string PawnKey;
        public bool Active = true;
        public int LastProductionJobId = -1;
    }
    public sealed class EconomyOrder : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Order;
        public EconomyOrderKind OrderKind;
        public EconomyOrderStatus Status;
        public string ClientKey, ProviderKey, AssetKey, PayerKey, SellerKey, Reason;
        public long InstitutionId, PositionId, Amount;
        public int MapId, ExpiresTick, Progress, LastProgressTick = -1, ClientPulse = -1, ProviderPulse = -1;
        public bool FundsHeld;
        public bool ProviderPaid;
        public bool Pending => Status == EconomyOrderStatus.Prepared || Status == EconomyOrderStatus.Active;
    }
    public sealed class EconomyReceipt : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Receipt;
        public string Key, Type, SubjectKey, FromKey, ToKey;
        public long Amount;
        public int Tick;
        public bool Paid;
    }
    public sealed class EconomyReport : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Report;
        public string PawnKey, DoctorKey;
        public long OrderId;
        public int Tick, ExpiresTick;
        public float Moving, Manipulation, Consciousness;
        public List<string> VisibleConditions = new List<string>();
    }
    public sealed class EconomyOwnership : EconomyRecord
    {
        public override EconomyRecordKind Kind => EconomyRecordKind.Ownership;
        public string AssetKey, OwnerKey;
    }
    public sealed class EconomyState
    {
        public const int CurrentSchema = 1;
        public const string PublicAccount = "public";
        public int Schema = CurrentSchema, Revision;
        public bool Enabled, Funded;
        public long Sequence;
        public string Fault;
        public readonly List<EconomyRecord> Records = new List<EconomyRecord>();
        public bool Ready => string.IsNullOrEmpty(Fault) && Schema == CurrentSchema;
        public long NewId()
        {
            if (Sequence >= int.MaxValue - 1) throw new InvalidOperationException("SequenceExhausted");
            return ++Sequence;
        }
        public T Add<T>(T record) where T : EconomyRecord
        {
            record.Id = NewId(); Records.Add(record); Revision++; return record;
        }
        public IEnumerable<T> All<T>() where T : EconomyRecord
        {
            foreach (var record in Records) if (record is T value) yield return value;
        }
        public T Find<T>(long id) where T : EconomyRecord
        {
            foreach (var record in Records) if (record.Id == id) return record as T;
            return null;
        }
    }

    public static class EconomyRules
    {
        public const long MaxAmount = 1000000000000L;
        public const long OpeningBudget = 100000L;
        public const int DecisionInterval = 1500, FailureCooldown = 600, OrderLifetime = 5000;
        public const int ExamDuration = 600, ReportLifetime = 60000, MaxPendingOrders = 128;
        public const int RetainedOrders = 128, RetainedReceipts = 512;
        public static bool Valid(EconomyAccount a) => a != null && a.Balance >= 0 && a.Balance <= MaxAmount
            && a.Reserved >= 0 && a.Reserved <= a.Balance && a.Income >= 0 && a.Expense >= 0;
        public static bool CanTransfer(EconomyAccount from, EconomyAccount to, long amount, bool reserved = false)
        {
            return Valid(from) && Valid(to) && from != to && amount > 0 && amount <= MaxAmount
                && (reserved ? from.Reserved >= amount : from.Available >= amount)
                && to.Balance <= MaxAmount - amount && to.Income <= long.MaxValue - amount
                && from.Expense <= long.MaxValue - amount;
        }
        public static bool Transfer(EconomyAccount from, EconomyAccount to, long amount, bool reserved = false)
        {
            if (!CanTransfer(from, to, amount, reserved)) return false;
            if (reserved) from.Reserved -= amount;
            from.Balance -= amount; to.Balance += amount; from.Expense += amount; to.Income += amount;
            return true;
        }
        public static bool Reserve(EconomyAccount account, long amount)
        {
            if (!Valid(account) || amount <= 0 || amount > account.Available) return false;
            account.Reserved += amount; return true;
        }
        public static bool InHours(int hour, int start, int end) => start == end || (start < end
            ? hour >= start && hour < end : hour >= start || hour < end);
        public static bool Fresh(int now, int observed, int lifetime) => now >= observed && (long)now - observed < lifetime;
    }
}
