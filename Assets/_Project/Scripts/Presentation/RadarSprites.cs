using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Makes simple sprites in code, so the radar works with no art.
    /// Put your own sprite in the Image and these are not used.
    /// </summary>
    public static class RadarSprites
    {
        public static Sprite Disc(int size = 256) => Make(size, (x, y) => Smooth(0.5f - Dist(x, y)));

        public static Sprite Ring(int size = 256, float thickness = 0.04f)
            => Make(size, (x, y) =>
            {
                float d = Dist(x, y);
                return Smooth(thickness - Mathf.Abs(d - (0.5f - thickness)));
            });

        /// <summary>Glow near the border. Transparent in the middle.</summary>
        public static Sprite EdgeGlow(int size = 256)
            => Make(size, (x, y) =>
            {
                float d = Dist(x, y) / 0.5f;           // 0 center, 1 edge
                float glow = Mathf.Clamp01((d - 0.65f) / 0.35f);
                return glow * glow * 0.55f * Smooth(0.5f - Dist(x, y));
            });

        /// <summary>Square grid lines. Cells x cells.</summary>
        public static Sprite Grid(int size = 512, int cells = 6, int linePixels = 2)
        {
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            float cell = (float)size / cells;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool line = Near(x, cell, linePixels) || Near(y, cell, linePixels);
                    pixels[y * size + x] = line ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            return Finish(texture, pixels, size);
        }

        /// <summary>Triangle pointing up.</summary>
        public static Sprite Triangle(int size = 64)
        {
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 4 samples per pixel for soft edges
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (InsideTriangle((x + 0.25f + sx * 0.5f) / size, (y + 0.25f + sy * 0.5f) / size)) hits++;

                    byte a = (byte)(hits * 255 / 4);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            return Finish(texture, pixels, size);
        }

        // ---------- helpers ----------

        private static bool InsideTriangle(float u, float v)
        {
            // Tip at (0.5, 0.95), base from (0.1, 0.05) to (0.9, 0.05)
            if (v < 0.05f || v > 0.95f) return false;
            float t = (v - 0.05f) / 0.9f;         // 0 base, 1 tip
            float half = 0.4f * (1f - t);
            return Mathf.Abs(u - 0.5f) <= half;
        }

        private static bool Near(int p, float cell, int linePixels)
        {
            float m = p % cell;
            return m < linePixels * 0.5f || m > cell - linePixels * 0.5f;
        }

        private static float Dist(float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Smooth(float v) => Mathf.Clamp01(v * 128f);

        private static Sprite Make(int size, System.Func<float, float, float> alphaAt)
        {
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alphaAt((x + 0.5f) / size, (y + 0.5f) / size));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            return Finish(texture, pixels, size);
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private static Sprite Finish(Texture2D texture, Color32[] pixels, int size)
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
