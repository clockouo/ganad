using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AfterTheGlowPettingSetup
{
    [MenuItem("Tools/After the Glow/Configure PPT Petting Puzzle")]
    private static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        const string path = "Assets/Resources/Puzzle02/PettingPortrait.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        foreach (string layer in new[] { "PettingDog_Separated", "PettingBackground_Separated" })
        {
            string layerPath = "Assets/Resources/Puzzle02/" + layer + ".png";
            AssetDatabase.ImportAsset(layerPath, ImportAssetOptions.ForceSynchronousImport);
            var layerImporter = (TextureImporter)AssetImporter.GetAtPath(layerPath);
            if (layerImporter == null) throw new InvalidOperationException("Missing petting layer: " + layerPath);
            layerImporter.textureType = TextureImporterType.Default;
            layerImporter.textureCompression = TextureImporterCompression.Uncompressed;
            layerImporter.mipmapEnabled = false;
            layerImporter.npotScale = TextureImporterNPOTScale.None;
            layerImporter.filterMode = FilterMode.Point;
            layerImporter.wrapMode = TextureWrapMode.Clamp;
            layerImporter.alphaIsTransparency = true;
            layerImporter.SaveAndReimport();
        }
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new InvalidOperationException("Open the story scene.");
        var ui = story.GetComponent<PettingPuzzleUI>();
        if (ui == null) ui = Undo.AddComponent<PettingPuzzleUI>(story.gameObject);
        Undo.RecordObject(story, "Connect PPT petting screen");
        story.pettingPuzzleUI = ui;
        EditorUtility.SetDirty(story);
        EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        AssetDatabase.SaveAssets();
    }
}
