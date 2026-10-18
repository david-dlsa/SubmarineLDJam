using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{

    [SerializeField] private List<EventSpec> _events = new();
    [SerializeField] private float _starDelay = 10;
    [SerializeField] private float _delayBetweenEvents = 10;
    [SerializeField] private AnimationCurve _amountCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private void Start()
    {
        foreach (var e in _events)
            e.EventObject.SetActive(false);
        
        StartDelay().LogExceptionsAndForget();
    }

    private async Awaitable StartDelay()
    {
        await Awaitable.WaitForSecondsAsync(_starDelay, destroyCancellationToken);
        EventLoop().LogExceptionsAndForget();
    }

    private async Awaitable EventLoop()
    {
        Debug.Log("Starting event loop");

        while (true)
        {
            if(destroyCancellationToken.IsCancellationRequested)
                break;
        
            var numOfEvents = Mathf.FloorToInt(_amountCurve.Evaluate(Time.time));
            var eventsToPlay = GetShuffled(numOfEvents);

            Debug.Log($"{eventsToPlay.Count}/{_events.Count} were selected");
        
            foreach(var e in eventsToPlay)
                PlayEvent(e).LogExceptionsAndForget();

            var waitTime = eventsToPlay.Count > 0 ? eventsToPlay.Max(x => x.Duration) : 0f;
            waitTime += _delayBetweenEvents;
        
            Debug.Log($"Waiting {waitTime}");
        
            await Awaitable.WaitForSecondsAsync(waitTime, destroyCancellationToken);
        }
        
        Debug.Log("Loop finished");
    }

    private async Awaitable PlayEvent(EventSpec eventToPlay)
    {
        eventToPlay.EventObject.SetActive(true);
        await Awaitable.WaitForSecondsAsync(eventToPlay.Duration, destroyCancellationToken);
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
