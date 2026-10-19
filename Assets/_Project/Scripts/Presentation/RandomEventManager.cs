using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SubmarineLDJam.Presentation;
using UnityEngine;

public class RandomEventManager : RunView
{
    [SerializeField] private List<EventSpec> _events = new();
    [SerializeField] private float _starDelay = 10;
    [SerializeField] private float _delayBetweenEvents = 10;
    [SerializeField] private AnimationCurve _amountCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private CancellationTokenSource _runEndedCts;
    private float _timeOnStart;
    
    private void Awake()
    {
        foreach (var e in _events)
            if (e.EventObject != null) e.EventObject.SetActive(false);
    }

    protected override void OnBind()
    {
        _timeOnStart = Time.time;
        _runEndedCts = new CancellationTokenSource();
        var linked = CancellationTokenSource.CreateLinkedTokenSource(_runEndedCts.Token, destroyCancellationToken);
        StartDelay(linked.Token).LogExceptionsAndForget();
    }

    protected override void OnUnbind()
    {
        _runEndedCts?.Cancel();
        _runEndedCts?.Dispose();
        _runEndedCts = null;
    }

    private async Awaitable StartDelay(CancellationToken token)
    {
        try { await Awaitable.WaitForSecondsAsync(_starDelay, token); }
        catch (OperationCanceledException) { return; } // the run ended
        EventLoop(token).LogExceptionsAndForget();
    }

    private async Awaitable EventLoop(CancellationToken token)
    {
        Debug.Log("Starting event loop");

        while (true)
        {
            if(token.IsCancellationRequested)
                break;
        
            var timeSinceStart = Time.time - _timeOnStart;
            var numOfEvents = Mathf.FloorToInt(_amountCurve.Evaluate(timeSinceStart));
            var eventsToPlay = GetShuffled(numOfEvents);

            Debug.Log($"{eventsToPlay.Count}/{_events.Count} were selected");
        
            foreach(var e in eventsToPlay)
                PlayEvent(e, token).LogExceptionsAndForget();

            var waitTime = eventsToPlay.Count > 0 ? eventsToPlay.Max(x => x.Duration) : 0f;
            waitTime += _delayBetweenEvents;
        
            Debug.Log($"Waiting {waitTime}");
        
            try { await Awaitable.WaitForSecondsAsync(waitTime, token); }
            catch (OperationCanceledException) { break; } // the run ended
        }
        
        Debug.Log("Loop finished");
    }

    private async Awaitable PlayEvent(EventSpec eventToPlay, CancellationToken token)
    {
        if (eventToPlay.EventObject != null) eventToPlay.EventObject.SetActive(true);
        var sfx = StartSfx(eventToPlay);
        try { await Awaitable.WaitForSecondsAsync(eventToPlay.Duration, token); }
        catch (OperationCanceledException) { } // the run ended: still clean up below
        if (sfx != null) Destroy(sfx);
        if (eventToPlay.EventObject != null) eventToPlay.EventObject.SetActive(false);
    }

    /// <summary>Plays the event sound. Loop ON: repeats until the event ends. Returns the source to destroy, or null.</summary>
    private AudioSource StartSfx(EventSpec spec)
    {
        if (spec.Sfx == null) return null;

        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D sound
        source.clip = spec.Sfx;
        source.volume = spec.Volume > 0f ? spec.Volume : 1f; // 0 = not set = full
        source.loop = spec.LoopSfx;
        source.Play();

        // One shot: the source is removed when the clip ends.
        if (!spec.LoopSfx) Destroy(source, spec.Sfx.length + 0.1f);
        return spec.LoopSfx ? source : null;
    }

    private IList<EventSpec> GetShuffled(int amount)
    {
        amount = Mathf.Min(amount, _events.Count);
        
        if(amount <= 0) return new List<EventSpec>();

        var allEvents = _events.ToArray();

        for (int i = 0; i < allEvents.Length; i++)
        {
            var randomIndex = UnityEngine.Random.Range(0, allEvents.Length);
            (allEvents[i], allEvents[randomIndex]) = (allEvents[randomIndex], allEvents[i]);
        }
        
        return allEvents.Take(amount).ToList();
    }

    [Serializable]
    public struct EventSpec
    {
        public string Name;
        [Tooltip("Can be empty (for example a blackout needs no object).")]
        public GameObject EventObject;
        public float Duration;
        [Tooltip("Sound of this event (sparks, smoke, drip...). Empty = silent.")]
        public AudioClip Sfx;
        [Tooltip("0 = full volume.")]
        [Range(0f, 1f)] public float Volume;
        [Tooltip("ON = the sound repeats while the event lasts. OFF = plays once at the start.")]
        public bool LoopSfx;
    }
}
