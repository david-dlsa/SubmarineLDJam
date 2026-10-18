using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    public enum AlarmSoundMode
    {
        /// <summary>A short beep, played again and again. Faster as oxygen falls.</summary>
        RepeatedBeep,

        /// <summary>A full alarm sound. Starts once when danger starts, stops when danger ends.</summary>
        ContinuousClip
    }

    /// <summary>
    /// Low oxygen alarm.
    /// Warning level: the siren lights up.
    /// Danger level: the siren blinks, the alarm sound plays and the screen pulses red.
    /// The red is a full-screen UI Image on top of the HUD. No 2D light is needed.
    /// The red and the sound fade in and out, so nothing starts or stops with a click.
    /// </summary>
    public sealed class OxygenAlarmView : RunView
    {
        [Header("Levels (oxygen left, 0..1)")]
        [Tooltip("At or below this, the siren lights up.")]
        [SerializeField, Range(0f, 1f)] private float warningPercent = 0.5f;
        [Tooltip("At or below this, sound + red screen. Keep it lower than Warning.")]
        [SerializeField, Range(0f, 1f)] private float dangerPercent = 0.25f;

        [Header("Siren")]
        [SerializeField] private Image siren;
        [SerializeField] private Sprite sirenOff;
        [SerializeField] private Sprite sirenOn;
        [Tooltip("In danger the siren blinks. OFF = it stays lit.")]
        [SerializeField] private bool blinkInDanger = true;
        [SerializeField, Min(0.05f)] private float blinkSeconds = 0.4f;

        [Header("Sound")]
        [Tooltip("RepeatedBeep: a short beep played again and again. ContinuousClip: a full alarm sound, started once when danger starts.")]
        [SerializeField] private AlarmSoundMode soundMode = AlarmSoundMode.RepeatedBeep;
        [Tooltip("The alarm sound. Empty = silent.")]
        [SerializeField] private AudioClip beepClip;
        [SerializeField, Range(0f, 1f)] private float beepVolume = 1f;
        [Tooltip("ContinuousClip only. ON = the clip starts again when it ends, while in danger. OFF = it plays once per danger.")]
        [SerializeField] private bool loopClip = true;
        [Tooltip("ContinuousClip only. Seconds to go from silent to full volume, and back.")]
        [SerializeField, Min(0f)] private float soundFadeSeconds = 0.5f;
        [Tooltip("RepeatedBeep only. Time between beeps when danger starts.")]
        [SerializeField, Min(0.1f)] private float beepIntervalAtDanger = 1.2f;
        [Tooltip("RepeatedBeep only. Time between beeps when oxygen is empty.")]
        [SerializeField, Min(0.1f)] private float beepIntervalAtEmpty = 0.4f;

        [Header("Red screen")]
        [Tooltip("Full-screen Image, Raycast Target off. Its color is used, only the alpha changes. It is turned off when there is no danger.")]
        [SerializeField] private Image redOverlay;
        [Tooltip("Alpha at the dimmest moment of the pulse.")]
        [SerializeField, Range(0f, 1f)] private float overlayMinAlpha = 0.05f;
        [Tooltip("Alpha at the brightest moment of the pulse.")]
        [SerializeField, Range(0f, 1f)] private float overlayMaxAlpha = 0.4f;
        [Tooltip("ContinuousClip only. Seconds for one pulse (dim, bright, dim).")]
        [SerializeField, Min(0.1f)] private float continuousPulseSeconds = 1f;
        [Tooltip("Seconds for the red to appear and to go away.")]
        [SerializeField, Min(0f)] private float overlayFadeSeconds = 0.4f;

        private AudioSource _audio;
        private bool _clipStarted;
        private float _beatTimer;
        private float _beatLength = 1f;
        private float _overlayFade;
        private float _soundFade;

        private void Awake() => HideOverlay();

        protected override void OnBind()
        {
            _clipStarted = false;
            _beatTimer = 0f;
            _overlayFade = 0f;
            _soundFade = 0f;
            SetSiren(false);
            HideOverlay();
        }

        protected override void OnUnbind()
        {
            StopClipNow();
            SetSiren(false);
            HideOverlay();
        }

        private void Update()
        {
            if (!IsBound) return;

            var run = Run;
            float oxygen = run.Oxygen.Percent;
            bool warning = run.IsRunning && oxygen <= warningPercent;
            bool danger = run.IsRunning && oxygen <= Mathf.Min(dangerPercent, warningPercent);

            // Siren
            bool lit = warning;
            if (danger && blinkInDanger) lit = Mathf.Repeat(Time.time, blinkSeconds * 2f) < blinkSeconds;
            SetSiren(lit);

            // Beat: one pulse per beep (or per Continuous Pulse Seconds). It drives the red wave.
            if (danger)
            {
                _beatTimer -= Time.deltaTime;
                if (_beatTimer <= 0f)
                {
                    if (soundMode == AlarmSoundMode.RepeatedBeep)
                    {
                        Beep();

                        // 0 at the danger level, 1 when empty: beeps get faster.
                        float closeness = dangerPercent > 0f ? 1f - Mathf.Clamp01(oxygen / dangerPercent) : 1f;
                        _beatLength = Mathf.Lerp(beepIntervalAtDanger, beepIntervalAtEmpty, closeness);
                    }
                    else
                    {
                        _beatLength = continuousPulseSeconds;
                    }

                    _beatTimer = _beatLength;
                }
            }
            else
            {
                _beatTimer = 0f; // a beat starts right away when danger comes
            }

            UpdateSound(danger);
            UpdateOverlay(danger);
        }

        // ---------- red screen ----------

        private void UpdateOverlay(bool danger)
        {
            _overlayFade = Step(_overlayFade, danger ? 1f : 0f, overlayFadeSeconds);

            if (_overlayFade <= 0.001f)
            {
                HideOverlay();
                return;
            }

            // Smooth wave: brightest at the beat, dimmest in the middle between two beats.
            float phase = 1f - Mathf.Clamp01(_beatTimer / Mathf.Max(0.01f, _beatLength));
            float wave = 0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            float alpha = Mathf.Lerp(overlayMinAlpha, overlayMaxAlpha, wave) * _overlayFade;

            if (redOverlay == null) return;

            SetOverlayActive(true);
            var color = redOverlay.color;
            color.a = alpha;
            redOverlay.color = color;
        }

        /// <summary>The red object is off whenever there is nothing to show.</summary>
        private void HideOverlay()
        {
            if (redOverlay == null) return;

            var color = redOverlay.color;
            color.a = 0f;
            redOverlay.color = color;
            SetOverlayActive(false);
        }

        private void SetOverlayActive(bool active)
        {
            // If the Image sits on this same object, only the Image is switched, so this script keeps running.
            if (redOverlay.gameObject == gameObject) redOverlay.enabled = active;
            else if (redOverlay.gameObject.activeSelf != active) redOverlay.gameObject.SetActive(active);
        }

        // ---------- sound ----------

        private void UpdateSound(bool danger)
        {
            if (soundMode != AlarmSoundMode.ContinuousClip || beepClip == null)
            {
                StopClipNow();
                return;
            }

            _soundFade = Step(_soundFade, danger ? 1f : 0f, soundFadeSeconds);

            if (danger && !_clipStarted) StartClip();
            if (!_clipStarted) return;

            _audio.volume = beepVolume * _soundFade;

            // Faded out and no danger: stop for real.
            if (!danger && _soundFade <= 0.001f) StopClipNow();
        }

        private void Beep()
        {
            if (beepClip == null) return;

            EnsureAudio();
            _audio.PlayOneShot(beepClip, beepVolume);
        }

        /// <summary>Starts the full alarm sound one time (silent, then it fades in).</summary>
        private void StartClip()
        {
            EnsureAudio();
            _audio.clip = beepClip;
            _audio.loop = loopClip;
            _audio.volume = beepVolume * _soundFade;
            _audio.Play();
            _clipStarted = true;
        }

        private void StopClipNow()
        {
            _soundFade = 0f;
            if (!_clipStarted) return;

            _clipStarted = false;
            if (_audio != null) _audio.Stop();
        }

        private void EnsureAudio()
        {
            if (_audio != null) return;

            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D sound
        }

        // ---------- siren + helpers ----------

        private void SetSiren(bool lit)
        {
            if (siren == null) return;

            if (sirenOn != null || sirenOff != null)
            {
                var sprite = lit ? sirenOn : sirenOff;
                if (sprite != null) siren.sprite = sprite;
                return;
            }

            // No sprites: a dim and a bright color.
            var color = siren.color;
            color.a = lit ? 1f : 0.3f;
            siren.color = color;
        }

        /// <summary>Moves a value toward a target so that 0 to 1 takes this many seconds. 0 seconds = instant.</summary>
        private static float Step(float value, float target, float seconds)
        {
            if (seconds <= 0f) return target;
            return Mathf.MoveTowards(value, target, Time.deltaTime / seconds);
        }
    }
}
