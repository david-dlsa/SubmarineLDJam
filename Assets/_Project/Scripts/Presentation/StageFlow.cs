using System.Collections.Generic;
using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Progress;
using SubmarineLDJam.Domain.Runs;
using SubmarineLDJam.UseCases;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Runs the game loop between stages:
    /// stage select -> PLAY -> maze appears, run starts, radar and HUD show -> R -> back to stage select.
    /// A finished stage is saved (best time, best stars) and opens the next one.
    /// Put it in a scene next to a RunBootstrap that has no Auto Start Level.
    /// </summary>
    public sealed class StageFlow : MonoBehaviour
    {
        [SerializeField] private StageCatalog catalog;
        [SerializeField] private RunBootstrap runner;
        [SerializeField] private StageSelectView selectView;
        [SerializeField] private SubmarineBody submarine;
        [Tooltip("The maze prefab of the chosen stage is created under this. Empty = under this object.")]
        [SerializeField] private Transform mazeParent;
        [Tooltip("Visible only while a run is going: radar screen, compass, oxygen, force bar, timer...")]
        [SerializeField] private GameObject[] showWhilePlaying;
        [Tooltip("Debug: every stage is open.")]
        [SerializeField] private bool unlockAll;
        [Tooltip("RETURN button. Hook your main menu here later.")]
        [SerializeField] private UnityEvent onReturn;

        private Campaign _campaign;
        private RunService _service;
        private GameObject _maze;
        private int _playing = -1;

        private void Start()
        {
            if (catalog == null || catalog.Count == 0 || runner == null || selectView == null || submarine == null)
            {
                Debug.LogError("StageFlow: set Catalog (with stages), Runner, Select View and Submarine.", this);
                enabled = false;
                return;
            }

            _campaign = CampaignStore.Load(catalog.Count);

            selectView.PlayRequested += Play;
            selectView.ReturnRequested += OnReturn;
            RunContext.ExitRequested += ExitRun;

            SetPlayingObjects(false);
            ShowSelection(unlockAll ? 0 : _campaign.HighestUnlocked);
        }

        private void OnDestroy()
        {
            if (selectView != null)
            {
                selectView.PlayRequested -= Play;
                selectView.ReturnRequested -= OnReturn;
            }

            RunContext.ExitRequested -= ExitRun;
            if (_service != null) _service.EventPublished -= OnRunEvent;
        }

        private void Update()
        {
            // ESC: give up the stage and go back to the stage select. Nothing is saved.
            var keyboard = Keyboard.current;
            if (_playing >= 0 && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ExitRun();
        }

        private bool IsUnlocked(int index) => unlockAll || _campaign.IsUnlocked(index);

        private void ShowSelection(int selected)
        {
            var cards = new List<StageCard>(catalog.Count);
            for (int i = 0; i < catalog.Count; i++)
            {
                bool completed = _campaign.TryGetRecord(i, out var record);
                cards.Add(new StageCard(catalog.NameOf(i), IsUnlocked(i), completed,
                    completed ? record.BestSeconds : 0f, completed ? record.Stars : 0));
            }

            selectView.gameObject.SetActive(true);
            selectView.Show(cards, selected);
        }

        private void Play(int index)
        {
            if (_playing >= 0 || !IsUnlocked(index)) return;

            var stage = catalog.stages[index];
            if (stage.level == null)
            {
                Debug.LogError($"StageFlow: stage {index + 1} has no Level Definition.", this);
                return;
            }

            _playing = index;
            selectView.gameObject.SetActive(false);

            // 1. Maze + submarine at the start point.
            Vector2 start = Vector2.zero;
            if (stage.mazePrefab != null)
            {
                _maze = Instantiate(stage.mazePrefab, mazeParent != null ? mazeParent : transform);
                var startPoint = _maze.GetComponentInChildren<StartPoint>();
                if (startPoint != null) start = startPoint.transform.position;
            }
            submarine.PlaceAt(start);

            // 2. Show the radar and HUD first, then start the run, so every view binds with the maze already there.
            SetPlayingObjects(true);
            _service = runner.Begin(stage.level);
            _service.EventPublished += OnRunEvent;
        }

        private void OnRunEvent(IDomainEvent domainEvent)
        {
            if (!(domainEvent is RunCompleted done) || _playing < 0) return;

            _campaign.RecordCompletion(_playing, done.ElapsedSeconds, done.Stars);
            CampaignStore.Save(_campaign);
        }

        /// <summary>R key: leave the run, back to stage select.</summary>
        private void ExitRun()
        {
            if (_playing < 0) return;

            if (_service != null) _service.EventPublished -= OnRunEvent;
            _service = null;

            runner.End();
            SetPlayingObjects(false);

            if (_maze != null)
            {
                Destroy(_maze);
                _maze = null;
            }

            int last = _playing;
            _playing = -1;
            ShowSelection(last);
        }

        private void OnReturn() => onReturn?.Invoke();

        private void SetPlayingObjects(bool visible)
        {
            if (showWhilePlaying == null) return;
            foreach (var go in showWhilePlaying)
                if (go != null) go.SetActive(visible);
        }
    }
}
