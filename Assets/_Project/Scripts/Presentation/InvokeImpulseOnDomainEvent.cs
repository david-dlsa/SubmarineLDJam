using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Domain.Runs;
using SubmarineLDJam.Presentation;
using Unity.Cinemachine;
using UnityEngine;

public class InvokeImpulseOnDomainEvent : RunView
{
    [SerializeField] private CinemachineImpulseSource _collisionImpulse;
    [SerializeField] private CinemachineImpulseSource _explosionImpulse;

    protected override void OnEvent(IDomainEvent domainEvent)
    {
        if(domainEvent is not CollisionRegistered collision)
            return;

        switch (collision.Kind)
        {
            case CollisionKind.Wall:
            case CollisionKind.Obstacle:
                _collisionImpulse.GenerateImpulse();
                break;
            
            case CollisionKind.Bomb:
                _explosionImpulse.GenerateImpulse();
                break;
        }
    }
}
