using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Put this on a Button that needs its own click sound (for example PLAY).
    /// The generic click of UiSoundView is skipped for this button.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonSound : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private void Start() => GetComponent<Button>().onClick.AddListener(PlayClip);

        private void OnDestroy()
        {
            if (TryGetComponent<Button>(out var button)) button.onClick.RemoveListener(PlayClip);
        }

        private void PlayClip()
        {
            if (clip == null) return;

            // The click often turns this button off (PLAY hides the menu), and an off object cannot play.
            // So the sound lives on its own temporary object.
            var go = new GameObject("ButtonSound");
            var source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f; // 2D sound
            source.clip = clip;
            source.volume = volume;
            source.Play();
            Destroy(go, clip.length + 0.1f);
        }
    }
}
