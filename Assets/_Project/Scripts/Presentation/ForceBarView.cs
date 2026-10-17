using System.Collections.Generic;
using SubmarineLDJam.Domain.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Bar indicator for movement part 2. Marker goes back and forth.
    /// Dividers show the force levels (LevelDefinition.forceLevels).
    /// </summary>
    public sealed class ForceBarView : RunView
    {
        [Tooltip("The bar background.")]
        [SerializeField] private RectTransform track;
        [Tooltip("Child of the track. Anchors are moved by this script.")]
        [SerializeField] private RectTransform marker;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Optional. Shows the level under the marker.")]
        [SerializeField] private Text levelLabel;
        [SerializeField] private Color dividerColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private float dividerWidth = 2f;
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.4f;

        private int _levels;
        private int _dividerLevels;
        private readonly List<GameObject> _dividers = new List<GameObject>();

        protected override void OnBind()
        {
            _levels = Run.Movement.ForceLevels;
            BuildDividers();
        }

        private void Update()
        {
            if (!IsBound) return;

            var movement = Run.Movement;
            bool selectingForce = movement.Phase == MovementPhase.SelectingForce;
            float pos = selectingForce ? movement.ForcePosition : 0f;

            marker.anchorMin = marker.anchorMax = new Vector2(pos, 0.5f);
            marker.anchoredPosition = Vector2.zero;

            if (group != null) group.alpha = selectingForce ? 1f : inactiveAlpha;

            if (levelLabel != null)
            {
                int level = Mathf.Min(_levels, (int)(pos * _levels) + 1);
                levelLabel.text = selectingForce ? $"FORCE {level}/{_levels}" : "";
            }
        }

        private void BuildDividers()
        {
            if (_dividerLevels == _levels && _dividers.Count > 0) return; // same dividers as before: keep them

            foreach (var old in _dividers)
                if (old != null) Destroy(old);
            _dividers.Clear();
            _dividerLevels = _levels;

            for (int i = 1; i < _levels; i++)
            {
                float x = (float)i / _levels;
                var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
                _dividers.Add(go);
                var rect = (RectTransform)go.transform;
                rect.SetParent(track, false);
                rect.SetAsFirstSibling(); // behind the marker
                rect.anchorMin = new Vector2(x, 0f);
                rect.anchorMax = new Vector2(x, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(dividerWidth, 0f);
                rect.anchoredPosition = Vector2.zero;

                var image = go.GetComponent<Image>();
                image.color = dividerColor;
                image.raycastTarget = false;
            }
        }
    }
}
