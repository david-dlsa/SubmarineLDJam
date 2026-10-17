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
        [Tooltip("Radar. Compass shows directions as the radar screen shows them. Found by itself if empty.")]
        [SerializeField] private RadarView radar;
        [SerializeField] private float tickRadius = 120f;
        [SerializeField] private float tickLength = 12f;
        [SerializeField] private float tickWidth = 3f;
        [SerializeField] private Color tickColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.4f;

        private RectTransform _ticks;
        private float _tickStep;

        protected override void OnBind()
        {
            if (radar == null) radar = FindFirstObjectByType<RadarView>(FindObjectsInactive.Include);
            if (lockedNeedle != null) lockedNeedle.gameObject.SetActive(false);
            BuildTicks();
        }

        private void Update()
        {
            if (!IsBound) return;

            var movement = Run.Movement;
            bool selectingHeading = movement.Phase == MovementPhase.SelectingHeading;

            // Radar turns the map around the submarine? Then "up" on screen is the submarine front,
            // not north. Show every direction relative to that, so left on screen is left.
            float reference = radar != null ? radar.ReferenceHeadingDegrees : 0f;

            needle.localEulerAngles = new Vector3(0f, 0f, -(movement.CurrentHeading.Degrees - reference));

            if (lockedNeedle != null)
            {
                lockedNeedle.gameObject.SetActive(!selectingHeading);
                lockedNeedle.localEulerAngles = new Vector3(0f, 0f, -(movement.LockedHeading.Degrees - reference));
            }

            // Ticks and the N letter are fixed to the world, so they turn the other way.
            if (_ticks != null) _ticks.localEulerAngles = new Vector3(0f, 0f, reference);
            if (northMarker != null) northMarker.localEulerAngles = new Vector3(0f, 0f, reference);

            if (group != null) group.alpha = selectingHeading ? 1f : inactiveAlpha;
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
