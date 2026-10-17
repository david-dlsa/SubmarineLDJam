using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// List of stages, in order. Each stage has its own level numbers and its own maze.
    /// To make a harder stage: duplicate a LevelDefinition and a maze prefab, change them, add a line here.
    /// </summary>
    [CreateAssetMenu(menuName = "Submarine/Stage Catalog", fileName = "StageCatalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Stage
        {
            [Tooltip("Shown in the selector. Example: STAGE 1")]
            public string displayName = "STAGE";
            [Tooltip("Oxygen, speeds, sonar, penalties, star times for this stage.")]
            public LevelDefinition level;
            [Tooltip("Maze for this stage. Needs a Start Point child and a Destination Zone. Things for the radar go on the Sonar layer.")]
            public GameObject mazePrefab;
        }

        public List<Stage> stages = new List<Stage>();

        public int Count => stages.Count;

        public string NameOf(int index)
            => string.IsNullOrWhiteSpace(stages[index].displayName) ? $"STAGE {index + 1}" : stages[index].displayName;
    }
}
