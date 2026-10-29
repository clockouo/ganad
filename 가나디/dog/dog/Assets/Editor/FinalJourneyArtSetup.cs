using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Unity.Pipeline.Commands;

/// <summary>Imports the actual final delivery, including renamed animation strips.</summary>
public static class FinalJourneyArtSetup
{
    private static bool? previousRunInBackground;
    [CliCommand("final_art", "Apply and inspect the final supplied artwork", MainThreadRequired = true)]
    public static object Command([CliArg("action", "import, apply, inspect, wake_layout, wake, pet, fountain, gate, stop_ui")] string action = "inspect")
    {
        if (action == "import") { Import(); return "Imported all final PNG assets."; }
        if (action == "apply") return Apply();
        if (action == "tick") { if (!previousRunInBackground.HasValue) previousRunInBackground = Application.runInBackground; Application.runInBackground = true; EditorApplication.isPaused = false; return "Background player loop enabled for verification"; }
        if (action == "restore") { Application.runInBackground = previousRunInBackground ?? PlayerSettings.runInBackground; previousRunInBackground = null; return "Background player loop setting restored"; }
        if (action == "step") { EditorApplication.Step(); return "One verification frame requested"; }
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (action == "fountain_status")
        {
            var fountain = story.GetComponent<ParkFountainExploration>();
            return new { frame = Time.frameCount, x = story.player.transform.position.x, grounded = story.player.IsGrounded,
                open = fountain.IsOpen, entry = fountain.entryWorldX, radius = fountain.approachDistance, angle = fountain.AngleDegrees };
        }
        if (action == "fountain_center" || action == "fountain_left_approach" || action == "fountain_left_inside")
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            var fountain = story.GetComponent<ParkFountainExploration>();
            story.bedWakePuzzleUI.Close(); story.pettingPuzzleUI.Close();
            typeof(AfterTheGlowStoryDirector).GetProperty("Stage").SetValue(story, AfterTheGlowStoryStage.ParkPlaceholder);
            float x = action == "fountain_center" ? fountain.fountainWorldX : fountain.entryWorldX;
            if (action == "fountain_left_approach") x -= fountain.approachDistance + .25f;
            var player = story.player;
            player.SetControlMode(PlayerControlMode.Free);
            player.Body.position = new Vector2(x, story.streetEntryPlayerY);
            player.transform.position = new Vector3(x, story.streetEntryPlayerY, player.transform.position.z);
            player.Body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            story.cameraFollow.enabled = false;
            var camera = story.cameraFollow.GetComponent<Camera>(); camera.orthographicSize = 3f;
            camera.transform.position = new Vector3(fountain.fountainWorldX - 1f, story.cameraFollow.exteriorLockedY, -10f);
            return new { x, entry = fountain.entryWorldX };
        }
        if (action.StartsWith("memory2"))
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            story.bedWakePuzzleUI.Close(); story.pettingPuzzleUI.Close();
            story.player.SetControlMode(PlayerControlMode.CutsceneLocked);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(AfterTheGlowStoryDirector).GetProperty("Stage").SetValue(story, AfterTheGlowStoryStage.GateMemoryPuzzle);
            typeof(AfterTheGlowStoryDirector).GetMethod("SetGateMemoryPage", flags).Invoke(story, new object[] { 3 });
            float elapsed = action == "memory2_early" || action == "memory2_play" ? 0f : action == "memory2_late" ? 2.4f : 1.8f;
            typeof(AfterTheGlowStoryDirector).GetField("gatePuzzlePageTime", flags).SetValue(story, Time.time - elapsed);
            EditorApplication.isPaused = action != "memory2_play";
            return new { page = 3, elapsed, texture = AssetDatabase.GetAssetPath(story.gateMemoryFrames[3]), paused = EditorApplication.isPaused };
        }
        if (action == "memory_status")
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var texture = (Texture2D)typeof(AfterTheGlowStoryDirector).GetField("activeGatePuzzleFrame", flags).GetValue(story);
            return new { frame = Time.frameCount, stage = story.Stage.ToString(),
                page = typeof(AfterTheGlowStoryDirector).GetField("gatePuzzlePage", flags).GetValue(story),
                texture = texture != null ? texture.name : null };
        }
        if (action == "wake_layout")
        {
            ConfigureWake(story);
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
                EditorSceneManager.SaveScene(story.gameObject.scene);
            }
            return "Wake portrait fitted to pillow; duvet and touch targets aligned";
        }
        if (action == "street" || action == "park")
        {
            story.cameraFollow.enabled = false;
            float center = action == "street" ? 17f : 33.6f;
            var camera = story.cameraFollow.GetComponent<Camera>(); camera.orthographicSize = 3f;
            camera.transform.position = new Vector3(center, Floor + 1.488f, -10);
            story.player.SetControlMode(PlayerControlMode.CutsceneLocked);
            story.player.Body.position = new Vector2(center - 3f, Floor);
            story.player.Body.linearVelocity = Vector2.zero;
            return action + " view positioned";
        }
        if (action == "wake") { story.bedWakePuzzleUI.Open(story); return "Wake puzzle opened"; }
        if (action == "pet") { story.pettingPuzzleUI.Open(() => { }); return "Pet puzzle opened"; }
        if (action == "fountain")
        {
            typeof(AfterTheGlowStoryDirector).GetProperty("Stage").SetValue(story, AfterTheGlowStoryStage.ParkPlaceholder);
            story.GetComponent<ParkFountainExploration>().Open(); return "Fountain opened";
        }
        if (action == "gate") { story.StartCoroutine(story.GetComponent<LeashBreakUI>().Play(null)); EditorApplication.isPaused = true; return "Leash break opened and paused for capture"; }
        if (action == "stop_ui") { story.bedWakePuzzleUI.Close(); story.pettingPuzzleUI.Close(); story.GetComponent<ParkFountainExploration>().Close(); return "UI closed"; }
        return new {
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            frame = Time.frameCount, time = Time.time,
            stage = story != null ? story.Stage.ToString() : "none",
            renderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => new { name = r.name, sprite = r.sprite == null ? "none" : r.sprite.name,
                    path = r.sprite == null ? "" : AssetDatabase.GetAssetPath(r.sprite), position = r.transform.position.ToString(), scale = r.transform.localScale.ToString(), size = r.bounds.size.ToString() }).ToArray()
        };
    }
    public const string Root = "Assets/Resources/JourneyArt/";
    private const float Floor = -3.86f;
    public static void Import()
    {
        foreach (string file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories)) Slice(file.Replace('\\', '/'));
        AssetDatabase.SaveAssets();
    }

    private static void Slice(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); importer = (TextureImporter)AssetImporter.GetAtPath(path); }
        string name = Path.GetFileNameWithoutExtension(path);
        int cols = 1, rows = 1;
        var grid = Regex.Match(name, @"__c(\d+)r(\d+)");
        if (grid.Success) { cols = int.Parse(grid.Groups[1].Value); rows = int.Parse(grid.Groups[2].Value); }
        if (name == "Dog_Idle__6x1" || name == "walk") cols = 6;
        if (new[] { "bite", "bite_pull", "jump", "lie_down", "sit" }.Contains(name)) cols = 4;
        if (name.EndsWith("_01")) cols = 5;
        if (name.EndsWith("_11")) cols = 6;
        if (name.EndsWith("_21")) cols = 12;
        bool stripDog = path.Contains("/Dog/") && rows == 1;
        bool stripOwner = name.StartsWith("KakaoTalk_");
        bool transparent = !path.Contains("/Backgrounds/") && !path.Contains("/Memories/");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = cols * rows > 1 ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f; importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None; importer.alphaIsTransparency = transparent;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        if (cols * rows == 1) return;
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (edit == null) throw new InvalidOperationException("Sprite editing unavailable: " + path);
        var capability = edit.GetEditCapability();
        foreach (var flag in new[] { EEditCapability.CreateAndDeleteSprite, EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect, EEditCapability.EditPivot })
            if (!capability.HasCapability(flag)) throw new InvalidOperationException("Unsupported sprite capability: " + flag);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null) throw new InvalidOperationException("Sprite file IDs unavailable: " + path);
        importer.GetSourceTextureWidthAndHeight(out int w, out int h);
        var tex = new Texture2D(2, 2); tex.LoadImage(File.ReadAllBytes(path)); var pixels = tex.GetPixels32();
        try
        {
            var previous = provider.GetSpriteRects(); var rects = new SpriteRect[cols * rows];
            for (int i = 0; i < rects.Length; i++)
            {
                int col = i % cols, row = i / cols;
                int x0 = Mathf.RoundToInt(w * col / (float)cols), x1 = Mathf.RoundToInt(w * (col + 1) / (float)cols);
                if (name == "Lights__c2r2") { int split = Mathf.RoundToInt(w * .64f); x0 = col == 0 ? 0 : split; x1 = col == 0 ? split : w; }
                if (name == "ParkMemoryVignettes__c2r2") { int split = Mathf.RoundToInt(w * .52f); x0 = col == 0 ? 0 : split; x1 = col == 0 ? split : w; }
                int y0 = h - Mathf.RoundToInt(h * (row + 1) / (float)rows), y1 = h - Mathf.RoundToInt(h * row / (float)rows);
                Rect rect = new Rect(x0, y0, x1 - x0, y1 - y0);
                Vector2 pivot = Vector2.one * .5f;
                if (stripDog) pivot = new Vector2(.5f, 12f / 128f);
                else if (stripOwner)
                {
                    rect = new Rect(i * 256, 20, 256, name.EndsWith("_11") ? 68 : 216);
                    pivot = new Vector2(.5f, 0);
                }
                else if (transparent)
                {
                    int minX = x1, minY = y1, maxX = -1, maxY = -1;
                    for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                        if (pixels[y * w + x].a > 12) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                    if (maxX >= minX) rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                }
                string frameName = name + "_" + i.ToString("00");
                var old = previous.FirstOrDefault(r => r.name == frameName);
                rects[i] = new SpriteRect { name = frameName, spriteID = old != null ? old.spriteID : GUID.Generate(), rect = rect, alignment = SpriteAlignment.Custom, pivot = pivot };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
        }
        finally { UnityEngine.Object.DestroyImmediate(tex); }
    }

    private static Sprite S(string path, int index = 0) => AssetDatabase.LoadAllAssetsAtPath(Root + path + ".png").OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ElementAt(index);
    private static Sprite[] F(string path) => AssetDatabase.LoadAllAssetsAtPath(Root + path + ".png").OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
    private static Transform Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene.IsValid() && t.name == name);
    private static Material unlit;
    private static SpriteRenderer Set(string name, Sprite sprite, Vector2 size, Vector3? position = null)
    {
        var t = Find(name); if (t == null) throw new MissingReferenceException(name);
        var r = t.GetComponent<SpriteRenderer>(); if (r == null) r = t.gameObject.AddComponent<SpriteRenderer>();
        r.sprite = sprite; r.sharedMaterial = unlit; r.color = Color.white; r.enabled = true; r.drawMode = SpriteDrawMode.Simple;
        var p = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size.x / sprite.bounds.size.x / Mathf.Abs(p.x), size.y / sprite.bounds.size.y / Mathf.Abs(p.y), 1);
        if (position.HasValue) t.position = position.Value;
        EditorUtility.SetDirty(r); return r;
    }
    private static SpriteRenderer Add(Transform parent, string name, Sprite sprite, Vector2 size, Vector3 position, int order = 15)
    {
        var t = Find(name); if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        var r = Set(name, sprite, size, position); r.sortingLayerID = Find("BackGround").GetComponent<SpriteRenderer>().sortingLayerID; r.sortingOrder = order; return r;
    }
    private static void Hide(params string[] names)
    {
        foreach (string name in names) { var t = Find(name); if (t != null) foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false; }
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        unlit = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("9dfc825aed78fcd4ba02077103263b40"));
        // 18 world units per 2172 pixels. Actual floor line is at 74.8% in the final plate.
        Set("BackGround", S("Backgrounds/01_House"), new Vector2(18, 6), new Vector3(-2.5f, Floor + 1.488f, 1));
        Set("Street Night Backdrop", S("Backgrounds/02_Street"), new Vector2(18, 6), new Vector3(15.6f, Floor + 1.488f, 1));
        Set("Park Map Backdrop", S("Backgrounds/03_ParkSide"), new Vector2(18, 6), new Vector3(33.6f, Floor + 1.488f, 1));
        Hide("Connected Exterior Backdrop", "Street Night Backdrop 2", "Street Night Backdrop 3", "Original Car Lamp Off 0", "Original Car Lamp Off 1",
            "Park Entry Placeholder Backdrop", "PixelSnapper Headlights Art", "Front Door Frame Left", "Front Door Frame Right", "Front Door Frame Header",
            "Bathroom Door Left", "Bathroom Door Right", "Bathroom Door Header", "Bathroom Interior", "Separated Bed Duvet");
        var bed = Set("Separated Bed Frame", S("Props/HouseItems__c4r2", 0), new Vector2(2.8f, .83f), new Vector3(-9.2f, Floor + .415f, -.05f));
        bed.sortingOrder = 5;
        var anim = story.GetComponent<OwnerAnimationPresentation>();
        var walk = F("Characters/Owner/KakaoTalk_261022_010814_21");
        var pet = F("Characters/Owner/KakaoTalk_261022_010814_01");
        var sleep = F("Characters/Owner/KakaoTalk_261022_010814_11");
        anim.walkFrames = walk; anim.petFrames = pet; anim.sleepFrames = sleep;
        anim.bedDuvetRenderer = null;
        var sleeping = Set("Owner Sleeping Animation", sleep[0], new Vector2(2.43f, .646f), new Vector3(-9.2f, Floor + .62f, -.1f));
        sleeping.sortingOrder = 7; anim.sleepingRenderer = sleeping;
        story.bed.ConfigureVisualInteraction(bed.transform);
        story.ownerWalkSprite = walk[0]; story.ownerPetSprite = pet[pet.Length - 1];
        var owner = Set("PixelSnapper Owner Visual", walk[0], new Vector2(2.43f, 2.05f));
        owner.transform.localPosition = new Vector3(0, -.24f, -.2f); owner.flipX = false; anim.ownerRenderer = owner;
        var ball = Set("PixelSnapper Ball Art", S("Props/HouseItems__c4r2", 2), new Vector2(.48f, .48f));
        ball.transform.localPosition = Vector3.zero;
        Set("House Wall Switch", S("Props/HouseItems__c4r2", 7), new Vector2(.23f, .3f));
        Hide("Switch Rocker", "Switch Indicator");
        HomeRoomLightingSetup.SortSwitchBehindOwner(story);
        Add(story.transform, "Final Bedside Lamp", S("Props/HouseItems__c4r2", 1), new Vector2(.65f, 1.15f), new Vector3(-7.3f, Floor + .575f, .2f), 4);
        Add(story.transform, "Final Dog Bowl", S("Props/HouseItems__c4r2", 4), new Vector2(.65f, .22f), new Vector3(-5.8f, Floor + .11f, -.1f), 6);
        Add(story.transform, "Final Coat", S("Props/HouseItems__c4r2", 6), new Vector2(.55f, 1.1f), new Vector3(3.1f, Floor + 1.9f, .2f), 4);
        story.frontDoorClosedSprite = S("Props/Doors__c2r2", 2); story.frontDoorOpenSprite = S("Props/Doors__c2r2", 3);
        Set("Front Door Panel", story.frontDoorClosedSprite, new Vector2(1.47f, 2.85f), new Vector3(5.1f, Floor + 1.425f, -.1f));
        var bathroom = Add(story.transform, "Final Bathroom Door", S("Props/Doors__c2r2", 0), new Vector2(1.5f, 2.85f), new Vector3(-3.8f, Floor + 1.425f, .1f), 3);
        var car = Find("PixelSnapper Car No Lights").GetComponent<CarHeadlightPresentation>();
        car.useBackdropCar = false; car.lightsOnSprite = car.lightsOffSprite = S("Props/StreetItems__c2r2", 0);
        car.worldSize = new Vector2(2.2f, 1.48f); car.wheelBottomOffsets = new Vector2(-car.lightsOffSprite.bounds.extents.y, -car.lightsOffSprite.bounds.extents.y);
        car.Refresh();
        var presentation = story.GetComponent<JourneyWorldArtPresentation>();
        if (presentation == null) presentation = story.gameObject.AddComponent<JourneyWorldArtPresentation>();
        presentation.story = story; presentation.bathroomDoor = bathroom;
        presentation.dog = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        var oldBeam = Find("Visible Headlight Beam");
        if (oldBeam != null) { var mesh = oldBeam.GetComponent<MeshRenderer>(); if (mesh != null) { mesh.enabled = false; EditorUtility.SetDirty(mesh); } }
        presentation.headlights = Add(story.transform, "Final Headlight Glow", S("Effects/Lights__c2r2", 0), new Vector2(2.3f, 1.5f),
            car.transform.position + new Vector3(0, -.35f, -.1f), 18);
        presentation.lampGlow = Add(story.transform, "Final Bedside Glow", S("Effects/Lights__c2r2", 1), new Vector2(1.4f, 1.4f), new Vector3(-7.3f, Floor + 1f, -.2f), 8);
        presentation.lampGlow.color = new Color(1, 1, 1, .35f); presentation.lampGlow.enabled = false;
        presentation.waterJet = Add(story.transform, "Final Fountain Water", S("Effects/Water__c2r2", 2), new Vector2(.9f, .85f), new Vector3(33.6f, Floor + 1.05f, -.1f), 14);
        var knot = Find("Visible Leash Knot");
        if (knot != null) presentation.leashKnot = Set(knot.name, S("Props/StreetItems__c2r2", 3), new Vector2(.25f, .2f));
        EditorUtility.SetDirty(presentation);
        var hinge = story.parkGate.GetComponent<GateHingePresentation>();
        if (hinge != null)
        {
            Vector2 leftSize = hinge.leftLeaf.bounds.size, rightSize = hinge.rightLeaf.bounds.size;
            var left = Set(hinge.leftLeaf.name, S("Props/StreetItems__c2r2", 1), leftSize);
            var right = Set(hinge.rightLeaf.name, S("Props/StreetItems__c2r2", 2), rightSize);
            left.transform.position = new Vector3(20.365f, Floor + .85f, -.3f);
            right.transform.position = new Vector3(21.685f, Floor + .85f, -.3f);
            hinge.boundLeftSprite = hinge.releasedLeftSprite = hinge.openedLeftSprite = left.sprite;
            hinge.boundRightSprite = hinge.releasedRightSprite = hinge.openedRightSprite = right.sprite;
            hinge.closedScale = hinge.releasedLeftScale = hinge.openedLeftScale = left.transform.localScale;
            hinge.releasedRightScale = hinge.openedRightScale = right.transform.localScale;
            hinge.closedMaterial = unlit; hinge.ResetPose();
        }
        var front = Add(story.transform, "Final Park Front Walls", S("Props/Foreground/ParkFrontWalls"), new Vector2(18, 6), new Vector3(33.6f, Floor + 1.488f, -.3f), 35);
        ConfigureDog(story.player);
        ConfigureWake(story);
        story.gateMemoryFrames = new Texture2D[8];
        string[] memories = { "01_ApproachingShoes", "02_HandStops", "03_WaitingOnBench", "04_FirstSniff" };
        for (int i = 0; i < 8; i++) story.gateMemoryFrames[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + (i % 2 == 0 ? "Backgrounds/07_LeashFence" : "Memories/" + memories[i / 2]) + ".png");
        foreach (var c in new UnityEngine.Object[] { story, anim, car, hinge, story.bed }) if (c != null) EditorUtility.SetDirty(c);
        FinalExteriorBlendSetup.ApplyToScene();
        DoorArtworkSetup.ApplyToScene(story);
        HeadlightAlignmentSetup.ApplyToScene(story);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(story.gameObject.scene); EditorSceneManager.SaveScene(story.gameObject.scene);
        StoryPolishV246.Apply(story);
        return "Final art applied to SampleScene; original PNGs preserved, sprites sliced natively; story transition polish retained.";
    }

    private static void ConfigureDog(PlayerMovement player)
    {
        var animator = player.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController;
        Directory.CreateDirectory("Assets/Art/FinalJourneyAnimations");
        var replacement = new AnimatorOverrideController(controller is AnimatorOverrideController old ? old.runtimeAnimatorController : controller);
        foreach (var clip in replacement.animationClips.Distinct())
        {
            string key = clip.name.ToLowerInvariant();
            string sheet = key.Contains("walk") || key.Contains("run") ? "walk" : key.Contains("jump") ? "jump" : key.Contains("crouch") ? "lie_down" : key.Contains("push") || key.Contains("pull") ? "bite_pull" : key.Contains("bite") ? "bite" : "Dog_Idle__6x1";
            var frames = F("Characters/Dog/" + sheet);
            if (key.Contains("crouch")) frames = frames.Skip(2).ToArray();
            var created = new AnimationClip { name = "Final_" + clip.name, frameRate = 10 };
            string path = "Assets/Art/FinalJourneyAnimations/" + created.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) { UnityEngine.Object.DestroyImmediate(created); created = existing; }
            else AssetDatabase.CreateAsset(created, path);
            var sourceBinding = AnimationUtility.GetObjectReferenceCurveBindings(clip).FirstOrDefault(b => b.type == typeof(SpriteRenderer));
            var binding = EditorCurveBinding.PPtrCurve(sourceBinding.path ?? "", typeof(SpriteRenderer), "m_Sprite");
            var curve = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i <= frames.Length; i++) curve[i] = new ObjectReferenceKeyframe { time = i / 10f, value = frames[i % frames.Length] };
            AnimationUtility.SetObjectReferenceCurve(created, binding, curve);
            var settings = AnimationUtility.GetAnimationClipSettings(created); settings.loopTime = !key.Contains("jump"); AnimationUtility.SetAnimationClipSettings(created, settings);
            replacement[clip.name] = created; EditorUtility.SetDirty(created);
        }
        const string controllerPath = "Assets/Art/FinalJourneyAnimations/FinalDog.overrideController";
        var saved = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if (saved != null) { EditorUtility.CopySerialized(replacement, saved); UnityEngine.Object.DestroyImmediate(replacement); replacement = saved; }
        else AssetDatabase.CreateAsset(replacement, controllerPath);
        animator.runtimeAnimatorController = replacement;
        var r = player.GetComponent<SpriteRenderer>(); r.sprite = S("Characters/Dog/Dog_Idle__6x1");
        var visual = player.transform.Find("PixelSnapper Dog Visual");
        if (visual != null)
        {
            visual.localPosition = new Vector3(0, 0, -.1f); visual.localScale = Vector3.one * .8f;
            visual.GetComponent<SpriteRenderer>().sprite = r.sprite;
        }
        EditorUtility.SetDirty(animator); EditorUtility.SetDirty(r);
    }

    private static void ConfigureWake(AfterTheGlowStoryDirector story)
    {
        var background = Find("Sleeping Owner Close-up").GetComponent<UnityEngine.UI.Image>();
        background.sprite = S("Backgrounds/05_WakeBed"); background.preserveAspect = true;
        // Keep the portrait, duvet and input in the bed artwork's coordinate space.
        var fit = background.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        if (fit == null) fit = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;
        var face = Find("Final Wake Face");
        if (face == null) { face = new GameObject("Final Wake Face", typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform; face.SetParent(background.transform, false); }
        var image = face.GetComponent<UnityEngine.UI.Image>(); image.sprite = S("Puzzles/WakeFaces__c2r2"); image.preserveAspect = true; image.raycastTarget = false;
        var rect = (RectTransform)face;
        rect.anchorMin = new Vector2(.32f, .16f); rect.anchorMax = new Vector2(.68f, .80f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        // Reuse the original lower bed pixels in front of the shoulders, so the
        // portrait continues into the blanket without changing the supplied art.
        var duvet = background.transform.Find("Wake Duvet Foreground");
        if (duvet == null)
        {
            duvet = new GameObject("Wake Duvet Foreground", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).transform;
            duvet.SetParent(background.transform, false);
        }
        var duvetImage = duvet.GetComponent<UnityEngine.UI.RawImage>();
        duvetImage.texture = background.sprite.texture;
        Rect uv = JourneyArt.UV(background.sprite);
        duvetImage.uvRect = new Rect(uv.x, uv.y, uv.width, uv.height * .201f);
        duvetImage.raycastTarget = false;
        var duvetRect = (RectTransform)duvet;
        duvetRect.anchorMin = Vector2.zero; duvetRect.anchorMax = new Vector2(1f, .201f);
        duvetRect.offsetMin = duvetRect.offsetMax = Vector2.zero;
        duvet.SetAsLastSibling();

        var target = Find("Owner Cheek Touch Target");
        target.SetParent(face, false);
        var targetRect = (RectTransform)target;
        targetRect.anchorMin = targetRect.anchorMax = new Vector2(.24f, .33f);
        targetRect.anchoredPosition = Vector2.zero;
        targetRect.sizeDelta = new Vector2(150f, 150f);
        Find("Touch Hint").GetComponent<RectTransform>().sizeDelta = new Vector2(112f, 112f);
        foreach (var dot in story.bedWakePuzzleUI.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i => i.name.StartsWith("Wake Progress"))) dot.sprite = S("UI/Indicators__c4r2", 3);
        Find("Touch Hint").GetComponent<UnityEngine.UI.Image>().sprite = S("UI/Indicators__c4r2", 0);
        EditorUtility.SetDirty(background); EditorUtility.SetDirty(image); EditorUtility.SetDirty(fit);
    }
}
