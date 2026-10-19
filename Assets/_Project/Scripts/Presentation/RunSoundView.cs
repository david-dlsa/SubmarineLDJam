using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Runs;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Plays one sound for each thing that happens in the run.
    /// Leave a clip empty and that sound is just silent.
    /// Keep this on an object that stays on, so end sounds (victory, game over) are not cut.
    /// </summary>
    public sealed class RunSoundView : RunView
    {
        [Header("Movement")]
        [Tooltip("First Space: the compass angle is locked.")]
        [SerializeField] private AudioClip clickRotation;
        [Tooltip("When the impulse is fired (second Space).")]
        [SerializeField] private AudioClip movement;
        [Tooltip("When the submarine hits a wall or thing.")]
        [SerializeField] private AudioClip metalCollide;

        [Header("Oxygen refill")]
        [SerializeField] private AudioClip sequenceCorrect;
        [SerializeField] private AudioClip sequenceWrong;

        [Header("Sonar")]
        [SerializeField] private AudioClip sonar;

        [Header("End of run")]
        [Tooltip("Stage finished without dying.")]
        [SerializeField] private AudioClip victory;
        [Tooltip("Oxygen ran out.")]
        [SerializeField] private AudioClip gameOver;

        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private AudioSource _audio;

        protected override void OnEvent(IDomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case HeadingLocked _: Play(clickRotation); break;
                case ImpulseFired _: Play(movement); break;
                case CollisionRegistered _: Play(metalCollide); break;
                case RefillStepResolved step: Play(step.Correct ? sequenceCorrect : sequenceWrong); break;
                case SonarPinged _: Play(sonar); break;
                case RunCompleted _: Play(victory); break;
                case RunFailed _: Play(gameOver); break;
            }
        }

        private void Play(AudioClip clip)
        {
            if (clip == null) return;

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f; // 2D sound
            }

            _audio.PlayOneShot(clip, volume);
        }
    }
}
