using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    // Original procedural pixel art. No external textures or Minecraft assets.
    public static class PixelArt
    {
        static readonly Dictionary<int, Sprite> Blocks = new();
        static readonly Dictionary<Color32, Sprite> Pickaxes = new();
        static Sprite square;
        public static Sprite Square => square ? square : square = Make(1, 1, new[] { Color.white });

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
                        pixels[(y + b) * 16 + x + a] = ore * (b == 0 ? 0.8f : 1);
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

        public static Sprite Pickaxe(Color32 color)
        {
            if (Pickaxes.TryGetValue(color, out var cached) && cached) return cached;
            var pixels = new Color[16 * 16];
            Color outline = Color.Lerp(color, new Color32(29, 38, 33, 255), 0.76f);
            // Long diagonal wooden handle and the familiar broad, curved pick head.
            for (int i = 2; i <= 10; i++)
            {
                Fill(pixels, 16, i - 1, i - 2, 3, 3, new Color32(74, 47, 27, 255));
                pixels[(i - 1) * 16 + i] = new Color32(185, 135, 73, 255);
                pixels[(i - 1) * 16 + i - 1] = new Color32(131, 85, 43, 255);
            }
            bool[] mask = new bool[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                mask[y * 16 + x] = (y == 12 && x >= 4 && x <= 10) || (y == 11 && x >= 3 && x <= 12) ||
                    (y == 10 && x >= 10 && x <= 13) || (y == 9 && x >= 11 && x <= 13) ||
                    (y >= 6 && y <= 8 && x >= 12 && x <= 13);
            for (int y = 1; y < 15; y++) for (int x = 1; x < 15; x++)
                if (mask[y * 16 + x])
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        pixels[(y + dy) * 16 + x + dx] = outline;
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                if (mask[y * 16 + x])
                    pixels[y * 16 + x] = Color.Lerp(color, Color.white, y == 12 || x == 13 ? 0.28f : 0);
            return Pickaxes[color] = Make(16, 16, pixels);
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
