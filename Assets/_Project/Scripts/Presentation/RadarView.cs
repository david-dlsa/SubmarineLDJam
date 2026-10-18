using System.Collections.Generic;
using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Navigation;
using SubmarineLDJam.Domain.Runs;
using SubmarineLDJam.Domain.Sonar;
using SubmarineLDJam.UseCases;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SubmarineLDJam.Presentation
{
    public enum RadarOrientation
    {
        /// <summary>Triangle always points up. The map turns around the center.</summary>
        HeadingUp,
        /// <summary>Map stays still, north is up. The triangle turns.</summary>
        NorthUp
    }

    /// <summary>
    /// Submarine radar. Round screen, submarine always in the center, map moves around it.
    /// The map can be much bigger than the screen.
    ///
    /// - Always on: grid, submarine triangle, exit dot (points where the exit is, not the path).
    /// - On M: sonar ping. A snapshot of everything on the "Sonar" layer around the submarine is taken.
    ///   A wave opens from the center to the edge, the picture stays a bit, then fades out.
    ///   The picture stays fixed in the world, so it slides when the submarine moves.
    /// </summary>
    public sealed class RadarView : RunView
    {
        [Header("Scene")]
        [SerializeField] private Transform submarine;
        [Tooltip("Sees only the Sonar layer. Renders every frame into a small texture. A ping copies that texture.")]
        [SerializeField] private Camera sonarCamera;
        [Tooltip("The normal game camera. Shows nothing by default, so the player only has the radar.")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("Start with the real world visible (debug). F2 toggles it.")]
        [SerializeField] private bool showWorld = false;

        [Header("Radar UI")]
        [Tooltip("Round area. Children are cut by the round mask. Pings are made inside.")]
        [SerializeField] private RectTransform pingLayers;
        [SerializeField] private Image disc;
        [SerializeField] private Image grid;
        [SerializeField] private Image edgeGlow;
        [SerializeField] private RectTransform exitDot;
        [SerializeField] private RectTransform subIcon;
        [Tooltip("Optional. Fill shows sonar charge.")]
        [SerializeField] private Image cooldownFill;
        [SerializeField] private Text cooldownLabel;

        [Header("Custom sprites (optional)")]
        [Tooltip("Sonar charge frames, from empty (first) to full (last). Shown on the Cooldown Fill image. Empty = normal fill bar.")]
        [SerializeField] private Sprite[] pingBarFrames;
        [Tooltip("Replaces the red arrow of the submarine. Empty = red arrow. It is not tinted.")]
        [SerializeField] private Sprite subIconSprite;

        [Header("Rotation")]
        [Tooltip("HeadingUp: triangle fixed pointing up, map turns around it. NorthUp: map fixed, triangle turns.")]
        [SerializeField] private RadarOrientation orientation = RadarOrientation.HeadingUp;
        [Tooltip("HeadingUp only: grid turns with the map, so you feel the turn.")]
        [SerializeField] private bool rotateGrid = true;
        [Tooltip("How fast the radar turns to the new direction. 0 = instant.")]
        [SerializeField, Min(0f)] private float turnSpeedDegreesPerSecond = 270f;

        [Header("Look")]
        [SerializeField] private int gridCells = 6;
        [SerializeField] private Color gridColor = new Color(0.2f, 0.9f, 0.5f, 0.35f);
        [SerializeField] private Color edgeGlowColor = new Color(0.2f, 0.9f, 0.5f, 0.6f);
        [SerializeField] private Color ringColor = new Color(0.4f, 1f, 0.6f, 0.9f);
        [SerializeField] private Color subColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color exitColor = new Color(1f, 0.65f, 0.1f);
        [Tooltip("Pixels. Exit dot never goes closer to the border than this.")]
        [SerializeField] private float exitEdgeMargin = 14f;
        [SerializeField] private int snapshotSize = 512;
        [Tooltip("The ping photo covers this many times the radar radius. 1 = only what the radar shows. Higher = fast movement never outruns the photo.")]
        [SerializeField, Min(1f)] private float captureAreaMultiplier = 2f;

        private sealed class Ping
        {
            public Vector2 Origin;
            public float Age;
            public RectTransform Root;
            public RectTransform Reveal;
            public RectTransform Ring;
            public Image RingImage;
            public CanvasGroup Group;
            public RenderTexture Texture;
        }

        private readonly List<Ping> _pings = new List<Ping>();
        private SubmarineRun _run;
        private DestinationZone _exit;
        private Sprite _discSprite;
        private Material _echoMaterial;
        private Image _exitImage;
        private Sprite _ringSprite;
        private RectTransform _liveLayer;
        private RenderTexture _live;
        private Vector2 _liveOrigin;
        private int _startFrame;
        private bool _pingWaiting;

        // The sonar camera needs a couple of frames to fill the live picture. Pings before that would be empty.
        private const int WarmupFrames = 3;
        private bool CameraWarm => Time.frameCount - _startFrame >= WarmupFrames;
        private float _shownAngle;

        /// <summary>
        /// Compass heading (degrees, clockwise) that points UP on the radar screen.
        /// HeadingUp: the submarine direction (map turns). NorthUp: 0.
        /// The compass HUD subtracts this, so the needle matches what the player sees.
        /// </summary>
        /// <summary>True while the radar is still turning to the submarine heading.</summary>
        public bool IsTurning => IsBound && submarine != null && Mathf.Abs(Mathf.DeltaAngle(_shownAngle, submarine.eulerAngles.z)) > 0.5f;

        public float ReferenceHeadingDegrees => orientation == RadarOrientation.HeadingUp ? -_shownAngle : 0f;

        protected override void OnBind()
        {
            _run = Run;

            _exit = FindFirstObjectByType<DestinationZone>();
            _exitImage = exitDot != null ? exitDot.GetComponent<Image>() : null;

            BuildSprites();
            StartLiveCamera();
            MakeEchoesUnlit();
            if (_run.Rules.SonarMode == SonarMode.Always) BuildLiveLayer();
            _shownAngle = submarine.eulerAngles.z;
            _pingWaiting = false;
            ApplyWorldVisibility();
        }

        protected override void OnUnbind()
        {
            _pingWaiting = false;
            foreach (var ping in _pings)
            {
                ReleaseTexture(ping);
                if (ping.Root != null) Destroy(ping.Root.gameObject);
            }
            _pings.Clear();

            if (_liveLayer != null)
            {
                Destroy(_liveLayer.gameObject);
                _liveLayer = null;
            }

            if (sonarCamera != null)
            {
                sonarCamera.targetTexture = null;
                sonarCamera.enabled = false;
            }

            if (_live != null)
            {
                _live.Release();
                Destroy(_live);
                _live = null;
            }

            _run = null;
        }

        private void LateUpdate()
        {
            if (!IsBound) return;

            // The sonar camera follows the submarine. What it renders this frame is the "live picture".
            Vector2 p = submarine.position;
            sonarCamera.transform.position = new Vector3(p.x, p.y, -10f);
            _liveOrigin = p;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                showWorld = !showWorld;
                ApplyWorldVisibility();
            }

            if (!IsBound) return;

            if (_pingWaiting && CameraWarm)
            {
                _pingWaiting = false;
                SpawnPing();
            }

            float radiusPx = pingLayers.rect.width * 0.5f;
            float viewRadius = _run.Rules.SonarViewRadius;
            float pixelsPerUnit = radiusPx / viewRadius;
            Vector2 subPos = submarine.position;

            float mapAngle = UpdateRotation();
            if (_liveLayer != null) _liveLayer.anchoredPosition = (_liveOrigin - subPos) * pixelsPerUnit;
            UpdatePings(subPos, radiusPx, pixelsPerUnit);
            UpdateExitDot(subPos, radiusPx, pixelsPerUnit, mapAngle);
            UpdateCooldown();
        }

        // ---------- setup ----------

        private void BuildSprites()
        {
            if (_discSprite != null) return; // made once, kept for the next runs
            _discSprite = RadarSprites.Disc(256);
            _ringSprite = RadarSprites.Ring(256, 0.02f);

            if (disc != null && disc.sprite == null) disc.sprite = _discSprite;
            if (grid != null)
            {
                if (grid.sprite == null) grid.sprite = RadarSprites.Grid(512, gridCells, 2);
                grid.color = gridColor;
            }
            if (edgeGlow != null)
            {
                if (edgeGlow.sprite == null) edgeGlow.sprite = RadarSprites.EdgeGlow(256);
                edgeGlow.color = edgeGlowColor;
            }
            if (subIcon != null)
            {
                var image = subIcon.GetComponent<Image>();
                if (subIconSprite != null)
                {
                    image.sprite = subIconSprite;
                    image.color = Color.white;
                    image.preserveAspect = true;
                }
                else
                {
                    if (image.sprite == null) image.sprite = RadarSprites.Triangle(64);
                    image.color = subColor;
                }
            }
            if (cooldownFill != null && pingBarFrames != null && pingBarFrames.Length > 0)
            {
                cooldownFill.type = Image.Type.Simple; // frames replace the fill
                cooldownFill.preserveAspect = true;
            }
            if (exitDot != null)
            {
                var image = exitDot.GetComponent<Image>();
                if (image.sprite == null) image.sprite = _discSprite;
                image.color = exitColor;
            }
        }

        private void StartLiveCamera()
        {
            // The camera sees more than the radar shows, so a ping photo already has what comes into view when you move.
            float radius = _run.Rules.SonarViewRadius * captureAreaMultiplier;
            int size = Mathf.Clamp(Mathf.RoundToInt(snapshotSize * captureAreaMultiplier), 64, 2048);
            _live = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
            _live.Create();

            _liveOrigin = submarine.position;
            _startFrame = Time.frameCount;
            sonarCamera.orthographic = true;
            sonarCamera.orthographicSize = radius;
            sonarCamera.transform.position = new Vector3(_liveOrigin.x, _liveOrigin.y, -10f);
            sonarCamera.targetTexture = _live;
            sonarCamera.enabled = true;
        }

        /// <summary>Always mode: show the live picture all the time. No ping, no fade.</summary>
        private void BuildLiveLayer()
        {
            float radiusPx = pingLayers.rect.width * 0.5f;
            _liveLayer = NewRect("Live", pingLayers);
            _liveLayer.SetAsFirstSibling();
            _liveLayer.sizeDelta = new Vector2(radiusPx * 2f, radiusPx * 2f) * captureAreaMultiplier;

            var raw = _liveLayer.gameObject.AddComponent<RawImage>();
            raw.texture = _live;
            raw.raycastTarget = false;
        }

        /// <summary>
        /// Sonar camera sees only the Sonar layer, so 2D lights (Default layer) do not reach it.
        /// Lit sprites would come out black. Echoes use an unlit material, so colors are exact.
        /// </summary>
        private void MakeEchoesUnlit()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            if (_echoMaterial == null) _echoMaterial = new Material(shader);
            var material = _echoMaterial;
            foreach (var spriteRenderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if ((sonarCamera.cullingMask & (1 << spriteRenderer.gameObject.layer)) != 0)
                    spriteRenderer.sharedMaterial = material;
            }
        }

        private void ApplyWorldVisibility()
        {
            if (worldCamera == null) return;
            worldCamera.cullingMask = showWorld ? ~0 : 0;
        }

        // ---------- events ----------

        protected override void OnEvent(IDomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case SonarPinged _:
                    if (CameraWarm) SpawnPing();
                    else _pingWaiting = true;
                    break;

                case SonarPingRejected _:
                    if (cooldownLabel != null) cooldownLabel.color = Color.red;
                    break;
            }
        }

        // ---------- ping ----------

        private void SpawnPing()
        {
            float radiusPx = pingLayers.rect.width * 0.5f;

            // 1. Take the picture: copy the live picture (made around the submarine last frame).
            var texture = new RenderTexture(_live.width, _live.height, 0, RenderTextureFormat.ARGB32);
            texture.Create();
            Graphics.Blit(_live, texture);
            Vector2 origin = _liveOrigin;

            // 2. UI: Root (moves with the world) > Reveal (round mask that grows) > Snapshot (the picture)
            var root = NewRect("Ping", pingLayers);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var reveal = NewRect("Reveal", root);
            var revealImage = reveal.gameObject.AddComponent<Image>();
            revealImage.sprite = _discSprite;
            revealImage.raycastTarget = false;
            var mask = reveal.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var snapshot = NewRect("Snapshot", reveal);
            snapshot.sizeDelta = new Vector2(radiusPx * 2f, radiusPx * 2f) * captureAreaMultiplier;
            var raw = snapshot.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;

            var ring = NewRect("Ring", root);
            var ringImage = ring.gameObject.AddComponent<Image>();
            ringImage.sprite = _ringSprite;
            ringImage.color = ringColor;
            ringImage.raycastTarget = false;

            _pings.Add(new Ping
            {
                Origin = origin,
                Root = root,
                Reveal = reveal,
                Ring = ring,
                RingImage = ringImage,
                Group = group,
                Texture = texture
            });
        }

        private void UpdatePings(Vector2 subPos, float radiusPx, float pixelsPerUnit)
        {
            var rules = _run.Rules;
            float wave = rules.SonarWaveSeconds;
            float hold = rules.SonarHoldSeconds;
            float fade = rules.SonarFadeSeconds;
            float total = wave + hold + fade;

            for (int i = _pings.Count - 1; i >= 0; i--)
            {
                var ping = _pings[i];
                ping.Age += Time.deltaTime;

                if (ping.Age >= total)
                {
                    ReleaseTexture(ping);
                    Destroy(ping.Root.gameObject);
                    _pings.RemoveAt(i);
                    continue;
                }

                // Picture is fixed in the world: it moves when the submarine moves.
                ping.Root.anchoredPosition = (ping.Origin - subPos) * pixelsPerUnit;

                // Wave: round mask grows from 0 to the full radar width.
                // Same speed as before (visible edge after SonarWaveSeconds), but it keeps going out to the photo edge,
                // so the far parts show up when the submarine moves toward them.
                float progress = Mathf.Clamp01(ping.Age / wave);
                float waveRadius = Mathf.Min(ping.Age * radiusPx / wave, radiusPx * captureAreaMultiplier);
                float diameter = waveRadius * 2f;
                ping.Reveal.sizeDelta = new Vector2(diameter, diameter);
                ping.Ring.sizeDelta = new Vector2(diameter, diameter);

                var ringColorNow = ringColor;
                ringColorNow.a = ringColor.a * (1f - progress);
                ping.RingImage.color = ringColorNow;

                // Hold, then fade.
                float fadeAge = ping.Age - wave - hold;
                ping.Group.alpha = fadeAge <= 0f ? 1f : 1f - Mathf.Clamp01(fadeAge / fade);
            }
        }

        private static void ReleaseTexture(Ping ping)
        {
            if (ping.Texture == null) return;
            ping.Texture.Release();
            Destroy(ping.Texture);
            ping.Texture = null;
        }

        // ---------- always-on parts ----------

        /// <summary>
        /// Turns either the triangle (NorthUp) or the whole map (HeadingUp).
        /// Returns the angle the map is turned by, so other parts can follow it.
        /// </summary>
        private float UpdateRotation()
        {
            float target = submarine.eulerAngles.z;
            _shownAngle = turnSpeedDegreesPerSecond <= 0f
                ? target
                : Mathf.MoveTowardsAngle(_shownAngle, target, turnSpeedDegreesPerSecond * Time.deltaTime);

            bool headingUp = orientation == RadarOrientation.HeadingUp;
            float mapAngle = headingUp ? -_shownAngle : 0f;

            pingLayers.localEulerAngles = new Vector3(0f, 0f, mapAngle);
            if (grid != null) grid.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotateGrid ? mapAngle : 0f);
            if (subIcon != null) subIcon.localEulerAngles = new Vector3(0f, 0f, headingUp ? 0f : _shownAngle);

            return mapAngle;
        }

        private void UpdateExitDot(Vector2 subPos, float radiusPx, float pixelsPerUnit, float mapAngle)
        {
            if (exitDot == null) return;

            // Exit hint: always on, or only for a while after a pickup. Blinks when about to go away.
            var hint = _run.ExitHint;
            bool visible = _exit != null && hint.IsVisible;
            if (exitDot.gameObject.activeSelf != visible) exitDot.gameObject.SetActive(visible);
            if (!visible) return;

            if (_exitImage != null)
            {
                bool ending = hint.Mode == ExitHintMode.OnPickup && hint.RemainingSeconds < 3f;
                var color = exitColor;
                color.a = ending && Mathf.Sin(Time.time * 14f) < 0f ? 0.2f : 1f;
                _exitImage.color = color;
            }

            Vector2 delta = ((Vector2)_exit.transform.position - subPos) * pixelsPerUnit;
            delta = Quaternion.Euler(0f, 0f, mapAngle) * delta; // follows the map turn
            float limit = radiusPx - exitEdgeMargin;

            // Far away: dot sits on the edge, showing only the side. Close: dot shows the real place.
            if (delta.magnitude > limit) delta = delta.normalized * limit;

            exitDot.anchoredPosition = delta;
            float pulse = 1f + 0.2f * Mathf.Sin(Time.time * 4f);
            exitDot.localScale = new Vector3(pulse, pulse, 1f);
        }

        /// <summary>Shows the sonar charge, 0..1. Uses the custom frames when there are any.</summary>
        private void SetCharge(float charge)
        {
            if (cooldownFill == null) return;

            charge = Mathf.Clamp01(charge);
            if (pingBarFrames != null && pingBarFrames.Length > 0)
            {
                int last = pingBarFrames.Length - 1;
                var frame = pingBarFrames[Mathf.Clamp(Mathf.FloorToInt(charge * last + 0.0001f), 0, last)];
                if (frame != null) cooldownFill.sprite = frame;
                return;
            }

            cooldownFill.fillAmount = charge;
        }

        private void UpdateCooldown()
        {
            var sonar = _run.Sonar;

            switch (sonar.Mode)
            {
                case SonarMode.Always:
                    SetCharge(1f);
                    if (cooldownLabel != null) cooldownLabel.text = "SONAR ALWAYS ON";
                    break;

                case SonarMode.Auto:
                    SetCharge(1f - sonar.CooldownPercent);
                    if (cooldownLabel != null) cooldownLabel.text = $"NEXT PING {sonar.CooldownRemaining:0.0}s";
                    break;

                default:
                    SetCharge(1f - sonar.CooldownPercent);
                    if (cooldownLabel == null) break;

                    if (sonar.IsReady)
                    {
                        cooldownLabel.color = Color.white;
                        cooldownLabel.text = "SONAR READY [M]";
                    }
                    else
                    {
                        cooldownLabel.text = $"RECHARGING {sonar.CooldownRemaining:0.0}s";
                        // Red flash from a rejected press fades back to white.
                        cooldownLabel.color = Color.Lerp(cooldownLabel.color, Color.white, Time.deltaTime * 4f);
                    }
                    break;
            }
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }
    }
}
