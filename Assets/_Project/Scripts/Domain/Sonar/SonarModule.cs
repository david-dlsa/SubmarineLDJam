using System;

namespace SubmarineLDJam.Domain.Sonar
{
    public enum SonarMode
    {
        /// <summary>Player presses M. Then waits the cooldown.</summary>
        Manual,

        /// <summary>Sonar pings by itself every X seconds. M does nothing.</summary>
        Auto,

        /// <summary>No pings. Radar always shows everything around.</summary>
        Always
    }

    /// <summary>
    /// Sonar rules. Manual: ping by key with cooldown. Auto: ping by timer. Always: no pings, always on.
    /// What the ping shows (walls, enemies) is Unity's job. The domain only says "ping now" or "ping allowed".
    /// </summary>
    public sealed class SonarModule
    {
        private readonly float _intervalSeconds;

        /// <param name="intervalSeconds">Manual: cooldown after a ping. Auto: time between pings.</param>
        public SonarModule(float intervalSeconds, SonarMode mode = SonarMode.Manual)
        {
            if (intervalSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            _intervalSeconds = intervalSeconds;
            Mode = mode;
        }

        public SonarMode Mode { get; }

        /// <summary>Cooldown seconds (Manual) or seconds until the next auto ping (Auto).</summary>
        public float CooldownSeconds => _intervalSeconds;

        public float CooldownRemaining { get; private set; }

        /// <summary>Manual: can the player ping now?</summary>
        public bool IsReady => CooldownRemaining <= 0f;

        /// <summary>1 right after a ping, 0 when ready (or when the next auto ping is due).</summary>
        public float CooldownPercent => _intervalSeconds <= 0f ? 0f : Math.Min(1f, CooldownRemaining / _intervalSeconds);

        /// <summary>Returns true when an automatic ping is due now (Auto mode only).</summary>
        public bool Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || Mode == SonarMode.Always) return false;

            CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaSeconds);
            if (Mode != SonarMode.Auto || CooldownRemaining > 0f) return false;

            // First auto ping comes at the start, then every interval.
            CooldownRemaining = _intervalSeconds;
            return true;
        }

        /// <summary>Manual only. Returns false if cooling down, or if the mode is not Manual.</summary>
        public bool TryPing()
        {
            if (Mode != SonarMode.Manual || !IsReady) return false;
            CooldownRemaining = _intervalSeconds;
            return true;
        }
    }
}
