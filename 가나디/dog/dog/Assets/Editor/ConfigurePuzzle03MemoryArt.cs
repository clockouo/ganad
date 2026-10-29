#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ConfigurePuzzle03MemoryArt
{
    private const string AssetFolder = "Assets/Resources/Puzzle03";

    static ConfigurePuzzle03MemoryArt()
    {
        EditorApplication.delayCall += ConfigureImportedArt;
    }

    [MenuItem("Tools/After the Glow/Configure Puzzle 03 Memory Art")]
    private static void Configure()
    {
        ConfigureImportedArt();
    }

    private static void ConfigureImportedArt()
    {
        if (!Directory.Exists(AssetFolder))
            return;

        string[] files = Directory.GetFiles(AssetFolder, "*.png", SearchOption.TopDirectoryOnly);
        bool changed = false;
        foreach (string file in files)
        {
            string assetPath = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                continue;

            bool needsUpdate = importer.textureType != TextureImporterType.Default ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.maxTextureSize < 2048 || !importer.alphaIsTransparency;
            if (!needsUpdate)
                continue;

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            changed = true;
        }

        AssetDatabase.Refresh();
        if (changed)
            Debug.Log("[After the Glow] Puzzle 03 memory images configured for crisp full-screen display.");
    }
}

internal sealed class Puzzle03MemoryTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Puzzle03/", System.StringComparison.Ordinal))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.alphaIsTransparency = true;
    }
}
#endif
