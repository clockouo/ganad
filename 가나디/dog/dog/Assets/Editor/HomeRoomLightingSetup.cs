using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HomeRoomLightingSetup
{
    public static void Apply(AfterTheGlowStoryDirector story)
    {
        var background = GameObject.Find("BackGround")?.GetComponent<SpriteRenderer>();
        if (background == null || background.sprite == null) throw new MissingReferenceException("The house illustration is required.");
        var lighting = story.GetComponent<HomeRoomLighting>();
        if (lighting == null) lighting = story.gameObject.AddComponent<HomeRoomLighting>();
        story.homeRoomLighting = lighting;
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CoreTestbed/CoreTestbedShape.png");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        var dog = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        var root = story.transform;
        var shade = Rectangle(root, "House Night Shade", square, material, dog.sortingLayerID, 200);
        var camera = story.cameraFollow;
        shade.transform.position = new Vector3((camera.mapMinX + camera.homeMapMaxX) * .5f, camera.lockedY, -.9f);
        shade.transform.localScale = new Vector3((camera.homeMapMaxX - camera.mapMinX) / square.bounds.size.x,
            8f / square.bounds.size.y, 1f);
        lighting.roomShade = shade;

        // Center of the EXISTING cream switch plate in Map01_Home.png, measured
        // from the unmodified 2173x724 source: x=702.5, y=395 from the top.
        Sprite map = background.sprite;
        Vector2 sourcePoint = new Vector2(702.5f, 724f - 395f);
        Vector2 local = (sourcePoint - map.pivot) / map.pixelsPerUnit;
        var switchRoot = root.Find("House Wall Switch");
        if (switchRoot == null) { switchRoot = new GameObject("House Wall Switch").transform; switchRoot.SetParent(root, false); }
        switchRoot.position = background.transform.TransformPoint(local);
        switchRoot.position = new Vector3(switchRoot.position.x, switchRoot.position.y, -.45f);
        lighting.wallSwitch = switchRoot;
        lighting.switchRocker = Rectangle(switchRoot, "Switch Rocker", square, material, dog.sortingLayerID, 35);
        lighting.switchRocker.transform.localScale = new Vector3(.105f / square.bounds.size.x, .115f / square.bounds.size.y, 1f);
        lighting.switchIndicator = Rectangle(switchRoot, "Switch Indicator", square, material, dog.sortingLayerID, 36);
        lighting.switchIndicator.transform.localPosition = new Vector3(0f, .085f, -.015f);
        lighting.switchIndicator.transform.localScale = new Vector3(.035f / square.bounds.size.x, .025f / square.bounds.size.y, 1f);

        const string armPath = "Assets/Art/AfterTheGlow/Characters/Owner/Owner_SwitchReach.mat";
        var armMaterial = AssetDatabase.LoadAssetAtPath<Material>(armPath);
        if (armMaterial == null) { armMaterial = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(armMaterial, armPath); }
        armMaterial.color = new Color(1f, .82f, .59f);
        EditorUtility.SetDirty(armMaterial);
        var arm = root.Find("Owner Switch Reach");
        if (arm == null) { arm = new GameObject("Owner Switch Reach").transform; arm.SetParent(root, false); }
        var line = arm.GetComponent<LineRenderer>();
        if (line == null) line = arm.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = armMaterial; line.useWorldSpace = true;
        line.positionCount = 3; line.startWidth = line.endWidth = .09f;
        line.numCapVertices = line.numCornerVertices = 0;
        line.sortingLayerID = dog.sortingLayerID; line.sortingOrder = 31;
        line.startColor = line.endColor = Color.white;
        lighting.ownerReach = line;
        lighting.ownerRenderer = story.owner.GetComponentInChildren<SpriteRenderer>(true);
        // Hide just the original lowered right arm while drawing that same arm
        // raised toward the switch. Keep the supplied raster sheet unchanged.
        var ownerArt = story.GetComponent<OwnerAnimationPresentation>();
        if (ownerArt != null && ownerArt.petFrames.Length > 0)
        {
            Sprite pose = ownerArt.petFrames[0];
            SpriteMask Mask(string name, Rect source)
            {
                var parent = lighting.ownerRenderer.transform;
                var child = parent.Find(name);
                if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
                var mask = child.GetComponent<SpriteMask>();
                if (mask == null) mask = child.gameObject.AddComponent<SpriteMask>();
                mask.sprite = square; mask.alphaCutoff = .1f;
                mask.isCustomRangeActive = true;
                mask.frontSortingLayerID = mask.backSortingLayerID = lighting.ownerRenderer.sortingLayerID;
                mask.frontSortingOrder = lighting.ownerRenderer.sortingOrder;
                mask.backSortingOrder = lighting.ownerRenderer.sortingOrder - 1;
                child.localPosition = new Vector3((source.center.x - pose.pivot.x) / pose.pixelsPerUnit,
                    (256f - source.center.y - pose.rect.yMin - pose.pivot.y) / pose.pixelsPerUnit, 0f);
                child.localScale = new Vector3(source.width / pose.pixelsPerUnit / square.bounds.size.x,
                    source.height / pose.pixelsPerUnit / square.bounds.size.y, 1f);
                mask.enabled = false; EditorUtility.SetDirty(mask); return mask;
            }
            foreach (string obsolete in new[] { "Switch Pose Upper Arm Mask", "Switch Pose Lower Arm Mask" })
            {
                var child = lighting.ownerRenderer.transform.Find(obsolete);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            lighting.restingArmMasks = new[] { Mask("Switch Pose Arm Mask", new Rect(144f, 106f, 18f, 68f)) };
        }
        lighting.startingDarkness = .28f; lighting.lightFadeSeconds = .35f; lighting.ownerStandOffset = .46f;
        SortSwitchBehindOwner(story);
        lighting.SetLightImmediate(false);
        foreach (var changed in new UnityEngine.Object[] { story, lighting, shade, lighting.switchRocker, lighting.switchIndicator, line })
            EditorUtility.SetDirty(changed);
    }

    public static void SortSwitchBehindOwner(AfterTheGlowStoryDirector story)
    {
        var owner = story.GetComponent<OwnerAnimationPresentation>()?.ownerRenderer;
        var wallSwitch = story.transform.Find("House Wall Switch");
        if (owner == null || wallSwitch == null) return;
        foreach (var renderer in wallSwitch.GetComponentsInChildren<SpriteRenderer>(true))
        {
            // Layer priority is evaluated before order: use the owner's layer
            // so the entire switch stays on the wall behind the passing actor.
            renderer.sortingLayerID = owner.sortingLayerID;
            renderer.sortingOrder = owner.sortingOrder - (renderer.transform == wallSwitch ? 3 : renderer.name == "Switch Indicator" ? 1 : 2);
            EditorUtility.SetDirty(renderer);
        }
    }

    private static SpriteRenderer Rectangle(Transform parent, string name, Sprite sprite, Material material, int layer, int order)
    {
        var item = parent.Find(name);
        if (item == null) { item = new GameObject(name).transform; item.SetParent(parent, false); }
        var renderer = item.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = item.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.sharedMaterial = material;
        renderer.sortingLayerID = layer; renderer.sortingOrder = order; renderer.enabled = true;
        return renderer;
    }

    [MenuItem("Tools/After the Glow/Apply Home Night and Owner Switch")]
    private static void ApplyAndSave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before saving room lighting.");
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        const string folder = "tmp/home-room-light-v2.45/";
        Directory.CreateDirectory(folder);
        if (!File.Exists(folder + "before-active.unity")) EditorSceneManager.SaveScene(story.gameObject.scene, folder + "before-active.unity", true);
        Apply(story);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        CaptureStates(story, folder);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        File.WriteAllLines(folder + "applied.txt", new[] {
            "Saved " + story.gameObject.scene.path,
            "Initial house-only night alpha=.28, warm house illustration revealed over .35 seconds after switch contact.",
            "Existing map wall switch=" + story.homeRoomLighting.wallSwitch.position + "; owner stand X=" + story.homeRoomLighting.OwnerSwitchStandX,
            "Wake .35 -> walk to switch -> reach .24 -> click -> room fade .35 -> lower arm -> bathroom -> original 4-second preparation.",
            "House light stays on when bathroom light is switched off; house shade ends at the home camera boundary.",
            "Static night/switch/lit previews; no Play Mode input sequence performed."
        });
        Debug.Log("Saved house night tint and owner wall switch: " + folder);
    }

    private static void CaptureStates(AfterTheGlowStoryDirector story, string folder)
    {
        var lighting = story.homeRoomLighting;
        var animation = story.GetComponent<OwnerAnimationPresentation>();
        var camera = story.cameraFollow.GetComponent<Camera>();
        Vector3 ownerPosition = story.owner.position, ownerScale = story.owner.localScale, cameraPosition = camera.transform.position;
        bool ownerActive = story.owner.gameObject.activeSelf, sleeping = animation.sleepingRenderer.enabled, duvet = animation.bedDuvetRenderer.enabled;
        Sprite ownerSprite = animation.ownerRenderer.sprite; bool oldFlip = animation.ownerRenderer.flipX;
        float size = camera.orthographicSize, aspect = camera.aspect;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1600, 900, 24); var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9f; camera.orthographicSize = 3f;
            float half = camera.orthographicSize * camera.aspect;
            camera.transform.position = new Vector3(Mathf.Clamp((story.bed.transform.position.x + lighting.OwnerSwitchStandX) * .5f,
                story.cameraFollow.mapMinX + half, story.cameraFollow.homeMapMaxX - half), story.cameraFollow.lockedY, -10f);
            void Shot(string name)
            {
                Physics2D.SyncTransforms(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply(); File.WriteAllBytes(folder + name + ".png", pixels.EncodeToPNG());
            }
            story.owner.gameObject.SetActive(false); animation.sleepingRenderer.enabled = true; animation.bedDuvetRenderer.enabled = false;
            lighting.SetLightImmediate(false); Shot("initial-night");
            story.owner.gameObject.SetActive(true); animation.sleepingRenderer.enabled = false; animation.bedDuvetRenderer.enabled = true;
            story.owner.localScale = Vector3.one; animation.ownerRenderer.sprite = animation.petFrames[0]; animation.ownerRenderer.flipX = false;
            story.owner.position = new Vector3(lighting.OwnerSwitchStandX, -3.86f, ownerPosition.z);
            lighting.SetOwnerReach(story.owner, 1f); Shot("switch-contact");
            lighting.SetLightImmediate(true); lighting.SetOwnerReach(story.owner, 1f); Shot("room-lit");
        }
        finally
        {
            story.owner.position = ownerPosition; story.owner.localScale = ownerScale; story.owner.gameObject.SetActive(ownerActive);
            animation.ownerRenderer.sprite = ownerSprite; animation.ownerRenderer.flipX = oldFlip;
            animation.sleepingRenderer.enabled = sleeping; animation.bedDuvetRenderer.enabled = duvet;
            lighting.SetLightImmediate(false);
            camera.transform.position = cameraPosition; camera.orthographicSize = size; camera.aspect = aspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Physics2D.SyncTransforms(); UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
