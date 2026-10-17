using System;

namespace SubmarineLDJam.Domain.Navigation
{
    /// <summary>Engine force chosen by the player. Discrete level from 1 to MaxLevel.</summary>
    public readonly struct Force
    {
        public int Level { get; }
        public int MaxLevel { get; }

        /// <summary>0..1. Level 1 is small, MaxLevel is 1.</summary>
        public float Normalized => (float)Level / MaxLevel;

        public Force(int level, int maxLevel)
        {
            if (maxLevel < 1) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            if (level < 1 || level > maxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            Level = level;
            MaxLevel = maxLevel;
        }
    }

    /// <summary>One push on the submarine: where, and how hard.</summary>
    public readonly struct Impulse
    {
        public Heading Heading { get; }
        public Force Force { get; }

        public Impulse(Heading heading, Force force)
        {
            Heading = heading;
            Force = force;
        }
    }
}
