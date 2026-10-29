using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;

public static class SpriteMetaInspect
{
    public static string Main()
    {
        string[] paths = new string[]
        {
            "Assets/Art/BorderCollie/BorderCollie_Walk_v4.png",
            "Assets/Art/BorderCollie/BorderCollie_Run_v4.png",
            "Assets/Art/BorderCollie/BorderCollie_Push_v4.png",
            "Assets/Art/BorderCollie/BorderCollie_Jump_v4.png"
        };
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var sb = new StringBuilder();
        foreach (var path in paths)
        {
            var importer = AssetImporter.GetAtPath(path);
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            sb.Append(path).Append(": existing=").Append(rects.Length).Append(" names=");
            for (int i = 0; i < rects.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(rects[i].name);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
