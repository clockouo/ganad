using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkFountainSetup
{
    [MenuItem("Tools/After the Glow/Configure Fountain Exploration")]
    private static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        foreach (string name in new[] { "FountainTopView", "DogTopView", "Ring", "Dot" })
        {
            string path="Assets/Resources/ParkFountain/"+name+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=(name=="Ring"||name=="Dot")?TextureImporterType.Sprite:TextureImporterType.Default;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;
            importer.SaveAndReimport();
        }
        var story=Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if(story==null)throw new System.InvalidOperationException("Open SampleScene.");
        var fountain=story.GetComponent<ParkFountainExploration>();
        if(fountain==null)fountain=Undo.AddComponent<ParkFountainExploration>(story.gameObject);
        fountain.fountainWorldX=AfterTheGlowWorldLayout.FountainX;
        fountain.entryWorldX=AfterTheGlowWorldLayout.FountainEntryX;
        fountain.exitWorldX=AfterTheGlowWorldLayout.FountainExitX;
        fountain.approachDistance=1f;
        EditorUtility.SetDirty(fountain);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(story.gameObject.scene);EditorSceneManager.SaveScene(story.gameObject.scene);
    }
}
