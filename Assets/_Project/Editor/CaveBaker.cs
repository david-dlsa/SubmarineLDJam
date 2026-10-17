using System;
using System.IO;
using SubmarineLDJam.Domain.Caves;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SubmarineLDJam.EditorTools
{
    /// <summary>
    /// Makes the cave of a CaveSettings asset and saves it as a maze prefab (next to the asset):
    /// uneven wall shape with collision, start point, exit, obstacles, enemies and exit hint pickups.
    /// </summary>
    public static class CaveBaker
    {
        private static readonly Color PickupColor = new Color(0.4f, 0.9f, 1f);

        public static GameObject Bake(CaveSettings settings)
        {
            SubmarineTestSetup.SonarLayer = SubmarineTestSetup.EnsureLayer("Sonar");
            int layer = SubmarineTestSetup.SonarLayer;

            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(settings)).Replace('\\', '/');
            string prefabPath = $"{dir}/{settings.name}.prefab";

            CaveMap map;
            try
            {
                map = CaveGenerator.Generate(settings.ToParameters());
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"{settings.name}: {exception.Message}", settings);
                return null;
            }

            if (map.PathLength < settings.minPathCells)
                Debug.LogWarning($"{settings.name}: path is {map.PathLength} cells, shorter than Min Path Cells ({settings.minPathCells}). Try another seed or a bigger cave.", settings);

            var geometry = CaveMeshBuilder.Build(map, settings.cellSize);
            var mesh = SaveMesh(geometry, $"{dir}/{settings.name}_Mesh.asset", settings.name);
            var material = SaveMaterial($"{dir}/{settings.name}_Wall.mat", settings.wallColor);

            var root = new GameObject(settings.name);

            // Walls: one flat mesh + outline colliders. No texture, only the uneven shape.
            var walls = new GameObject("Walls") { layer = layer };
            walls.transform.SetParent(root.transform, false);
            walls.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = walls.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -1;
            foreach (var loop in geometry.Loops)
            {
                var edge = walls.AddComponent<EdgeCollider2D>();
                edge.points = loop;
                edge.edgeRadius = 0.05f;
            }

            Vector2 At(CaveCell cell) => CaveMeshBuilder.CellCenter(map, settings.cellSize, cell);

            var start = new GameObject("Start");
            start.transform.SetParent(root.transform, false);
            start.transform.position = At(map.Start);
            start.AddComponent<StartPoint>();

            var goal = SubmarineTestSetup.Box("Destination", root.transform, At(map.Goal), new Vector2(3f, 3f), SubmarineTestSetup.GoalColor);
            goal.GetComponent<BoxCollider2D>().isTrigger = true;
            goal.AddComponent<DestinationZone>();

            for (int i = 0; i < map.Obstacles.Count; i++)
                AddHazard(SubmarineTestSetup.Box("Obstacle_" + i, root.transform, At(map.Obstacles[i]), new Vector2(1.6f, 1.6f), SubmarineTestSetup.ObstacleColor), CollisionKind.Obstacle);

            for (int i = 0; i < map.Enemies.Count; i++)
                AddHazard(SubmarineTestSetup.Disc("Enemy_" + i, root.transform, At(map.Enemies[i]), 1.4f, SubmarineTestSetup.EnemyColor), CollisionKind.Enemy);

            for (int i = 0; i < map.Pickups.Count; i++)
            {
                var pickup = SubmarineTestSetup.Disc("ExitHintPickup_" + i, root.transform, At(map.Pickups[i]), 1.1f, PickupColor);
                pickup.GetComponent<CircleCollider2D>().isTrigger = true;
                pickup.AddComponent<ExitHintPickup>();
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"{settings.name}: baked. Path {map.PathLength} cells, {map.Obstacles.Count} obstacles, {map.Enemies.Count} enemies, {map.Pickups.Count} pickups.", prefab);
            return prefab;
        }

        private static void AddHazard(GameObject go, CollisionKind kind)
        {
            var hazard = go.AddComponent<Hazard>();
            var so = new SerializedObject(hazard);
            so.FindProperty("<Kind>k__BackingField").enumValueIndex = (int)kind;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Re-bake keeps the same mesh asset, so prefabs that use it do not break.</summary>
        private static Mesh SaveMesh(CaveMeshBuilder.Geometry geometry, string path, string baseName)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = baseName + "_Mesh" };
            else mesh.Clear();

            mesh.indexFormat = geometry.Vertices.Length > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = geometry.Vertices;
            mesh.triangles = geometry.Triangles;
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Unlit flat color. Same shader the radar echoes use.</summary>
        private static Material SaveMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }
    }

    [CustomEditor(typeof(CaveSettings))]
    public sealed class CaveSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Bake Cave Prefab makes (or replaces) '<name>.prefab' next to this asset. " +
                "Replacing loses hand edits of that prefab. Duplicate the prefab first if you changed it by hand.",
                MessageType.Info);

            if (GUILayout.Button("Random Seed"))
            {
                Undo.RecordObject(target, "Random seed");
                ((CaveSettings)target).seed = UnityEngine.Random.Range(1, 100000);
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button("Bake Cave Prefab"))
            {
                var prefab = CaveBaker.Bake((CaveSettings)target);
                if (prefab != null) EditorGUIUtility.PingObject(prefab);
            }
        }
    }
}
