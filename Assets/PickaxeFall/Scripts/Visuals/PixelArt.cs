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

        public static Color BlockColor(BlockKind kind) => kind switch
        {
            BlockKind.Dirt => new Color32(130, 88, 63, 255),
            BlockKind.Deepstone => new Color32(46, 53, 70, 255),
            BlockKind.Workbench => new Color32(187, 129, 67, 255),
            _ => new Color32(80, 93, 111, 255)
        };

        public static Sprite Block(BlockKind kind, int variant)
        {
            int key = (int)kind * 4 + variant % 4;
            if (Blocks.TryGetValue(key, out var cached)) return cached;
            var random = new System.Random(key * 7919 + 23);
            Color[] pixels = new Color[16 * 16];
            Color baseColor = BlockColor(kind);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float noise = (float)random.NextDouble() * 0.18f - 0.09f;
                Color c = baseColor * (1 + noise);
                if (x == 0 || y == 0) c *= 0.65f;
                if (y == 15 || x == 15) c *= 1.14f;
                if (kind == BlockKind.Dirt && variant == 0 && y > 12)
                    c = new Color32(90, (byte)(142 + random.Next(20)), 83, 255);
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
                for (int y = 3; y < 13; y++) for (int x = 2; x < 14; x++)
                    if (y > 9 || x < 4 || x > 11 || (x + y) % 6 == 0)
                        pixels[y * 16 + x] = new Color32(78, 48, 33, 255);
                for (int x = 5; x < 11; x++) pixels[13 * 16 + x] = new Color32(255, 210, 120, 255);
            }
            return Blocks[key] = Make(16, 16, pixels);
        }

        public static Sprite Pickaxe(Color32 color)
        {
            if (Pickaxes.TryGetValue(color, out var cached)) return cached;
            var pixels = new Color[24 * 24];
            for (int y = 3; y < 18; y++) for (int x = 0; x < 24; x++)
                if (Mathf.Abs(x - y) < 2)
                    pixels[y * 24 + x] = x < y ? new Color32(108, 64, 44, 255) : new Color32(183, 121, 66, 255);
            for (int y = 12; y < 22; y++) for (int x = 5; x < 23; x++)
            {
                bool head = (y >= 18 && x >= 7 && x <= 19) ||
                            (x >= 18 && y >= 13 && y < 19) || (x >= 5 && x < 8 && y >= 16 && y <= 19);
                if (head) pixels[y * 24 + x] = (Color)color * (y == 18 || x == 18 ? 0.72f : 1);
            }
            return Pickaxes[color] = Make(24, 24, pixels);
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
