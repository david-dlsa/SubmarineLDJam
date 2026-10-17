using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Runs;
using SubmarineLDJam.UseCases;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Base for every script that shows or reacts to the current run.
    /// It finds the run by itself (RunContext), so no "bootstrap" field is needed.
    /// When a run starts, <see cref="OnBind"/> is called. When it ends, <see cref="OnUnbind"/>.
    /// Works if this object is turned on or off at any time.
    ///
    /// Do not write your own OnEnable / OnDisable in a child class. Use OnBind / OnUnbind.
    /// </summary>
    public abstract class RunView : MonoBehaviour
    {
        protected RunService Service { get; private set; }
        protected SubmarineRun Run => Service != null ? Service.Run : null;
        protected bool IsBound => Service != null;

        protected virtual void OnEnable()
        {
            RunContext.Changed += Rebind;
            Rebind();
        }

        protected virtual void OnDisable()
        {
            RunContext.Changed -= Rebind;
            Unbind();
        }

        /// <summary>A run started. Read rules, build things.</summary>
        protected virtual void OnBind() { }

        /// <summary>The run is gone. Clean up what OnBind made.</summary>
        protected virtual void OnUnbind() { }

        /// <summary>A domain event of the current run.</summary>
        protected virtual void OnEvent(IDomainEvent domainEvent) { }

        private void Rebind()
        {
            if (ReferenceEquals(Service, RunContext.Current)) return;

            Unbind();
            Service = RunContext.Current;
            if (Service == null) return;

            Service.EventPublished += HandleEvent;
            OnBind();
        }

        private void Unbind()
        {
            if (Service == null) return;

            Service.EventPublished -= HandleEvent;
            Service = null;
            OnUnbind();
        }

        private void HandleEvent(IDomainEvent domainEvent) => OnEvent(domainEvent);
    }
}
