using System;
using System.Collections.Generic;

namespace SubmarineLDJam.Domain.Progress
{
    /// <summary>Best result of one stage.</summary>
    public readonly struct StageRecord
    {
        public int Index { get; }
        public float BestSeconds { get; }
        public int Stars { get; }

        public StageRecord(int index, float bestSeconds, int stars)
        {
            Index = index;
            BestSeconds = bestSeconds;
            Stars = stars;
        }
    }

    /// <summary>
    /// Progress through the stages. Stage 0 is always open.
    /// Stage N opens only after stage N-1 was completed once. No Unity code.
    /// </summary>
    public sealed class Campaign
    {
        private readonly Dictionary<int, StageRecord> _records = new Dictionary<int, StageRecord>();
        private readonly HashSet<int> _introSeen = new HashSet<int>();

        public Campaign(int stageCount, IEnumerable<StageRecord> records = null, IEnumerable<int> introSeen = null)
        {
            if (stageCount < 0) throw new ArgumentOutOfRangeException(nameof(stageCount));
            StageCount = stageCount;

            if (records != null)
                foreach (var record in records)
                    if (record.Index >= 0 && record.Index < stageCount) _records[record.Index] = record;

            if (introSeen != null)
                foreach (var index in introSeen)
                    if (index >= 0 && index < stageCount) _introSeen.Add(index);
        }

        public int StageCount { get; }

        /// <summary>Stages whose intro popup the player already confirmed.</summary>
        public IReadOnlyCollection<int> IntroSeen => _introSeen;

        public bool HasSeenIntro(int index) => _introSeen.Contains(index);

        /// <summary>The player confirmed the intro popup of this stage. It will not show again. True if it was new.</summary>
        public bool MarkIntroSeen(int index)
        {
            if (index < 0 || index >= StageCount) return false;
            return _introSeen.Add(index);
        }

        public IReadOnlyCollection<StageRecord> Records => _records.Values;

        public bool IsCompleted(int index) => _records.ContainsKey(index);

        public bool IsUnlocked(int index)
            => index >= 0 && index < StageCount && (index == 0 || IsCompleted(index - 1));

        public bool TryGetRecord(int index, out StageRecord record) => _records.TryGetValue(index, out record);

        /// <summary>The last open stage. -1 if there are no stages.</summary>
        public int HighestUnlocked
        {
            get
            {
                for (int i = StageCount - 1; i >= 0; i--)
                    if (IsUnlocked(i)) return i;
                return -1;
            }
        }

        /// <summary>
        /// Saves a finished run. Keeps the best time and the best stars (each on its own).
        /// Returns true if something got better.
        /// </summary>
        public bool RecordCompletion(int index, float seconds, int stars)
        {
            if (!IsUnlocked(index)) throw new InvalidOperationException($"Stage {index} is locked.");

            if (!_records.TryGetValue(index, out var old))
            {
                _records[index] = new StageRecord(index, seconds, stars);
                return true;
            }

            float bestSeconds = Math.Min(old.BestSeconds, seconds);
            int bestStars = Math.Max(old.Stars, stars);
            bool improved = bestSeconds < old.BestSeconds || bestStars > old.Stars;
            if (improved) _records[index] = new StageRecord(index, bestSeconds, bestStars);
            return improved;
        }
    }
}
