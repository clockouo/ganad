// Requires Unity 2022.3+ and the 2D Sprite package.
// API: https://docs.unity.cn/Manual/Sprite-data-provider-api.html
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

namespace DogJourney.Art
{
    public static class ArtImport
    {
        const string Root = "Assets/DogJourneyArt/";
        [Serializable] public class Manifest { public Entry[] assets; }
        [Serializable] public class Entry
        {
            public string file; public int cols; public int rows;
            public bool alpha; public string[] names; public float splitX;
        }
        static Manifest Read() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "asset_manifest.json"));

        // Rect coordinates in source image pixels, Unity bottom-left origin.
        static Rect GetRect(Entry entry, int index, int w, int h)
        {
            int col = index % entry.cols, row = index / entry.cols;
            int x0 = Mathf.RoundToInt(w * col / (float)entry.cols);
            int x1 = Mathf.RoundToInt(w * (col + 1) / (float)entry.cols);
            if (entry.cols == 2 && entry.splitX > 0)
            {
                int split = Mathf.RoundToInt(w * entry.splitX);
                x0 = col == 0 ? 0 : split; x1 = col == 0 ? split : w;
            }
            int y0 = h - Mathf.RoundToInt(h * (row + 1) / (float)entry.rows);
            int y1 = h - Mathf.RoundToInt(h * row / (float)entry.rows);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }
        static string FrameName(Entry e, int i) =>
            Path.GetFileNameWithoutExtension(e.file).Split(new[]{"__"}, StringSplitOptions.None)[0]
            + "_" + i.ToString("00") + "_" + e.names[i];

        [MenuItem("Tools/Dog Journey/1 - Import and slice art")]
        public static void Import()
        {
            var manifest = Read();
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            int total = 0;
            foreach (var e in manifest.assets)
            {
                string path = Root + e.file;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { Debug.LogError("Missing: " + path); continue; }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = e.cols * e.rows > 1 ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = e.alpha;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.spritePixelsPerUnit = 100;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                if (e.cols * e.rows == 1) { total++; continue; }

                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                if (provider == null) { Debug.LogError("2D Sprite provider missing: " + path); continue; }
                provider.InitSpriteEditorDataProvider();
                var old = provider.GetSpriteRects();
                var rects = new List<SpriteRect>();
                for (int i = 0; i < e.cols * e.rows; i++)
                {
                    string name = FrameName(e, i);
                    var previous = old.FirstOrDefault(x => x.name == name);
                    rects.Add(new SpriteRect {
                        name = name, spriteID = previous != null ? previous.spriteID : GUID.Generate(),
                        rect = GetRect(e, i, w, h),
                        alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                    });
                }
                provider.SetSpriteRects(rects.ToArray());
                var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (ids != null)
                    ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                provider.Apply();
                importer.SaveAndReimport();
                total += rects.Count;
            }
            Debug.Log("Dog Journey art imported: " + total + " sprites. Check frame alignment before animation.");
        }

        [MenuItem("Tools/Dog Journey/2 - Export individual PNG files")]
        public static void ExportIndividual()
        {
            // New output directory each time; source PNGs and existing exports stay untouched.
            string folder = "DogJourneyIndividualPNG_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, folder);
            Directory.CreateDirectory(output);
            foreach (var e in Read().assets)
            {
                string source = Root + e.file;
                string destination = Path.Combine(output, Path.GetDirectoryName(e.file));
                Directory.CreateDirectory(destination);
                if (e.cols * e.rows == 1)
                {
                    File.Copy(source, Path.Combine(destination, Path.GetFileName(source)), false);
                    continue;
                }
                // Read original bytes, not a resized/compressed imported texture.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(source))) {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new IOException("Cannot load: " + source);
                }
                try {
                    for (int i = 0; i < e.cols * e.rows; i++)
                    {
                        Rect rect = GetRect(e, i, texture.width, texture.height);
                        var frame = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                        try {
                            frame.SetPixels(texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
                            frame.Apply();
                            File.WriteAllBytes(Path.Combine(destination, FrameName(e, i) + ".png"), frame.EncodeToPNG());
                        } finally { UnityEngine.Object.DestroyImmediate(frame); }
                    }
                } finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            EditorUtility.RevealInFinder(output);
            Debug.Log("Individual PNG files exported to " + output);
        }
    }
}

