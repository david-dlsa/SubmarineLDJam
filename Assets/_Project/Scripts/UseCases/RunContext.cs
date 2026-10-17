using System;

namespace SubmarineLDJam.UseCases
{
    /// <summary>
    /// The one place that says "this is the run that is going now".
    /// Views read <see cref="Current"/> and listen to <see cref="Changed"/>, so they need no scene references.
    /// A new stage = a new RunService = <see cref="Set"/> again. Plain C#, no Unity.
    /// </summary>
    public static class RunContext
    {
        public static RunService Current { get; private set; }

        /// <summary>Raised when <see cref="Current"/> changes (new run, or run cleared).</summary>
        public static event Action Changed;

        /// <summary>Someone asked to leave the run (R key). Stage flow listens to this.</summary>
        public static event Action ExitRequested;

        public static void Set(RunService service)
        {
            Current = service;
            Changed?.Invoke();
        }

        public static void Clear() => Set(null);

        /// <summary>Returns false if nobody is listening, so the caller can do its own thing.</summary>
        public static bool RequestExit()
        {
            if (ExitRequested == null) return false;
            ExitRequested();
            return true;
        }

        /// <summary>For play mode start with domain reload off, and for tests.</summary>
        public static void ResetAll()
        {
            Current = null;
            Changed = null;
            ExitRequested = null;
        }
    }
}
