using SubmarineLDJam.UseCases;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Clears the static RunContext when play starts (needed if "Reload Domain" is off).</summary>
    internal static class RunContextReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => RunContext.ResetAll();
    }
}
