using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public sealed class BlockSpriteImporter : AssetPostprocessor
    {
        public const string Root = "Assets/PickaxeFall/Resources/Blocks/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            byte[] header = new byte[24];
            using (var stream = File.OpenRead(assetPath))
                if (stream.Read(header, 0, header.Length) != header.Length) throw new InvalidDataException("Block image has an invalid PNG header: " + assetPath);
            if (header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71)
                throw new InvalidDataException("Block PNG data is missing. Run 'git lfs install --local' and 'git lfs pull' before opening Unity: " + assetPath);
            int width = ReadDimension(header, 16), height = ReadDimension(header, 20);
            if (width <= 0 || width != height) throw new InvalidDataException("Block sprites must be square: " + assetPath);
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = width;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = false;
            importer.isReadable = true;
            importer.maxTextureSize = 1024;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }

        static int ReadDimension(byte[] bytes, int offset)
            => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
