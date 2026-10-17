using SubmarineLDJam.Domain.Caves;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Numbers for one cave. Select the asset and press "Bake Cave Prefab" in the Inspector:
    /// it makes a maze prefab (walls with uneven edges + collision, start, exit, hazards, pickups)
    /// next to this asset. Put that prefab in a stage of the StageCatalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Submarine/Cave Settings", fileName = "Cave_01")]
    public sealed class CaveSettings : ScriptableObject
    {
        [Header("Size")]
        [Min(12)] public int width = 44;
        [Min(12)] public int height = 28;
        [Tooltip("World units per cell. The submarine must fit in 2 cells.")]
        [Min(0.5f)] public float cellSize = 1.5f;

        [Header("Shape")]
        [Tooltip("Same seed = same cave. Change it to get another cave.")]
        public int seed = 7;
        [Tooltip("More rock = narrower and more twisting. Around 45 to 50 looks like a cave.")]
        [Range(35f, 60f)] public float rockPercent = 47f;
        [Tooltip("More smoothing = rounder rocks.")]
        [Range(0, 10)] public int smoothSteps = 5;
        [Tooltip("Cave is made again if less than this part is open water.")]
        [Range(10f, 60f)] public float minOpenPercent = 25f;

        [Header("Route")]
        [Tooltip("Start and exit are at least this many cells apart by swimming. Longer = harder.")]
        [Min(10)] public int minPathCells = 40;

        [Header("Things")]
        [Min(0)] public int obstacles = 3;
        [Min(0)] public int enemies = 2;
        [Tooltip("Exit hint pickups. Only used by stages with Exit Hint Mode = OnPickup.")]
        [Min(0)] public int pickups = 0;

        [Header("Look")]
        [Tooltip("Flat color. The radar shows this color.")]
        public Color wallColor = new Color(0.25f, 0.9f, 0.5f);

        public CaveParameters ToParameters() => new CaveParameters(
            width, height, seed, rockPercent, smoothSteps, minOpenPercent, minPathCells, obstacles, enemies, pickups);
    }
}
