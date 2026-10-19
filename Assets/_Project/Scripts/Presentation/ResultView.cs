using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>What the result screen shows. Plain data, the view knows nothing else.</summary>
    public readonly struct ResultCard
    {
        public bool Completed { get; }
        public float Seconds { get; }
        public int Stars { get; }
        public float BestSeconds { get; }
        public bool NewBest { get; }
        public bool HasNext { get; }

        public ResultCard(bool completed, float seconds, int stars, float bestSeconds, bool newBest, bool hasNext)
        {
            Completed = completed;
            Seconds = seconds;
            Stars = stars;
            BestSeconds = bestSeconds;
            NewBest = newBest;
            HasNext = hasNext;
        }
    }

    /// <summary>
    /// End of a stage. Completed: time, stars, RESTART, NEXT (if there is a next stage), STAGES.
    /// Not completed: RESTART and STAGES only.
    /// Keys: Enter = NEXT (or RESTART when there is no NEXT). R and ESC are handled by StageFlow.
    /// Put it inside the radar screen, same place as the stage select.
    /// </summary>
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] private Button restartButton;
        [Tooltip("Shown only when the stage was completed and there is a next stage.")]
        [SerializeField] private Button nextButton;
        [SerializeField] private Button stagesButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text timeText;
        [Tooltip("Optional. Stars as text, like ***.")]
        [SerializeField] private TMP_Text starsText;
        [Tooltip("Optional. Up to 3 star images. They are colored on or off. You can also give them sprites.")]
        [SerializeField] private Image[] starImages;
        [SerializeField] private Color starOnColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color starOffColor = new Color(1f, 1f, 1f, 0.2f);
        [Tooltip("Optional. Best time, or NEW BEST.")]
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private string completedTitle = "ARRIVED!";
        [SerializeField] private string failedTitle = "OUT OF OXYGEN";

        public event Action RestartRequested;
        public event Action NextRequested;
        public event Action StagesRequested;

        private ResultCard _card;

        private void OnEnable()
        {
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (stagesButton != null) stagesButton.onClick.AddListener(Stages);
        }

        private void OnDisable()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (stagesButton != null) stagesButton.onClick.RemoveListener(Stages);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (CanGoNext) Next();
                else Restart();
            }
        }

        private bool CanGoNext => _card.Completed && _card.HasNext;

        public void Show(ResultCard card)
        {
            _card = card;

            if (titleText != null) titleText.text = card.Completed ? completedTitle : failedTitle;
            if (timeText != null) timeText.text = $"{TimeFormatterHelper.FormatTime(card.Seconds)}";

            int stars = card.Completed ? Mathf.Clamp(card.Stars, 0, 3) : 0;
            if (starsText != null)
            {
                starsText.gameObject.SetActive(card.Completed);
                starsText.text = new string('*', stars) + new string('-', 3 - stars);
            }

            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    if (starImages[i] == null) continue;
                    starImages[i].gameObject.SetActive(card.Completed);
                    starImages[i].color = i < stars ? starOnColor : starOffColor;
                }
            }

            if (bestText != null)
            {
                bestText.gameObject.SetActive(card.Completed);
                bestText.text = card.NewBest ? "NEW BEST!" : $"BEST {card.BestSeconds:0.0}s";
            }

            if (nextButton != null) nextButton.gameObject.SetActive(CanGoNext);
        }

        private void Restart() => RestartRequested?.Invoke();

        private void Next()
        {
            if (CanGoNext) NextRequested?.Invoke();
        }

        private void Stages() => StagesRequested?.Invoke();
    }
}
