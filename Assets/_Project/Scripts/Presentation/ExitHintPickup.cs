using SubmarineLDJam.Domain.Navigation;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Pickup in the maze. Takes it = the exit dot shows on the radar for a while.
    /// Needs a Collider2D with "Is Trigger" on. If the level shows the exit dot all the time
    /// (Exit Hint Mode = Always), the pickup removes itself.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class ExitHintPickup : RunView
    {
        protected override void OnBind()
        {
            if (Run.ExitHint.Mode != ExitHintMode.OnPickup) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsBound || other.GetComponentInParent<SubmarineBody>() == null) return;

            Service.CollectExitHint();
            Destroy(gameObject);
        }
    }
}
