using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Original, unmodified artwork supplied in the final art folder.</summary>
public static class JourneyArt
{
    public const string Root = "JourneyArt/";
    private static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
    public static Texture2D Texture(string path) => Resources.Load<Texture2D>(Root + path);
    public static Sprite[] Frames(string path)
    {
        if (!sheets.TryGetValue(path, out var frames))
        {
            frames = Resources.LoadAll<Sprite>(Root + path).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            sheets[path] = frames;
        }
        return frames;
    }
    public static Sprite Frame(string path, int index = 0)
    {
        var frames = Frames(path);
        if (frames.Length == 0) throw new InvalidOperationException("Final artwork has not been sliced: " + path);
        return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }
    public static Rect UV(Sprite sprite)
    {
        Rect r = sprite.rect;
        return new Rect(r.x / sprite.texture.width, r.y / sprite.texture.height,
            r.width / sprite.texture.width, r.height / sprite.texture.height);
    }
    public static void Set(UnityEngine.UI.RawImage image, Sprite sprite)
    {
        image.texture = sprite.texture;
        image.uvRect = UV(sprite);
    }
    public static void Draw(Rect area, Sprite sprite)
    {
        float aspect = sprite.rect.width / sprite.rect.height;
        Vector2 size = area.width / area.height > aspect ? new Vector2(area.height * aspect, area.height)
            : new Vector2(area.width, area.width / aspect);
        GUI.DrawTextureWithTexCoords(new Rect(area.center - size * .5f, size), sprite.texture, UV(sprite), true);
    }
}
