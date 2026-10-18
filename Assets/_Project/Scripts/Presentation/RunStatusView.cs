using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Run timer. The end of the run is shown by ResultView.
    /// </summary>
    public sealed class RunStatusView : RunView
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private float _monospaceDistance = 1;

        protected override void OnBind() => timerText.text = "";

        private void Update()
        {
            if (!IsBound) return;

            var time = TimeSpan.FromSeconds(Run.ElapsedSeconds);
            var format = @"mm\:ss\.ff";
            
            timerText.text = $"<mspace={_monospaceDistance.ToString(CultureInfo.InvariantCulture)}em>{time.ToString(format)}</mspace>";
        }
    }
}
