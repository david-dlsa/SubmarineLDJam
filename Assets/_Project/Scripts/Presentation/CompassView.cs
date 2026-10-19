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
        [Tooltip("Optional. The wheel with N, S, E, W (full size, centered). It turns together with the radar map, so N always points to where north really is. Put the N at the top of the image.")]
        [SerializeField] private RectTransform northMarker;
        [Header("Click / tap")]
        [Tooltip("Click or tap the compass = the same as pressing Space. Works with mouse and finger.")]
        [SerializeField] private bool clickToConfirm = true;
        [Tooltip("Optional. The area that can be clicked. Empty = the parent of the Needle (the compass dial).")]
        [SerializeField] private RectTransform tapArea;

        [Tooltip("Optional. The radar the wheel follows. Empty = found by itself.")]
        [SerializeField] private RadarView radar;

        private float _shownFacing;
        private bool _snapFacing;

        protected override void OnBind()
        {
            if (lockedNeedle != null) lockedNeedle.gameObject.SetActive(false);
            _snapFacing = true;
            if (clickToConfirm)
            {
                var area = tapArea != null ? tapArea : needle != null ? needle.parent as RectTransform : transform as RectTransform;
                TouchInput.AddTapArea(area, "TapToConfirm", () => Service?.ConfirmMovement());
            }
            if (radar == null) radar = FindFirstObjectByType<RadarView>(FindObjectsInactive.Include);
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

            // The wheel turns exactly like the radar map (same angle, same smooth turn), so N shows where north is.
            // With no radar it falls back to the facing of the movement module.
            if (northMarker != null)
            {
                float angle = radar != null ? radar.ReferenceHeadingDegrees : _shownFacing;
                northMarker.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            if (group != null) group.alpha = 1f;
        }
    }
}
