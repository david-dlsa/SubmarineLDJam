using System;

namespace SubmarineLDJam.Domain.Oxygen
{
    /// <summary>The submarine "life". Always falling. Hits make it fall more or faster.</summary>
    public sealed class OxygenSupply
    {
        private readonly float _baseDrainPerSecond;
        private float _boostMultiplier = 1f;
        private float _boostRemaining;

        public OxygenSupply(float max, float baseDrainPerSecond)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            if (baseDrainPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(baseDrainPerSecond));

            Max = max;
            Current = max;
            _baseDrainPerSecond = baseDrainPerSecond;
        }

        public float Max { get; }
        public float Current { get; private set; }

        /// <summary>0..1</summary>
        public float Percent => Current / Max;

        public bool IsDepleted => Current <= 0f;
        public bool IsFull => Current >= Max;
        public bool IsBoosted => _boostRemaining > 0f;
        public float CurrentDrainPerSecond => IsBoosted ? _baseDrainPerSecond * _boostMultiplier : _baseDrainPerSecond;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            float boostedTime = Math.Min(deltaSeconds, _boostRemaining);
            float normalTime = deltaSeconds - boostedTime;
            Drain(_baseDrainPerSecond * _boostMultiplier * boostedTime + _baseDrainPerSecond * normalTime);

            _boostRemaining -= boostedTime;
            if (_boostRemaining <= 0f)
            {
                _boostRemaining = 0f;
                _boostMultiplier = 1f;
            }
        }

        /// <summary>Lose a chunk at once.</summary>
        public void Drain(float amount)
        {
            if (amount <= 0f) return;
            Current = Math.Max(0f, Current - amount);
        }

        /// <summary>Fall faster for some time. A weaker boost does not replace a stronger one.</summary>
        public void AddDrainBoost(float multiplier, float seconds)
        {
            if (multiplier <= 1f || seconds <= 0f) return;
            if (IsBoosted && multiplier < _boostMultiplier) return;

            _boostMultiplier = multiplier;
            _boostRemaining = Math.Max(_boostRemaining, seconds);
        }

        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            Current = Math.Min(Max, Current + amount);
        }
    }
}
