using System.Collections;
using System.Collections.Generic;
using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Oxygen;
using SubmarineLDJam.Domain.Runs;
using SubmarineLDJam.UseCases;
using UnityEngine;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Shows the arrow sequence of the oxygen minigame. Each slot turns green (right) or red (wrong).
    /// Add your arrow sprites below. If you only set Up Sprite, the others are made by rotating it.
    /// </summary>
    public sealed class RefillSequenceView : RunView
    {
        [Tooltip("Shown when the puzzle is active, hidden when not.")]
        [SerializeField] private GameObject root;
        [Tooltip("Parent of the slots (use a Horizontal Layout Group).")]
        [SerializeField] private RectTransform slotsParent;
        [Tooltip("Image used as a model for each slot. Keep it inactive in the scene.")]
        [SerializeField] private Image slotTemplate;

        [Header("Arrow sprites")]
        [SerializeField] private Sprite upSprite;
        [SerializeField] private Sprite downSprite;
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite rightSprite;

        [Header("Colors")]
        [SerializeField] private Color pendingColor = Color.white;
        [SerializeField] private Color correctColor = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] private Color wrongColor = new Color(1f, 0.25f, 0.25f);

        [Header("Touch screens only")]
        [Tooltip("Tapping the arrows panel counts as the right arrow for the next slot. Auto = only a finger counts, mouse clicks do nothing. Always = finger and mouse (to test in the editor). Never = off.")]
        [SerializeField] private TouchMode tapToRefill = TouchMode.Auto;

        [Tooltip("How long the last colors stay before the next sequence shows.")]
        [SerializeField] private float resultDelay = 0.25f;

        private readonly List<Image> _slots = new List<Image>();
        private Coroutine _pending;

        protected override void OnBind()
        {
            slotTemplate.gameObject.SetActive(false);
            root.SetActive(false);

            if (TouchInput.IsEnabled(tapToRefill))
                TouchInput.AddTapArea(root.transform as RectTransform, "TapToRefill", () => Service?.TapRefill(),
                    touchOnly: tapToRefill == TouchMode.Auto);
        }

        protected override void OnUnbind()
        {
            CancelPending();
            root.SetActive(false);
        }

        protected override void OnEvent(IDomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case RefillPuzzleActivated _:
                    CancelPending();
                    ApplyState();
                    break;

                case RefillStepResolved step:
                    if (_pending != null)
                    {
                        // Player already pressed into the next sequence: show it now, state has the color.
                        CancelPending();
                        ApplyState();
                    }
                    else if (step.SlotIndex < _slots.Count && _slots[step.SlotIndex] != null)
                    {
                        _slots[step.SlotIndex].color = step.Correct ? correctColor : wrongColor;
                    }
                    break;

                case RefillSequenceCompleted _:
                case RefillSequenceFailed _: // strict mode: show the red slot a moment, then the same sequence again
                case RefillPuzzleDeactivated _:
                    if (_pending == null && isActiveAndEnabled)
                        _pending = StartCoroutine(SettleAfterDelay());
                    break;

                case RunCompleted _:
                case RunFailed _:
                    CancelPending();
                    root.SetActive(false);
                    break;
            }
        }

        private IEnumerator SettleAfterDelay()
        {
            yield return new WaitForSeconds(resultDelay);
            _pending = null;
            ApplyState();
        }

        private void CancelPending()
        {
            if (_pending == null) return;
            StopCoroutine(_pending);
            _pending = null;
        }

        /// <summary>Draw whatever the domain says now.</summary>
        private void ApplyState()
        {
            if (!IsBound) return;

            var refill = Run.Refill;
            if (!refill.IsActive || !Run.IsRunning)
            {
                root.SetActive(false);
                return;
            }

            // Slots are reused, never destroyed: destroying them while selected in the Inspector throws editor errors.
            while (_slots.Count < refill.Sequence.Count)
                _slots.Add(Instantiate(slotTemplate, slotsParent));

            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < refill.Sequence.Count;
                _slots[i].gameObject.SetActive(used);
                if (!used) continue;

                ApplyArrow(_slots[i], refill.Sequence[i]);
                _slots[i].color = ColorFor(refill.Slots[i]);
            }

            root.SetActive(true);
        }

        private void ApplyArrow(Image image, Arrow arrow)
        {
            Sprite specific = null;
            float rotation = 0f;

            switch (arrow)
            {
                case Arrow.Up: specific = upSprite; rotation = 0f; break;
                case Arrow.Right: specific = rightSprite; rotation = -90f; break;
                case Arrow.Down: specific = downSprite; rotation = 180f; break;
                case Arrow.Left: specific = leftSprite; rotation = 90f; break;
            }

            if (specific != null)
            {
                image.sprite = specific;
                image.rectTransform.localEulerAngles = Vector3.zero;
            }
            else
            {
                // No own sprite: use the Up sprite turned to the right side.
                if (upSprite != null) image.sprite = upSprite;
                image.rectTransform.localEulerAngles = new Vector3(0f, 0f, upSprite != null ? rotation : 0f);
            }
        }

        private Color ColorFor(RefillSlotState state)
        {
            switch (state)
            {
                case RefillSlotState.Correct: return correctColor;
                case RefillSlotState.Wrong: return wrongColor;
                default: return pendingColor;
            }
        }
    }
}
