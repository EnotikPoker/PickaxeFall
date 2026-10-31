using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    // Imported block PNGs, plus procedural tools, effects, scenery and missing-asset fallback.
    public static class PixelArt
    {
        static readonly Dictionary<int, Sprite> Blocks = new();
        static readonly Dictionary<Color32, Sprite> Pickaxes = new();
        static readonly Sprite[] CrackSprites = new Sprite[9];
        public const float PickaxeScale = 1.15f;
        static Sprite square;
        static Sprite chest;
        public static Sprite Square => square ? square : square = Make(1, 1, new[] { Color.white });

        public static Sprite Chest()
        {
            if (chest) return chest;
            var pixels = new Color[256];
            Fill(pixels, 16, 1, 1, 14, 13, new Color32(53, 35, 20, 255));
            Fill(pixels, 16, 2, 2, 12, 11, new Color32(164, 110, 46, 255));
            Fill(pixels, 16, 3, 3, 10, 4, new Color32(139, 85, 34, 255));
            Fill(pixels, 16, 2, 8, 12, 1, new Color32(64, 40, 23, 255));
            Fill(pixels, 16, 3, 11, 10, 1, new Color32(205, 158, 75, 255));
            Fill(pixels, 16, 7, 6, 2, 4, new Color32(218, 218, 184, 255));
            Fill(pixels, 16, 7, 7, 1, 2, new Color32(135, 139, 124, 255));
            return chest = Make(16, 16, pixels);
        }

        public static Color BlockColor(BlockKind kind, bool deep = false) => kind switch
        {
            BlockKind.Dirt => new Color32(130, 88, 63, 255),
            BlockKind.Deepstone => new Color32(46, 53, 70, 255),
            BlockKind.Workbench => new Color32(187, 129, 67, 255),
            BlockKind.Tnt => new Color32(196, 63, 46, 255),
            _ => deep ? new Color32(46, 53, 70, 255) : new Color32(103, 111, 123, 255)
        };

        public static Sprite Block(BlockKind kind, int variant, bool deep = false, bool surface = false)
        {
            int key = (int)kind * 32 + variant % 4 + (deep ? 4 : 0) + (surface ? 8 : 0);
            if (Blocks.TryGetValue(key, out var cached) && cached) return cached;
            var imported = Resources.Load<Sprite>(BlockResourcePath(kind, variant, deep, surface));
            if (imported) return Blocks[key] = imported;
            var random = new System.Random(key * 7919 + 23);
            Color[] pixels = new Color[16 * 16];
            Color baseColor = BlockColor(kind, deep);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float noise = (float)random.NextDouble() * 0.18f - 0.09f;
                Color c = baseColor * (1 + noise);
                if (x == 0 || y == 0) c *= 0.65f;
                if (y == 15 || x == 15) c *= 1.14f;
                if (kind == BlockKind.Dirt && surface && y >= 12 - (x + variant) % 3)
                    c = new Color32(83, (byte)(137 + random.Next(30)), 56, 255);
                if (deep && kind != BlockKind.Dirt && kind != BlockKind.Workbench && kind != BlockKind.Tnt && y % 4 == 0)
                    c *= 0.82f;
                c.a = 1; pixels[y * 16 + x] = c;
            }
            Color ore = kind switch
            {
                BlockKind.Coal => new Color32(30, 36, 47, 255),
                BlockKind.Copper => new Color32(224, 141, 95, 255),
                BlockKind.Iron => new Color32(207, 184, 159, 255),
                BlockKind.Gold => new Color32(255, 202, 83, 255),
                BlockKind.Diamond => new Color32(92, 237, 217, 255), _ => Color.clear
            };
            if (ore.a > 0)
                for (int n = 0; n < 5; n++)
                {
                    int x = random.Next(2, 12), y = random.Next(2, 12);
                    for (int a = 0; a < 3; a++) for (int b = 0; b < 2; b++)
                    {
                        Color fleck = ore * (b == 0 ? 0.8f : 1);
                        fleck.a = 1;
                        pixels[(y + b) * 16 + x + a] = fleck;
                    }
                }
            if (kind == BlockKind.Workbench)
            {
                Color edge = new Color32(71, 43, 27, 255), wood = new Color32(158, 101, 51, 255);
                Fill(pixels, 16, 1, 1, 14, 13, wood);
                Fill(pixels, 16, 0, 0, 16, 2, edge);
                Fill(pixels, 16, 0, 13, 16, 3, new Color32(199, 151, 85, 255));
                // The top shows a clear 3x3 crafting grid, the front has legs and tools.
                for (int x = 2; x <= 14; x += 4) Fill(pixels, 16, x, 13, 1, 3, edge);
                Fill(pixels, 16, 1, 14, 14, 1, edge);
                Fill(pixels, 16, 1, 1, 2, 12, edge); Fill(pixels, 16, 13, 1, 2, 12, edge);
                Fill(pixels, 16, 3, 10, 10, 2, new Color32(110, 66, 35, 255));
                Fill(pixels, 16, 5, 4, 1, 6, new Color32(222, 185, 115, 255));
                Fill(pixels, 16, 4, 8, 3, 2, new Color32(189, 199, 194, 255));
                Fill(pixels, 16, 10, 4, 1, 6, new Color32(222, 185, 115, 255));
                Fill(pixels, 16, 9, 8, 3, 3, new Color32(77, 85, 84, 255));
            }
            if (kind == BlockKind.Tnt)
            {
                for (int x = 2; x < 16; x += 4) Fill(pixels, 16, x, 1, 1, 14, new Color32(116, 37, 31, 255));
                Fill(pixels, 16, 1, 5, 14, 6, new Color32(242, 231, 197, 255));
                string[] letters = { "###.#.#.###", ".#..###..#.", ".#..###..#.", ".#..#.#..#.", ".#..#.#..#." };
                for (int y = 0; y < letters.Length; y++) for (int x = 0; x < letters[y].Length; x++)
                    if (letters[y][x] == '#') pixels[(10 - y) * 16 + x + 2] = new Color32(55, 42, 35, 255);
                Fill(pixels, 16, 2, 14, 12, 1, new Color32(234, 110, 73, 255));
            }
            return Blocks[key] = Make(16, 16, pixels);
        }

        public static string BlockResourcePath(BlockKind kind, int variant, bool deep = false, bool surface = false)
        {
            string layer = kind == BlockKind.Dirt && surface ? "_Surface"
                : deep && kind != BlockKind.Dirt && kind != BlockKind.Workbench && kind != BlockKind.Tnt ? "_Deep" : "";
            string work = layer.Length == 0 && (kind == BlockKind.Coal || kind == BlockKind.Stone) ? "_Work" : "";
            return $"Blocks/{kind}/{kind}{layer}_{variant % 4}{work}";
        }

        public static Sprite Pickaxe(Color32 color)
        {
            if (Pickaxes.TryGetValue(color, out var cached) && cached) return cached;
            const int size = 32;
            var pixels = new Color[size * size];
            Color outline = Color.Lerp(color, new Color32(29, 38, 33, 255), 0.76f);
            var handle = new bool[size * size];
            var head = new bool[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 p = new((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                float u = (p.x - p.y) * 0.70710678f, v = (p.x + p.y) * 0.70710678f;
                handle[y * size + x] = Mathf.Abs(u) < 0.044f && v >= -0.43f && v <= 0.25f;
                head[y * size + x] = Mathf.Abs(u) <= 0.44f && Mathf.Abs(v - HeadCurve(u)) <= HeadThickness(u);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                bool[] mask = pass == 0 ? handle : head;
                for (int y = 1; y < size - 1; y++) for (int x = 1; x < size - 1; x++)
                    if (mask[y * size + x])
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        pixels[(y + dy) * size + x + dx] = pass == 0 ? new Color32(66, 42, 25, 255) : outline;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    if (mask[y * size + x])
                        pixels[y * size + x] = pass == 0
                            ? (Color)new Color32((byte)(x > y ? 187 : 140), (byte)(x > y ? 137 : 92), 58, 255)
                            : Color.Lerp(color, Color.white, (x + y) % 3 == 0 ? 0.22f : 0);
            }
            return Pickaxes[color] = Make(size, size, pixels);
        }

        static float HeadCurve(float u) => 0.25f - 0.23f * Mathf.Pow(Mathf.Abs(u) / 0.44f, 1.7f);
        static float HeadThickness(float u) => Mathf.Lerp(0.052f, 0.027f, Mathf.Abs(u) / 0.44f);
        static Vector2 ToolPoint(float u, float v) => new((v + u) * 0.70710678f, (v - u) * 0.70710678f);

        public static Vector2[] PickaxeHeadOutline()
        {
            const int steps = 9;
            var points = new Vector2[steps * 2];
            for (int i = 0; i < steps; i++)
            {
                float u = Mathf.Lerp(-0.44f, 0.44f, i / (float)(steps - 1));
                points[i] = ToolPoint(u, HeadCurve(u) + HeadThickness(u));
                points[steps * 2 - 1 - i] = ToolPoint(u, HeadCurve(u) - HeadThickness(u));
            }
            return points;
        }

        public static Sprite Cracks(int stage)
        {
            stage = Mathf.Clamp(stage, 1, 8);
            if (CrackSprites[stage]) return CrackSprites[stage];
            var pixels = new Color[256];
            int[][] paths =
            {
                new[] { 135, 150, 166, 181, 180, 195, 211, 226, 242 },
                new[] { 135, 136, 153, 154, 171, 172, 189, 190, 207 },
                new[] { 135, 119, 120, 104, 89, 73, 74, 58, 43, 27, 12 },
                new[] { 135, 134, 117, 116, 99, 98, 81, 80, 64 },
                new[] { 135, 151, 167, 168, 184, 201, 217, 218, 235, 251 },
                new[] { 135, 118, 102, 101, 85, 84, 68, 51, 35, 18, 2 }
            };
            foreach (var path in paths)
                for (int i = 0; i < Mathf.CeilToInt(path.Length * stage / 8f); i++)
                    pixels[path[i]] = new Color32(20, 18, 17, 245);
            // A light lip on the right of each dark fissure gives it depth.
            for (int y = 0; y < 16; y++) for (int x = 14; x >= 0; x--)
                if (pixels[y * 16 + x].a > 0.9f && pixels[y * 16 + x + 1].a == 0)
                    pixels[y * 16 + x + 1] = new Color32(193, 185, 165, 80);
            return CrackSprites[stage] = Make(16, 16, pixels);
        }

        public static Sprite Landscape()
        {
            const int width = 256, height = 128;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                Color color = Color.Lerp(new Color32(193, 226, 211, 255), new Color32(103, 172, 218, 255), y / 80f);
                int farRidge = 12 + Mathf.RoundToInt(Mathf.Sin(x * 0.065f) * 6 + Mathf.Sin(x * 0.15f) * 3);
                int nearRidge = 4 + Mathf.RoundToInt(Mathf.Sin(x * 0.11f + 2) * 4);
                if (y < farRidge) color = new Color32(93, 143, 142, 255);
                if (y < nearRidge) color = new Color32(62, 113, 103, 255);
                pixels[y * width + x] = color;
            }
            Fill(pixels, width, 140, 31, 4, 4, new Color32(255, 233, 161, 255));
            foreach (var cloud in new[] { new Vector2Int(34, 29), new Vector2Int(112, 35), new Vector2Int(175, 24), new Vector2Int(224, 42) })
            {
                Fill(pixels, width, cloud.x, cloud.y, 25, 3, new Color32(224, 239, 229, 255));
                Fill(pixels, width, cloud.x + 5, cloud.y + 3, 13, 3, new Color32(241, 247, 229, 255));
            }
            for (int x = 4; x < width - 5; x += 13)
            {
                int h = 5 + (x * 7) % 5;
                Fill(pixels, width, x, 0, 2, h, new Color32(65, 84, 66, 255));
                Fill(pixels, width, x - 3, h - 2, 8, 4, new Color32(50, 100, 79, 255));
                Fill(pixels, width, x - 1, h + 2, 4, 2, new Color32(57, 111, 82, 255));
            }
            return Make(width, height, pixels);
        }

        public static Sprite CaveWall()
        {
            const int width = 32, height = 64;
            var pixels = new Color[width * height];
            var random = new System.Random(819);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                Color c = new Color32(42, 44, 48, 255);
                c *= 0.94f + (float)random.NextDouble() * 0.12f;
                if (y % 6 == 0 || (x + (y / 6) * 5) % 11 == 0) c *= 0.82f;
                c.a = 1; pixels[y * width + x] = c;
            }
            return Make(width, height, pixels);
        }

        static void Fill(Color[] pixels, int width, int x, int y, int w, int h, Color color)
        {
            int height = pixels.Length / width;
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(height, y + h); yy++)
                for (int xx = Mathf.Max(0, x); xx < Mathf.Min(width, x + w); xx++) pixels[yy * width + xx] = color;
        }

        static Sprite Make(int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelArt" };
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
        }
    }
}
