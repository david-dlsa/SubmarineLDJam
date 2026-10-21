using System;
using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Runs;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// The cabin lights go out by themselves (LevelDefinition → Blackouts).
    /// The lights flicker on and off for a moment, then stay dark. When they come back they flicker again.
    /// The dark is a full-screen black UI Image on top of the HUD. Put it BELOW the red alarm overlay,
    /// so the emergency red still shows in the dark.
    /// </summary>
    public sealed class BlackoutView : RunView
    {
        [Header("Dark screen")]
        [Tooltip("Full-screen black Image, Raycast Target off. Only the alpha changes. It is turned off when the lights are on.")]
        [SerializeField] private Image darkOverlay;
        [Tooltip("How dark the cabin gets. 1 = black, nothing visible. Lower = the HUD can still be seen a little.")]
        [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.96f;

        [Header("Flicker")]
        [Tooltip("Seconds of flicker while the lights go out.")]
        [SerializeField, Min(0.05f)] private float flickerOffSeconds = 0.9f;
        [Tooltip("Seconds of flicker while the lights come back.")]
        [SerializeField, Min(0.05f)] private float flickerOnSeconds = 0.7f;
        [Tooltip("How many times the light changes during a flicker. Odd numbers are used, so it ends in the right state.")]
        [SerializeField, Range(1, 15)] private int flickerCount = 5;
        [Tooltip("Small fade on every change, so it is not a hard cut. 0 = hard.")]
        [SerializeField, Range(0f, 0.3f)] private float flickerSmoothSeconds = 0.03f;

        [Header("Sound (optional)")]
        [SerializeField] private AudioClip powerOffClip;
        [SerializeField] private AudioClip powerOnClip;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        private AudioSource _audio;
        private float _alpha;
        private bool _toDark;
        private bool _flickering;
        private float _flickerTime;
        private float _flickerLength;
        private float[] _times = Array.Empty<float>();
        private float[] _levels = Array.Empty<float>();

        private void Awake()
        {
            // Only a picture. It must never catch clicks meant for the buttons under it.
            if (darkOverlay != null) darkOverlay.raycastTarget = false;
            HideOverlay();
        }

        protected override void OnBind()
        {
            _alpha = 0f;
            _toDark = false;
            _flickering = false;
            HideOverlay();

            // A run can start while the lights are already out (not today, but cheap to be right).
            if (Run.Blackout.IsDark)
            {
                _toDark = true;
                _alpha = darkAlpha;
            }
        }

        protected override void OnUnbind()
        {
            _flickering = false;
            _alpha = 0f;
            HideOverlay();
        }

        protected override void OnEvent(IDomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case BlackoutStarted _:
                    BeginFlicker(true, flickerOffSeconds);
                    Play(powerOffClip);
                    break;

                case BlackoutEnded _:
                    BeginFlicker(false, flickerOnSeconds);
                    Play(powerOnClip);
                    break;
            }
        }

        private void Update()
        {
            if (!IsBound) return;

            float target = _toDark ? darkAlpha : 0f;

            if (_flickering)
            {
                _flickerTime += Time.deltaTime;

                // How many changes already happened? Odd = the opposite of where it started.
                int changes = 0;
                while (changes < _times.Length && _flickerTime >= _times[changes]) changes++;

                bool startedDark = !_toDark;
                bool dark = startedDark ^ (changes % 2 == 1);
                target = dark ? darkAlpha * _levels[Mathf.Min(changes, _levels.Length - 1)] : 0f;

                if (_flickerTime >= _flickerLength) _flickering = false;
            }

            _alpha = flickerSmoothSeconds <= 0f
                ? target
                : Mathf.MoveTowards(_alpha, target, Time.deltaTime / flickerSmoothSeconds * Mathf.Max(0.01f, darkAlpha));

            ApplyOverlay();
        }

        /// <summary>Plans the flicker: a few random moments where the light changes. The last change leaves the right state.</summary>
        private void BeginFlicker(bool toDark, float seconds)
        {
            _toDark = toDark;
            _flickering = true;
            _flickerTime = 0f;
            _flickerLength = seconds;

            int changes = flickerCount % 2 == 0 ? flickerCount + 1 : flickerCount;
            _times = new float[changes];
            for (int i = 0; i < changes; i++) _times[i] = Random.Range(0.03f, seconds * 0.95f);
            Array.Sort(_times);

            // How dark each dark moment is. The last one (the final state) is full.
            _levels = new float[changes + 1];
            for (int i = 0; i < _levels.Length; i++) _levels[i] = Random.Range(0.4f, 1f);
            _levels[changes] = 1f;
        }

        private void ApplyOverlay()
        {
            if (darkOverlay == null) return;

            if (_alpha <= 0.002f)
            {
                HideOverlay();
                return;
            }

            SetOverlayActive(true);
            var color = darkOverlay.color;
            color.a = _alpha;
            darkOverlay.color = color;
        }

        private void HideOverlay()
        {
            if (darkOverlay == null) return;

            var color = darkOverlay.color;
            color.a = 0f;
            darkOverlay.color = color;
            SetOverlayActive(false);
        }

        private void SetOverlayActive(bool active)
        {
            // If the Image sits on this same object, only the Image is switched, so this script keeps running.
            if (darkOverlay.gameObject == gameObject) darkOverlay.enabled = active;
            else if (darkOverlay.gameObject.activeSelf != active) darkOverlay.gameObject.SetActive(active);
        }

        private void Play(AudioClip clip)
        {
            if (clip == null) return;

            if (_audio == null)
            {
                _audio = GetComponent<AudioSource>();
                if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f; // 2D sound
            }

            _audio.PlayOneShot(clip, soundVolume);
        }
    }
}
