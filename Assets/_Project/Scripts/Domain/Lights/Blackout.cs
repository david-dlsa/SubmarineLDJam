using System;

namespace SubmarineLDJam.Domain.Lights
{
    /// <summary>
    /// When the lights go out by themselves. Time between blackouts and time in the dark are random between a min and a max.
    /// </summary>
    public readonly struct BlackoutSettings
    {
        public bool Enabled { get; }
        public float MinIntervalSeconds { get; }
        public float MaxIntervalSeconds { get; }
        public float MinDurationSeconds { get; }
        public float MaxDurationSeconds { get; }

        public BlackoutSettings(float minIntervalSeconds, float maxIntervalSeconds, float minDurationSeconds, float maxDurationSeconds)
        {
            if (minIntervalSeconds <= 0f || maxIntervalSeconds < minIntervalSeconds)
                throw new ArgumentException("Need 0 < minInterval <= maxInterval.");
            if (minDurationSeconds <= 0f || maxDurationSeconds < minDurationSeconds)
                throw new ArgumentException("Need 0 < minDuration <= maxDuration.");

            Enabled = true;
            MinIntervalSeconds = minIntervalSeconds;
            MaxIntervalSeconds = maxIntervalSeconds;
            MinDurationSeconds = minDurationSeconds;
            MaxDurationSeconds = maxDurationSeconds;
        }

        /// <summary>No blackouts.</summary>
        public static BlackoutSettings Disabled => default;
    }

    public enum BlackoutChange { None, Started, Ended }

    /// <summary>
    /// Decides when the cabin lights go out and when they come back. Time comes in through Tick.
    /// The picture (flicker, fade) is not decided here, only the moments.
    /// </summary>
    public sealed class BlackoutModule
    {
        private readonly BlackoutSettings _settings;
        private readonly Random _random;
        private float _timer;

        public BlackoutModule(BlackoutSettings settings, Random random = null)
        {
            _settings = settings;
            _random = random ?? new Random();
            if (_settings.Enabled) _timer = Between(_settings.MinIntervalSeconds, _settings.MaxIntervalSeconds);
        }

        public bool Enabled => _settings.Enabled;

        /// <summary>The lights are out right now.</summary>
        public bool IsDark { get; private set; }

        /// <summary>Length of the blackout going on. 0 when the lights are on.</summary>
        public float CurrentDurationSeconds { get; private set; }

        /// <summary>Seconds until the next change (blackout start, or lights back).</summary>
        public float SecondsToNextChange => _timer;

        public BlackoutChange Tick(float deltaSeconds)
        {
            if (!_settings.Enabled || deltaSeconds <= 0f) return BlackoutChange.None;

            _timer -= deltaSeconds;
            if (_timer > 0f) return BlackoutChange.None;

            if (IsDark)
            {
                IsDark = false;
                CurrentDurationSeconds = 0f;
                _timer = Between(_settings.MinIntervalSeconds, _settings.MaxIntervalSeconds);
                return BlackoutChange.Ended;
            }

            IsDark = true;
            CurrentDurationSeconds = Between(_settings.MinDurationSeconds, _settings.MaxDurationSeconds);
            _timer = CurrentDurationSeconds;
            return BlackoutChange.Started;
        }

        private float Between(float min, float max) => min + (max - min) * (float)_random.NextDouble();
    }
}
