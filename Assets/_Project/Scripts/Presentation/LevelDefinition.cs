using SubmarineLDJam.Domain.Levels;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Level numbers in an asset. Designer edits it in the Inspector.</summary>
    [CreateAssetMenu(menuName = "Submarine/Level Definition", fileName = "Level_01")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Header("Stars (seconds to reach destination)")]
        public float threeStarSeconds = 30f;
        public float twoStarSeconds = 60f;

        [Header("Oxygen")]
        public float maxOxygen = 100f;
        public float oxygenDrainPerSecond = 1f;

        [Header("Movement")]
        public float compassDegreesPerSecond = 90f;
        public float forceBarSpeed = 1.2f;
        [Min(1)] public int forceLevels = 5;
        [Tooltip("Compass snaps to steps of this many degrees (e.g. 15). 0 = free, no steps.")]
        [Min(0)] public float headingStepDegrees = 0f;

        [Tooltip("Clockwise, CounterClockwise, Alternating (flips after each movement) or Chaotic (random speed, but always makes a full circle).")]
        public SubmarineLDJam.Domain.Navigation.CompassMode compassMode = SubmarineLDJam.Domain.Navigation.CompassMode.Clockwise;

        [Header("Oxygen refill minigame")]
        [Range(0.05f, 1f)] public float refillTriggerPercent = 0.75f;
        [Min(1)] public int refillSequenceLength = 3;
        public float refillRestoreAmount = 8f;
        [Tooltip("Part of the restore lost per wrong arrow. 0.25 = -25% each. Wrong arrows never block the sequence.")]
        [Range(0f, 1f)] public float refillMistakePenalty = 0.25f;

        [Header("Sonar")]
        [Tooltip("Manual: press M (with cooldown). Auto: pings by itself. Always: no pings, radar always shows everything.")]
        public SubmarineLDJam.Domain.Sonar.SonarMode sonarMode = SubmarineLDJam.Domain.Sonar.SonarMode.Manual;
        [Tooltip("Auto mode: seconds between pings.")]
        [Min(0.5f)] public float sonarAutoIntervalSeconds = 6f;
        [Tooltip("Radar sees this far, in world units. The map can be much bigger.")]
        [Min(1f)] public float sonarViewRadius = 10f;
        [Tooltip("Manual mode: wait between pings. Stops M spam.")]
        [Min(0f)] public float sonarCooldownSeconds = 8f;
        [Tooltip("Time for the wave to reach the radar edge.")]
        [Min(0.05f)] public float sonarWaveSeconds = 1f;
        [Tooltip("Time the ping picture stays fully visible.")]
        [Min(0f)] public float sonarHoldSeconds = 1.5f;
        [Tooltip("Time the ping picture takes to fade out.")]
        [Min(0.05f)] public float sonarFadeSeconds = 2f;

        [Header("Exit hint (the orange dot on the radar)")]
        [Tooltip("Always: the dot is always on. OnPickup: it shows only for a while after the submarine takes an Exit Hint Pickup placed in the maze.")]
        public SubmarineLDJam.Domain.Navigation.ExitHintMode exitHintMode = SubmarineLDJam.Domain.Navigation.ExitHintMode.Always;
        [Tooltip("OnPickup: how long the dot shows after a pickup.")]
        [Min(1f)] public float exitHintSeconds = 15f;

        [Header("Collisions")]
        public float collisionCooldownSeconds = 0.5f;
        public Penalty wall = new Penalty { instantLoss = 10f, drainMultiplier = 1f, boostSeconds = 0f };
        public Penalty obstacle = new Penalty { instantLoss = 5f, drainMultiplier = 1.5f, boostSeconds = 3f };
        public Penalty enemy = new Penalty { instantLoss = 15f, drainMultiplier = 2f, boostSeconds = 5f };

        [System.Serializable]
        public struct Penalty
        {
            public float instantLoss;
            public float drainMultiplier;
            public float boostSeconds;

            public CollisionPenalty ToDomain() => new CollisionPenalty(instantLoss, drainMultiplier, boostSeconds);
        }

        public LevelRules ToRules() => new LevelRules(
            levelId: name,
            stars: new StarThresholds(threeStarSeconds, twoStarSeconds),
            maxOxygen: maxOxygen,
            oxygenDrainPerSecond: oxygenDrainPerSecond,
            compassDegreesPerSecond: compassDegreesPerSecond,
            forceBarSpeed: forceBarSpeed,
            forceLevels: forceLevels,
            refillTriggerPercent: refillTriggerPercent,
            refillSequenceLength: refillSequenceLength,
            refillRestoreAmount: refillRestoreAmount,
            collisionCooldownSeconds: collisionCooldownSeconds,
            wallPenalty: wall.ToDomain(),
            obstaclePenalty: obstacle.ToDomain(),
            enemyPenalty: enemy.ToDomain(),
            headingStepDegrees: headingStepDegrees,
            refillMistakePenalty: refillMistakePenalty,
            sonarViewRadius: sonarViewRadius,
            sonarCooldownSeconds: sonarCooldownSeconds,
            sonarWaveSeconds: sonarWaveSeconds,
            sonarHoldSeconds: sonarHoldSeconds,
            sonarFadeSeconds: sonarFadeSeconds,
            sonarMode: sonarMode,
            sonarAutoIntervalSeconds: sonarAutoIntervalSeconds,
            exitHintMode: exitHintMode,
            exitHintSeconds: exitHintSeconds,
            compassMode: compassMode);
    }
}
