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

        protected override void OnBind() => timerText.text = "";

        private void Update()
        {
            if (!IsBound) return;

            timerText.text = $"<mspace=0.75em>{Run.ElapsedSeconds:0.0}s</mspace>";
        }
    }
}
