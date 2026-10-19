using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Oxygen bar. Image set to "Filled" (Horizontal). Gets smaller as oxygen falls.</summary>
    public sealed class OxygenBarView : RunView
    {
        [Tooltip("Image with Image Type = Filled.")]
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color fullColor = new Color(0.2f, 0.8f, 1f);
        [SerializeField] private Color lowColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Color while oxygen falls faster (after a hit).")]
        [SerializeField] private Color boostedColor = new Color(1f, 0.6f, 0.1f);

        [Header("Touch screens only")]
        [Tooltip("On a touch device, tapping the oxygen bar answers the oxygen minigame: each tap fills one slot with the right arrow. A full sequence of taps (3 by default) restores the oxygen. Auto = only when there is a touch screen. Always = test with the mouse.")]
        [SerializeField] private TouchMode tapToRefill = TouchMode.Auto;
        [Tooltip("Optional. The area that can be tapped. Empty = the Fill image.")]
        [SerializeField] private RectTransform tapArea;

        protected override void OnBind()
        {
            if (!TouchInput.IsEnabled(tapToRefill)) return;
            TouchInput.AddTapArea(tapArea != null ? tapArea : fill.rectTransform, "TapToRefill", () => Service?.TapRefill());
        }

        private void Update()
        {
            if (!IsBound) return;

            var oxygen = Run.Oxygen;

            fill.fillAmount = oxygen.Percent;
            fill.color = oxygen.IsBoosted ? boostedColor : Color.Lerp(lowColor, fullColor, oxygen.Percent);

            if (label != null) label.text = $"{Mathf.CeilToInt(oxygen.Percent * 100f)}%";
        }
    }
}
