using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Click sound for every Button in the scene (menu, result, popup).
    /// Finds the buttons once at start, even the ones that are turned off.
    /// Buttons made later by code are not covered. Call <see cref="Hook"/> on them.
    /// </summary>
    public sealed class UiSoundView : MonoBehaviour
    {
        [SerializeField] private AudioClip click;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private AudioSource _audio;

        private void Start()
        {
            if (click == null) return;

            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (button.GetComponent<ButtonSound>() != null) continue; // has its own sound
                Hook(button);
            }
        }

        public void Hook(Button button)
        {
            if (button != null) button.onClick.AddListener(PlayClick);
        }

        private void PlayClick()
        {
            if (click == null) return;

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f; // 2D sound
            }

            _audio.PlayOneShot(click, volume);
        }
    }
}
