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
    /// stage select -> PLAY -> maze appears, run starts, radar and HUD show -> result screen
    /// (RESTART, NEXT, STAGES) -> ... ESC = back to stage select, R = restart.
    /// A finished stage is saved (best time, best stars) and opens the next one.
    /// Put it in a scene next to a RunBootstrap that has no Auto Start Level.
    /// </summary>
    public sealed class StageFlow : MonoBehaviour
    {
        [SerializeField] private StageCatalog catalog;
        [SerializeField] private RunBootstrap runner;
        [SerializeField] private StageSelectView selectView;
        [Tooltip("Popup with the intro pages of a stage (new mechanics). Shown before the stage starts, only the first time. Empty = no popups.")]
        [SerializeField] private IntroPopupView introView;
        [Tooltip("Shown when the run ends (arrived or out of oxygen). Empty = no result screen.")]
        [SerializeField] private ResultView resultView;
        [Tooltip("Pause between the end of the run and the result screen.")]
        [SerializeField, Min(0f)] private float resultDelaySeconds = 1f;
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
        private bool _awaitingIntro;
        private bool _resultPending;
        private float _resultAt;
        private ResultCard _resultCard;

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

            if (resultView != null)
            {
                resultView.RestartRequested += Restart;
                resultView.NextRequested += PlayNext;
                resultView.StagesRequested += ExitRun;
                resultView.gameObject.SetActive(false);
            }

            if (introView != null)
            {
                introView.Confirmed += OnIntroConfirmed;
                introView.gameObject.SetActive(false);
            }

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

            if (resultView != null)
            {
                resultView.RestartRequested -= Restart;
                resultView.NextRequested -= PlayNext;
                resultView.StagesRequested -= ExitRun;
            }

            if (introView != null) introView.Confirmed -= OnIntroConfirmed;

            RunContext.ExitRequested -= ExitRun;
            if (_service != null) _service.EventPublished -= OnRunEvent;
        }

        private void Update()
        {
            if (_playing < 0) return;

            // The run ended: after a short pause the result screen takes the place of the radar.
            if (_resultPending && Time.unscaledTime >= _resultAt) ShowResult();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // ESC: give up the stage and go back to the stage select. A stage not finished is not saved.
            if (keyboard.escapeKey.wasPressedThisFrame) ExitRun();
            // R: start the same stage again.
            else if (keyboard.rKey.wasPressedThisFrame && !_awaitingIntro) Restart();
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
            _resultPending = false;
            selectView.gameObject.SetActive(false);
            if (resultView != null) resultView.gameObject.SetActive(false);

            // First time on this stage and it has an intro? Show it. The stage starts when the player presses OK.
            var pages = stage.level.introPages;
            if (introView != null && pages != null && pages.Length > 0 && !_campaign.HasSeenIntro(index))
            {
                _awaitingIntro = true;
                introView.gameObject.SetActive(true);
                introView.Show(pages);
                return;
            }

            StartRun(index);
        }

        private void OnIntroConfirmed()
        {
            if (!_awaitingIntro || _playing < 0) return;

            _awaitingIntro = false;
            introView.gameObject.SetActive(false);
            _campaign.MarkIntroSeen(_playing);
            CampaignStore.Save(_campaign);
            StartRun(_playing);
        }

        /// <summary>Builds the stage and starts the run.</summary>
        private void StartRun(int index)
        {
            var stage = catalog.stages[index];

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
            if (_playing < 0) return;

            if (domainEvent is RunCompleted done)
            {
                bool improved = _campaign.RecordCompletion(_playing, done.ElapsedSeconds, done.Stars);
                CampaignStore.Save(_campaign);

                _campaign.TryGetRecord(_playing, out var record);
                bool hasNext = _playing + 1 < catalog.Count && IsUnlocked(_playing + 1);
                _resultCard = new ResultCard(true, done.ElapsedSeconds, done.Stars, record.BestSeconds, improved, hasNext);
                QueueResult();
            }
            else if (domainEvent is RunFailed)
            {
                float seconds = _service != null && _service.Run != null ? _service.Run.ElapsedSeconds : 0f;
                _resultCard = new ResultCard(false, seconds, 0, 0f, false, false);
                QueueResult();
            }
        }

        private void QueueResult()
        {
            if (resultView == null) return;

            _resultPending = true;
            _resultAt = Time.unscaledTime + resultDelaySeconds;
        }

        /// <summary>The run is over. Radar and HUD go away, the result takes the same place.</summary>
        private void ShowResult()
        {
            _resultPending = false;

            if (_service != null) _service.EventPublished -= OnRunEvent;
            _service = null;
            runner.End();
            SetPlayingObjects(false);
            DestroyMaze();

            resultView.gameObject.SetActive(true);
            resultView.Show(_resultCard);
        }

        /// <summary>Stops everything about the current stage. Nothing is shown after this.</summary>
        private int Teardown()
        {
            if (_service != null) _service.EventPublished -= OnRunEvent;
            _service = null;
            _resultPending = false;
            _awaitingIntro = false;
            if (introView != null) introView.gameObject.SetActive(false);

            runner.End();
            SetPlayingObjects(false);
            if (resultView != null) resultView.gameObject.SetActive(false);
            DestroyMaze();

            int last = _playing;
            _playing = -1;
            return last;
        }

        private void DestroyMaze()
        {
            if (_maze == null) return;
            Destroy(_maze);
            _maze = null;
        }

        /// <summary>ESC or STAGES: leave the stage, back to stage select.</summary>
        private void ExitRun()
        {
            if (_playing < 0) return;
            ShowSelection(Teardown());
        }

        /// <summary>R or RESTART: the same stage again.</summary>
        private void Restart()
        {
            if (_playing < 0) return;
            Play(Teardown());
        }

        /// <summary>NEXT: the stage after this one, if it is open.</summary>
        private void PlayNext()
        {
            if (_playing < 0) return;

            int next = _playing + 1;
            if (next >= catalog.Count || !IsUnlocked(next)) return;
            Teardown();
            Play(next);
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
