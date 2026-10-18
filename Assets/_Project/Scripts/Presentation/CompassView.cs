using SubmarineLDJam.Domain.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// 360° indicator for movement part 1. Needle spins clockwise.
    /// Tick marks are not made here. Draw them in your own image.
    /// </summary>
    public sealed class CompassView : RunView
    {
        [Tooltip("Needle image. Pivot at the bottom center, points up at 0°.")]
        [SerializeField] private RectTransform needle;
        [Tooltip("Optional. Shows the locked direction during part 2.")]
        [SerializeField] private RectTransform lockedNeedle;
        [Tooltip("Optional. Dims the compass when it is not the active part.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("Optional. Full-size container with the N letter. Turns with the map in HeadingUp mode.")]
        [SerializeField] private RectTransform northMarker;

        private float _shownFacing;
        private bool _snapFacing;

        protected override void OnBind()
        {
            if (lockedNeedle != null) lockedNeedle.gameObject.SetActive(false);
            _snapFacing = true;
        }

        private void Update()
        {
            if (!IsBound) return;

            var movement = Run.Movement;
            bool selectingHeading = movement.Phase == MovementPhase.SelectingHeading;

            // The needle is measured from the submarine front, which is always "up" for the player.
            // So after a turn, "left" on the compass is left of the new front.
            // The dial stays where it stopped while the force is chosen, and spins again from that same point.
            // Only the N letter follows the turn, and it changes when the movement is done.
            if (_snapFacing || selectingHeading)
            {
                _shownFacing = movement.Facing.Degrees;
                _snapFacing = false;
            }

            needle.localEulerAngles = new Vector3(0f, 0f, -movement.CurrentHeading.Degrees);

            if (lockedNeedle != null)
            {
                lockedNeedle.gameObject.SetActive(!selectingHeading);
                lockedNeedle.localEulerAngles = new Vector3(0f, 0f, -movement.LockedNeedleDegrees);
            }

            // The N letter shows where north is.
            if (northMarker != null) northMarker.localEulerAngles = new Vector3(0f, 0f, _shownFacing);

            if (group != null) group.alpha = 1f;
        }
    }
}
