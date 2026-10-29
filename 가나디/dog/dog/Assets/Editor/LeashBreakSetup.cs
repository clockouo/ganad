using UnityEditor;
using UnityEngine;

public static class LeashBreakSetup
{
    [MenuItem("Tools/After the Glow/Configure Leash Break Layers")]
    private static void Configure()
    {
        foreach (string name in new[] { "LeashGateClean", "LeashTornHalves", "Memory02HandWithdrawn" })
        {
            string path = "Assets/Resources/Puzzle03/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }
}
