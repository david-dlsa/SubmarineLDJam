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
            e.EventObject.SetActive(false);
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
        await Awaitable.WaitForSecondsAsync(_starDelay, token);
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
        
            await Awaitable.WaitForSecondsAsync(waitTime, token);
        }
        
        Debug.Log("Loop finished");
    }

    private async Awaitable PlayEvent(EventSpec eventToPlay, CancellationToken token)
    {
        eventToPlay.EventObject.SetActive(true);
        await Awaitable.WaitForSecondsAsync(eventToPlay.Duration, token);
        eventToPlay.EventObject.SetActive(false);
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
        public GameObject EventObject;
        public float Duration;
    }
}
