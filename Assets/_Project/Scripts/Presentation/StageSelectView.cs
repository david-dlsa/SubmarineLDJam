using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace SubmarineLDJam.Presentation
{
    /// <summary>What the selector shows for one stage. Plain data, the view knows nothing else.</summary>
    public readonly struct StageCard
    {
        public string Title { get; }
        public bool Unlocked { get; }
        public bool Completed { get; }
        public float BestSeconds { get; }
        public int Stars { get; }

        public StageCard(string title, bool unlocked, bool completed, float bestSeconds, int stars)
        {
            Title = title;
            Unlocked = unlocked;
            Completed = completed;
            BestSeconds = bestSeconds;
            Stars = stars;
        }
    }

    /// <summary>
    /// Stage picker: arrows change the stage, PLAY starts it.
    /// First stage has no left arrow, last stage has no right arrow.
    /// A locked stage cannot be played. A completed stage shows best time and stars.
    /// Keys: Left / Right arrows to change, Enter to play.
    /// </summary>
    public sealed class StageSelectView : MonoBehaviour
    {
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button returnButton;
        [Tooltip("Stage name, e.g. STAGE 1.")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("Best time and stars, or LOCKED.")]
        [SerializeField] private TMP_Text recordText;
        [SerializeField] private string lockedText = "LOCKED";
        [SerializeField] private string notCompletedText = "";

        /// <summary>Player pressed PLAY on an open stage. Gives the stage index.</summary>
        public event Action<int> PlayRequested;

        public event Action ReturnRequested;

        private IReadOnlyList<StageCard> _cards = Array.Empty<StageCard>();
        private int _index;

        public int SelectedIndex => _index;

        private void OnEnable()
        {
            if (leftButton != null) leftButton.onClick.AddListener(Previous);
            if (rightButton != null) rightButton.onClick.AddListener(Next);
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (returnButton != null) returnButton.onClick.AddListener(Return);
            Refresh();
        }

        private void OnDisable()
        {
            if (leftButton != null) leftButton.onClick.RemoveListener(Previous);
            if (rightButton != null) rightButton.onClick.RemoveListener(Next);
            if (playButton != null) playButton.onClick.RemoveListener(Play);
            if (returnButton != null) returnButton.onClick.RemoveListener(Return);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame) Previous();
            if (keyboard.rightArrowKey.wasPressedThisFrame) Next();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Play();
        }

        public void Show(IReadOnlyList<StageCard> cards, int selectedIndex)
        {
            _cards = cards ?? Array.Empty<StageCard>();
            _index = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, _cards.Count - 1));
            Refresh();
        }

        private void Previous()
        {
            if (_index <= 0) return;
            _index--;
            Refresh();
        }

        private void Next()
        {
            if (_index >= _cards.Count - 1) return;
            _index++;
            Refresh();
        }

        private void Play()
        {
            if (_cards.Count == 0 || !_cards[_index].Unlocked) return;
            PlayRequested?.Invoke(_index);
        }

        private void Return() => ReturnRequested?.Invoke();

        private void Refresh()
        {
            if (_cards.Count == 0)
            {
                if (titleText != null) titleText.text = "";
                if (recordText != null) recordText.text = "";
                if (leftButton != null) leftButton.gameObject.SetActive(false);
                if (rightButton != null) rightButton.gameObject.SetActive(false);
                if (playButton != null) playButton.interactable = false;
                return;
            }

            var card = _cards[_index];

            if (titleText != null) titleText.text = card.Title;
            if (recordText != null) recordText.text = DescribeRecord(card);

            // No arrow where there is no stage to go to.
            if (leftButton != null) leftButton.gameObject.SetActive(_index > 0);
            if (rightButton != null) rightButton.gameObject.SetActive(_index < _cards.Count - 1);
            if (playButton != null) playButton.interactable = card.Unlocked;
        }

        private string DescribeRecord(StageCard card)
        {
            if (!card.Unlocked) return lockedText;
            if (!card.Completed) return notCompletedText;

            int stars = Mathf.Clamp(card.Stars, 0, 3);
            return $"BEST\n{TimeFormatterHelper.FormatTime(card.BestSeconds)}   {new string('*', stars)}{new string('-', 3 - stars)}";
        }
    }
}
