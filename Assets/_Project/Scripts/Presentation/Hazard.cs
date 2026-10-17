using SubmarineLDJam.Domain.Levels;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Put on a collider that hurts the submarine. Pick what kind it is.</summary>
    public sealed class Hazard : MonoBehaviour
    {
        [field: SerializeField] public CollisionKind Kind { get; private set; } = CollisionKind.Obstacle;
    }
}
