using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using MapUI = Verse.UI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private sealed class SpeechMemory
        {
            public readonly PawnNeedSpeechState State = new PawnNeedSpeechState();
            public int MapId, LastSeenTick;
        }
        private sealed class SpeechBubble
        {
            public Pawn Pawn;
            public string Text;
            public float CreatedTime;
            public bool Urgent;
        }
        private const int MaximumSpeechPawns = 4096, SpeechBatchSize = 8, MaximumBubbles = 4;
        private const float BubbleLifetime = 4.5f;
        private readonly LaborSpeechQueue laborSpeechQueue = new LaborSpeechQueue();
        private int laborSpeechPostFailures;
        /// <summary>Bounded feedback loss counters; inspection never mutates finance or workflow.</summary>
        internal string LaborSpeechDiagnostics => "pendingPawns=" + laborSpeechQueue.PawnCount
            + "|duplicates=" + laborSpeechQueue.DuplicateCount + "|overflow=" + laborSpeechQueue.OverflowCount
            + "|expired=" + laborSpeechQueue.ExpiredCount + "|discarded=" + laborSpeechQueue.DiscardedCount
            + "|enqueueFailures=" + laborSpeechPostFailures;
        private readonly Dictionary<string, SpeechMemory> needSpeechMemory = new Dictionary<string, SpeechMemory>();
        private readonly List<string> retiredSpeechMemory = new List<string>();
        private readonly List<SpeechBubble> needSpeechBubbles = new List<SpeechBubble>(MaximumBubbles);
        private readonly Rect[] speechRects = new Rect[MaximumBubbles];
        private static GUIStyle speechBubbleStyle;
        private static Texture2D speechTailTexture;
        private long needSpeechEpoch;
        private int needSpeechCursor, needSpeechTick = -1;
        private float nextBubbleTime;

        private void ResetNeedSpeech()
        {
            needSpeechMemory.Clear(); needSpeechBubbles.Clear(); retiredSpeechMemory.Clear();
            laborSpeechQueue.Clear();
            laborSpeechPostFailures = 0;
            needSpeechCursor = 0; needSpeechEpoch = 0; needSpeechTick = -1; nextBubbleTime = 0f;
        }
        private void TickNeedSpeech()
        {
            if (PersonalChronicleMod.Settings?.EnablePawnNeedSpeech != true || perception == null)
            {
                if (needSpeechMemory.Count != 0 || needSpeechBubbles.Count != 0 || laborSpeechQueue.PawnCount != 0)
                    ResetNeedSpeech();
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (needSpeechEpoch != PerceptionSessionEpoch || now < needSpeechTick) ResetNeedSpeech();
            needSpeechEpoch = PerceptionSessionEpoch; needSpeechTick = now;
            if (now % 5 != 0) return;
            float realtime = Time.realtimeSinceStartup;
            for (int i = needSpeechBubbles.Count - 1; i >= 0; i--)
                if (realtime - needSpeechBubbles[i].CreatedTime >= BubbleLifetime
                    || !SpeechPawnEligible(needSpeechBubbles[i].Pawn)
                    || needSpeechBubbles[i].Pawn.Map != Find.CurrentMap)
                    needSpeechBubbles.RemoveAt(i);
            if (now % 600 == 0)
            {
                retiredSpeechMemory.Clear();
                foreach (var pair in needSpeechMemory)
                    if ((long)now - pair.Value.LastSeenTick > 1200) retiredSpeechMemory.Add(pair.Key);
                foreach (string id in retiredSpeechMemory) needSpeechMemory.Remove(id);
            }
            var subjects = ObservedSubjects;
            int count = System.Math.Min(SpeechBatchSize, subjects.Count);
            for (int i = 0; i < count; i++)
            {
                if (needSpeechCursor >= subjects.Count) needSpeechCursor = 0;
                var subject = subjects[needSpeechCursor++];
                if (perceptionSource == null || !perceptionSource.TryGetPlanningPawn(subject.PawnId, subject.MapId, out Pawn pawn)
                    || !SpeechPawnEligible(pawn)) continue;
                var facts = QueryPawnFacts(new PerceptionReadRequest(subject.PawnId, PerceptionSessionEpoch, subject.MapId,
                    PerceptionPurpose.Display, PerceptionGroups.Needs, PawnNeedSpeechState.MaximumFactAge, 500, 1200));
                if (!facts.RequiredGroupsUsable || facts.Snapshot?.Needs.Data == null) continue;
                if (!needSpeechMemory.TryGetValue(subject.PawnId, out var memory) || memory.MapId != subject.MapId)
                {
                    if (memory == null && needSpeechMemory.Count >= MaximumSpeechPawns) continue;
                    needSpeechMemory[subject.PawnId] = memory = new SpeechMemory { MapId = subject.MapId };
                }
                memory.LastSeenTick = now;
                bool show = CanShowSpeech(pawn) && needSpeechBubbles.Count < MaximumBubbles && realtime >= nextBubbleTime;
                for (int b = 0; show && b < needSpeechBubbles.Count; b++)
                    if (needSpeechBubbles[b].Pawn == pawn) show = false;
                NeedSpeechCue cue = memory.State.Observe(facts.Snapshot.Needs.Data.Needs, now, SpeechAction(pawn), show);
                if (cue == null) continue;
                needSpeechBubbles.Add(new SpeechBubble { Pawn = pawn, Text = cue.TranslationKey.Translate().ToString(),
                    CreatedTime = realtime, Urgent = cue.Severity == 2 });
                nextBubbleTime = realtime + .75f;
            }
        }
        private static bool SpeechPawnEligible(Pawn pawn) => pawn != null && pawn.Spawned && !pawn.Dead
            && pawn.IsFreeNonSlaveColonist && pawn.RaceProps.Humanlike && pawn.DevelopmentalStage != DevelopmentalStage.Baby;
        private static bool CanShowSpeech(Pawn pawn)
        {
            if (!WorldRendererUtility.DrawingMap || !SpeechPawnEligible(pawn) || pawn.Map != Find.CurrentMap || pawn.Position.Fogged(pawn.Map)
                || pawn.Drafted || pawn.Downed || pawn.InMentalState
                || (pawn.CurJob?.playerForced == true && !IsLaborSpeechJob(pawn.CurJob.def?.defName)) || !pawn.Awake()) return false;
            Vector2 point = MapUI.MapToUIPosition(pawn.DrawPos);
            return point.x >= 12f && point.x <= MapUI.screenWidth - 12f
                && point.y >= 85f && point.y <= MapUI.screenHeight - 180f;
        }
        private static bool IsLaborSpeechJob(string defName) => defName == "PC_PEA_VisitLaborOffice"
            || defName == "PC_PEA_SubmitWorkContract" || defName == "PC_PEA_CollectContractOutput";
        private static NeedSpeechAction SpeechAction(Pawn pawn)
        {
            var job = pawn.CurJob;
            if (job?.def == JobDefOf.Ingest && job.targetA.Thing?.def.IsNutritionGivingIngestible == true) return NeedSpeechAction.Eating;
            if (job?.def == JobDefOf.LayDown || job?.def?.defName == "LayDownResting") return NeedSpeechAction.Resting;
            return job?.def?.joyKind != null ? NeedSpeechAction.Recreation : NeedSpeechAction.None;
        }

        /// <summary>Pure enqueue from committed business events: no translation, Unity clock or camera access.
        /// Event IDs come from workflow/submission/payment identity and remain stable when a call is replayed.</summary>
        internal void PostLaborSpeech(Pawn pawn, string translationKey, string businessEventId, params object[] args)
        {
            try
            {
                if (pawn == null || string.IsNullOrEmpty(translationKey) || string.IsNullOrEmpty(businessEventId)) return;
                // Bind the event to the current load session before the first speech tick. Otherwise
                // that tick's epoch reset could erase the just-committed signing/payment feedback.
                if (needSpeechEpoch != PerceptionSessionEpoch)
                {
                    ResetNeedSpeech(); needSpeechEpoch = PerceptionSessionEpoch;
                }
                int priority = translationKey.EndsWith(".Paid", System.StringComparison.Ordinal)
                    || translationKey.EndsWith(".NoFunds", System.StringComparison.Ordinal)
                    || translationKey.EndsWith(".PaymentUnavailable", System.StringComparison.Ordinal)
                    || translationKey.EndsWith(".SignRejected", System.StringComparison.Ordinal) ? 2
                    : translationKey.EndsWith(".SignedDocument", System.StringComparison.Ordinal)
                    || translationKey.EndsWith(".SubmittedProducts", System.StringComparison.Ordinal)
                    || translationKey.EndsWith(".SubmittedProof", System.StringComparison.Ordinal) ? 1 : 0;
                laborSpeechQueue.Enqueue(pawn.GetUniqueLoadID(), businessEventId, translationKey, pawn, args, priority);
            }
            catch (System.Exception)
            {
                // Offline-safe bounded diagnostics: no engine Log/ECall on the business call path.
                if (laborSpeechPostFailures < int.MaxValue) laborSpeechPostFailures++;
            }
        }

        private bool LaborSpeechVisible(LaborSpeechQueue.Entry entry)
            => entry.Payload is Pawn pawn && CanShowSpeech(pawn);
        private static bool LaborSpeechPermanentlyInvalid(LaborSpeechQueue.Entry entry)
            => entry.Payload == null || (entry.Payload is Pawn pawn && (pawn.Dead || pawn.Destroyed));

        private bool LaborSpeechSlotAvailable(LaborSpeechQueue.Entry entry)
        {
            if (!LaborSpeechVisible(entry)) return false;
            foreach (var bubble in needSpeechBubbles) if (bubble.Pawn == entry.Payload) return false;
            return true;
        }

        /// <summary>Runs on repaint, including while paused. Waiting lifetime and bubble lifetime both use real seconds.
        /// Hidden, sleeping and other-map pawns keep their pending event without consuming its visible-time budget.</summary>
        private void FlushLaborSpeech(float realtime)
        {
            laborSpeechQueue.Observe(realtime, LaborSpeechVisible);
            if (realtime < nextBubbleTime || needSpeechBubbles.Count >= MaximumBubbles) return;
            var pending = laborSpeechQueue.Peek(LaborSpeechSlotAvailable);
            if (pending == null) return;
            string text = pending.Args == null || pending.Args.Length == 0
                ? pending.Key.Translate().ToString()
                : EconomyLabels.Formatted(pending.Key, pending.Key, pending.Args);
            if (string.IsNullOrEmpty(text) || text == pending.Key)
            {
                // An unavailable translation cannot be rendered; explicitly discard this presentation-only event.
                laborSpeechQueue.Consume(pending);
                return;
            }
            needSpeechBubbles.Add(new SpeechBubble { Pawn = (Pawn)pending.Payload, Text = text,
                CreatedTime = realtime, Urgent = pending.Priority == 2 });
            laborSpeechQueue.Consume(pending);
            nextBubbleTime = realtime + .75f;
        }

        public override void GameComponentOnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawLaborCarryPresentation(); // Real held-item markers do not depend on the optional speech setting.
            if (PersonalChronicleMod.Settings?.EnablePawnNeedSpeech != true) { ResetNeedSpeech(); return; }
            float realtime = Time.realtimeSinceStartup;
            for (int i = needSpeechBubbles.Count - 1; i >= 0; i--)
                if (realtime - needSpeechBubbles[i].CreatedTime >= BubbleLifetime || !SpeechPawnEligible(needSpeechBubbles[i].Pawn))
                    needSpeechBubbles.RemoveAt(i);
            laborSpeechQueue.DiscardWhere(LaborSpeechPermanentlyInvalid);
            FlushLaborSpeech(realtime);
            if (!WorldRendererUtility.DrawingMap || Find.CurrentMap == null || needSpeechBubbles.Count == 0) return;
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                EnsureSpeechBubbleStyle();
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.MiddleCenter; Text.WordWrap = true;
                int placed = 0;
                foreach (var bubble in needSpeechBubbles)
                {
                    float age = realtime - bubble.CreatedTime;
                    if (age < 0f || age >= BubbleLifetime || !CanShowSpeech(bubble.Pawn)) continue;
                    Vector2 head = MapUI.MapToUIPosition(bubble.Pawn.DrawPos);
                    float width = Mathf.Min(230f, Mathf.Max(110f, Text.CalcSize(bubble.Text).x + 22f));
                    float height = Text.CalcHeight(bubble.Text, width - 22f) + 14f;
                    Rect box = new Rect(Mathf.Clamp(head.x - width / 2f, 8f, MapUI.screenWidth - width - 8f),
                        head.y - height - 23f, width, height);
                    bool fits = false;
                    for (int attempt = 0; attempt < MaximumBubbles; attempt++)
                    {
                        bool overlap = false;
                        for (int r = 0; r < placed; r++)
                            if (box.Overlaps(speechRects[r])) { overlap = true; break; }
                        if (!overlap) { fits = box.y >= 8f; break; }
                        box.y -= height + 8f;
                    }
                    if (!fits) continue;
                    speechRects[placed++] = box;
                    float alpha = Mathf.Min(Mathf.Clamp01(age / .15f), Mathf.Clamp01((BubbleLifetime - age) / .5f));
                    // A short filled tail joins the card. No detached leader lines or bright frame.
                    Rect tail = new Rect(Mathf.Clamp(head.x - 5f, box.x + 10f, box.xMax - 20f), box.yMax - 1f, 10f, 8f);
                    Rect shadow = box; shadow.position += new Vector2(1f, 2f);
                    Rect tailShadow = tail; tailShadow.position += new Vector2(1f, 2f);
                    GUI.color = new Color(0f, 0f, 0f, .24f * alpha);
                    GUI.Box(shadow, GUIContent.none, speechBubbleStyle);
                    GUI.DrawTexture(tailShadow, speechTailTexture);
                    GUI.color = bubble.Urgent ? new Color(1f, .91f, .76f, .97f * alpha)
                        : new Color(.96f, .95f, .90f, .97f * alpha);
                    GUI.Box(box, GUIContent.none, speechBubbleStyle);
                    GUI.DrawTexture(tail, speechTailTexture);
                    GUI.color = new Color(.19f, .17f, .14f, alpha);
                    Widgets.Label(new Rect(box.x + 11f, box.y + 7f, box.width - 22f, box.height - 14f), bubble.Text);
                }
            }
            finally { GUI.color = oldColor; Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; }
        }

        private static void EnsureSpeechBubbleStyle()
        {
            if (speechBubbleStyle != null) return;
            // Generated on the GUI thread once; nine-slice borders keep the corner radius constant.
            var fill = new Texture2D(32, 32, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - 16f) - 8f, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - 16f) - 8f, 0f);
                    float coverage = Mathf.Clamp01(8.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    fill.SetPixel(x, y, new Color(1f, 1f, 1f, coverage));
                }
            fill.Apply(false, true);
            speechBubbleStyle = new GUIStyle { border = new RectOffset(8, 8, 8, 8) };
            speechBubbleStyle.normal.background = fill;
            speechTailTexture = new Texture2D(20, 16, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 20; x++)
                    speechTailTexture.SetPixel(x, y, new Color(1f, 1f, 1f,
                        Mathf.Clamp01((y + 1f) * .625f - Mathf.Abs(x + .5f - 10f))));
            speechTailTexture.Apply(false, true);
        }
    }
}
