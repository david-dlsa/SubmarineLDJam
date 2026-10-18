using System.Collections.Generic;
using System.IO;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Domain.Navigation;
using SubmarineLDJam.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SubmarineLDJam.EditorTools
{
    /// <summary>
    /// Menu: Submarine > Create Test Setup In Scene.
    /// Builds the whole game scene: 3 stages (level numbers + maze prefab each), stage select inside the radar,
    /// submarine, sonar camera and the HUD. Already wired.
    /// Assets that exist (levels, mazes, catalog) are kept, so your edits are safe. Scene objects are rebuilt.
    /// </summary>
    public static class SubmarineTestSetup
    {
        private const string LevelsDir = "Assets/_Project/Levels";
        private const string CavesDir = LevelsDir + "/Caves";
        private const string CatalogPath = LevelsDir + "/StageCatalog.asset";
        private const string SonarLayerName = "Sonar";


        private const int StageCount = 3;

        internal static int SonarLayer;

        private sealed class HudParts
        {
            public StageSelectView Select;
            public ResultView Result;
            public readonly List<GameObject> Playing = new List<GameObject>();
        }

        [MenuItem("Submarine/Create Test Setup In Scene")]
        public static void Create()
        {
            CleanOldObjects();
            SonarLayer = EnsureLayer(SonarLayerName);

            var levels = new LevelDefinition[StageCount];
            var mazes = new GameObject[StageCount];
            for (int i = 0; i < StageCount; i++)
            {
                levels[i] = GetOrCreateLevel(i + 1);
                mazes[i] = GetOrCreateCave(i + 1);
            }
            var catalog = GetOrCreateCatalog(levels, mazes);

            // No Auto Start Level: StageFlow starts the runs.
            var runner = new GameObject("RunBootstrap").AddComponent<RunBootstrap>();

            var world = new GameObject("World").transform;
            var mazeRoot = new GameObject("MazeRoot").transform;
            mazeRoot.SetParent(world, false);

            var submarine = CreateSubmarine();
            var worldCamera = SetupWorldCamera(submarine.transform);
            var sonarCamera = CreateSonarCamera(levels[0]);
            var parts = CreateHud(submarine.transform, sonarCamera, worldCamera);
            EnsureEventSystem();

            var flowGo = new GameObject("StageFlow");
            var flow = flowGo.AddComponent<StageFlow>();
            SetRef(flow, "catalog", catalog);
            SetRef(flow, "runner", runner);
            SetRef(flow, "selectView", parts.Select);
            SetRef(flow, "resultView", parts.Result);
            SetRef(flow, "submarine", submarine);
            SetRef(flow, "mazeParent", mazeRoot);
            SetRefs(flow, "showWhilePlaying", parts.Playing);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Submarine setup created. Play: pick a stage, Enter or PLAY. Space = movement, arrows = oxygen, M = sonar, R = restart, ESC = back to stage select, F2 = show real world (debug).");
        }

        private static void CleanOldObjects()
        {
            foreach (var objectName in new[] { "RunBootstrap", "World", "HUD", "Submarine", "SonarCamera", "StageFlow" })
            {
                GameObject found;
                while ((found = GameObject.Find(objectName)) != null)
                    Object.DestroyImmediate(found);
            }
        }

        // ---------- layer + assets ----------

        internal static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;

                element.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }

            Debug.LogError($"No free layer for '{layerName}'. Free one in Project Settings > Tags and Layers.");
            return 0;
        }

        /// <summary>Level_01, Level_02, Level_03. Each harder than the one before. Existing assets are kept.</summary>
        private static LevelDefinition GetOrCreateLevel(int number)
        {
            string path = $"{LevelsDir}/Level_{number:00}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level != null) return level;

            Directory.CreateDirectory(LevelsDir);
            level = ScriptableObject.CreateInstance<LevelDefinition>();

            switch (number)
            {
                case 1:
                    level.threeStarSeconds = 90f;
                    level.twoStarSeconds = 150f;
                    level.oxygenDrainPerSecond = 0.7f;
                    break;
                case 2:
                    level.threeStarSeconds = 130f;
                    level.twoStarSeconds = 220f;
                    level.oxygenDrainPerSecond = 0.85f;
                    level.sonarCooldownSeconds = 10f;
                    level.sonarViewRadius = 9f;
                    level.exitHintMode = ExitHintMode.OnPickup;
                    level.exitHintSeconds = 20f;
                    break;
                default:
                    level.threeStarSeconds = 170f;
                    level.twoStarSeconds = 280f;
                    level.oxygenDrainPerSecond = 1f;
                    level.sonarCooldownSeconds = 12f;
                    level.sonarViewRadius = 8f;
                    level.refillSequenceLength = 4;
                    level.refillMistakePenalty = 0.35f;
                    level.exitHintMode = ExitHintMode.OnPickup;
                    level.exitHintSeconds = 12f;
                    break;
            }

            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            return level;
        }

        private static StageCatalog GetOrCreateCatalog(LevelDefinition[] levels, GameObject[] mazes)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            if (catalog != null)
            {
                Debug.Log("StageCatalog already exists, kept as is. Delete the asset to get the default 3 cave stages, or put the Cave prefabs (Levels/Caves) in its Maze Prefab fields.");
                return catalog;
            }

            Directory.CreateDirectory(LevelsDir);
            catalog = ScriptableObject.CreateInstance<StageCatalog>();
            for (int i = 0; i < levels.Length; i++)
                catalog.stages.Add(new StageCatalog.Stage { displayName = $"STAGE {i + 1}", level = levels[i], mazePrefab = mazes[i] });

            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>Cave_01..03: a CaveSettings asset + the baked maze prefab. Existing prefabs are kept (your edits are safe).</summary>
        private static GameObject GetOrCreateCave(int number)
        {
            string name = $"Cave_{number:00}";
            string settingsPath = $"{CavesDir}/{name}.asset";
            string prefabPath = $"{CavesDir}/{name}.prefab";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null) return prefab;

            var settings = AssetDatabase.LoadAssetAtPath<CaveSettings>(settingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(CavesDir);
                AssetDatabase.Refresh();

                settings = ScriptableObject.CreateInstance<CaveSettings>();
                switch (number)
                {
                    case 1:
                        settings.width = 44; settings.height = 28; settings.seed = 7; settings.rockPercent = 46f;
                        settings.minPathCells = 40; settings.obstacles = 3; settings.enemies = 2; settings.pickups = 0;
                        break;
                    case 2:
                        settings.width = 56; settings.height = 34; settings.seed = 21; settings.rockPercent = 47f;
                        settings.minPathCells = 60; settings.obstacles = 4; settings.enemies = 3; settings.pickups = 1;
                        break;
                    default:
                        settings.width = 68; settings.height = 40; settings.seed = 99; settings.rockPercent = 48f;
                        settings.minPathCells = 80; settings.obstacles = 5; settings.enemies = 4; settings.pickups = 2;
                        break;
                }
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            return CaveBaker.Bake(settings);
        }

        // ---------- world ----------

        internal static Sprite Square => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        internal static Sprite Circle => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        internal static readonly Color WallColor = new Color(0.25f, 0.9f, 0.5f);
        internal static readonly Color ObstacleColor = new Color(1f, 0.85f, 0.2f);
        internal static readonly Color EnemyColor = new Color(1f, 0.25f, 0.25f);
        internal static readonly Color GoalColor = new Color(0.3f, 0.8f, 1f);

        private static SubmarineBody CreateSubmarine()
        {
            var start = Vector2.zero; // StageFlow moves it to the maze start

            // Submarine (Default layer: the radar never sees it, it draws its own triangle)
            var sub = new GameObject("Submarine");
            sub.transform.position = start;
            sub.transform.localScale = new Vector3(0.8f, 1.2f, 1f);
            var renderer = sub.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.color = new Color(0.95f, 0.8f, 0.2f);
            renderer.sortingOrder = 2;
            var box = sub.AddComponent<BoxCollider2D>();
            box.size = Square.bounds.size;
            sub.AddComponent<SubmarineBody>(); // adds Rigidbody2D too
            sub.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var nose = new GameObject("Nose");
            nose.transform.SetParent(sub.transform, false);
            nose.transform.localPosition = new Vector3(0f, Square.bounds.size.y * 0.5f, 0f);
            nose.transform.localScale = new Vector3(0.5f, 0.25f, 1f);
            var noseRenderer = nose.AddComponent<SpriteRenderer>();
            noseRenderer.sprite = Square;
            noseRenderer.color = new Color(0.9f, 0.2f, 0.2f);
            noseRenderer.sortingOrder = 3;

            return sub.GetComponent<SubmarineBody>();
        }

        internal static GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name) { layer = SonarLayer };
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sprite = Square;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;

            go.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = sprite.bounds.size;
            return go;
        }

        internal static GameObject Disc(string name, Transform parent, Vector2 pos, float diameter, Color color)
        {
            var go = new GameObject(name) { layer = SonarLayer };
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sprite = Circle;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;

            go.transform.localScale = new Vector3(diameter / sprite.bounds.size.x, diameter / sprite.bounds.size.y, 1f);
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = sprite.bounds.extents.x;
            return go;
        }

        // ---------- cameras ----------

        private static Camera SetupWorldCamera(Transform submarine)
        {
            var cam = Camera.main;
            if (cam == null) return null;

            cam.orthographic = true;
            cam.orthographicSize = 14f;
            cam.transform.position = new Vector3(submarine.position.x, submarine.position.y, -10f);
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);

            var follow = cam.GetComponent<FollowTarget>();
            if (follow == null) follow = cam.gameObject.AddComponent<FollowTarget>();
            SetRef(follow, "target", submarine);
            return cam;
        }

        private static Camera CreateSonarCamera(LevelDefinition level)
        {
            var go = new GameObject("SonarCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = level.sonarViewRadius;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 1 << SonarLayer;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.depth = -10f;
            cam.enabled = false; // RadarView turns it on for one frame per ping
            go.transform.position = new Vector3(0f, 0f, -10f);
            return cam;
        }

        // ---------- HUD ----------

        private static HudParts CreateHud(Transform submarine, Camera sonarCamera, Camera worldCamera)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.transform;

            // Everything added to parts.Playing is shown only while a run is going.
            var parts = new HudParts();
            BuildCompass(hud, parts);
            BuildForceBar(hud, parts);
            BuildOxygen(hud, parts);
            BuildBlackout(hud, parts);
            BuildAlarm(hud, parts);
            BuildRefill(hud, parts);
            BuildStatus(hud, parts);
            BuildRadar(hud, parts, submarine, sonarCamera, worldCamera);
            return parts;
        }

        private static void BuildRadar(Transform hud, HudParts parts, Transform submarine, Camera sonarCamera, Camera worldCamera)
        {
            var center = new Vector2(0.5f, 0.5f);

            var root = PlainRect("Radar", hud, new Vector2(1f, 1f), center, new Vector2(-250f, -250f), new Vector2(420f, 420f));

            // Frame (swap the sprite for your own art)
            Img("Frame", root, center, center, Vector2.zero, new Vector2(420f, 420f), new Color(0.22f, 0.25f, 0.3f), Square);

            // Round screen. The Mask cuts everything inside to a circle.
            var disc = Img("Disc", root, center, center, Vector2.zero, new Vector2(380f, 380f), Color.black, null);
            var mask = disc.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var pingLayers = PlainRect("PingLayers", disc, center, center, Vector2.zero, Vector2.zero);
            Stretch(pingLayers);

            var grid = Img("Grid", disc, center, center, Vector2.zero, Vector2.zero, Color.white, null);
            Stretch(grid);
            var glow = Img("EdgeGlow", disc, center, center, Vector2.zero, Vector2.zero, Color.white, null);
            Stretch(glow);

            var exitDot = Img("ExitDot", disc, center, center, Vector2.zero, new Vector2(20f, 20f), Color.white, null);
            var subIcon = Img("SubIcon", disc, center, center, Vector2.zero, new Vector2(30f, 30f), Color.white, null);

            // Sonar charge
            var label = Label("SonarLabel", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), 24, TextAnchor.UpperCenter);
            var barBack = Img("SonarBar", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -46f),
                new Vector2(320f, 12f), new Color(0f, 0f, 0f, 0.6f), Square);
            var barFill = Img("Fill", barBack, center, center, Vector2.zero, Vector2.zero, new Color(0.4f, 1f, 0.6f), Square);
            Stretch(barFill);
            var barFillImage = barFill.GetComponent<Image>();
            barFillImage.type = Image.Type.Filled;
            barFillImage.fillMethod = Image.FillMethod.Horizontal;
            barFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            // Radar screen + sonar charge show only while playing. The frame stays: the stage select lives inside it.
            parts.Playing.Add(disc.gameObject);
            parts.Playing.Add(label.gameObject);
            parts.Playing.Add(barBack.gameObject);

            var view = root.gameObject.AddComponent<RadarView>();
            SetRef(view, "submarine", submarine);
            SetRef(view, "sonarCamera", sonarCamera);
            SetRef(view, "worldCamera", worldCamera);
            SetRef(view, "pingLayers", pingLayers);
            SetRef(view, "disc", disc.GetComponent<Image>());
            SetRef(view, "grid", grid.GetComponent<Image>());
            SetRef(view, "edgeGlow", glow.GetComponent<Image>());
            SetRef(view, "exitDot", exitDot);
            SetRef(view, "subIcon", subIcon);
            SetRef(view, "cooldownFill", barFillImage);
            SetRef(view, "cooldownLabel", label);

            BuildStageSelect(root, parts);
            BuildResult(root, parts);
        }

        /// <summary>The result screen, inside the radar display, same place as the stage select.</summary>
        private static void BuildResult(RectTransform radarRoot, HudParts parts)
        {
            var center = new Vector2(0.5f, 0.5f);

            var panel = Img("Result", radarRoot, center, center, Vector2.zero, new Vector2(380f, 380f),
                new Color(0.02f, 0.06f, 0.05f, 0.97f), Circle);

            var title = TmpLabel("Title", panel, new Vector2(0f, 120f), new Vector2(300f, 44f), 34);
            var time = TmpLabel("Time", panel, new Vector2(0f, 75f), new Vector2(300f, 40f), 30);
            var stars = TmpLabel("Stars", panel, new Vector2(0f, 35f), new Vector2(300f, 44f), 40);
            stars.color = new Color(1f, 0.85f, 0.2f);
            var best = TmpLabel("Best", panel, new Vector2(0f, 0f), new Vector2(300f, 30f), 20);
            best.color = new Color(0.4f, 1f, 0.6f);

            var next = MakeButton("Next", panel, new Vector2(0f, -40f), new Vector2(170f, 44f), "NEXT", 26, new Color(0.15f, 0.55f, 0.3f, 0.9f));
            var restart = MakeButton("Restart", panel, new Vector2(0f, -90f), new Vector2(170f, 40f), "RESTART", 24, new Color(0.2f, 0.35f, 0.55f, 0.9f));
            var stages = MakeButton("Stages", panel, new Vector2(0f, -135f), new Vector2(150f, 34f), "STAGES", 20, new Color(0.3f, 0.3f, 0.3f, 0.8f));

            var view = panel.gameObject.AddComponent<ResultView>();
            SetRef(view, "restartButton", restart);
            SetRef(view, "nextButton", next);
            SetRef(view, "stagesButton", stages);
            SetRef(view, "titleText", title);
            SetRef(view, "timeText", time);
            SetRef(view, "starsText", stars);
            SetRef(view, "bestText", best);
            parts.Result = view;
        }

        private static TextMeshProUGUI TmpLabel(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize)
        {
            var center = new Vector2(0.5f, 0.5f);
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Anchor(rect, center, center, pos, size);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>The stage picker, inside the radar display. Round panel, arrows, name, best time, PLAY, RETURN.</summary>
        private static void BuildStageSelect(RectTransform radarRoot, HudParts parts)
        {
            var center = new Vector2(0.5f, 0.5f);

            var panel = Img("StageSelect", radarRoot, center, center, Vector2.zero, new Vector2(380f, 380f),
                new Color(0.02f, 0.06f, 0.05f, 0.97f), Circle);

            var title = Label("Title", panel, center, center, new Vector2(0f, 75f), 40, TextAnchor.MiddleCenter);
            var record = Label("Record", panel, center, center, new Vector2(0f, 25f), 22, TextAnchor.MiddleCenter);
            record.color = new Color(0.4f, 1f, 0.6f);

            var left = MakeButton("Left", panel, new Vector2(-135f, 75f), new Vector2(60f, 70f), "<", 52, new Color(1f, 1f, 1f, 0.06f));
            var right = MakeButton("Right", panel, new Vector2(135f, 75f), new Vector2(60f, 70f), ">", 52, new Color(1f, 1f, 1f, 0.06f));
            var play = MakeButton("Play", panel, new Vector2(0f, -45f), new Vector2(170f, 50f), "PLAY", 30, new Color(0.15f, 0.55f, 0.3f, 0.9f));
            var back = MakeButton("Return", panel, new Vector2(0f, -105f), new Vector2(150f, 38f), "RETURN", 22, new Color(0.3f, 0.3f, 0.3f, 0.8f));

            var view = panel.gameObject.AddComponent<StageSelectView>();
            SetRef(view, "leftButton", left);
            SetRef(view, "rightButton", right);
            SetRef(view, "playButton", play);
            SetRef(view, "returnButton", back);
            SetRef(view, "titleText", title);
            SetRef(view, "recordText", record);
            parts.Select = view;
        }

        private static Button MakeButton(string name, Transform parent, Vector2 pos, Vector2 size, string text, int fontSize, Color color)
        {
            var center = new Vector2(0.5f, 0.5f);
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Anchor(rect, center, center, pos, size);

            var image = go.GetComponent<Image>();
            image.sprite = Square;
            image.color = color; // raycast target stays on: the button needs it

            var label = Label("Text", rect, center, center, Vector2.zero, fontSize, TextAnchor.MiddleCenter);
            label.rectTransform.sizeDelta = size;
            label.text = text;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            return button;
        }

        /// <summary>Buttons need an EventSystem. With the new Input System it must use the Input System UI module.</summary>
        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

            var oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null) Object.DestroyImmediate(oldModule);

            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        private static void BuildCompass(Transform hud, HudParts parts)
        {
            var center2 = new Vector2(0.5f, 0.5f);
            var panel = Img("Compass", hud, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(190f, 190f),
                new Vector2(260f, 260f), new Color(0f, 0f, 0f, 0.55f), Circle);
            var group = panel.gameObject.AddComponent<CanvasGroup>();

            var needle = Img("Needle", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(8f, 100f), Color.white, Square);
            var locked = Img("LockedNeedle", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(8f, 100f), new Color(1f, 0.85f, 0.2f), Square);
            locked.SetAsFirstSibling();

            // N letter inside a full-size container, so the view can turn it around the compass center.
            var north = PlainRect("NorthMarker", panel, center2, center2, Vector2.zero, Vector2.zero);
            Stretch(north);
            Label("N", north, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), 26, TextAnchor.LowerCenter);

            parts.Playing.Add(panel.gameObject);
            var view = panel.gameObject.AddComponent<CompassView>();
            SetRef(view, "northMarker", north);
            SetRef(view, "needle", needle);
            SetRef(view, "lockedNeedle", locked);
            SetRef(view, "group", group);
            SetRef(view, "tickRoot", panel);
        }

        private static void BuildForceBar(Transform hud, HudParts parts)
        {
            var track = Img("ForceBar", hud, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
                new Vector2(600f, 44f), new Color(0f, 0f, 0f, 0.6f), Square);
            var group = track.gameObject.AddComponent<CanvasGroup>();

            var marker = Img("Marker", track, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(12f, 64f), Color.white, Square);
            var label = Label("Level", track, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), 26, TextAnchor.LowerCenter);

            parts.Playing.Add(track.gameObject);
            var view = track.gameObject.AddComponent<ForceBarView>();
            SetRef(view, "track", track);
            SetRef(view, "marker", marker);
            SetRef(view, "group", group);
            SetRef(view, "levelLabel", label);
        }

        /// <summary>Lights out: a black full-screen Image. Turn blackouts on in a Level Definition to see it.</summary>
        private static void BuildBlackout(Transform hud, HudParts parts)
        {
            var holder = new GameObject("Blackout", typeof(RectTransform));
            var holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(hud, false);
            Stretch(holderRect);

            var dark = Img("DarkOverlay", holderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero, new Color(0f, 0f, 0f, 0f), Square);
            Stretch(dark);
            dark.gameObject.SetActive(false);

            parts.Playing.Add(holder);
            var view = holder.AddComponent<BlackoutView>();
            SetRef(view, "darkOverlay", dark.GetComponent<Image>());
        }

        /// <summary>Low oxygen: siren (top left) and a red screen pulse. Swap the siren sprites for your own.</summary>
        private static void BuildAlarm(Transform hud, HudParts parts)
        {
            var holder = new GameObject("OxygenAlarm", typeof(RectTransform));
            var holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(hud, false);
            Stretch(holderRect);

            var overlay = Img("RedOverlay", holderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero, new Color(1f, 0f, 0f, 0f), Square);
            Stretch(overlay);

            var siren = Img("Siren", holderRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f),
                new Vector2(64f, 64f), new Color(1f, 0.2f, 0.2f, 0.3f), Circle);

            parts.Playing.Add(holder);
            var view = holder.AddComponent<OxygenAlarmView>();
            SetRef(view, "siren", siren.GetComponent<Image>());
            SetRef(view, "redOverlay", overlay.GetComponent<Image>());
        }

        private static void BuildOxygen(Transform hud, HudParts parts)
        {
            var back = Img("OxygenBar", hud, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f),
                new Vector2(520f, 36f), new Color(0f, 0f, 0f, 0.6f), Square);

            var fill = Img("Fill", back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-8f, -8f), new Color(0.2f, 0.8f, 1f), Square);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            var labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(back, false);
            Anchor(labelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 38f));
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 24;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            parts.Playing.Add(back.gameObject);
            var view = back.gameObject.AddComponent<OxygenBarView>();
            SetRef(view, "fill", fillImage);
            SetRef(view, "label", label);
        }

        private static void BuildRefill(Transform hud, HudParts parts)
        {
            var holder = new GameObject("Refill", typeof(RectTransform));
            var holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(hud, false);
            Anchor(holderRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(600f, 120f));

            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(holderRect, false);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

            var slotsGo = new GameObject("Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var slots = (RectTransform)slotsGo.transform;
            slots.SetParent(contentRect, false);
            slots.anchorMin = Vector2.zero;
            slots.anchorMax = Vector2.one;
            slots.offsetMin = slots.offsetMax = Vector2.zero;
            var layout = slotsGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var template = Img("SlotTemplate", slots, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(96f, 96f), Color.white, Square);
            template.gameObject.SetActive(false);

            parts.Playing.Add(holder);
            var view = holder.AddComponent<RefillSequenceView>();
            SetRef(view, "root", content);
            SetRef(view, "slotsParent", slots);
            SetRef(view, "slotTemplate", template.GetComponent<Image>());
        }

        private static void BuildStatus(Transform hud, HudParts parts)
        {
            var holder = new GameObject("Status", typeof(RectTransform));
            var holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(hud, false);
            holderRect.anchorMin = Vector2.zero;
            holderRect.anchorMax = Vector2.one;
            holderRect.offsetMin = holderRect.offsetMax = Vector2.zero;

            var timer = TmpLabel("Timer", holderRect, Vector2.zero, new Vector2(400f, 64f), 40);
            Anchor(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 64f));
            parts.Playing.Add(holder);
            var view = holder.AddComponent<RunStatusView>();
            SetRef(view, "timerText", timer);
        }

        // ---------- helpers ----------

        private static RectTransform PlainRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Anchor(rect, anchor, pivot, pos, size);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos,
            Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Anchor(rect, anchor, pivot, pos, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Anchor(rect, anchor, pivot, pos, new Vector2(400f, fontSize * 1.6f));
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void SetRefs(Object target, string property, IList<GameObject> values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"Property '{property}' not found on {target.GetType().Name}");
                return;
            }
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"Property '{property}' not found on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
