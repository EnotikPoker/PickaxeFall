using UnityEditor;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public sealed class AudioImporter:AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/PickaxeFall/Resources/Audio/"))return;
            var importer=(UnityEditor.AudioImporter)assetImporter;bool music=assetPath.Contains("/Music/");
            importer.forceToMono=!music;importer.loadInBackground=music;
            var settings=importer.defaultSampleSettings;settings.preloadAudioData=!music;settings.loadType=music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=music?.5f:.65f;importer.defaultSampleSettings=settings;
        }
    }
}
