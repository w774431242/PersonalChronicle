# Work intensity integration contract

This document is the stable integration surface for other RimWorld modders.
The contract is additive: `IArchiveService` is unchanged, and integrations do
not need to reference UI classes or private data structures.

## 1. Register an external evaluator

Use a higher `Priority` when a mod needs to override the built-in evaluation.
Return a tier `defName`, never localized text. The tier can be supplied by the
external mod's own XML.

```csharp
using PersonalChronicle;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;

public sealed class MyWorkIntensityProvider : IWorkIntensityProvider
{
    public string ProviderId => "MyMod.WorkIntensity";
    public int Priority => 100;

    public bool TryEvaluate(
        WorkIntensityInput input,
        out WorkIntensityEvaluation evaluation)
    {
        evaluation = null;
        if (input == null || input.ObservedDays < 5d)
        {
            return false;
        }

        double daily = input.TotalWorkHours / input.ObservedDays;
        if (daily < 15d)
        {
            return false;
        }

        evaluation = new WorkIntensityEvaluation(
            true,
            "MyModWorkIntensityExtreme",
            daily,
            daily * 7d,
            daily * 30d,
            input.ObservedDays,
            input.ColonyAverageDailyHours > 0d
                ? daily / input.ColonyAverageDailyHours : 0d,
            false,
            false,
            ProviderId);
        return true;
    }
}

// Call after both mods have been constructed, for example from a startup
// callback owned by the integrating mod.
PersonalChronicleMod.RegisterWorkIntensityProvider(
    new MyWorkIntensityProvider());
```

The provider is isolated behind a registry. If it throws, PersonalChronicle
logs one warning per provider and falls back to the next provider or the built-in
Def-driven evaluator.

Example tier Def:

```xml
<PersonalChronicle.Domain.WorkIntensityTierDef>
  <defName>MyModWorkIntensityExtreme</defName>
  <tierKey>MyExtreme</tierKey>
  <displayCode>MX</displayCode>
  <minimumDailyHours>15</minimumDailyHours>
  <labelKey>MyMod.UI.WorkIntensity.Extreme</labelKey>
  <tagKey>MyMod.UI.WorkIntensity.ExtremeTag</tagKey>
  <colorHex>#B83A3A</colorHex>
  <order>0</order>
</PersonalChronicle.Domain.WorkIntensityTierDef>
```

## 2. Record work from another work system

Use `IWorkTimeCaptureService` when a custom work system cannot be observed by
the built-in current-job sampler. The service validates that the pawn is a
current colony member, rejects archived pawns, updates the persistent ledger,
and invalidates the aggregate cache through `DataRevision`.

```csharp
IWorkTimeCaptureService capture = PersonalChronicleMod.WorkTimeCaptureService;
if (capture != null)
{
    capture.RecordSample(new WorkTimeSample(
        pawn.GetUniqueLoadID(),
        "MyModCustomWork",
        sampleTicks: 120L,
        gameTick: Find.TickManager.TicksGame,
        sourceId: "MyMod.WorkSystem"));
}
```

`WorkTypeDefName` is intentionally a string. Unknown work types remain in the
ledger and render using the Def name until a corresponding Def is available;
this prevents a hard dependency on any specific work-type mod.

## 3. Read-only career data

```csharp
IWorkIntensityService service = PersonalChronicleMod.WorkIntensityService;
WorkIntensityView intensity = service?.GetWorkIntensity(pawn.GetUniqueLoadID());
IReadOnlyList<WorkIntensityWorkTypeView> rows =
    service?.GetWorkTypeBreakdown(pawn.GetUniqueLoadID(), false);
ColonyWorkAggregateView colony = service?.GetColonyWorkAggregate();
```

All returned views are read-only snapshots. Do not retain them across game
loads; resolve them again from the service when the current game changes.

## 4. Compatibility rules

- Do not patch `ArchiveMainTabWindow` to inject text or layout.
- Do not access `ChronicleGameComponent`, `PawnObject`, or `WorkTimeAccumulator`
  through reflection for normal integration.
- Use XML to add/replace tiers and translation keys; do not duplicate default
  thresholds in UI code.
- Keep provider IDs stable and unique to make registration idempotent.
- Treat `false` from `RecordSample` as a rejected sample, not as permission to
  mutate PersonalChronicle's save data directly.
