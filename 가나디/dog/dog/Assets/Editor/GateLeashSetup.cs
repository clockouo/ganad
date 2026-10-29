using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Uses the existing clean gate art and dog tug frames; no new raster art.</summary>
public static class GateLeashSetup
{
    public static void Apply(AfterTheGlowStoryDirector story)
    {
        Physics2D.SyncTransforms();
        var gate = story.parkGate;
        var view = gate.GetComponent<GateHingePresentation>();
        if (view == null || view.releasedLeftSprite == null || view.releasedRightSprite == null)
            throw new MissingReferenceException("Prepare the two gate leaves before the separate leash.");
        var leash = gate.GetComponent<GateLeashPresentation>();
        if (leash == null) leash = gate.gameObject.AddComponent<GateLeashPresentation>();
        leash.gate = gate;
        float centerX = (view.leftLeaf.transform.position.x + view.rightLeaf.transform.position.x) * .5f;
        // The artwork is a plane behind the walking lane. Let the dog walk in
        // front of the left leaf to reach the CENTRAL knot, then block passage.
        var blocker = gate.GetComponent<BoxCollider2D>();
        Vector2 size = blocker.size, offset = blocker.offset;
        size.x = .12f / Mathf.Abs(gate.transform.lossyScale.x);
        offset.x = gate.transform.InverseTransformPoint(new Vector3(centerX + .34f, gate.transform.position.y, 0f)).x;
        blocker.size = size; blocker.offset = offset;
        leash.knot = Point(gate.transform, "Leash Tie Point",
            new Vector3(centerX, gate.transform.position.y + .82f, -.55f));
        leash.grip = Point(gate.transform, "Reachable Leash Handle",
            new Vector3(centerX, gate.transform.position.y + .124f, -.55f));
        const string path = "Assets/Art/PixelSnapper/Props/Gate_Separate_Leash.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = new Color(.71f, .25f, .19f, 1f);
        EditorUtility.SetDirty(material);
        var dogRenderer = story.player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        leash.strap = Line(gate.transform, "Flexible Red Leash", material, dogRenderer.sortingLayerID);
        leash.knotLoop = Line(gate.transform, "Leash Bar Knot", material, dogRenderer.sortingLayerID);
        const string forwardPath = "Assets/Art/PixelSnapper/Props/Gate_ForwardOpening.mat";
        var forwardMaterial = AssetDatabase.LoadAssetAtPath<Material>(forwardPath);
        if (forwardMaterial == null)
        {
            var shader = Shader.Find("AfterTheGlow/GateForwardOpening");
            if (shader == null) throw new MissingReferenceException("Import the forward opening gate shader first.");
            forwardMaterial = new Material(shader);
            AssetDatabase.CreateAsset(forwardMaterial, forwardPath);
        }
        view.closedMaterial = view.rightLeaf.sharedMaterial;
        view.closedSortingLayerID = view.rightLeaf.sortingLayerID;
        view.closedSortingOrder = view.rightLeaf.sortingOrder;
        view.forwardOpenMaterial = forwardMaterial;
        view.opensIntoGarden = true;
        view.foregroundSortingLayerID = dogRenderer.sortingLayerID;
        view.foregroundSortingOrder = dogRenderer.sortingOrder + 10;
        view.forwardDepth = .42f;
        view.forwardPerspective = .18f;
        view.separateLeashVisual = true;
        view.ResetPose();
        var oldKnot = gate.transform.Find("Visible Leash Knot")?.GetComponent<SpriteRenderer>();
        if (oldKnot != null) oldKnot.enabled = false;
        var dust = gate.transform.Find("Pixel Dust on Leash Release");
        if (dust != null) dust.position = leash.knot.position;
        leash.RenderPose(null);
        EditorUtility.SetDirty(leash); EditorUtility.SetDirty(view); EditorUtility.SetDirty(gate); EditorUtility.SetDirty(blocker);
    }

    private static Transform Point(Transform parent, string name, Vector3 position)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        child.position = position;
        return child;
    }

    private static LineRenderer Line(Transform parent, string name, Material material, int sortingLayer)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        var line = child.GetComponent<LineRenderer>();
        if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = true;
        line.startWidth = line.endWidth = .035f;
        line.numCapVertices = line.numCornerVertices = 0;
        line.sortingLayerID = sortingLayer; line.sortingOrder = 17;
        line.startColor = line.endColor = Color.white;
        line.enabled = true;
        EditorUtility.SetDirty(line);
        return line;
    }

    public static string ApplyAndSave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before saving the leash.");
        var story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        if (story == null) throw new MissingReferenceException("Open SampleScene first.");
        const string folder = "tmp/gate-center-inward-v2.44/";
        Directory.CreateDirectory(folder);
        if (!File.Exists(folder + "before-active.unity"))
            EditorSceneManager.SaveScene(story.gameObject.scene, folder + "before-active.unity", true);
        Physics2D.SyncTransforms();
        Apply(story);
        // Rebuild the existing controller with its new mouth-hold/tug state.
        typeof(AfterTheGlowStorySetup).GetMethod("CreatePixelDogController", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        CapturePoses(story, folder);
        EditorSceneManager.SaveScene(story.gameObject.scene);
        var leash = story.parkGate.GetComponent<GateLeashPresentation>();
        File.WriteAllLines(folder + "applied.txt", new[] {
            "Saved scene: " + story.gameObject.scene.path,
            "Gate knot=" + leash.knot.position + "; reachable handle=" + leash.grip.position,
            "Knot and hanging grip are exactly at the join of the two leaves; the blocker begins .28 units to its right.",
            "Click nearby: walk to handle, latch only within .15 mouth distance; second click releases/cancels.",
            "Bite pause=.18; backward tug speed=.85; new tension beyond .06 slack triggers existing .45 pull memory sequence.",
            "Leash follows measured muzzle points in current sprite, including all four tug frames, in LateUpdate.",
            "Left leaf recedes .42 units into the garden, its free edge shrinks 18%, and it draws behind the dog; right leaf stays closed.",
            "Existing frames19-22 used for hold/tug. Static pose previews only; no input playtest performed."
        });
        return "Saved reachable mouth-grab leash, bite/tug animation and static previews: " + folder;
    }

    [MenuItem("Tools/After the Glow/Apply Reachable Bite and Pull Leash")]
    private static void ApplyMenu() => Debug.Log(ApplyAndSave());

    private static void CapturePoses(AfterTheGlowStoryDirector story, string folder)
    {
        var camera = story.cameraFollow.GetComponent<Camera>();
        var player = story.player;
        var sprite = player.transform.Find("PixelSnapper Dog Visual").GetComponent<SpriteRenderer>();
        var interaction = player.GetComponent<PlayerInteraction>();
        var leash = story.parkGate.GetComponent<GateLeashPresentation>();
        Vector3 oldPlayer = player.transform.position, oldScale = player.transform.localScale, oldCamera = camera.transform.position;
        Sprite oldSprite = sprite.sprite;
        var oldTarget = camera.targetTexture; var oldRender = RenderTexture.active;
        float oldSize = camera.orthographicSize, oldAspect = camera.aspect;
        var target = new RenderTexture(1440, 810, 24);
        var pixels = new Texture2D(1440, 810, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9f; camera.orthographicSize = 1.45f;
            player.transform.localScale = new Vector3(Mathf.Abs(oldScale.x), oldScale.y, oldScale.z);
            Sprite FindFrame(string name)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/AfterTheGlow/Characters/Dog_User_Actions_5Rows.png"))
                    if (asset is Sprite frame && frame.name == name) return frame;
                throw new MissingReferenceException(name);
            }
            sprite.sprite = FindFrame("Dog_Storybook_19");
            player.transform.position = new Vector3(leash.GripPosition.x - .403f, story.parkGate.transform.position.y, oldPlayer.z);
            Vector3 bitePosition = player.transform.position;
            void Shot(string name, Vector3? mouth)
            {
                leash.RenderPose(mouth);
                camera.transform.position = new Vector3(leash.knot.position.x, story.parkGate.transform.position.y + 1f, -10f);
                Physics2D.SyncTransforms(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 810), 0, 0); pixels.Apply();
                File.WriteAllBytes(folder + name + ".png", pixels.EncodeToPNG());
            }
            player.transform.position = bitePosition + Vector3.left * .9f;
            Shot("approach", null);
            player.transform.position = bitePosition;
            leash.BeginBite(interaction);
            Shot("bite", interaction.MouthPosition);
            player.transform.position = bitePosition + Vector3.left * .65f;
            sprite.sprite = FindFrame("Dog_Storybook_21");
            Shot("pull", interaction.MouthPosition);
            leash.EndBite();
            leash.strap.enabled = leash.knotLoop.enabled = false;
            var view = story.parkGate.GetComponent<GateHingePresentation>();
            view.SetReleased(true);
            // Place the dog across the opened leaf to show it stays behind him.
            player.transform.position = new Vector3(view.leftLeaf.transform.position.x + .45f,
                story.parkGate.transform.position.y, oldPlayer.z);
            view.SetOpenProgress(.5f);
            ShotOpen("half-open-inward");
            view.SetOpenProgress(1f);
            ShotOpen("open-inward");
            void ShotOpen(string name)
            {
                camera.transform.position = new Vector3(story.parkGate.transform.position.x,
                    story.parkGate.transform.position.y + .7f, -10f);
                Physics2D.SyncTransforms(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 810), 0, 0); pixels.Apply();
                File.WriteAllBytes(folder + name + ".png", pixels.EncodeToPNG());
            }
        }
        finally
        {
            player.transform.position = oldPlayer; player.transform.localScale = oldScale; sprite.sprite = oldSprite;
            story.parkGate.GetComponent<GateHingePresentation>().ResetPose();
            leash.EndBite();
            camera.transform.position = oldCamera; camera.orthographicSize = oldSize; camera.aspect = oldAspect;
            camera.targetTexture = oldTarget; RenderTexture.active = oldRender;
            Physics2D.SyncTransforms(); UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
