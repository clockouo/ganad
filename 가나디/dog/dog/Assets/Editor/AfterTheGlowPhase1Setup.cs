#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the map-independent phase-one test scene the first time the project
/// is opened with its scripts compiled. The source SampleScene is preserved
/// except for repairing Player wiring and aligning its ground collider and
/// spawn with the visible floor line.
/// </summary>
[InitializeOnLoad]
internal static class AfterTheGlowPhase1Setup
{
    private const string TestbedScenePath = "Assets/Scenes/Core_Testbed.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ShapeTexturePath = "Assets/Art/CoreTestbed/CoreTestbedShape.png";
    private const string AnimatorControllerPath = "Assets/Art/BorderCollie/BorderCollie_v5.controller";
    private const string SpriteLitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";
    private const int PlayerLayer = 25;
    private const int GroundLayer = 26;
    private const float SampleFloorSurfaceOffsetFromGroundPivot = -0.58f;

    static AfterTheGlowPhase1Setup()
    {
        EditorApplication.delayCall += EnsurePhaseOneSetup;
    }

    private static void EnsurePhaseOneSetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsurePhaseOneSetup;
            return;
        }

        if (File.Exists(ToAbsolutePath(TestbedScenePath)))
            return;

        try
        {
            BuildCoreTestbed();
            RepairSampleScenePlayer();
            AddTestbedToBuildSettings();
            Debug.Log("[After the Glow] Core_Testbed was generated. Open it from Assets/Scenes to inspect the phase-one systems.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[After the Glow] Phase-one scene setup failed. Use Tools > After the Glow > Generate Core Testbed to retry.\n" + exception);
        }
    }

    [MenuItem("Tools/After the Glow/Open Core_Testbed")]
    private static void OpenCoreTestbed()
    {
        if (!File.Exists(ToAbsolutePath(TestbedScenePath)))
            BuildCoreTestbed();

        RepairSampleScenePlayer();
        RepairCoreTestbedPlayer();
        AddTestbedToBuildSettings();
        EditorSceneManager.OpenScene(TestbedScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/After the Glow/Generate Core Testbed")]
    private static void GenerateCoreTestbed()
    {
        if (File.Exists(ToAbsolutePath(TestbedScenePath)))
        {
            RepairSampleScenePlayer();
            RepairCoreTestbedPlayer();
            AddTestbedToBuildSettings();
            EditorSceneManager.OpenScene(TestbedScenePath, OpenSceneMode.Single);
            Debug.Log("[After the Glow] Core_Testbed already exists and has been opened. Delete the scene asset first if you want a clean regeneration.");
            return;
        }

        BuildCoreTestbed();
        RepairSampleScenePlayer();
        RepairCoreTestbedPlayer();
        AddTestbedToBuildSettings();
        EditorSceneManager.OpenScene(TestbedScenePath, OpenSceneMode.Single);
    }

    private static void BuildCoreTestbed()
    {
        Sprite shapeSprite = EnsureShapeSprite();
        if (shapeSprite == null)
            throw new InvalidOperationException("The generated Core_Testbed shape sprite could not be imported.");

        Sprite dogSprite = FindIdleSprite();
        RuntimeAnimatorController animatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene testbed = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testbed);

        try
        {
            CreateBackground(shapeSprite);
            CameraFollow cameraFollow = CreateCamera();
            CreateGlobalLight();
            CreateGround(shapeSprite);

            PlayerMovement player = CreatePlayer(shapeSprite, dogSprite, animatorController);
            if (cameraFollow != null)
                cameraFollow.target = player.transform;
            CreateStaticBlocker(shapeSprite, "Fixed Shadow Blocker", new Vector2(-3.5f, -0.9f), new Vector2(1f, 2.2f));
            CreatePushableBlocker(shapeSprite, "Pushable Shadow Blocker", new Vector2(-0.3f, -1.25f), new Vector2(1.1f, 1.1f));
            CreateGameplayLight("Gameplay Light A", new Vector2(-6f, 0.8f), 5.5f, new Color(1f, 0.78f, 0.48f));
            CreateGameplayLight("Gameplay Light B", new Vector2(2f, 0.8f), 5.5f, new Color(1f, 0.9f, 0.68f));
            CreateDecorativeLight("Decoration Only Light", new Vector2(9f, 0.8f), 3.4f, new Color(0.45f, 0.68f, 1f));
            CreateGoal(shapeSprite, new Vector2(12f, -1.15f));
            CreateDebugOverlay(player);

            EditorSceneManager.MarkSceneDirty(testbed);
            if (!EditorSceneManager.SaveScene(testbed, TestbedScenePath))
                throw new InvalidOperationException("Unity could not save Assets/Scenes/Core_Testbed.unity.");
        }
        finally
        {
            EditorSceneManager.CloseScene(testbed, true);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    private static void CreateBackground(Sprite sprite)
    {
        GameObject background = CreateSpriteObject(
            "Testbed Background",
            sprite,
            new Vector2(0f, 1.5f),
            new Vector2(40f, 18f),
            new Color(0.12f, 0.15f, 0.2f),
            -20);
        background.transform.position = new Vector3(0f, 1.5f, 2f);
    }

    private static CameraFollow CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.backgroundColor = new Color(0.055f, 0.07f, 0.1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>();

        CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
        follow.smoothTime = 0.18f;
        follow.offset = new Vector3(0f, 0.2f, -10f);
        follow.minY = -0.2f;
        return follow;
    }

    private static void CreateGlobalLight()
    {
        GameObject lightObject = new GameObject("Global Light 2D");
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.blendStyleIndex = 0;
        light.color = new Color(0.55f, 0.61f, 0.76f);
        light.intensity = 0.28f;
        light.shadowsEnabled = false;
    }

    private static void CreateGround(Sprite sprite)
    {
        GameObject ground = CreateSpriteObject(
            "Ground",
            sprite,
            new Vector2(0f, -2.5f),
            new Vector2(34f, 1f),
            new Color(0.25f, 0.29f, 0.34f),
            -5);
        ground.tag = "Ground";
        if (GroundLayer >= 0)
            ground.layer = GroundLayer;

        BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }

    private static PlayerMovement CreatePlayer(
        Sprite shapeSprite,
        Sprite dogSprite,
        RuntimeAnimatorController animatorController)
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.tag = "Player";
        if (PlayerLayer >= 0)
            playerObject.layer = PlayerLayer;
        playerObject.transform.position = new Vector3(-11f, -1.86f, 0f);
        playerObject.transform.localScale = Vector3.one * 1.4f;

        Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
        body.mass = 1f;
        body.gravityScale = 3f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D collider = playerObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.68f, 0.82f);

        SpriteRenderer renderer = playerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = dogSprite != null ? dogSprite : shapeSprite;
        float spriteBottom = renderer.sprite != null ? renderer.sprite.bounds.min.y : -0.1f;
        collider.offset = new Vector2(0f, spriteBottom + collider.size.y * 0.5f);
        renderer.sharedMaterial = FindSpriteLitMaterial();
        renderer.color = Color.white;
        renderer.sortingOrder = 1;

        Animator animator = playerObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;

        PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
        movement.moveSpeed = 5f;
        movement.jumpForce = 6.8f;
        movement.fallMultiplier = 2.5f;
        movement.lowJumpMultiplier = 2f;
        movement.groundCheckRadius = 0.17f;
        movement.groundLayer = GroundLayer >= 0 ? 1 << GroundLayer : Physics2D.DefaultRaycastLayers;

        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(playerObject.transform, false);
        groundCheck.transform.localPosition = new Vector3(0f, spriteBottom - 0.02f, 0f);
        movement.groundCheck = groundCheck.transform;

        playerObject.AddComponent<LightExposureSensor>();
        playerObject.AddComponent<PlayerInteraction>();
        return movement;
    }

    private static void CreateStaticBlocker(
        Sprite sprite,
        string objectName,
        Vector2 position,
        Vector2 size)
    {
        GameObject blocker = CreateSpriteObject(
            objectName,
            sprite,
            position,
            size,
            new Color(0.36f, 0.32f, 0.28f),
            0);
        blocker.AddComponent<BoxCollider2D>();
        blocker.AddComponent<LightOccluder2D>();
    }

    private static void CreatePushableBlocker(
        Sprite sprite,
        string objectName,
        Vector2 position,
        Vector2 size)
    {
        GameObject blocker = CreateSpriteObject(
            objectName,
            sprite,
            position,
            size,
            new Color(0.55f, 0.36f, 0.22f),
            0);
        Rigidbody2D body = blocker.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = 2f;
        body.gravityScale = 3f;
        body.linearDamping = 1.2f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezePositionX |
                           RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D collider = blocker.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        blocker.AddComponent<LightOccluder2D>();
        blocker.AddComponent<PushableObject>();
    }

    private static void CreateGameplayLight(
        string objectName,
        Vector2 position,
        float radius,
        Color color)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.position = position;
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = 1;
        light.color = color;
        light.intensity = 1.55f;
        light.pointLightInnerRadius = radius * 0.28f;
        light.pointLightOuterRadius = radius;
        light.shadowsEnabled = true;
        light.shadowIntensity = 0.78f;
        light.shadowSoftness = 0.22f;
        light.falloffIntensity = 0.75f;

        GameplayLight2D gameplayLight = lightObject.AddComponent<GameplayLight2D>();
        SerializedObject serializedLight = new SerializedObject(gameplayLight);
        serializedLight.FindProperty("gameplayRadius").floatValue = radius;
        serializedLight.FindProperty("gameplayIntensityMultiplier").floatValue = 1f;
        serializedLight.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateDecorativeLight(
        string objectName,
        Vector2 position,
        float radius,
        Color color)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.position = position;
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = 1;
        light.color = color;
        light.intensity = 0.8f;
        light.pointLightOuterRadius = radius;
        light.shadowsEnabled = false;
    }

    private static void CreateGoal(Sprite sprite, Vector2 position)
    {
        GameObject goal = CreateSpriteObject(
            "Goal Trigger",
            sprite,
            position,
            new Vector2(0.35f, 2.25f),
            new Color(0.28f, 0.85f, 0.52f, 0.72f),
            2);
        BoxCollider2D trigger = goal.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(
            2.2f / Mathf.Max(0.01f, goal.transform.localScale.x),
            2.6f / Mathf.Max(0.01f, goal.transform.localScale.y));
        goal.AddComponent<GoalTrigger2D>();
    }

    private static void CreateDebugOverlay(PlayerMovement player)
    {
        GameObject debugObject = new GameObject("Core Debug Overlay");
        CoreDebugOverlay overlay = debugObject.AddComponent<CoreDebugOverlay>();
        SerializedObject serializedOverlay = new SerializedObject(overlay);
        serializedOverlay.FindProperty("player").objectReferenceValue = player;
        serializedOverlay.FindProperty("exposureSensor").objectReferenceValue =
            player.GetComponent<LightExposureSensor>();
        serializedOverlay.FindProperty("interaction").objectReferenceValue =
            player.GetComponent<PlayerInteraction>();
        serializedOverlay.FindProperty("visibleAtStart").boolValue = true;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateSpriteObject(
        string objectName,
        Sprite sprite,
        Vector2 position,
        Vector2 size,
        Color color,
        int sortingOrder)
    {
        GameObject gameObject = new GameObject(objectName);
        gameObject.transform.position = position;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = FindSpriteLitMaterial();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        Vector2 spriteSize = sprite != null ? sprite.bounds.size : Vector2.one;
        gameObject.transform.localScale = new Vector3(
            size.x / Mathf.Max(0.01f, spriteSize.x),
            size.y / Mathf.Max(0.01f, spriteSize.y),
            1f);
        return gameObject;
    }

    private static Sprite EnsureShapeSprite()
    {
        string absolutePath = ToAbsolutePath(ShapeTexturePath);
        string directory = Path.GetDirectoryName(absolutePath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(absolutePath))
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(
            ShapeTexturePath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(ShapeTexturePath) as TextureImporter;
        if (importer != null &&
            (importer.textureType != TextureImporterType.Sprite ||
             importer.spriteImportMode != SpriteImportMode.Single ||
             !Mathf.Approximately(importer.spritePixelsPerUnit, 2f)))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 2f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(ShapeTexturePath);
    }

    private static Sprite FindIdleSprite()
    {
        const string idleClipPath = "Assets/Art/BorderCollie/Idle_v5.anim";
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idleClipPath);
        if (idleClip != null)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(idleClip);
            foreach (EditorCurveBinding binding in bindings)
            {
                if (!binding.propertyName.Contains("m_Sprite"))
                    continue;

                ObjectReferenceKeyframe[] keyframes =
                    AnimationUtility.GetObjectReferenceCurve(idleClip, binding);
                foreach (ObjectReferenceKeyframe keyframe in keyframes)
                {
                    Sprite sprite = keyframe.value as Sprite;
                    if (sprite != null)
                        return sprite;
                }
            }
        }

        UnityEngine.Object[] sprites =
            AssetDatabase.LoadAllAssetsAtPath("Assets/Art/BorderCollie/BorderCollie_Walk_v5.png");
        return sprites.OfType<Sprite>().FirstOrDefault();
    }

    private static Material FindSpriteLitMaterial()
    {
        string materialPath = AssetDatabase.GUIDToAssetPath(SpriteLitMaterialGuid);
        return string.IsNullOrEmpty(materialPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    }

    private static void RepairSampleScenePlayer()
    {
        if (!File.Exists(ToAbsolutePath(SampleScenePath)))
            return;

        Scene previousActiveScene = SceneManager.GetActiveScene();
        string previousActiveScenePath = previousActiveScene.path;
        Scene existing = SceneManager.GetSceneByPath(SampleScenePath);
        bool alreadyLoaded = existing.IsValid() && existing.isLoaded;
        Scene sample = alreadyLoaded
            ? existing
            : EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        try
        {
            GameObject playerObject = FindRootObject(sample, "Player");
            if (playerObject == null)
                return;

            bool changed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(playerObject) > 0;

            PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
            if (movement == null)
            {
                movement = Undo.AddComponent<PlayerMovement>(playerObject);
                changed = true;
            }

            if (playerObject.GetComponent<LightExposureSensor>() == null)
            {
                Undo.AddComponent<LightExposureSensor>(playerObject);
                changed = true;
            }
            if (playerObject.GetComponent<PlayerInteraction>() == null)
            {
                Undo.AddComponent<PlayerInteraction>(playerObject);
                changed = true;
            }

            Transform groundCheck = playerObject.transform.Find("GroundCheck");
            if (groundCheck == null)
            {
                GameObject groundCheckObject = new GameObject("GroundCheck");
                groundCheckObject.transform.SetParent(playerObject.transform, false);
                groundCheck = groundCheckObject.transform;
                changed = true;
            }

            if (movement.groundCheck != groundCheck)
            {
                movement.groundCheck = groundCheck;
                changed = true;
            }

            if (GroundLayer >= 0 && movement.groundLayer.value != 1 << GroundLayer)
            {
                movement.groundLayer = 1 << GroundLayer;
                changed = true;
            }

            GameObject groundObject = FindRootObject(sample, "Ground");
            if (groundObject != null && GroundLayer >= 0 && groundObject.layer != GroundLayer)
            {
                groundObject.layer = GroundLayer;
                changed = true;
            }

            BoxCollider2D groundCollider = groundObject != null
                ? groundObject.GetComponent<BoxCollider2D>()
                : null;
            if (groundCollider != null)
            {
                Vector2 groundOffset = groundCollider.offset;
                groundOffset.y = SampleFloorSurfaceOffsetFromGroundPivot - groundCollider.size.y * 0.5f;
                if (!Mathf.Approximately(groundCollider.offset.y, groundOffset.y))
                {
                    groundCollider.offset = groundOffset;
                    changed = true;
                }
            }

            if (AlignPlayerToGround(playerObject, groundObject, groundCheck))
                changed = true;

            if (movement.jumpForce > 8f)
            {
                movement.jumpForce = 6.8f;
                changed = true;
            }

            if (playerObject.layer != PlayerLayer)
            {
                playerObject.layer = PlayerLayer;
                changed = true;
            }

            CameraFollow cameraFollow = FindComponentInScene<CameraFollow>(sample);
            if (cameraFollow != null && cameraFollow.target != playerObject.transform)
            {
                cameraFollow.target = playerObject.transform;
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(sample);
                if (!EditorSceneManager.SaveScene(sample))
                    throw new InvalidOperationException("Unity could not save Assets/Scenes/SampleScene.unity.");
                Debug.Log("[After the Glow] SampleScene Player wiring and ground alignment were repaired and saved.");
            }
        }
        finally
        {
            if (!alreadyLoaded)
                RestoreSceneAfterRepair(previousActiveScene, previousActiveScenePath, SampleScenePath);
            else if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    private static void RepairCoreTestbedPlayer()
    {
        if (!File.Exists(ToAbsolutePath(TestbedScenePath)))
            return;

        Scene previousActiveScene = SceneManager.GetActiveScene();
        string previousActiveScenePath = previousActiveScene.path;
        Scene existing = SceneManager.GetSceneByPath(TestbedScenePath);
        bool alreadyLoaded = existing.IsValid() && existing.isLoaded;
        Scene testbed = alreadyLoaded
            ? existing
            : EditorSceneManager.OpenScene(TestbedScenePath, OpenSceneMode.Single);

        try
        {
            GameObject playerObject = FindRootObject(testbed, "Player");
            GameObject groundObject = FindRootObject(testbed, "Ground");
            if (playerObject == null)
                return;

            bool changed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(playerObject) > 0;
            PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
            Transform groundCheck = playerObject.transform.Find("GroundCheck");
            if (groundCheck == null)
            {
                GameObject groundCheckObject = new GameObject("GroundCheck");
                groundCheckObject.transform.SetParent(playerObject.transform, false);
                groundCheck = groundCheckObject.transform;
                changed = true;
            }

            if (movement != null)
            {
                if (movement.groundCheck != groundCheck)
                {
                    movement.groundCheck = groundCheck;
                    changed = true;
                }

                if (GroundLayer >= 0 && movement.groundLayer.value != 1 << GroundLayer)
                {
                    movement.groundLayer = 1 << GroundLayer;
                    changed = true;
                }
            }

            if (GroundLayer >= 0 && groundObject != null && groundObject.layer != GroundLayer)
            {
                groundObject.layer = GroundLayer;
                changed = true;
            }

            if (PlayerLayer >= 0 && playerObject.layer != PlayerLayer)
            {
                playerObject.layer = PlayerLayer;
                changed = true;
            }

            if (AlignPlayerToGround(playerObject, groundObject, groundCheck))
                changed = true;

            CameraFollow cameraFollow = FindComponentInScene<CameraFollow>(testbed);
            if (cameraFollow != null && cameraFollow.target != playerObject.transform)
            {
                cameraFollow.target = playerObject.transform;
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(testbed);
                if (!EditorSceneManager.SaveScene(testbed))
                    throw new InvalidOperationException("Unity could not save Assets/Scenes/Core_Testbed.unity.");
                Debug.Log("[After the Glow] Core_Testbed Player and ground alignment were repaired and saved.");
            }
        }
        finally
        {
            if (!alreadyLoaded)
                RestoreSceneAfterRepair(previousActiveScene, previousActiveScenePath, TestbedScenePath);
            else if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    private static void RestoreSceneAfterRepair(Scene previousActiveScene, string previousScenePath, string repairedScenePath)
    {
        if (string.IsNullOrEmpty(previousScenePath) || previousScenePath == repairedScenePath)
            return;

        if (File.Exists(ToAbsolutePath(previousScenePath)))
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        else if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);
    }

    private static bool AlignPlayerToGround(
        GameObject playerObject,
        GameObject groundObject,
        Transform groundCheck)
    {
        if (playerObject == null)
            return false;

        bool changed = false;
        SpriteRenderer spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
        BoxCollider2D playerCollider = playerObject.GetComponent<BoxCollider2D>();
        float spriteBottom = spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.sprite.bounds.min.y
            : playerCollider != null ? -playerCollider.size.y * 0.5f : -0.1f;

        if (playerCollider != null)
        {
            Vector2 colliderOffset = playerCollider.offset;
            colliderOffset.y = spriteBottom + playerCollider.size.y * 0.5f;
            if (!Mathf.Approximately(playerCollider.offset.y, colliderOffset.y))
            {
                playerCollider.offset = colliderOffset;
                changed = true;
            }
        }

        if (groundCheck != null)
        {
            Vector3 groundCheckPosition = groundCheck.localPosition;
            groundCheckPosition.y = spriteBottom - 0.02f;
            if (!Mathf.Approximately(groundCheck.localPosition.y, groundCheckPosition.y))
            {
                groundCheck.localPosition = groundCheckPosition;
                changed = true;
            }
        }

        Collider2D groundCollider = groundObject != null
            ? groundObject.GetComponent<Collider2D>()
            : null;
        if (playerCollider == null || groundCollider == null)
            return changed;

        Bounds groundBounds = groundCollider.bounds;
        Vector3 scale = playerObject.transform.lossyScale;
        float scaleX = Mathf.Abs(scale.x);
        float scaleY = Mathf.Abs(scale.y);
        float playerHalfWidth = playerCollider.size.x * 0.5f * scaleX;
        float minimumPlayerX = groundBounds.min.x + playerHalfWidth + 0.05f;
        float maximumPlayerX = groundBounds.max.x - playerHalfWidth - 0.05f;
        Vector3 playerPosition = playerObject.transform.position;

        if (minimumPlayerX <= maximumPlayerX)
        {
            float alignedX = Mathf.Clamp(playerPosition.x, minimumPlayerX, maximumPlayerX);
            if (!Mathf.Approximately(playerPosition.x, alignedX))
            {
                playerPosition.x = alignedX;
                changed = true;
            }
        }

        float colliderBottomLocal = playerCollider.offset.y - playerCollider.size.y * 0.5f;
        float alignedY = groundBounds.max.y - colliderBottomLocal * scaleY;
        if (!Mathf.Approximately(playerPosition.y, alignedY))
        {
            playerPosition.y = alignedY;
            changed = true;
        }

        if (changed)
            playerObject.transform.position = playerPosition;
        return changed;
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;

            Transform found = root.transform.Find(objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static void AddTestbedToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == TestbedScenePath && scene.enabled))
            return;

        EditorBuildSettings.scenes = scenes
            .Where(scene => scene.path != TestbedScenePath)
            .Concat(new[] { new EditorBuildSettingsScene(TestbedScenePath, true) })
            .ToArray();
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
#endif
