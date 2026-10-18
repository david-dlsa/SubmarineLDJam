using System;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Domain.Oxygen;
using SubmarineLDJam.Domain.Runs;

namespace SubmarineLDJam.UseCases
{
    /// <summary>
    /// Door between Unity and the domain. Unity calls these commands,
    /// domain events come out through EventPublished.
    /// </summary>
    public sealed class RunService
    {
        public SubmarineRun Run { get; private set; }

        public event Action<Domain.Common.IDomainEvent> EventPublished;

        public void StartRun(LevelRules rules, Random random = null)
        {
            Run = new SubmarineRun(rules, random);
        }

        public void Tick(float deltaSeconds)
        {
            if (Run == null) return;
            Run.Tick(deltaSeconds);
            Publish();
        }

        public void ConfirmMovement()
        {
            if (Run == null) return;
            Run.ConfirmMovement();
            Publish();
        }

        public void FinishMovement()
        {
            if (Run == null) return;
            Run.FinishMovement();
            Publish();
        }

        public void PingSonar()
        {
            if (Run == null) return;
            Run.PingSonar();
            Publish();
        }

        public void CollectExitHint()
        {
            if (Run == null) return;
            Run.CollectExitHint();
            Publish();
        }

        public void PressArrow(Arrow arrow)
        {
            if (Run == null) return;
            Run.PressArrow(arrow);
            Publish();
        }

        public void RegisterCollision(CollisionKind kind)
        {
            if (Run == null) return;
            Run.RegisterCollision(kind);
            Publish();
        }

        public void RegisterCollision(CollisionKind kind, CollisionPenalty penalty, bool ignoreCooldown = false)
        {
            if (Run == null) return;
            Run.RegisterCollision(kind, penalty, ignoreCooldown);
            Publish();
        }

        public void ReachDestination()
        {
            if (Run == null) return;
            Run.ReachDestination();
            Publish();
        }

        private void Publish()
        {
            foreach (var domainEvent in Run.PullEvents())
                EventPublished?.Invoke(domainEvent);
        }
    }
}
