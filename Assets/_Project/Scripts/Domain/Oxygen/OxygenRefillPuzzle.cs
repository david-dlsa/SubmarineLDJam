using System;
using System.Collections.Generic;

namespace SubmarineLDJam.Domain.Oxygen
{
    public enum Arrow { Up, Down, Left, Right }

    public enum RefillSlotState { Pending, Correct, Wrong }

    public enum RefillInputStatus
    {
        /// <summary>Puzzle is not showing. Input does nothing.</summary>
        Ignored,
        Correct,
        /// <summary>Wrong arrow. Slot is used up, but sequence still goes on.</summary>
        Wrong
    }

    public readonly struct RefillInputOutcome
    {
        public RefillInputStatus Status { get; }
        public int SlotIndex { get; }
        public bool SequenceCompleted { get; }
        public float Restored { get; }
        public int Mistakes { get; }

        public RefillInputOutcome(RefillInputStatus status, int slotIndex, bool sequenceCompleted, float restored, int mistakes)
        {
            Status = status;
            SlotIndex = slotIndex;
            SequenceCompleted = sequenceCompleted;
            Restored = restored;
            Mistakes = mistakes;
        }
    }

    /// <summary>
    /// Oxygen minigame. Shows an arrow sequence when oxygen falls to the trigger percent.
    /// Every press uses one slot, right or wrong. Wrong ones never block the sequence,
    /// they only cut how much oxygen comes back. New sequences keep coming until oxygen is full.
    /// </summary>
    public sealed class OxygenRefillPuzzle
    {
        private static readonly Arrow[] AllArrows = { Arrow.Up, Arrow.Down, Arrow.Left, Arrow.Right };

        private readonly Random _random;
        private readonly int _sequenceLength;
        private readonly float _restoreAmount;
        private readonly float _triggerPercent;
        private readonly float _mistakePenalty;
        private Arrow[] _sequence = new Arrow[0];
        private RefillSlotState[] _slots = new RefillSlotState[0];

        /// <param name="mistakePenalty">Part of the restore lost per wrong press. 0.25 = -25% each.</param>
        public OxygenRefillPuzzle(int sequenceLength, float restoreAmount, float triggerPercent, float mistakePenalty, Random random)
        {
            if (sequenceLength < 1) throw new ArgumentOutOfRangeException(nameof(sequenceLength));
            if (restoreAmount <= 0f) throw new ArgumentOutOfRangeException(nameof(restoreAmount));
            if (triggerPercent <= 0f || triggerPercent > 1f) throw new ArgumentOutOfRangeException(nameof(triggerPercent));
            if (mistakePenalty < 0f) throw new ArgumentOutOfRangeException(nameof(mistakePenalty));

            _sequenceLength = sequenceLength;
            _restoreAmount = restoreAmount;
            _triggerPercent = triggerPercent;
            _mistakePenalty = mistakePenalty;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool IsActive { get; private set; }

        /// <summary>How many slots were used in the current sequence.</summary>
        public int Progress { get; private set; }

        public IReadOnlyList<Arrow> Sequence => _sequence;
        public IReadOnlyList<RefillSlotState> Slots => _slots;

        /// <summary>Check oxygen. Returns true if the puzzle turned on or off.</summary>
        public bool Refresh(OxygenSupply oxygen)
        {
            if (!IsActive && oxygen.Percent <= _triggerPercent && !oxygen.IsFull)
            {
                IsActive = true;
                NewSequence();
                return true;
            }

            if (IsActive && oxygen.IsFull)
            {
                IsActive = false;
                Progress = 0;
                return true;
            }

            return false;
        }

        public RefillInputOutcome Input(Arrow arrow, OxygenSupply oxygen)
        {
            if (!IsActive) return new RefillInputOutcome(RefillInputStatus.Ignored, -1, false, 0f, 0);

            int index = Progress;
            bool correct = _sequence[index] == arrow;
            _slots[index] = correct ? RefillSlotState.Correct : RefillSlotState.Wrong;
            Progress++;

            var status = correct ? RefillInputStatus.Correct : RefillInputStatus.Wrong;
            if (Progress < _sequence.Length)
                return new RefillInputOutcome(status, index, false, 0f, 0);

            int mistakes = 0;
            foreach (var slot in _slots)
                if (slot == RefillSlotState.Wrong) mistakes++;

            float restored = _restoreAmount * Math.Max(0f, 1f - mistakes * _mistakePenalty);
            oxygen.Restore(restored);
            NewSequence();
            return new RefillInputOutcome(status, index, true, restored, mistakes);
        }

        private void NewSequence()
        {
            var next = new Arrow[_sequenceLength];
            for (int i = 0; i < next.Length; i++)
                next[i] = AllArrows[_random.Next(AllArrows.Length)];

            _sequence = next;
            _slots = new RefillSlotState[_sequenceLength];
            Progress = 0;
        }
    }
}
