using System;
using SubmarineLDJam.Domain.Lights;
using SubmarineLDJam.Domain.Navigation;
using SubmarineLDJam.Domain.Sonar;

namespace SubmarineLDJam.Domain.Levels
{
    public enum CollisionKind { Wall, Obstacle, Enemy, Bomb }

    /// <summary>What a hit costs: a chunk at once, plus faster fall for some time.</summary>
    public readonly struct CollisionPenalty
    {
        public float InstantOxygenLoss { get; }
        public float DrainMultiplier { get; }
        public float DrainBoostSeconds { get; }

        public CollisionPenalty(float instantOxygenLoss, float drainMultiplier = 1f, float drainBoostSeconds = 0f)
        {
            InstantOxygenLoss = instantOxygenLoss;
            DrainMultiplier = drainMultiplier;
            DrainBoostSeconds = drainBoostSeconds;
        }
    }

    /// <summary>Finish time for stars. Slower than TwoStar still gives 1 star.</summary>
    public readonly struct StarThresholds
    {
        public float ThreeStarSeconds { get; }
        public float TwoStarSeconds { get; }

        public StarThresholds(float threeStarSeconds, float twoStarSeconds)
        {
            if (threeStarSeconds <= 0f || twoStarSeconds < threeStarSeconds)
                throw new ArgumentException("Need 0 < threeStar <= twoStar.");
            ThreeStarSeconds = threeStarSeconds;
            TwoStarSeconds = twoStarSeconds;
        }

        public int Evaluate(float elapsedSeconds)
        {
            if (elapsedSeconds <= ThreeStarSeconds) return 3;
            if (elapsedSeconds <= TwoStarSeconds) return 2;
            return 1;
        }
    }

    /// <summary>All numbers that change from level to level. Level design lives here.</summary>
    public sealed class LevelRules
    {
        public LevelRules(
            string levelId,
            StarThresholds stars,
            float maxOxygen = 100f,
            float oxygenDrainPerSecond = 1f,
            float compassDegreesPerSecond = 90f,
            float forceBarSpeed = 1.2f,
            int forceLevels = 5,
            float refillTriggerPercent = 0.75f,
            int refillSequenceLength = 3,
            float refillRestoreAmount = 8f,
            float collisionCooldownSeconds = 0.5f,
            CollisionPenalty? wallPenalty = null,
            CollisionPenalty? obstaclePenalty = null,
            CollisionPenalty? enemyPenalty = null,
            float headingStepDegrees = 0f,
            float refillMistakePenalty = 0.25f,
            float sonarViewRadius = 10f,
            float sonarCooldownSeconds = 8f,
            float sonarWaveSeconds = 1f,
            float sonarHoldSeconds = 1.5f,
            float sonarFadeSeconds = 2f,
            SonarMode sonarMode = SonarMode.Manual,
            float sonarAutoIntervalSeconds = 6f,
            ExitHintMode exitHintMode = ExitHintMode.Always,
            float exitHintSeconds = 15f,
            CompassMode compassMode = CompassMode.Clockwise,
            BlackoutSettings blackout = default,
            bool refillRestartOnMistake = false)
        {
            RefillRestartOnMistake = refillRestartOnMistake;
            Blackout = blackout;
            CompassMode = compassMode;
            ExitHintMode = exitHintMode;
            ExitHintSeconds = exitHintSeconds;
            SonarMode = sonarMode;
            SonarAutoIntervalSeconds = sonarAutoIntervalSeconds;
            SonarViewRadius = sonarViewRadius;
            SonarCooldownSeconds = sonarCooldownSeconds;
            SonarWaveSeconds = sonarWaveSeconds;
            SonarHoldSeconds = sonarHoldSeconds;
            SonarFadeSeconds = sonarFadeSeconds;
            HeadingStepDegrees = headingStepDegrees;
            RefillMistakePenalty = refillMistakePenalty;
            LevelId = levelId;
            Stars = stars;
            MaxOxygen = maxOxygen;
            OxygenDrainPerSecond = oxygenDrainPerSecond;
            CompassDegreesPerSecond = compassDegreesPerSecond;
            ForceBarSpeed = forceBarSpeed;
            ForceLevels = forceLevels;
            RefillTriggerPercent = refillTriggerPercent;
            RefillSequenceLength = refillSequenceLength;
            RefillRestoreAmount = refillRestoreAmount;
            CollisionCooldownSeconds = collisionCooldownSeconds;
            WallPenalty = wallPenalty ?? new CollisionPenalty(10f);
            ObstaclePenalty = obstaclePenalty ?? new CollisionPenalty(5f, 1.5f, 3f);
            EnemyPenalty = enemyPenalty ?? new CollisionPenalty(15f, 2f, 5f);
        }

        public string LevelId { get; }
        public StarThresholds Stars { get; }
        public float MaxOxygen { get; }
        public float OxygenDrainPerSecond { get; }
        public float CompassDegreesPerSecond { get; }
        public float ForceBarSpeed { get; }
        public int ForceLevels { get; }

        /// <summary>Compass snaps to steps of this many degrees (e.g. 15). 0 = free, no steps.</summary>
        public float HeadingStepDegrees { get; }

        /// <summary>Radar sees this far (world units). The map can be much bigger.</summary>
        public float SonarViewRadius { get; }

        /// <summary>The lights go out by themselves. Disabled by default.</summary>
        public BlackoutSettings Blackout { get; }

        /// <summary>How the compass needle spins: clockwise, counter-clockwise, alternating or chaotic.</summary>
        public CompassMode CompassMode { get; }

        /// <summary>Always = exit dot always on. OnPickup = shows only for a while after a pickup.</summary>
        public ExitHintMode ExitHintMode { get; }

        /// <summary>OnPickup: how long the exit dot shows after a pickup.</summary>
        public float ExitHintSeconds { get; }

        /// <summary>Manual = press M. Auto = pings by itself. Always = no pings, always shows.</summary>
        public SonarMode SonarMode { get; }

        /// <summary>Auto mode: seconds between pings.</summary>
        public float SonarAutoIntervalSeconds { get; }

        /// <summary>Manual mode: wait between two pings. Stops M spam.</summary>
        public float SonarCooldownSeconds { get; }

        /// <summary>Time for the wave to go from the submarine to the radar edge.</summary>
        public float SonarWaveSeconds { get; }

        /// <summary>Time the ping picture stays fully visible.</summary>
        public float SonarHoldSeconds { get; }

        /// <summary>Time the ping picture takes to fade out.</summary>
        public float SonarFadeSeconds { get; }

        /// <summary>Part of the restore lost per wrong arrow. 0.25 = -25% each.</summary>
        public float RefillMistakePenalty { get; }
        public bool RefillRestartOnMistake { get; }
        public float RefillTriggerPercent { get; }
        public int RefillSequenceLength { get; }
        public float RefillRestoreAmount { get; }

        /// <summary>Min time between two penalties. Stops a wall from draining every frame.</summary>
        public float CollisionCooldownSeconds { get; }

        public CollisionPenalty WallPenalty { get; }
        public CollisionPenalty ObstaclePenalty { get; }
        public CollisionPenalty EnemyPenalty { get; }

        public CollisionPenalty PenaltyFor(CollisionKind kind)
        {
            switch (kind)
            {
                case CollisionKind.Obstacle: return ObstaclePenalty;
                case CollisionKind.Enemy: return EnemyPenalty;
                default: return WallPenalty;
            }
        }
    }
}
