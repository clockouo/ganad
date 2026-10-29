#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

internal sealed class AudioLevelMatchImporter : AssetPostprocessor
{
    private const string AudioFolder = "Assets/Resources/Audio/";

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(AudioFolder, System.StringComparison.OrdinalIgnoreCase))
            return;

        ConfigureForExisting((AudioImporter)assetImporter);
    }

    internal static void ConfigureForExisting(AudioImporter importer)
    {
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.quality = 1f;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = false;
    }
}

[InitializeOnLoad]
internal static class ExistingGameAudioImportSettings
{
    private const string AudioFolder = "Assets/Resources/Audio";

    static ExistingGameAudioImportSettings()
    {
        EditorApplication.delayCall += ConfigureExistingAudio;
    }

    private static void ConfigureExistingAudio()
    {
        if (!AssetDatabase.IsValidFolder(AudioFolder))
            return;

        string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder });
        foreach (string guid in audioGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
                continue;

            AudioImporterSampleSettings current = importer.defaultSampleSettings;
            bool alreadyMatched = current.loadType == AudioClipLoadType.DecompressOnLoad &&
                                  current.compressionFormat == AudioCompressionFormat.PCM &&
                                  current.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate &&
                                  current.preloadAudioData && !importer.loadInBackground;
            if (alreadyMatched)
                continue;

            AudioLevelMatchImporter.ConfigureForExisting(importer);
            importer.SaveAndReimport();
        }
    }
}
#endif
