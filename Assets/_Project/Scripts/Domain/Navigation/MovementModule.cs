using System;

namespace SubmarineLDJam.Domain.Navigation
{
    public enum MovementPhase
    {
        /// <summary>Part 1: compass spins. Player locks it.</summary>
        SelectingHeading,

        /// <summary>Part 2: bar goes back and forth. Player locks it.</summary>
        SelectingForce,

        /// <summary>
        /// The impulse was fired. Compass and force bar stay locked until the movement is finished
        /// (<see cref="MovementModule.FinishMovement"/>).
        /// </summary>
        Executing
    }

    /// <summary>How the compass needle spins in part 1.</summary>
    public enum CompassMode
    {
        /// <summary>Always clockwise.</summary>
        Clockwise,

        /// <summary>Always counter-clockwise.</summary>
        CounterClockwise,

        /// <summary>Starts clockwise. After each finished movement it turns the other way.</summary>
        Alternating,

        /// <summary>
        /// Wild: speed changes all the time and it sometimes goes back a little.
        /// It always makes more way forward than back, so a full circle always comes.
        /// </summary>
        Chaotic
    }

    /// <summary>
    /// Skill-based movement. Two presses of the same key:
    /// 1st locks heading (cannot undo), 2nd locks force and fires the impulse.
    /// </summary>
    public sealed class MovementModule
    {
        // Chaotic mode: a forward leg is always longer than a back leg, so there is always net progress.
        private const float ForwardLegMinDegrees = 90f;
        private const float ForwardLegMaxDegrees = 300f;
        private const float BackLegMinDegrees = 15f;
        private const float BackLegMaxDegrees = 70f;
        private const float LegSpeedMin = 0.5f;
        private const float LegSpeedMax = 2.5f;
        private const double BackLegChance = 0.6;

        private readonly float _compassDegreesPerSecond;
        private readonly float _forceBarSpeed;
        private readonly int _forceLevels;
        private readonly CompassMode _mode;
        private readonly Random _random;

        private float _compassDegrees;
        private float _forcePosition;
        private bool _forceRising = true;
        private Heading _lockedHeading;
        private float _lockedNeedleDegrees;
        private float _facingDegrees;

        private readonly float _headingStep;

        // Alternating mode: +1 clockwise, -1 counter-clockwise.
        private int _alternatingDirection = 1;

        // Chaotic mode
        private int _mainDirection = 1;
        private int _legDirection = 1;
        private float _legSpeed = 1f;
        private float _legDegreesLeft;
        private bool _lastLegWasBack;

        /// <param name="headingStepDegrees">Compass snaps to steps of this size. 0 = free.</param>
        /// <param name="random">Only used by the Chaotic mode.</param>
        public MovementModule(float compassDegreesPerSecond, float forceBarSpeed, int forceLevels, float headingStepDegrees = 0f,
            CompassMode compassMode = CompassMode.Clockwise, Random random = null)
        {
            if (headingStepDegrees < 0f) throw new ArgumentOutOfRangeException(nameof(headingStepDegrees));
            _headingStep = headingStepDegrees;

            if (compassDegreesPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(compassDegreesPerSecond));
            if (forceBarSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(forceBarSpeed));
            if (forceLevels < 1) throw new ArgumentOutOfRangeException(nameof(forceLevels));

            _compassDegreesPerSecond = compassDegreesPerSecond;
            _forceBarSpeed = forceBarSpeed;
            _forceLevels = forceLevels;
            _mode = compassMode;
            _random = random ?? new Random();

            StartNewSpin();
        }

        public MovementPhase Phase { get; private set; } = MovementPhase.SelectingHeading;

        public CompassMode Mode => _mode;

        /// <summary>
        /// Where the compass needle is now, measured from the submarine front (0 = straight ahead).
        /// Before the first turn the front is north, so this is also the world heading.
        /// </summary>
        public Heading CurrentHeading => new Heading(Snap(_compassDegrees));

        /// <summary>World heading the submarine front points to. Changes when the heading is locked.</summary>
        public Heading Facing => new Heading(_facingDegrees);

        /// <summary>Needle position (from the front) at the moment the heading was locked.</summary>
        public float LockedNeedleDegrees => _lockedNeedleDegrees;

        public float HeadingStepDegrees => _headingStep;

        /// <summary>World heading locked in part 1 (front + needle). Valid in SelectingForce.</summary>
        public Heading LockedHeading => _lockedHeading;

        /// <summary>Force bar position, 0..1.</summary>
        public float ForcePosition => _forcePosition;

        public int ForceLevels => _forceLevels;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            if (Phase == MovementPhase.SelectingHeading) SpinCompass(deltaSeconds);
            else if (Phase == MovementPhase.SelectingForce) AdvanceForceBar(deltaSeconds);
            // Executing: everything stays locked
        }

        /// <summary>
        /// Player pressed the key. Returns null after locking heading,
        /// returns the impulse after locking force.
        /// </summary>
        public Impulse? Confirm()
        {
            if (Phase == MovementPhase.Executing) return null; // locked until the movement ends

            if (Phase == MovementPhase.SelectingHeading)
            {
                _lockedNeedleDegrees = CurrentHeading.Degrees;
                _lockedHeading = new Heading(_facingDegrees + _lockedNeedleDegrees);
                _facingDegrees = _lockedHeading.Degrees; // submarine turns right away: this is the new "up"
                _forcePosition = 0f;
                _forceRising = true;
                Phase = MovementPhase.SelectingForce;
                return null;
            }

            int level = Math.Min(_forceLevels, (int)(_forcePosition * _forceLevels) + 1);
            Phase = MovementPhase.Executing;

            return new Impulse(_lockedHeading, new Force(level, _forceLevels));
        }

        /// <summary>
        /// The submarine finished the movement. Unlocks the compass and clears the force bar.
        /// The next spin may go the other way. Returns false if there was no movement to finish.
        /// </summary>
        public bool FinishMovement()
        {
            if (Phase != MovementPhase.Executing) return false;

            Phase = MovementPhase.SelectingHeading;
            _forcePosition = 0f;

            if (_mode == CompassMode.Alternating) _alternatingDirection = -_alternatingDirection;
            StartNewSpin();
            return true;
        }

        private void SpinCompass(float deltaSeconds)
        {
            switch (_mode)
            {
                case CompassMode.CounterClockwise:
                    Rotate(-_compassDegreesPerSecond * deltaSeconds);
                    break;
                case CompassMode.Alternating:
                    Rotate(_alternatingDirection * _compassDegreesPerSecond * deltaSeconds);
                    break;
                case CompassMode.Chaotic:
                    SpinChaotic(deltaSeconds);
                    break;
                default:
                    Rotate(_compassDegreesPerSecond * deltaSeconds);
                    break;
            }
        }

        private void Rotate(float degrees) => _compassDegrees = Heading.Normalize(_compassDegrees + degrees);

        /// <summary>A new spin starts (game start, or after a movement). Chaotic picks a new main way.</summary>
        private void StartNewSpin()
        {
            if (_mode != CompassMode.Chaotic) return;

            _mainDirection = _random.Next(2) == 0 ? 1 : -1;
            _legDegreesLeft = 0f;
            _lastLegWasBack = false;
        }

        private void SpinChaotic(float deltaSeconds)
        {
            float timeLeft = deltaSeconds;

            // A leg can end in the middle of a tick, so loop until the time is used.
            for (int guard = 0; timeLeft > 0f && guard < 16; guard++)
            {
                if (_legDegreesLeft <= 0f) StartNextLeg();

                float speed = _compassDegreesPerSecond * _legSpeed;
                float step = Math.Min(speed * timeLeft, _legDegreesLeft);

                Rotate(step * _legDirection);
                _legDegreesLeft -= step;
                timeLeft -= step / speed;
            }
        }

        private void StartNextLeg()
        {
            // After a forward leg there is a good chance of a short step back. Never two backs in a row.
            bool back = !_lastLegWasBack && _random.NextDouble() < BackLegChance;

            _lastLegWasBack = back;
            _legDirection = back ? -_mainDirection : _mainDirection;
            _legSpeed = Lerp(LegSpeedMin, LegSpeedMax, (float)_random.NextDouble());
            _legDegreesLeft = back
                ? Lerp(BackLegMinDegrees, BackLegMaxDegrees, (float)_random.NextDouble())
                : Lerp(ForwardLegMinDegrees, ForwardLegMaxDegrees, (float)_random.NextDouble());
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

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
