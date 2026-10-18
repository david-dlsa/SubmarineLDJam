using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Domain.Navigation;

namespace SubmarineLDJam.Domain.Runs
{
    public enum RunFailureReason { OxygenDepleted }

    public sealed class HeadingLocked : IDomainEvent
    {
        public Heading Heading { get; }
        public HeadingLocked(Heading heading) => Heading = heading;
    }

    public sealed class ImpulseFired : IDomainEvent
    {
        public Impulse Impulse { get; }
        public ImpulseFired(Impulse impulse) => Impulse = impulse;
    }

    public sealed class CollisionRegistered : IDomainEvent
    {
        public CollisionKind Kind { get; }
        public CollisionPenalty Penalty { get; }

        public CollisionRegistered(CollisionKind kind, CollisionPenalty penalty)
        {
            Kind = kind;
            Penalty = penalty;
        }
    }

    public sealed class RefillPuzzleActivated : IDomainEvent { }
    public sealed class RefillPuzzleDeactivated : IDomainEvent { }

    /// <summary>One arrow was pressed (right or wrong).</summary>
    public sealed class RefillStepResolved : IDomainEvent
    {
        public int SlotIndex { get; }
        public bool Correct { get; }

        public RefillStepResolved(int slotIndex, bool correct)
        {
            SlotIndex = slotIndex;
            Correct = correct;
        }
    }

    /// <summary>Whole sequence used. Oxygen came back (less if there were mistakes).</summary>
    public sealed class RefillSequenceCompleted : IDomainEvent
    {
        public float Restored { get; }
        public int Mistakes { get; }

        public RefillSequenceCompleted(float restored, int mistakes)
        {
            Restored = restored;
            Mistakes = mistakes;
        }
    }

    public sealed class SonarPinged : IDomainEvent { }

    public sealed class SonarPingRejected : IDomainEvent
    {
        public float CooldownRemaining { get; }
        public SonarPingRejected(float cooldownRemaining) => CooldownRemaining = cooldownRemaining;
    }

    public sealed class RunCompleted : IDomainEvent
    {
        public float ElapsedSeconds { get; }
        public int Stars { get; }

        public RunCompleted(float elapsedSeconds, int stars)
        {
            ElapsedSeconds = elapsedSeconds;
            Stars = stars;
        }
    }

    public sealed class RunFailed : IDomainEvent
    {
        public RunFailureReason Reason { get; }
        public RunFailed(RunFailureReason reason) => Reason = reason;
    }

    /// <summary>The cabin lights went out. They come back after this many seconds.</summary>
    public sealed class BlackoutStarted : IDomainEvent
    {
        public float DurationSeconds { get; }
        public BlackoutStarted(float durationSeconds) => DurationSeconds = durationSeconds;
    }

    /// <summary>The cabin lights came back.</summary>
    public sealed class BlackoutEnded : IDomainEvent
    {
    }

    /// <summary>The submarine stopped. The player can start a new movement.</summary>
    public sealed class MovementFinished : IDomainEvent
    {
    }

    /// <summary>The submarine took an exit hint pickup. The exit dot shows for this many seconds.</summary>
    public sealed class ExitHintCollected : IDomainEvent
    {
        public float Seconds { get; }

        public ExitHintCollected(float seconds)
        {
            Seconds = seconds;
        }
    }
}
