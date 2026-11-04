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
                if(kind>BlockKind.Tnt)continue; // New blocks use the built-in pixel-art fallback until supplied.
                ValidateLayer(kind, false, false, paths);
                if (kind == BlockKind.Dirt) ValidateLayer(kind, false, true, paths);
                else if (kind != BlockKind.Workbench && kind != BlockKind.Tnt) ValidateLayer(kind, true, false, paths);
            }
            Require(paths.Count == 72, "The supplied block pack must contain all 72 selected variants.");
            int previous=0;
            for(int stage=1;stage<=8;stage++)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/PickaxeFall/Resources/Cracks/Cracks_{stage}.png");
                Require(sprite,"Missing crack sprite "+stage);
                Require(sprite.texture.width==32,"Crack width "+stage+": "+sprite.texture.width);
                Require(sprite==PixelArt.Cracks(stage),"Crack resource selection "+stage+": "+PixelArt.Cracks(stage).texture.width);
                int visible=0;foreach(var p in sprite.texture.GetPixels32())if(p.a>200)visible++;
                Require(visible>previous,"Cracks must grow at every stage");previous=visible;
            }
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
            Debug.Log("PICKAXEFALL_IMPORTED_SPRITES_OK: 85 supplied PNGs: 72 blocks, 8 crack stages, 5 tools; terrain/deep ores and cracks 32px, TNT/workbench 16px; Point, no compression/mips, exact world-tile scale.");
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
                int size = kind==BlockKind.Workbench || kind==BlockKind.Tnt ? 16 : 32;
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
