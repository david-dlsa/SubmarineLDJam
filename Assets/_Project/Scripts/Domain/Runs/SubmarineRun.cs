using System;
using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Domain.Lights;
using SubmarineLDJam.Domain.Navigation;
using SubmarineLDJam.Domain.Oxygen;
using SubmarineLDJam.Domain.Sonar;

namespace SubmarineLDJam.Domain.Runs
{
    public enum RunStatus { Running, Completed, Failed }

    /// <summary>
    /// One attempt at one level. Aggregate root: all changes go through here,
    /// so the rules stay in one place. No Unity code. Time comes in through Tick.
    /// </summary>
    public sealed class SubmarineRun : AggregateRoot
    {
        private float _collisionCooldown;

        public SubmarineRun(LevelRules rules, Random random = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));

            random = random ?? new Random();
            Movement = new MovementModule(rules.CompassDegreesPerSecond, rules.ForceBarSpeed, rules.ForceLevels,
                rules.HeadingStepDegrees, rules.CompassMode, random);
            Oxygen = new OxygenSupply(rules.MaxOxygen, rules.OxygenDrainPerSecond);
            Sonar = new SonarModule(
                rules.SonarMode == SonarMode.Auto ? rules.SonarAutoIntervalSeconds : rules.SonarCooldownSeconds,
                rules.SonarMode);
            ExitHint = new ExitHintModule(rules.ExitHintMode, rules.ExitHintSeconds);
            Blackout = new BlackoutModule(rules.Blackout, random);
            Refill = new OxygenRefillPuzzle(
                rules.RefillSequenceLength,
                rules.RefillRestoreAmount,
                rules.RefillTriggerPercent,
                rules.RefillMistakePenalty,
                random,
                rules.RefillRestartOnMistake);
        }

        public LevelRules Rules { get; }
        public MovementModule Movement { get; }
        public OxygenSupply Oxygen { get; }
        public OxygenRefillPuzzle Refill { get; }
        public SonarModule Sonar { get; }
        public ExitHintModule ExitHint { get; }
        public BlackoutModule Blackout { get; }

        public RunStatus Status { get; private set; } = RunStatus.Running;
        public float ElapsedSeconds { get; private set; }

        /// <summary>0 until completed. Then 1 to 3.</summary>
        public int Stars { get; private set; }

        public bool IsRunning => Status == RunStatus.Running;

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f) return;

            ElapsedSeconds += deltaSeconds;
            _collisionCooldown = Math.Max(0f, _collisionCooldown - deltaSeconds);

            Movement.Tick(deltaSeconds);
            Oxygen.Tick(deltaSeconds);
            ExitHint.Tick(deltaSeconds);
            switch (Blackout.Tick(deltaSeconds))
            {
                case BlackoutChange.Started: Raise(new BlackoutStarted(Blackout.CurrentDurationSeconds)); break;
                case BlackoutChange.Ended: Raise(new BlackoutEnded()); break;
            }
            if (Sonar.Tick(deltaSeconds)) Raise(new SonarPinged());

            if (!CheckOxygen()) SyncRefill();
        }

        /// <summary>Space key. 1st press locks heading, 2nd press locks force and fires.</summary>
        public void ConfirmMovement()
        {
            if (!IsRunning || Movement.Phase == MovementPhase.Executing) return; // locked until the movement ends

            var impulse = Movement.Confirm();
            if (impulse.HasValue) Raise(new ImpulseFired(impulse.Value));
            else Raise(new HeadingLocked(Movement.LockedHeading));
        }

        /// <summary>Physics says the submarine stopped. Compass and force bar are free again.</summary>
        public void FinishMovement()
        {
            if (!IsRunning) return;
            if (Movement.FinishMovement()) Raise(new MovementFinished());
        }

        /// <summary>M key. Sends a sonar ping if it is charged.</summary>
        public void PingSonar()
        {
            if (!IsRunning || Sonar.Mode != SonarMode.Manual) return; // Auto and Always ignore M

            if (Sonar.TryPing()) Raise(new SonarPinged());
            else Raise(new SonarPingRejected(Sonar.CooldownRemaining));
        }

        /// <summary>Physics says the submarine took an exit hint pickup.</summary>
        public void CollectExitHint()
        {
            if (!IsRunning) return;
            if (ExitHint.Collect()) Raise(new ExitHintCollected(ExitHint.DurationSeconds));
        }

        /// <summary>Arrow key for the oxygen minigame.</summary>
        public void PressArrow(Arrow arrow)
        {
            if (!IsRunning) return;

            var outcome = Refill.Input(arrow, Oxygen);
            if (outcome.Status == RefillInputStatus.Ignored) return;

            Raise(new RefillStepResolved(outcome.SlotIndex, outcome.Status == RefillInputStatus.Correct));
            if (outcome.SequenceFailed) Raise(new RefillSequenceFailed());

            if (outcome.SequenceCompleted)
            {
                Raise(new RefillSequenceCompleted(outcome.Restored, outcome.Mistakes));
                SyncRefill();
            }
        }

        /// <summary>Physics says submarine hit something.</summary>
        public void RegisterCollision(CollisionKind kind) => RegisterCollision(kind, Rules.PenaltyFor(kind));

        /// <summary>
        /// Same, but the thing that hit brings its own penalty (for example a bomb with its own strength).
        /// <paramref name="ignoreCooldown"/>: an explosion always hurts, even right after another hit.
        /// </summary>
        public void RegisterCollision(CollisionKind kind, CollisionPenalty penalty, bool ignoreCooldown = false)
        {
            if (!IsRunning || (!ignoreCooldown && _collisionCooldown > 0f)) return;

            _collisionCooldown = Rules.CollisionCooldownSeconds;

            Oxygen.Drain(penalty.InstantOxygenLoss);
            Oxygen.AddDrainBoost(penalty.DrainMultiplier, penalty.DrainBoostSeconds);
            Raise(new CollisionRegistered(kind, penalty));

            if (!CheckOxygen()) SyncRefill();
        }

        /// <summary>Physics says submarine is in the destination.</summary>
        public void ReachDestination()
        {
            if (!IsRunning) return;

            Status = RunStatus.Completed;
            Stars = Rules.Stars.Evaluate(ElapsedSeconds);
            Raise(new RunCompleted(ElapsedSeconds, Stars));
        }

        private bool CheckOxygen()
        {
            if (!Oxygen.IsDepleted) return false;

            Status = RunStatus.Failed;
            Raise(new RunFailed(RunFailureReason.OxygenDepleted));
            return true;
        }

        private void SyncRefill()
        {
            if (!Refill.Refresh(Oxygen)) return;

            if (Refill.IsActive) Raise(new RefillPuzzleActivated());
            else Raise(new RefillPuzzleDeactivated());
        }
    }
}
