using System;

namespace SubmarineLDJam.Domain.Navigation
{
    /// <summary>Always: the exit dot is always on. OnPickup: it shows only for a while after a pickup.</summary>
    public enum ExitHintMode { Always, OnPickup }

    /// <summary>
    /// Says if the player may see where the exit is.
    /// Always = yes. OnPickup = only for <see cref="DurationSeconds"/> after taking the pickup.
    /// Taking another pickup while it still shows starts the time again.
    /// </summary>
    public sealed class ExitHintModule
    {
        public ExitHintModule(ExitHintMode mode, float durationSeconds)
        {
            Mode = mode;
            DurationSeconds = Math.Max(0f, durationSeconds);
        }

        public ExitHintMode Mode { get; }
        public float DurationSeconds { get; }

        /// <summary>OnPickup only: seconds left. Always 0 in Always mode.</summary>
        public float RemainingSeconds { get; private set; }

        public bool IsVisible => Mode == ExitHintMode.Always || RemainingSeconds > 0f;

        public void Tick(float deltaSeconds)
        {
            if (RemainingSeconds > 0f) RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
        }

        /// <summary>Returns false in Always mode (nothing to collect).</summary>
        public bool Collect()
        {
            if (Mode != ExitHintMode.OnPickup) return false;
            RemainingSeconds = DurationSeconds;
            return true;
        }
    }
}
