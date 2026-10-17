using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Oxygen bar. Image set to "Filled" (Horizontal). Gets smaller as oxygen falls.</summary>
    public sealed class OxygenBarView : RunView
    {
        [Tooltip("Image with Image Type = Filled.")]
        [SerializeField] private Image fill;
        [SerializeField] private Text label;
        [SerializeField] private Color fullColor = new Color(0.2f, 0.8f, 1f);
        [SerializeField] private Color lowColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Color while oxygen falls faster (after a hit).")]
        [SerializeField] private Color boostedColor = new Color(1f, 0.6f, 0.1f);

        private void Update()
        {
            if (!IsBound) return;

            var oxygen = Run.Oxygen;

            fill.fillAmount = oxygen.Percent;
            fill.color = oxygen.IsBoosted ? boostedColor : Color.Lerp(lowColor, fullColor, oxygen.Percent);

            if (label != null) label.text = $"O2 {Mathf.CeilToInt(oxygen.Percent * 100f)}%";
        }
    }
}
