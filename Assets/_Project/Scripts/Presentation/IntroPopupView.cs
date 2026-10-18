using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>One page of the intro popup of a stage. Written in the Level Definition.</summary>
    [Serializable]
    public struct IntroPage
    {
        public string title;
        [TextArea(2, 8)] public string text;
        [Tooltip("Optional picture for this page.")]
        public Sprite image;
    }

    /// <summary>
    /// Popup shown before a stage starts, the first time it is played. Tells about new mechanics.
    /// The stage only starts when the player presses OK (or Enter / Space) on the last page.
    /// ESC is handled by StageFlow (goes back to the stage select).
    /// </summary>
    public sealed class IntroPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("Optional.")]
        [SerializeField] private Image picture;
        [SerializeField] private Button okButton;
        [Tooltip("Optional. Text of the button.")]
        [SerializeField] private TMP_Text okLabel;
        [Tooltip("Optional. Shows 1/3 when there is more than one page.")]
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private string nextButtonText = "NEXT";
        [SerializeField] private string okButtonText = "OK";

        /// <summary>The player pressed OK on the last page.</summary>
        public event Action Confirmed;

        private IntroPage[] _pages = Array.Empty<IntroPage>();
        private int _page;
        private int _shownFrame;

        private void OnEnable()
        {
            if (okButton != null) okButton.onClick.AddListener(Advance);
        }

        private void OnDisable()
        {
            if (okButton != null) okButton.onClick.RemoveListener(Advance);
        }

        private void Update()
        {
            // The same key press that opened the popup must not close it.
            if (Time.frameCount <= _shownFrame) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                Advance();
        }

        public void Show(IntroPage[] pages)
        {
            _pages = pages ?? Array.Empty<IntroPage>();
            _page = 0;
            _shownFrame = Time.frameCount;
            Refresh();
        }

        private void Advance()
        {
            if (_page < _pages.Length - 1)
            {
                _page++;
                Refresh();
                return;
            }

            Confirmed?.Invoke();
        }

        private void Refresh()
        {
            if (_pages.Length == 0) return;

            var page = _pages[_page];
            bool last = _page == _pages.Length - 1;

            if (titleText != null) titleText.text = page.title;
            if (bodyText != null) bodyText.text = page.text;

            if (picture != null)
            {
                picture.gameObject.SetActive(page.image != null);
                if (page.image != null) picture.sprite = page.image;
            }

            if (okLabel != null) okLabel.text = last ? okButtonText : nextButtonText;

            if (pageText != null)
            {
                pageText.gameObject.SetActive(_pages.Length > 1);
                pageText.text = $"{_page + 1}/{_pages.Length}";
            }
        }
    }
}
