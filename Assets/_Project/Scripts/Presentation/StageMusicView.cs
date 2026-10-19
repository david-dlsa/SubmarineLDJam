using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Background music by stage. Stage number (from 0) picks the track in a repeating order:
    /// with 3 tracks, stages 0,3,6,9 play track 1, stages 1,4,7,10 track 2, stages 2,5,8 track 3.
    /// Restarting or going to the next stage with the same track keeps the music going.
    /// Changing track crossfades. Going back to the stage select fades the music out.
    /// </summary>
    public sealed class StageMusicView : MonoBehaviour
    {
        [SerializeField] private StageFlow flow;
        [Tooltip("Track for stage 0, then stage 1, ... then it repeats.")]
        [SerializeField] private AudioClip[] tracks = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;

        private AudioSource _current;
        private AudioSource _fading;

        private void OnEnable()
        {
            if (flow == null) return;
            flow.StageStarted += OnStageStarted;
            flow.StageLeft += OnStageLeft;
        }

        private void OnDisable()
        {
            if (flow == null) return;
            flow.StageStarted -= OnStageStarted;
            flow.StageLeft -= OnStageLeft;
        }

        private void OnStageStarted(int stageIndex)
        {
            if (tracks == null || tracks.Length == 0) return;

            var clip = tracks[stageIndex % tracks.Length];
            if (clip == null) return;
            if (_current != null && _current.clip == clip) return;

            FadeOutCurrent();
            _current = NewSource(clip);
        }

        private void OnStageLeft() => FadeOutCurrent();

        private void FadeOutCurrent()
        {
            if (_fading != null) Destroy(_fading); // a third track never stacks up
            _fading = _current;
            _current = null;
        }

        private AudioSource NewSource(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D sound
            source.volume = fadeSeconds > 0f ? 0f : volume;
            source.Play();
            return source;
        }

        private void Update()
        {
            float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds * volume : volume;

            if (_current != null)
                _current.volume = Mathf.MoveTowards(_current.volume, volume, step);

            if (_fading != null)
            {
                _fading.volume = Mathf.MoveTowards(_fading.volume, 0f, step);
                if (_fading.volume <= 0.001f)
                {
                    Destroy(_fading);
                    _fading = null;
                }
            }
        }
    }
}
