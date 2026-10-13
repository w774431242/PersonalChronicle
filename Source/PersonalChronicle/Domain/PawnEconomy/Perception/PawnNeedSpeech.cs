using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    internal enum NeedSpeechAction { None, Eating, Resting, Recreation }

    internal sealed class NeedSpeechCue
    {
        public string NeedId { get; }
        public int Severity { get; }
        public bool Acting { get; }
        public string TranslationKey => "PersonalChronicle.NeedSpeech." + NeedId
            + (Acting ? ".Action" : Severity == 2 ? ".Urgent" : ".Gap");
        public NeedSpeechCue(string needId, int severity, bool acting)
        { NeedId = needId; Severity = severity; Acting = acting; }
    }

    /// <summary>Presentation memory only. Never changes a need, planning target or job.</summary>
    internal sealed class PawnNeedSpeechState
    {
        // Speech thresholds, not replacements for vanilla or life-policy thresholds.
        private sealed class Rule
        {
            public readonly string Id;
            public readonly float Enter, Exit, Urgent;
            public readonly NeedSpeechAction Action;
            public Rule(string id, float enter, float exit, float urgent, NeedSpeechAction action = NeedSpeechAction.None)
            { Id = id; Enter = enter; Exit = exit; Urgent = urgent; Action = action; }
        }
        private static readonly Rule[] Rules =
        {
            new Rule("Food", .30f, .55f, .10f, NeedSpeechAction.Eating),
            new Rule("Rest", .28f, .55f, .10f, NeedSpeechAction.Resting),
            new Rule("Joy", .25f, .50f, .10f, NeedSpeechAction.Recreation),
            new Rule("Comfort", .25f, .50f, .10f),
            new Rule("Outdoors", .20f, .45f, .05f)
        };
        public const int CooldownTicks = 1200, TransitionCooldownTicks = 300, MaximumFactAge = 300;
        private readonly bool[] active = new bool[Rules.Length], actionSpoken = new bool[Rules.Length];
        private readonly int[] spokenSeverity = new int[Rules.Length];
        private int lastTick = -1;
        private long nextSpeechTick, nextTransitionTick;

        public NeedSpeechCue Observe(IReadOnlyList<NumericNeedFact> readings, int now,
            NeedSpeechAction action = NeedSpeechAction.None, bool allowSpeech = true)
        {
            if (now < 0) return null;
            if (now < lastTick) Reset();
            lastTick = now;
            int winner = -1, winnerSeverity = 0, bestScore = -1;
            bool winnerActing = false;
            for (int i = 0; i < Rules.Length; i++)
            {
                Rule rule = Rules[i];
                NumericNeedFact reading = Find(readings, rule.Id);
                if (reading == null || !reading.ShowOnNeedList) { Rearm(i); continue; }
                if ((long)now - reading.CapturedTick > MaximumFactAge || now < reading.CapturedTick
                    || reading.IsFrozen.TryGetValue(out bool frozen) && frozen) continue;
                // Share the verified numeric semantics with the needs dashboard.
                var ratio = CurrentNeedProjector.Project("speech", reading, default).ActualRatio;
                if (!ratio.TryGetValue(out float value)) continue;
                if (value >= rule.Exit) { Rearm(i); continue; }
                if (value <= rule.Enter) active[i] = true;
                if (!active[i]) continue;
                int severity = value <= rule.Urgent ? 2 : 1;
                bool acting = rule.Action != NeedSpeechAction.None && rule.Action == action;
                bool transition = acting && !actionSpoken[i];
                bool escalation = severity > spokenSeverity[i] && spokenSeverity[i] > 0;
                if (!allowSpeech || (transition || escalation ? now < nextTransitionTick : now < nextSpeechTick)) continue;
                if (acting ? !transition : severity <= spokenSeverity[i]) continue;
                // Urgent food/rest wins over an optional recreation announcement.
                int score = severity * 100 + (acting ? 20 : 0) + Rules.Length - i;
                if (score <= bestScore) continue;
                winner = i; winnerSeverity = severity; winnerActing = acting; bestScore = score;
            }
            if (winner < 0) return null;
            spokenSeverity[winner] = Math.Max(spokenSeverity[winner], winnerSeverity);
            if (winnerActing) actionSpoken[winner] = true;
            nextSpeechTick = (long)now + CooldownTicks;
            nextTransitionTick = (long)now + TransitionCooldownTicks;
            return new NeedSpeechCue(Rules[winner].Id, winnerSeverity, winnerActing);
        }

        private static NumericNeedFact Find(IReadOnlyList<NumericNeedFact> readings, string id)
        {
            if (readings != null)
                for (int i = 0; i < readings.Count; i++)
                    if (readings[i]?.NeedDefName == id) return readings[i];
            return null;
        }
        private void Rearm(int index)
        { active[index] = false; actionSpoken[index] = false; spokenSeverity[index] = 0; }
        private void Reset()
        {
            for (int i = 0; i < Rules.Length; i++) Rearm(i);
            nextSpeechTick = nextTransitionTick = 0;
        }
    }
}
