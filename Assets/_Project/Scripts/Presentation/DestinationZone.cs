using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Marker for the destination. Needs a Collider2D with "Is Trigger" on.
    /// Color: the destination square on the maze and on the radar. The dot at the radar edge
    /// has its own color: Exit Color on the RadarView.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestinationZone : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.3f, 0.8f, 1f);

        private void Awake()
        {
            if (TryGetComponent<SpriteRenderer>(out var sprite)) sprite.color = color;
        }
    }
}
