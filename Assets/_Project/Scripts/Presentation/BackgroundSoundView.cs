using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Ambient sound (water) that plays all the time: menu, game, result.
    /// Not tied to any run. Put it on an object that is never turned off.
    /// </summary>
    public sealed class BackgroundSoundView : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

        private void Start()
        {
            if (clip == null) return;

            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.loop = true;
            source.spatialBlend = 0f; // 2D sound
            source.playOnAwake = false;
            source.Play();
        }
    }
}
