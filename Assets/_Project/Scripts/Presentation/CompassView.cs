using SubmarineLDJam.Domain.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// 360° indicator for movement part 1. Needle spins clockwise.
    /// If the level has a heading step, tick marks show where the needle can stop.
    /// </summary>
    public sealed class CompassView : RunView
    {
        [Tooltip("Needle image. Pivot at the bottom center, points up at 0°.")]
        [SerializeField] private RectTransform needle;
        [Tooltip("Optional. Shows the locked direction during part 2.")]
        [SerializeField] private RectTransform lockedNeedle;
        [Tooltip("Optional. Dims the compass when it is not the active part.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("Optional. Parent of the step ticks.")]
        [SerializeField] private RectTransform tickRoot;
        [Tooltip("Optional. Full-size container with the N letter. Turns with the map in HeadingUp mode.")]
        [SerializeField] private RectTransform northMarker;
        [SerializeField] private float tickRadius = 120f;
        [SerializeField] private float tickLength = 12f;
        [SerializeField] private float tickWidth = 3f;
        [SerializeField] private Color tickColor = new Color(1f, 1f, 1f, 0.8f);

        private RectTransform _ticks;
        private float _tickStep;
        private float _shownFacing;
        private bool _snapFacing;

        protected override void OnBind()
        {
            if (lockedNeedle != null) lockedNeedle.gameObject.SetActive(false);
            _snapFacing = true;
            BuildTicks();
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

            // Ticks turn with the needle scale (fixed). The N letter shows where north is.
            if (_ticks != null) _ticks.localEulerAngles = Vector3.zero;
            if (northMarker != null) northMarker.localEulerAngles = new Vector3(0f, 0f, _shownFacing);

            if (group != null) group.alpha = 1f;
        }

        private void BuildTicks()
        {
            float step = Run.Movement.HeadingStepDegrees;
            if (_ticks != null && Mathf.Approximately(step, _tickStep)) return; // same ticks as before: keep them
            if (_ticks != null)
            {
                Destroy(_ticks.gameObject);
                _ticks = null;
            }

            _tickStep = step;
            if (tickRoot == null || step <= 0f) return;

            // Own container, so the ticks can turn as one piece.
            var container = new GameObject("Ticks", typeof(RectTransform));
            _ticks = (RectTransform)container.transform;
            _ticks.SetParent(tickRoot, false);
            _ticks.anchorMin = Vector2.zero;
            _ticks.anchorMax = Vector2.one;
            _ticks.offsetMin = _ticks.offsetMax = Vector2.zero;

            int count = Mathf.Min(360, Mathf.RoundToInt(360f / step));
            for (int i = 0; i < count; i++)
            {
                float degrees = i * step;
                float rad = degrees * Mathf.Deg2Rad;

                var go = new GameObject("Tick", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(_ticks, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(tickWidth, tickLength);
                rect.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * tickRadius;
                rect.localEulerAngles = new Vector3(0f, 0f, -degrees);

                var image = go.GetComponent<Image>();
                image.color = tickColor;
                image.raycastTarget = false;
            }
        }
    }
}
