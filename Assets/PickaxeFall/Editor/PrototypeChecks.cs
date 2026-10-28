using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class PrototypeChecks
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        public static void Validate(GameBalance balance)
        {
            Require(balance.RollChance(2) > balance.RollChance(3), "Gold must be more common than iron.");
            foreach (BlockKind kind in Enum.GetValues(typeof(BlockKind)))
                foreach (bool deep in new[] { false, true })
                    foreach (Color32 pixel in PixelArt.Block(kind, 0, deep).texture.GetPixels32())
                        Require(pixel.a == 255, "Block texture must remain fully opaque: " + kind);
            int previous = 0;
            for (int stage = 1; stage <= 8; stage++)
            {
                int dark = 0;
                foreach (Color32 pixel in PixelArt.Cracks(stage).texture.GetPixels32()) if (pixel.a > 200) dark++;
                Require(dark > previous, "Every crack stage must add more fissures."); previous = dark;
            }
            var tool = PixelArt.Pickaxe(balance.pickaxes[3].color);
            Color32[] pixels = tool.texture.GetPixels32(); int width = tool.texture.width;
            var occupied = new List<Vector2>();
            for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
            {
                Require(pixels[y * width + x].a == pixels[x * width + y].a, "Pickaxe silhouette must be symmetric about its handle.");
                if (pixels[y * width + x].a > 0) occupied.Add(new Vector2(x, y) / width * PixelArt.PickaxeScale);
            }
            float diameter = 0;
            foreach (var a in occupied) foreach (var b in occupied) diameter = Mathf.Max(diameter, Vector2.Distance(a, b));
            Require(diameter <= 1.2f, "Pickaxe should be approximately one block in length.");

            int benches = 0, tnt = 0, central = 0, outer = 0;
            const int maps = 32, depth = 160;
            for (int seed = 1; seed <= maps; seed++)
            {
                var generator = new MineGenerator(balance, seed * 7919);
                var benchPositions = new List<Vector2Int>(); var tntPositions = new List<Vector2Int>();
                for (int row = 0; row < depth; row++) for (int x = -balance.worldWidth / 2; x <= balance.worldWidth / 2; x++)
                {
                    var cell = generator.Sample(x, row);
                    if (!cell.exists) continue;
                    var same = generator.Sample(x, row);
                    Require(cell.kind == same.kind && cell.variant == same.variant, "Cell sampling must be repeatable.");
                    if (cell.kind == BlockKind.Workbench)
                    {
                        Require(row >= balance.workbenchMinimumDepth, "Workbench appeared above its depth gate.");
                        foreach (var old in benchPositions)
                            Require((old - new Vector2Int(x, row)).sqrMagnitude >= balance.workbenchSpacing * balance.workbenchSpacing, "Workbenches are too close.");
                        benchPositions.Add(new Vector2Int(x, row)); benches++;
                        if (Mathf.Abs(x) <= 2) central++; else outer++;
                    }
                    if (cell.kind == BlockKind.Tnt)
                    {
                        Require(row >= balance.tntMinimumDepth, "TNT appeared above its depth gate.");
                        foreach (var old in tntPositions)
                            Require((old - new Vector2Int(x, row)).sqrMagnitude >= balance.tntSpacing * balance.tntSpacing, "TNT spacing failed.");
                        tntPositions.Add(new Vector2Int(x, row)); tnt++;
                    }
                }
                // Query in reverse order as a separate streaming/camera path would do.
                var other = new MineGenerator(balance, seed * 7919);
                for (int row = depth - 1; row >= 0; row -= 7) for (int x = balance.worldWidth / 2; x >= -balance.worldWidth / 2; x--)
                {
                    var a = generator.Sample(x, row); var b = other.Sample(x, row);
                    Require(a.exists == b.exists && a.kind == b.kind, "Generation must not depend on query order or pickaxe route.");
                }
            }
            float ratio = (central / 5f) / (outer / (balance.worldWidth - 5f));
            Require(benches > 100 && tnt > 100 && ratio > 0.65f && ratio < 1.5f, "Special blocks are unexpectedly biased toward the central path.");
            Debug.Log($"PICKAXEFALL_GENERATOR_OK: {maps} maps, benches={benches}, TNT={tnt}, center/outside density={ratio:F3}, spacing={balance.workbenchSpacing}/{balance.tntSpacing}; route-independent.");
            Debug.Log($"PICKAXEFALL_VISUALS_OK: opaque blocks, 8 increasing crack stages, symmetric tool, visible diameter={diameter:F3} blocks.");
        }

        public static void ExportPreview(GameBalance balance)
        {
            const int width = 696, height = 224;
            var canvas = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var background = new Color[width * height];
            for (int i = 0; i < background.Length; i++) background[i] = new Color32(24, 31, 37, 255);
            canvas.SetPixels(background);
            for (int stage = 0; stage < 8; stage++)
            {
                Blit(canvas, PixelArt.Block(BlockKind.Stone, 0), 8 + stage * 86, 136, 4);
                if (stage > 0) Blit(canvas, PixelArt.Cracks(stage), 8 + stage * 86, 136, 4);
            }
            for (int tier = 0; tier < balance.pickaxes.Length; tier++)
                Blit(canvas, PixelArt.Pickaxe(balance.pickaxes[tier].color), 16 + tier * 136, 24, 2 * PixelArt.PickaxeScale);
            canvas.Apply(); Directory.CreateDirectory("Docs/Screenshots");
            File.WriteAllBytes("Docs/Screenshots/06-cracks-and-pickaxes.png", canvas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(canvas);
        }

        static void Blit(Texture2D target, Sprite sprite, int left, int bottom, float scale)
        {
            var texture = sprite.texture;
            for (int y = 0; y < Mathf.CeilToInt(texture.height * scale); y++)
            for (int x = 0; x < Mathf.CeilToInt(texture.width * scale); x++)
            {
                Color pixel = texture.GetPixel(Mathf.Min(texture.width - 1, Mathf.FloorToInt(x / scale)), Mathf.Min(texture.height - 1, Mathf.FloorToInt(y / scale)));
                if (pixel.a <= 0) continue;
                Color mixed = Color.Lerp(target.GetPixel(left + x, bottom + y), pixel, pixel.a); mixed.a = 1;
                target.SetPixel(left + x, bottom + y, mixed);
            }
        }
    }
}
