public static class SpriteRectProbe
{
    public static string Run()
    {
        var paths = new string[]
        {
            "Assets/Art/PixelSnapper/Characters/Dog_Actions_BorderCollie.png",
            "Assets/Art/PixelSnapper/Characters/Owner_Actions.png",
            "Assets/Art/PixelSnapper/Props/Story_Props.png"
        };
        var result = new System.Text.StringBuilder();
        foreach (var path in paths)
        {
            var importer = UnityEditor.AssetImporter.GetAtPath(path);
            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new System.Exception("No sprite provider: " + path);
            provider.InitSpriteEditorDataProvider();
            var frameCapability = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
            if (frameCapability == null || !frameCapability.GetEditCapability().HasCapability(UnityEditor.U2D.Sprites.EEditCapability.CreateAndDeleteSprite))
                throw new System.Exception("Slice capability missing: " + path);
            var textureProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ITextureDataProvider>();
            if (textureProvider == null) throw new System.Exception("No texture provider: " + path);
            var texture = textureProvider.GetReadableTexture2D();
            if (texture == null) throw new System.Exception("Texture not readable for slicing: " + path);
            var rects = UnityEditorInternal.InternalSpriteUtility.GenerateAutomaticSpriteRectangles(texture, 4, 0);
            result.Append(System.IO.Path.GetFileName(path)).Append(" ").Append(rects.Length).Append(" rects");
            for (int i = 0; i < rects.Length; i++)
            {
                var r = rects[i];
                result.Append(" | ").Append(i).Append(":").Append(r.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(r.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(r.width.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(r.height.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            }
            result.AppendLine();
        }
        return result.ToString();
    }
}
