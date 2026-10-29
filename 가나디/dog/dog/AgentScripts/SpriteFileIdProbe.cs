public static class SpriteFileIdProbe
{
    public static string Run()
    {
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        System.Type databaseType = null;
        foreach (var assembly in assemblies)
        {
            databaseType = assembly.GetType("UnityEditor.AssetDatabase", false);
            if (databaseType != null) break;
        }
        if (databaseType == null) return "AssetDatabase type unavailable";
        var loadMethod = databaseType.GetMethod("LoadAllAssetsAtPath", new System.Type[] { typeof(string) });
        var loaded = loadMethod.Invoke(null, new object[] { "Assets/Art/PixelSnapper/Backgrounds/Interior_Background.png" }) as System.Array;
        if (loaded == null) return "No assets loaded";
        var methods = databaseType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        System.Reflection.MethodInfo idMethod = null;
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (method.Name == "TryGetGUIDAndLocalFileIdentifier" && parameters.Length == 3 && parameters[0].ParameterType == typeof(UnityEngine.Object))
            {
                idMethod = method;
                break;
            }
        }
        if (idMethod == null) return "ID method unavailable";
        var result = new System.Text.StringBuilder();
        foreach (UnityEngine.Object item in loaded)
        {
            if (item == null || item.GetType().FullName != "UnityEngine.Sprite") continue;
            var arguments = new object[] { item, null, 0L };
            var ok = (bool)idMethod.Invoke(null, arguments);
            result.Append(item.name).Append(" | ").Append(arguments[1]).Append(" | ").Append(arguments[2]).Append(" | ").Append(ok).AppendLine();
        }
        return result.Length == 0 ? "No Sprite subasset found" : result.ToString();
    }
}
