using System;

namespace SubmarineLDJam.Domain.Navigation
{
    public enum MovementPhase
    {
        /// <summary>Part 1: compass spins clockwise. Player locks it.</summary>
        SelectingHeading,

        /// <summary>Part 2: bar goes back and forth. Player locks it.</summary>
        SelectingForce
    }

    /// <summary>
    /// Skill-based movement. Two presses of the same key:
    /// 1st locks heading (cannot undo), 2nd locks force and fires the impulse.
    /// </summary>
    public sealed class MovementModule
    {
        private readonly float _compassDegreesPerSecond;
        private readonly float _forceBarSpeed;
        private readonly int _forceLevels;

        private float _compassDegrees;
        private float _forcePosition;
        private bool _forceRising = true;
        private Heading _lockedHeading;

        private readonly float _headingStep;

        /// <param name="headingStepDegrees">Compass snaps to steps of this size. 0 = free.</param>
        public MovementModule(float compassDegreesPerSecond, float forceBarSpeed, int forceLevels, float headingStepDegrees = 0f)
        {
            if (headingStepDegrees < 0f) throw new ArgumentOutOfRangeException(nameof(headingStepDegrees));
            _headingStep = headingStepDegrees;

            if (compassDegreesPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(compassDegreesPerSecond));
            if (forceBarSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(forceBarSpeed));
            if (forceLevels < 1) throw new ArgumentOutOfRangeException(nameof(forceLevels));

            _compassDegreesPerSecond = compassDegreesPerSecond;
            _forceBarSpeed = forceBarSpeed;
            _forceLevels = forceLevels;
        }

        public MovementPhase Phase { get; private set; } = MovementPhase.SelectingHeading;

        /// <summary>Where the compass needle is now.</summary>
        public Heading CurrentHeading => new Heading(Snap(_compassDegrees));

        public float HeadingStepDegrees => _headingStep;

        /// <summary>Heading locked in part 1. Valid in SelectingForce.</summary>
        public Heading LockedHeading => _lockedHeading;

        /// <summary>Force bar position, 0..1.</summary>
        public float ForcePosition => _forcePosition;

        public int ForceLevels => _forceLevels;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            if (Phase == MovementPhase.SelectingHeading)
                _compassDegrees = Heading.Normalize(_compassDegrees + _compassDegreesPerSecond * deltaSeconds);
            else
                AdvanceForceBar(deltaSeconds);
        }

        /// <summary>
        /// Player pressed the key. Returns null after locking heading,
        /// returns the impulse after locking force.
        /// </summary>
        public Impulse? Confirm()
        {
            if (Phase == MovementPhase.SelectingHeading)
            {
                _lockedHeading = CurrentHeading;
                _forcePosition = 0f;
                _forceRising = true;
                Phase = MovementPhase.SelectingForce;
                return null;
            }

            int level = Math.Min(_forceLevels, (int)(_forcePosition * _forceLevels) + 1);
            Phase = MovementPhase.SelectingHeading;
            return new Impulse(_lockedHeading, new Force(level, _forceLevels));
        }

        private float Snap(float degrees)
        {
            if (_headingStep <= 0f) return degrees;
            return (float)Math.Round(degrees / _headingStep) * _headingStep;
        }

        private void AdvanceForceBar(float deltaSeconds)
        {
            float delta = _forceBarSpeed * deltaSeconds;
            _forcePosition += _forceRising ? delta : -delta;

            if (_forcePosition >= 1f)
            {
                _forcePosition = 2f - _forcePosition;
                _forceRising = false;
            }
            else if (_forcePosition <= 0f)
            {
                _forcePosition = -_forcePosition;
                _forceRising = true;
            }

            _forcePosition = Math.Max(0f, Math.Min(1f, _forcePosition));
        }
    }
}
