using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class ImportedSpriteChecks
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        public static void Validate()
        {
            var paths = new HashSet<string>();
            foreach (BlockKind kind in Enum.GetValues(typeof(BlockKind)))
            {
                ValidateLayer(kind, false, false, paths);
                if (kind == BlockKind.Dirt) ValidateLayer(kind, false, true, paths);
                else if (kind != BlockKind.Workbench && kind != BlockKind.Tnt) ValidateLayer(kind, true, false, paths);
            }
            Require(paths.Count == 72, "The supplied block pack must contain all 72 selected variants.");
            string[] names={"Wood","Stone","Gold","Iron","Diamond"};
            for(int tier=0;tier<5;tier++)
            {
                string path="Assets/PickaxeFall/Resources/Pickaxes/"+names[tier]+".png";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Require(sprite && PixelArt.Pickaxe(tier)==sprite,"Imported pickaxe is selected: "+path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Require(sprite.texture.width==32 && sprite.texture.height==32 && sprite.GetPhysicsShapeCount()>0,"Pickaxe art and physics outline: "+path);
                Require(importer.filterMode==FilterMode.Point && !importer.mipmapEnabled && importer.spritePixelsPerUnit==32 && importer.textureCompression==TextureImporterCompression.Uncompressed,"Pickaxe pixel import: "+path);
                var pixels=sprite.texture.GetPixels32();var outline=PixelArt.PickaxeOutline(tier).texture.GetPixels32();int marked=0;
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    int i=y*32+x;if(outline[i].a==0)continue;marked++;
                    Require(pixels[i].a==0,"Selection outline must not fill the tool");
                    Require(x>0 && pixels[i-1].a>0 || x<31 && pixels[i+1].a>0 || y>0 && pixels[i-32].a>0 || y<31 && pixels[i+32].a>0,"Selection outline must be one texture pixel thick");
                }
                Require(marked>20,"Visible silhouette outline");
            }
            Debug.Log("PICKAXEFALL_IMPORTED_SPRITES_OK: 77 PNGs including five supplied pickaxes; ordinary terrain/ores at 32px; deep/special blocks at 16px; Point, no compression/mips; all variants one opaque world tile.");
        }

        static void ValidateLayer(BlockKind kind, bool deep, bool surface, HashSet<string> paths)
        {
            for (int variant = 0; variant < 4; variant++)
            {
                string path = "Assets/PickaxeFall/Resources/" + PixelArt.BlockResourcePath(kind, variant, deep, surface) + ".png";
                Require(paths.Add(path), "A block variant was accidentally mapped twice: " + path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(sprite, "Missing imported sprite: " + path);
                Require(PixelArt.Block(kind, variant, deep, surface) == sprite, "The game selected generated art instead of the supplied PNG: " + path);
                int size = !deep && kind <= BlockKind.Diamond ? 32 : 16;
                Require(sprite.texture.width == size && sprite.texture.height == size, "Incorrect source variant or resolution: " + path);
                Require(Mathf.Abs(sprite.bounds.size.x - 1) < 0.001f && Mathf.Abs(sprite.bounds.size.y - 1) < 0.001f,
                    "Mixed pixel sizes must still fill exactly one world tile: " + path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Require(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    importer.spriteImportMode == SpriteImportMode.Single && importer.spritePixelsPerUnit == size,
                    "Pixel-art import settings are incorrect: " + path);
                foreach (Color32 pixel in sprite.texture.GetPixels32())
                    Require(pixel.a == 255, "Imported block must be opaque: " + path);
            }
        }
    }
}
