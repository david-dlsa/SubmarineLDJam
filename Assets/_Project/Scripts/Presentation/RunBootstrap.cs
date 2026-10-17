using SubmarineLDJam.Domain.Oxygen;
using SubmarineLDJam.UseCases;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Starts runs, feeds time and keyboard into the domain, and publishes the run in RunContext.
    /// Space = movement module. Arrows = oxygen minigame. M = sonar ping.
    ///
    /// Stage mode: leave Auto Start Level empty. StageFlow calls Begin / End.
    /// Single level mode: set Auto Start Level and the run starts with the scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class RunBootstrap : MonoBehaviour
    {
        [FormerlySerializedAs("level")]
        [Tooltip("Optional. If set, this level starts by itself when the scene starts. Leave empty when using StageFlow.")]
        [SerializeField] private LevelDefinition autoStartLevel;

        public RunService Service { get; private set; }

        private void Awake()
        {
            if (autoStartLevel != null) Begin(autoStartLevel);
        }

        private void OnDestroy()
        {
            if (Service != null && RunContext.Current == Service) RunContext.Clear();
        }

        /// <summary>Starts a new run. Every view picks it up through RunContext.</summary>
        public RunService Begin(LevelDefinition level)
        {
            Service = new RunService();
            Service.StartRun(level.ToRules());
            RunContext.Set(Service);
            return Service;
        }

        /// <summary>Stops the run. Views let go of it.</summary>
        public void End()
        {
            if (Service == null) return;

            if (RunContext.Current == Service) RunContext.Clear();
            Service = null;
        }

        private void Update()
        {
            if (Service == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) Service.ConfirmMovement();
                if (keyboard.mKey.wasPressedThisFrame) Service.PingSonar();
                if (keyboard.upArrowKey.wasPressedThisFrame) Service.PressArrow(Arrow.Up);
                if (keyboard.downArrowKey.wasPressedThisFrame) Service.PressArrow(Arrow.Down);
                if (keyboard.leftArrowKey.wasPressedThisFrame) Service.PressArrow(Arrow.Left);
                if (keyboard.rightArrowKey.wasPressedThisFrame) Service.PressArrow(Arrow.Right);
            }

            Service.Tick(Time.deltaTime);
        }
    }
}
