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
        [Tooltip("When the impulse is fired (second Space). One of these is picked each time, never the same twice in a row.")]
        [SerializeField] private AudioClip[] movementClips = new AudioClip[0];
        [Tooltip("When the submarine hits a wall or thing.")]
        [SerializeField] private AudioClip metalCollide;

        [Header("Oxygen refill")]
        [Tooltip("Arrow key press. Not played on the press that ends the sequence (Correct / Wrong plays there).")]
        [SerializeField] private AudioClip sequence;
        [Tooltip("The whole sequence was right.")]
        [SerializeField] private AudioClip sequenceCorrect;
        [Tooltip("Strict mode: a mistake, the sequence starts again. Normal mode: the sequence ended with mistakes.")]
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
        private bool _strictRefill;
        private int _lastMovement = -1;

        protected override void OnBind()
        {
            _strictRefill = Run != null && Run.Refill.RestartOnMistake;
        }

        protected override void OnEvent(IDomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case HeadingLocked _: Play(clickRotation); break;
                case ImpulseFired _: Play(PickMovement()); break;
                case CollisionRegistered _: Play(metalCollide); break;
                case RefillStepResolved step:
                    // One sound per press. The last slot and a strict mistake belong to Correct / Wrong, so no key sound there.
                    bool lastSlot = step.SlotIndex >= Run.Refill.Sequence.Count - 1;
                    if (_strictRefill ? step.Correct && !lastSlot : !lastSlot) Play(sequence);
                    break;
                case RefillSequenceCompleted done:
                    Play(_strictRefill || done.Mistakes == 0 ? sequenceCorrect : sequenceWrong);
                    break;
                case RefillSequenceFailed _: Play(sequenceWrong); break;
                case SonarPinged _: Play(sonar); break;
                case RunCompleted _: Play(victory); break;
                case RunFailed _: Play(gameOver); break;
            }
        }

        private AudioClip PickMovement()
        {
            if (movementClips == null || movementClips.Length == 0) return null;
            if (movementClips.Length == 1) return movementClips[0];

            int index;
            do index = Random.Range(0, movementClips.Length);
            while (index == _lastMovement);

            _lastMovement = index;
            return movementClips[index];
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
