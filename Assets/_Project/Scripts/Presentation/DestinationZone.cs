using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Marker for the destination. Needs a Collider2D with "Is Trigger" on.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestinationZone : MonoBehaviour { }
}
