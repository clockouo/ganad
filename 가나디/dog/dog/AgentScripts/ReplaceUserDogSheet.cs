using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Standalone Unity asset preparation. No postprocessor or MenuItem; run through Pipeline run_script.
public static class ReplaceUserDogSheet
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before replacing the dog sheet.");
        const string folder = "Assets/Art/AfterTheGlow/Characters/";
        const string source = "C:/Users/admin/AppData/Local/Temp/codex-clipboard-bb841db3-10dd-4005-a046-f2b7654db52a.png";
        const string output = folder + "Dog_User_Actions_5Rows.png";
        Directory.CreateDirectory("tmp/feedback-v2.37");
        var scene = EditorSceneManager.GetActiveScene();
        if (!File.Exists("tmp/feedback-v2.37/before-active.unity"))
            EditorSceneManager.SaveScene(scene, "tmp/feedback-v2.37/before-active.unity", true);
        foreach (string path in Directory.GetFiles("Assets/Art/PixelSnapper/Animations/"))
            if (Path.GetFileName(path).StartsWith("PixelSnapperBorderCollie") &&
                !File.Exists("tmp/feedback-v2.37/" + Path.GetFileName(path)))
                File.Copy(path, "tmp/feedback-v2.37/" + Path.GetFileName(path));

        byte[] original = File.ReadAllBytes(source);
        File.WriteAllBytes(folder + "Dog_User_Original_6Rows.png", original);
        var input = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var compact = new Texture2D(768, 640, TextureFormat.RGBA32, false, true);
        try
        {
            if (!ImageConversion.LoadImage(input, original) || input.width != 768 || input.height != 768)
                throw new InvalidDataException("Expected the user's original 768 x 768 RGBA sheet.");
            Color32[] originalPixels = input.GetPixels32();
            Color32[] compactPixels = new Color32[768 * 640];
            // Unity pixels run bottom-up: retain the original bottom row, skip the
            // fifth row from the top (bottom-up rows 128..255), then retain the top four.
            Array.Copy(originalPixels, 0, compactPixels, 0, 768 * 128);
            Array.Copy(originalPixels, 768 * 256, compactPixels, 768 * 128, 768 * 512);
            compact.SetPixels32(compactPixels);
            compact.Apply(false, false);
            File.WriteAllBytes(output, ImageConversion.EncodeToPNG(compact));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(input);
            UnityEngine.Object.DestroyImmediate(compact);
        }
        AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(folder + "Dog_User_Original_6Rows.png", ImportAssetOptions.ForceSynchronousImport);
        var setup = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AfterTheGlowStorySetup"))
            .First(t => t != null);
        // The persistent setup performs all Sprite Editor capability checks before
        // changing rectangles, IDs, names or pivots; it also rebuilds the existing clips.
        setup.GetMethod("RefreshPptDogArt", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(output).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        File.WriteAllLines("tmp/feedback-v2.37/import.txt", new[] {
            "Sheet=" + output + " (768 x 640), source row 5 deleted; rows 1,2,3,4,6 preserved exactly.",
            "Frames=" + sprites.Length + "; Point; uncompressed; PPU=160; foot pivot=(0.5,12/128).",
            "Idle=01; Walk=01..06; Jump=07..10; Crouch=11..14; Sit=15..18 (available); Push=19..22."
        }.Concat(sprites.Select(s => s.name + " rect=" + s.rect + " pivot=" + s.pivot)));
        return output + ": imported " + sprites.Length + " sprites and refreshed player animation references.";
    }
}
