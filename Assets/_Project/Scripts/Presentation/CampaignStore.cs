using System;
using System.Collections.Generic;
using SubmarineLDJam.Domain.Progress;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Saves and loads the campaign progress (PlayerPrefs, as JSON).</summary>
    public static class CampaignStore
    {
        private const string Key = "submarine.campaign.v1";

        [Serializable]
        private sealed class Entry
        {
            public int index;
            public float seconds;
            public int stars;
        }

        [Serializable]
        private sealed class Data
        {
            public List<Entry> records = new List<Entry>();
            public List<int> introSeen = new List<int>();
        }

        public static Campaign Load(int stageCount)
        {
            var records = new List<StageRecord>();
            var introSeen = new List<int>();
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var data = JsonUtility.FromJson<Data>(json);
                    if (data?.records != null)
                        foreach (var entry in data.records)
                            records.Add(new StageRecord(entry.index, entry.seconds, entry.stars));
                    if (data?.introSeen != null) introSeen.AddRange(data.introSeen);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read saved progress, starting fresh. {exception.Message}");
            }

            return new Campaign(stageCount, records, introSeen);
        }

        public static void Save(Campaign campaign)
        {
            var data = new Data();
            foreach (var record in campaign.Records)
                data.records.Add(new Entry { index = record.Index, seconds = record.BestSeconds, stars = record.Stars });
            data.introSeen.AddRange(campaign.IntroSeen);

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>Wipes all progress.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
