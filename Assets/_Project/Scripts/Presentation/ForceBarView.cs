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

        [Header("Custom sprites (optional)")]
        [Tooltip("Power frames, from empty (first) to full (last). The frame follows the bar going up and back down. Empty = normal bar with marker.")]
        [SerializeField] private Sprite[] powerFrames;
        [Tooltip("Image that shows the frames. Empty = the Image on Track.")]
        [SerializeField] private Image frameImage;
        [Tooltip("After the force is chosen, how long the bar takes to go back to empty. 0 = instant.")]
        [SerializeField, Min(0f)] private float fallSeconds = 0.4f;
        [Tooltip("Frames blend into each other (fade) instead of jumping.")]
        [SerializeField] private bool blendFrames = true;

        private int _levels;
        private float _shown;
        private Image _blendImage;
        private int _dividerLevels;
        private readonly List<GameObject> _dividers = new List<GameObject>();

        protected override void OnBind()
        {
            _levels = Run.Movement.ForceLevels;
            _shown = 0f;

            if (UsesFrames)
            {
                if (frameImage == null && track != null) frameImage = track.GetComponent<Image>();
                if (frameImage != null)
                {
                    frameImage.preserveAspect = true;
                    EnsureBlendImage();
                }
                if (marker != null) marker.gameObject.SetActive(false);
                ClearDividers();
                return;
            }

            if (marker != null) marker.gameObject.SetActive(true);
            BuildDividers();
        }

        private bool UsesFrames => powerFrames != null && powerFrames.Length > 0;

        private void Update()
        {
            if (!IsBound) return;

            var movement = Run.Movement;
            bool selectingForce = movement.Phase == MovementPhase.SelectingForce;
            float pos = selectingForce ? movement.ForcePosition : 0f;

            // Follow the real bar while choosing. After that, glide back to empty instead of jumping.
            float speed = selectingForce || fallSeconds <= 0f ? 20f : 1f / fallSeconds;
            _shown = Mathf.MoveTowards(_shown, pos, speed * Time.deltaTime);

            if (UsesFrames)
            {
                // Position goes 0 -> 1 -> 0, so the frames go up to the full one and back down to the empty one.
                ShowFrames(_shown);
            }
            else
            {
                marker.anchorMin = marker.anchorMax = new Vector2(_shown, 0.5f);
                marker.anchoredPosition = Vector2.zero;
            }

            if (group != null) group.alpha = 1f;

            if (levelLabel != null)
            {
                int level = Mathf.Min(_levels, (int)(pos * _levels) + 1);
                levelLabel.text = selectingForce ? $"FORCE {level}/{_levels}" : "";
            }
        }

        private void ShowFrames(float position)
        {
            if (frameImage == null) return;

            int last = powerFrames.Length - 1;
            float exact = Mathf.Clamp01(position) * last;
            int from = Mathf.Clamp(Mathf.FloorToInt(exact), 0, last);
            int to = Mathf.Min(from + 1, last);
            float blend = exact - from;

            if (!blendFrames || _blendImage == null)
            {
                var single = powerFrames[Mathf.Clamp(Mathf.RoundToInt(exact), 0, last)];
                if (single != null) frameImage.sprite = single;
                return;
            }

            // Base shows the lower frame. The top image shows the next frame, more visible as the bar gets closer to it.
            if (powerFrames[from] != null) frameImage.sprite = powerFrames[from];
            if (powerFrames[to] != null) _blendImage.sprite = powerFrames[to];
            var color = frameImage.color;
            color.a *= blend;
            _blendImage.color = color;
        }

        private void EnsureBlendImage()
        {
            if (!blendFrames || _blendImage != null) return;

            var go = new GameObject("FrameBlend", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(frameImage.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _blendImage = go.GetComponent<Image>();
            _blendImage.raycastTarget = false;
            _blendImage.preserveAspect = true;
            _blendImage.material = frameImage.material;
        }

        private void ClearDividers()
        {
            foreach (var old in _dividers)
                if (old != null) Destroy(old);
            _dividers.Clear();
            _dividerLevels = 0;
        }

        private void BuildDividers()
        {
            if (_dividerLevels == _levels && _dividers.Count > 0) return; // same dividers as before: keep them

            ClearDividers();
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
