using SubmarineLDJam.Domain.Runs;
using SubmarineLDJam.UseCases;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Timer and end message. Press R to leave the run.
    /// With StageFlow, R goes back to stage select. Without it, R reloads the scene.
    /// </summary>
    public sealed class RunStatusView : RunView
    {
        [SerializeField] private Text timerText;
        [SerializeField] private Text resultText;

        protected override void OnBind()
        {
            resultText.gameObject.SetActive(false);
            timerText.text = "";
        }

        private void Update()
        {
            if (!IsBound) return;

            var run = Run;
            timerText.text = $"{run.ElapsedSeconds:0.0}s";

            switch (run.Status)
            {
                case RunStatus.Completed:
                    resultText.gameObject.SetActive(true);
                    resultText.text = $"ARRIVED!\n{new string('*', run.Stars)}  ({run.Stars}/3)\n{run.ElapsedSeconds:0.0}s\n\nPress R or ESC";
                    break;
                case RunStatus.Failed:
                    resultText.gameObject.SetActive(true);
                    resultText.text = "OUT OF OXYGEN\n\nPress R or ESC";
                    break;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                // Someone (StageFlow) handles it? If not, old behavior: reload the scene.
                if (!RunContext.RequestExit())
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
