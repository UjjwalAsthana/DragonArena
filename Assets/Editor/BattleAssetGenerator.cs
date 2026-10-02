using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DragonBattle.EditorTools
{
    /// <summary>Generates the placeholder textures, ability icons and sound effects used by the battle scene.</summary>
    public static class BattleAssetGenerator
    {
        public const string TexDir = "Assets/Textures";
        public const string AudioDir = "Assets/Audio";
        private const int SampleRate = 44100;

        // ---------------------------------------------------------------- textures

        public static void GenerateTextures()
        {
            Directory.CreateDirectory(TexDir);

            SaveTexture("White", 4, 4, (u, v) => Color.white, sprite: true);
            SaveTexture("Circle", 128, 128, (u, v) =>
            {
                float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                return new Color(1, 1, 1, Mathf.Clamp01((1f - r) * 64f));
            }, sprite: true);
            SaveTexture("Glow", 64, 64, (u, v) =>
            {
                float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                return new Color(1, 1, 1, a);
            });
            SaveTexture("Ring", 256, 256, RingPixel);
            SaveStoneTexture();

            SaveTexture("Icon_Fire", 128, 128, (u, v) => IconPixel(u, v, new Color(0.55f, 0.1f, 0.05f), new Color(0.95f, 0.45f, 0.1f), FlameGlyph), sprite: true);
            SaveTexture("Icon_Tail", 128, 128, (u, v) => IconPixel(u, v, new Color(0.05f, 0.3f, 0.15f), new Color(0.25f, 0.75f, 0.35f), SpiralGlyph), sprite: true);
            SaveTexture("Icon_Fly", 128, 128, (u, v) => IconPixel(u, v, new Color(0.08f, 0.2f, 0.5f), new Color(0.35f, 0.65f, 1f), WingGlyph), sprite: true);

            AssetDatabase.Refresh();
        }

        private static void SaveTexture(string name, int w, int h, Func<float, float, Color> pixel, bool sprite = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply();

            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = !sprite;
            if (sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
            }
            importer.SaveAndReimport();
        }

        private static void SaveStoneTexture()
        {
            const int size = 256, tiles = 4;
            var rng = new System.Random(7);
            var tileShade = new float[tiles, tiles];
            for (int i = 0; i < tiles; i++)
                for (int j = 0; j < tiles; j++)
                    tileShade[i, j] = 0.85f + (float)rng.NextDouble() * 0.3f;

            SaveTexture("Stone", size, size, (u, v) =>
            {
                float fu = u * tiles, fv = v * tiles;
                int tx = Mathf.Min(tiles - 1, (int)fu), ty = Mathf.Min(tiles - 1, (int)fv);
                float edge = Mathf.Min(Mathf.Min(fu - tx, tx + 1 - fu), Mathf.Min(fv - ty, ty + 1 - fv));
                float mortar = Mathf.SmoothStep(0.7f, 1f, 1f - Mathf.Clamp01(edge / 0.035f));
                float grain = Mathf.PerlinNoise(u * 60f, v * 60f) * 0.25f + Mathf.PerlinNoise(u * 14f, v * 14f) * 0.2f;
                float shade = tileShade[tx, ty] * (0.75f + grain);
                Color stone = new Color(0.46f, 0.47f, 0.52f) * shade;
                Color c = Color.Lerp(stone, new Color(0.12f, 0.12f, 0.14f), mortar);
                c.a = 1f;
                return c;
            });
        }

        private static Color RingPixel(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = 0f;
            a = Mathf.Max(a, Band(r, 0.92f, 0.012f));
            a = Mathf.Max(a, Band(r, 0.86f, 0.006f) * 0.6f);
            a = Mathf.Max(a, Band(r, 0.45f, 0.008f) * 0.8f);
            float ang = Mathf.Atan2(v - 0.5f, u - 0.5f);
            float spokes = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 40f);
            if (r > 0.45f && r < 0.86f) a = Mathf.Max(a, spokes * 0.5f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        private static float Band(float r, float center, float halfWidth) =>
            Mathf.Clamp01(1f - Mathf.Abs(r - center) / halfWidth);

        // Icon background: round gradient badge with a bright rim, glyph drawn on top in white / yellow.
        private static Color IconPixel(float u, float v, Color dark, Color light, Func<float, float, Color> glyph)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float cover = Mathf.Clamp01((0.98f - r) * 40f);
            if (cover <= 0f) return Color.clear;

            Color bg = Color.Lerp(dark, light, Mathf.Clamp01(v * 0.9f));
            float rim = Mathf.Clamp01((r - 0.86f) / 0.1f);
            bg = Color.Lerp(bg, Color.Lerp(light, Color.white, 0.5f), rim);

            Color g = glyph(u, v);
            Color c = Color.Lerp(bg, g, g.a);
            c.a = cover;
            return c;
        }

        private static Color FlameGlyph(float u, float v)
        {
            float t = (v - 0.16f) / 0.7f;
            if (t < 0f || t > 1f) return Color.clear;

            // Outer flame: round base, pointed tip that curls slightly to one side.
            float x = u - 0.5f - Mathf.Sin(t * 3f) * 0.05f * t;
            float hw = 0.33f * Mathf.Pow(1f - t, 1.1f) * Mathf.Sqrt(Mathf.Min(1f, t * 5f));
            if (Mathf.Abs(x) >= hw) return Color.clear;

            // Inner, brighter core.
            float ti = t / 0.6f;
            if (ti < 1f)
            {
                float hwi = 0.16f * Mathf.Pow(1f - ti, 1.1f) * Mathf.Sqrt(Mathf.Min(1f, ti * 5f));
                if (Mathf.Abs(x) < hwi) return new Color(1f, 0.95f, 0.55f, 1f);
            }
            return new Color(1f, 0.72f, 0.25f, 1f);
        }

        private static Color SpiralGlyph(float u, float v)
        {
            float x = u - 0.5f, y = v - 0.5f;
            float r = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            float k = Mathf.Clamp01((ang - 20f) / 300f);
            float target = 0.1f + 0.24f * k;
            if (ang < 20f || ang > 320f) return Color.clear;
            return Mathf.Abs(r - target) < 0.05f ? new Color(0.95f, 1f, 0.9f, 1f) : Color.clear;
        }

        private static Color WingGlyph(float u, float v)
        {
            float x = Mathf.Abs(u - 0.5f);
            if (x > 0.38f) return Color.clear;
            for (int i = 0; i < 2; i++)
            {
                float y = 0.36f + 0.5f * x - i * 0.14f;
                if (Mathf.Abs(v - y) < 0.045f) return new Color(0.95f, 0.98f, 1f, 1f);
            }
            if (Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.4f)) < 0.06f)
                return new Color(0.95f, 0.98f, 1f, 1f);
            return Color.clear;
        }

        // ---------------------------------------------------------------- audio

        public static void GenerateAudio()
        {
            Directory.CreateDirectory(AudioDir);
            var rng = new System.Random(1234);
            float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

            // Fire: filtered noise with swell + low rumble.
            SaveWav("Fire", 1.3f, (t, d) =>
            {
                float env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((d - t) / 0.3f);
                lpFire += 0.22f * (Noise() - lpFire);
                return (lpFire * 2.2f + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.25f) * env * 0.8f;
            });

            // Roar: pitched saw with distortion.
            SaveWav("Roar", 0.8f, (t, d) =>
            {
                float f = Mathf.Lerp(130f, 65f, t / d);
                roarPhase += f / SampleRate;
                float saw = (roarPhase % 1f) * 2f - 1f;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Pow(Mathf.Clamp01(1f - t / d), 0.7f);
                return (float)Math.Tanh((saw + Noise() * 0.4f) * 2.5f) * env * 0.7f;
            });

            // Tail whoosh: noise with a rising then falling filter.
            SaveWav("TailWhoosh", 0.45f, (t, d) =>
            {
                float k = Mathf.Sin(Mathf.PI * t / d);
                lpWhoosh += (0.05f + 0.4f * k) * (Noise() - lpWhoosh);
                return lpWhoosh * 2.5f * k;
            });

            // Tail hit: low thump + noise crack.
            SaveWav("TailHit", 0.35f, (t, d) =>
            {
                float env = Mathf.Exp(-t * 12f);
                float f = Mathf.Lerp(160f, 45f, Mathf.Clamp01(t / 0.2f));
                thumpPhase += f / SampleRate;
                return (Mathf.Sin(2f * Mathf.PI * thumpPhase) * 0.9f + Noise() * 0.5f * Mathf.Exp(-t * 40f)) * env;
            });

            // Wing flaps: two soft low-passed noise pulses.
            SaveWav("Flap", 0.6f, (t, d) =>
            {
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t * 3f)), 2f) * Mathf.Clamp01((d - t) / 0.2f);
                lpFlap += 0.08f * (Noise() - lpFlap);
                return lpFlap * 4f * pulse;
            });

            // Slam: big sub drop with debris noise.
            SaveWav("Slam", 0.8f, (t, d) =>
            {
                float f = Mathf.Lerp(90f, 28f, Mathf.Clamp01(t / 0.4f));
                slamPhase += f / SampleRate;
                float env = Mathf.Exp(-t * 5f);
                return (Mathf.Sin(2f * Mathf.PI * slamPhase) + Noise() * 0.6f * Mathf.Exp(-t * 14f)) * env;
            });

            // Generic impact.
            SaveWav("Hit", 0.25f, (t, d) =>
            {
                float env = Mathf.Exp(-t * 20f);
                float f = Mathf.Lerp(220f, 80f, Mathf.Clamp01(t / 0.12f));
                hitPhase += f / SampleRate;
                return (Mathf.Sin(2f * Mathf.PI * hitPhase) * 0.7f + Noise() * 0.6f) * env;
            });

            // Win jingle: C - E - G - C arpeggio.
            SaveWav("Win", 1.6f, (t, d) =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                int i = Mathf.Min(3, (int)(t / 0.22f));
                float nt = t - i * 0.22f;
                float env = Mathf.Exp(-nt * 3f) * Mathf.Clamp01((d - t) / 0.4f);
                return (Mathf.Sin(2f * Mathf.PI * notes[i] * t) + 0.4f * Mathf.Sin(4f * Mathf.PI * notes[i] * t)) * env * 0.35f;
            });

            SaveWav("Beep", 0.15f, (t, d) => Mathf.Sin(2f * Mathf.PI * 660f * t) * Mathf.Clamp01((d - t) / 0.05f) * 0.5f);
            SaveWav("Go", 0.45f, (t, d) => Mathf.Sin(2f * Mathf.PI * 990f * t) * Mathf.Exp(-t * 6f) * 0.55f);

            // Music: 4-bar loop at 100 bpm, kick + hat + dark bass line.
            SaveWav("Music", 9.6f, (t, d) =>
            {
                float beat = 0.6f;
                float bt = t % beat;
                float kick = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 45f, Mathf.Clamp01(bt / 0.12f)) * bt) * Mathf.Exp(-bt * 10f);
                float ht = (t + beat * 0.5f) % beat;
                float hat = Noise() * Mathf.Exp(-ht * 60f) * 0.25f;
                float[] bass = { 55f, 55f, 65.41f, 49f };
                float bf = bass[(int)(t / 2.4f) % 4];
                float bassWave = Mathf.Sin(2f * Mathf.PI * bf * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * bf * 1.5f * t) * 0.08f;
                return kick * 0.6f + hat + bassWave;
            });

            AssetDatabase.Refresh();
        }

        // Filter / phase state captured by the lambdas above.
        private static float lpFire, lpWhoosh, lpFlap, roarPhase, thumpPhase, slamPhase, hitPhase;

        private static void SaveWav(string name, float seconds, Func<float, float, float> sample)
        {
            lpFire = lpWhoosh = lpFlap = roarPhase = thumpPhase = slamPhase = hitPhase = 0f;

            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new short[count];
            for (int i = 0; i < count; i++)
            {
                float s = Mathf.Clamp(sample(i / (float)SampleRate, seconds), -1f, 1f);
                data[i] = (short)(s * 0.9f * short.MaxValue);
            }

            string path = $"{AudioDir}/{name}.wav";
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = count * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(SampleRate);
                w.Write(SampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                foreach (short s in data) w.Write(s);
            }
        }
    }
}
