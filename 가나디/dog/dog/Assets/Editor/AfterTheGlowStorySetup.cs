#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Builds the story slice with the titleless map illustrations and interactive story props.</summary>
internal static class AfterTheGlowStorySetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ShapePath = "Assets/Art/CoreTestbed/CoreTestbedShape.png";
    private const string CirclePath = "Assets/Art/Generated/Phase1Circle.png";
    private const string PixelArtRoot = "Assets/Art/PixelSnapper/";
    private const string InteriorBackgroundPath = PixelArtRoot + "Backgrounds/Interior_Background.png";
    private const string InteriorBackgroundNoBedPath = PixelArtRoot + "Backgrounds/Interior_Background_NoBed.png";
    private const string Map01BackgroundPath = "Assets/Art/AfterTheGlow/Backgrounds/Map01_Home.png";
    private const string Map02BackgroundPath = "Assets/Art/AfterTheGlow/References/Map02_Original.png";
    private const string Map03BackgroundPath = "Assets/Art/AfterTheGlow/Backgrounds/Map03_Park.png";
    private const string StorybookDogPath = "Assets/Art/AfterTheGlow/Characters/Dog_User_Actions_5Rows.png";
    private const string UserDogIdlePath = "Assets/Art/AfterTheGlow/Characters/Dog_User_Idle_6Frames.png";
    private const string CarFrontOffTexturePath = "Assets/Art/AfterTheGlow/Props/CarFront_LightsOff.png";
    private const string CarFrontOnTexturePath = "Assets/Art/AfterTheGlow/Props/CarFront_LightsOn.png";
    private const string CarFrontOffSpritePath = "Assets/Art/AfterTheGlow/Props/CarFront_LightsOff.asset";
    private const string CarFrontOnSpritePath = "Assets/Art/AfterTheGlow/Props/CarFront_LightsOn.asset";
    private const string OwnerWalkPath = PixelArtRoot + "Characters/Owner_03_WalkA.asset";
    private const string OwnerPetPath = PixelArtRoot + "Characters/Owner_05_Pet.asset";
    private const string BedArtPath = PixelArtRoot + "Props/Bed.png";
    private const string BallArtPath = PixelArtRoot + "Props/Ball.png";
    private const string FrontDoorClosedArtPath = PixelArtRoot + "Props/FrontDoor_Closed.png";
    private const string FrontDoorOpenArtPath = PixelArtRoot + "Props/FrontDoor_Open.png";
    private const string GateBoundArtPath = PixelArtRoot + "Props/Gate_LeashBound.png";
    private const string GateLooseArtPath = PixelArtRoot + "Props/Gate_ChainLoose.png";
    private const string GateBrokenArtPath = PixelArtRoot + "Props/Gate_ChainBroken.png";
    private const string PixelDogControllerPath = PixelArtRoot + "Animations/PixelSnapperBorderCollie.controller";
    private const string PixelDogIdleClipPath = PixelArtRoot + "Animations/PixelSnapperBorderCollie_Idle.anim";
    private const string PixelDogWalkClipPath = PixelArtRoot + "Animations/PixelSnapperBorderCollie_Walk.anim";
    private const string PixelDogPushClipPath = PixelArtRoot + "Animations/PixelSnapperBorderCollie_Push.anim";
    private const string PixelDogJumpClipPath = PixelArtRoot + "Animations/PixelSnapperBorderCollie_Jump.anim";
    private const string PixelDogCrouchClipPath = PixelArtRoot + "Animations/PixelSnapperBorderCollie_Crouch.anim";
    private const string SpriteUnlitMaterialGuid = "9dfc825aed78fcd4ba02077103263b40";
    private const string SpriteLitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";
    private const string StoryRootName = "AfterTheGlowStoryDemo";
    private const float RoomExtensionLeftWidth = 0f;
    private const float RoomBackgroundWidth = 18f;
    private const float RoomBackgroundHeight = 6f;
    private const float RoomBackgroundLeftX = -11.5f;
    private const float RoomBackgroundCenterX = RoomBackgroundLeftX + RoomBackgroundWidth * 0.5f;
    private const float TutorialStartX = -10.8f;
    private const float CameraAspect = 16f / 9f;
    private const float HomeOrthographicSize = RoomBackgroundHeight * 0.5f;
    private const float InitialCameraHalfWidth = HomeOrthographicSize * CameraAspect;
    private const float TutorialCameraLeadX = RoomBackgroundLeftX + InitialCameraHalfWidth - TutorialStartX;
    // Keep the stationary bed in the left tutorial nook, leaving a clear approach to the ball.
    private const float BedCenterFromBaseRoomLeft = 1.7f;
    private const float BathroomX = -3.8f;
    private const float BallX = -5.0f;
    private const float BallDiameter = 0.48f;
    private const float OwnerPreparationProgressX = 1.4f;
    private const float FrontDoorX = 5.1f;
    private const float OwnerDoorStandOffset = 1f;
    private const float OwnerDoorExitOffset = 1.25f;
    private const float StreetEntryX = AfterTheGlowWorldLayout.StreetEntryX;
    private const float ExteriorCameraOffsetX = -0.5f;
    private const float ExteriorCameraOrthographicSize = HomeOrthographicSize;
    private const float StreetBackdropLeftX = 6.6f;
    private const float StreetGroundLeftX = FrontDoorX - 0.15f;
    private const float StreetGroundRightX = AfterTheGlowWorldLayout.ParkEndX;
    private const float StreetBackdropTileWidth = AfterTheGlowWorldLayout.IllustrationWidth;
    private const float StreetBackdropTileHeight = StreetBackdropTileWidth * 724f / 2172f;
    private const float ParkBackdropStartX = AfterTheGlowWorldLayout.StreetEndX;
    private const float ParkBackdropWidth = AfterTheGlowWorldLayout.IllustrationWidth;
    private const float ParkBackdropHeight = ParkBackdropWidth * 724f / 2172f;
    private const float Map02CenterY = FloorTop + (0.5f - 183f / 724f) * StreetBackdropTileHeight;
    private const float Map03CenterY = FloorTop + (530f - 362f) * (ParkBackdropWidth / 2172f);
    private const float CarX = AfterTheGlowWorldLayout.CarX;
    private const float GateX = 6.6f + 1740.5f * AfterTheGlowWorldLayout.UnitsPerPixel;
    private const float ParkEntryX = AfterTheGlowWorldLayout.ParkEntryX;
    private const float FloorTop = -3.86f;
    private const float FloorCenterY = -4.36f;
    private const float DogVisualFootOffset = -0.13f;
    private const float RoomBackgroundCenterY = FloorTop + (0.5f - 183f / 724f) * RoomBackgroundHeight;

    [MenuItem("Tools/After the Glow/Build House and Street Demo")]
    private static void BuildHouseAndStreetDemo()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureStorybookDogSprites();
        GameObject oldRoot = GameObject.Find(StoryRootName);
        if (oldRoot != null)
        {
            PlayerMovement existingPlayer = FindRoot<PlayerMovement>(scene);
            CameraFollow existingCamera = FindRoot<CameraFollow>(scene);
            if (existingPlayer == null || existingCamera == null)
                throw new MissingComponentException("SampleScene needs its existing PlayerMovement and CameraFollow components.");
            RefreshExistingStoryDemo(oldRoot);
            RepairPlayerAndFloor(scene, existingPlayer, existingCamera);
            CreateOrRefreshHouseLeftBoundary(oldRoot.transform, GetBackgroundLeftX(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Unity could not save SampleScene after refreshing graybox materials.");

            Debug.Log("[After the Glow] Existing story demo refreshed with the titleless illustrated maps, storybook dog, owner, and story props.");
            return;
        }

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(ShapePath);
        Sprite circle = EnsureCircleSprite();
        if (square == null || circle == null)
            throw new FileNotFoundException("Could not load the phase-one shape sprites.");

        GameObject root = new GameObject(StoryRootName);
        Undo.RegisterCreatedObjectUndo(root, "Build After the Glow story demo");

        PlayerMovement player = FindRoot<PlayerMovement>(scene);
        if (player == null)
            throw new MissingComponentException("SampleScene needs its existing PlayerMovement component.");
        CameraFollow cameraFollow = FindRoot<CameraFollow>(scene);
        if (cameraFollow == null)
            throw new MissingComponentException("SampleScene needs its existing CameraFollow component.");
        GameObject backgroundObject = FindRootObject(scene, "BackGround");
        SpriteRenderer backgroundRenderer = backgroundObject != null
            ? backgroundObject.GetComponent<SpriteRenderer>()
            : null;
        int backgroundLayerId = backgroundRenderer != null ? backgroundRenderer.sortingLayerID : 0;
        ApplyRestoredRoomArt(scene, root.transform, player);
        RepairPlayerAndFloor(scene, player, cameraFollow);
        PhysicsMaterial2D zeroFriction = FindZeroFrictionMaterial();
        float bedCenterX = GetBedCenterX(scene);
        CreateOrRefreshHouseLeftBoundary(root.transform, GetBackgroundLeftX(scene));

        PushableObject bed = CreatePushable(root.transform, square, "Bed Push Target",
            new Vector2(bedCenterX, FloorTop + 0.45f), new Vector2(2.8f, 0.9f),
            new Color(0.54f, 0.31f, 0.16f), backgroundLayerId, 3, zeroFriction,
            0.52f, false, 0f, false, null, null, null);
        bed.ConfigureInteractionOnlyTrigger(true);
        player.jumpBlocker = bed.GetComponent<Collider2D>();
        player.jumpBlockerPadding = 0.12f;
        SpriteRenderer bedTargetRenderer = bed.GetComponent<SpriteRenderer>();
        if (bedTargetRenderer != null)
            bedTargetRenderer.enabled = false; // The original bed remains visible in the room artwork.
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null)
            SetPrivateFloat(interaction, "interactionRange", 2.4f);

        CreateBathroomFrame(root.transform, square, backgroundLayerId);
        Transform owner = CreateOwner(root.transform, square, circle, backgroundLayerId, bedCenterX);
        Light2D bathroomLight = CreateBathroomLight(root.transform, backgroundLayerId);
        CreateBall(root.transform, circle, backgroundLayerId, zeroFriction);
        Transform doorPivot = CreateFrontDoor(root.transform, square, backgroundLayerId);
        CreateExteriorDoorwayConnector(root.transform, square, backgroundLayerId);
        CreateStreetBackdropAndFloor(root.transform, square, backgroundLayerId, scene);
        GameObject headlightObject = CreateCarAndHeadlight(root.transform, square, circle,
            backgroundLayerId, zeroFriction);
        PushableObject gate = CreateParkGate(root.transform, square, circle,
            backgroundLayerId, zeroFriction);
        GameObject parkEntry = CreateParkArea(root.transform, square, circle,
            backgroundLayerId, scene);

        AfterTheGlowStoryDirector director = root.AddComponent<AfterTheGlowStoryDirector>();
        director.player = player;
        director.cameraFollow = cameraFollow;
        director.owner = owner;
        director.bathroomLight = bathroomLight;
        director.bed = bed;
        director.rollingBall = FindDirectChild(root.transform, "Rolling Ball");
        director.parkGate = gate;
        director.carHeadlight = headlightObject;
        director.frontDoorPivot = doorPivot;
        director.petSeconds = 1f;
        director.headlightOffDelay = 2.5f;
        director.bathroomX = BathroomX;
        director.frontDoorX = FrontDoorX;
        director.ownerDoorExitOffset = OwnerDoorExitOffset;
        director.streetEntryPlayerX = StreetEntryX;
        director.ownerPreparationTargetX = OwnerPreparationProgressX;
        director.ownerStreetStartX = StreetEntryX + 0.8f;
        director.ownerStreetEndX = 22.5f;
        director.parkEntryX = parkEntry.transform.position.x;
        director.actorMoveSpeed = 1.8f;

        CreateStoryTrigger(root.transform, "House To Street Trigger",
            new Vector2(StreetEntryX, FloorTop + 1.1f), new Vector2(0.5f, 2.2f),
            director, AfterTheGlowTriggerKind.HouseExit, backgroundLayerId);
        CreateStoryTrigger(root.transform, "Owner Preparation Progress Trigger",
            new Vector2(OwnerPreparationProgressX, FloorTop + 1.1f), new Vector2(0.55f, 2.2f),
            director, AfterTheGlowTriggerKind.OwnerPreparationTarget, backgroundLayerId);
        CreateStoryTrigger(root.transform, "Park Entry Trigger",
            new Vector2(ParkEntryX, FloorTop + 1.1f), new Vector2(0.55f, 2.2f),
            director, AfterTheGlowTriggerKind.ParkEntry, backgroundLayerId);
        CreateDebugOverlay(root.transform, player);
        ApplyPixelSnapperPropArt(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Unity could not save SampleScene.");

        Debug.Log("[After the Glow] House and street story slice built from Assets/Art/PixelSnapper. Controls: A/D, Space, S, left click. R restarts the scene; F1 toggles debug.");
    }

    [MenuItem("Tools/After the Glow/Refresh User Dog Art")]
    private static void RefreshPptDogArt()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before refreshing the dog art.");

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureStorybookDogSprites();
        PlayerMovement player = FindRoot<PlayerMovement>(scene);
        if (player == null)
            throw new MissingComponentException("SampleScene needs its existing PlayerMovement component.");

        Transform visual = player.transform.Find("PixelSnapper Dog Visual");
        if (visual == null)
            throw new MissingComponentException("SampleScene is missing PixelSnapper Dog Visual.");

        SpriteRenderer dogRenderer = visual.GetComponent<SpriteRenderer>();
        if (dogRenderer == null)
            dogRenderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
        dogRenderer.sprite = LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_01");
        visual.localPosition = new Vector3(0f, DogVisualFootOffset, -0.1f);
        visual.localScale = Vector3.one;
        player.crouchVisualHeightMultiplier = 1f;

        Animator animator = player.GetComponent<Animator>();
        if (animator == null)
            animator = Undo.AddComponent<Animator>(player.gameObject);
        animator.runtimeAnimatorController = CreatePixelDogController();
        animator.applyRootMotion = false;
        animator.enabled = true;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Unity could not save SampleScene after refreshing the PPT-style dog.");

        Debug.Log("[After the Glow] Refreshed the player using the user's five-row, 22-frame dog sheet.");
    }

    private static void RepairPlayerAndFloor(Scene scene, PlayerMovement player, CameraFollow cameraFollow)
    {
        player.moveSpeed = 4.4f;
        player.jumpForce = 6.8f;
        player.groundCheckRadius = 0.18f;
        player.retreatDistance = 1.8f;
        player.retreatSpeed = 4.2f;
        player.retreatCooldown = 1.2f;
        player.groundLayer = LayerMask.GetMask("Ground");

        Transform playerTransform = player.transform;
        Vector3 scale = playerTransform.localScale;
        scale.x = Mathf.Abs(scale.x);
        playerTransform.localScale = scale;
        BoxCollider2D playerCollider = player.GetComponent<BoxCollider2D>();
        if (playerCollider != null)
        {
            playerCollider.size = new Vector2(0.8f, 0.82f);
            playerCollider.offset = new Vector2(0f, 0.41f);
            float verticalScale = Mathf.Abs(playerTransform.localScale.y);
            float feetOffset = playerCollider.offset.y * verticalScale -
                playerCollider.size.y * verticalScale * 0.5f;
            playerTransform.position = new Vector3(TutorialStartX, FloorTop - feetOffset,
                playerTransform.position.z);
        }
        if (player.groundCheck != null)
        {
            Vector3 groundCheckPosition = player.groundCheck.localPosition;
            groundCheckPosition.y = -0.1f;
            player.groundCheck.localPosition = groundCheckPosition;
        }
        if (cameraFollow != null)
        {
            if (cameraFollow.target == null)
                cameraFollow.target = player.transform;
            cameraFollow.homeOrthographicSize = HomeOrthographicSize;
            cameraFollow.offset = new Vector3(0f, cameraFollow.offset.y, cameraFollow.offset.z);
            cameraFollow.exteriorOffset = new Vector3(0f, cameraFollow.exteriorOffset.y, cameraFollow.exteriorOffset.z);
            cameraFollow.smoothTime = cameraFollow.exteriorSmoothTime = .18f;
            cameraFollow.homeFollowResponseMultiplier = 1f;
            cameraFollow.constrainHorizontalPosition = true;
            cameraFollow.mapMinX = RoomBackgroundLeftX;
            cameraFollow.mapMaxX = StreetGroundRightX;
            cameraFollow.homeMapMaxX = FrontDoorX + 0.9f;
            cameraFollow.exteriorMapMinX = RoomBackgroundLeftX + RoomBackgroundWidth + 0.1f;
            cameraFollow.lockY = true;
            cameraFollow.lockedY = RoomBackgroundCenterY;
            Camera attachedCamera = cameraFollow.GetComponent<Camera>();
            if (attachedCamera != null && attachedCamera.orthographic)
                attachedCamera.orthographicSize = HomeOrthographicSize;
            Vector3 cameraPosition = cameraFollow.transform.position;
            cameraPosition.x = TutorialStartX + TutorialCameraLeadX;
            cameraPosition.y = RoomBackgroundCenterY;
            cameraFollow.transform.position = cameraPosition;
        }

        Transform bedTarget = FindRootObject(scene, StoryRootName)?.transform.Find("Bed Push Target");
        player.jumpBlocker = bedTarget != null ? bedTarget.GetComponent<Collider2D>() : null;
        player.jumpBlockerPadding = 0.12f;

        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null)
            interaction = Undo.AddComponent<PlayerInteraction>(player.gameObject);
        SetPrivateFloat(interaction, "interactionRange", 2.4f);
        SetPrivateFloat(interaction, "interactionHeight", 1.5f);
        SetPrivateFloat(interaction, "activeTargetReleaseRange", 4.5f);

        GameObject groundObject = FindRootObject(scene, "Ground");
        if (groundObject == null)
            throw new MissingReferenceException("SampleScene Ground object was not found.");
        BoxCollider2D groundCollider = groundObject.GetComponent<BoxCollider2D>();
        if (groundCollider == null)
            throw new MissingComponentException("SampleScene Ground needs a BoxCollider2D.");
        Vector2 offset = groundCollider.offset;
        offset = Vector2.zero;
        groundCollider.offset = offset;
        groundCollider.size = Vector2.one;
        float floorLeftX = GetBackgroundLeftX(scene);
        float floorWidth = StreetGroundLeftX - floorLeftX;
        if (floorWidth <= 0f)
            throw new System.InvalidOperationException("The house background edge must be left of the doorway floor connection.");
        groundObject.transform.position = new Vector3(floorLeftX + floorWidth * 0.5f, FloorCenterY,
            groundObject.transform.position.z);
        groundObject.transform.localScale = new Vector3(floorWidth, 1f, 1f);

        SpriteRenderer groundRenderer = groundObject.GetComponent<SpriteRenderer>();
        if (groundRenderer != null)
        {
            // The interior/street artwork already contains the walkable floor. Keep only the physics collider.
            groundRenderer.enabled = false;
            groundRenderer.sharedMaterial = LoadSpriteLitMaterial();
            GameObject backgroundObject = FindRootObject(scene, "BackGround");
            SpriteRenderer backgroundRenderer = backgroundObject != null
                ? backgroundObject.GetComponent<SpriteRenderer>()
                : null;
            if (backgroundRenderer != null)
            {
                groundRenderer.sortingLayerID = backgroundRenderer.sortingLayerID;
                groundRenderer.sortingOrder = backgroundRenderer.sortingOrder + 1;
            }
        }

        if (player.GetComponent<LightExposureSensor>() == null)
            Undo.AddComponent<LightExposureSensor>(player.gameObject);
    }

    private static float GetBackgroundLeftX(Scene scene)
    {
        GameObject backgroundObject = FindRootObject(scene, "BackGround");
        SpriteRenderer backgroundRenderer = backgroundObject != null
            ? backgroundObject.GetComponent<SpriteRenderer>()
            : null;
        return backgroundRenderer != null ? backgroundRenderer.bounds.min.x : -13.83f;
    }

    private static float GetBedCenterX(Scene scene)
    {
        GameObject backgroundObject = FindRootObject(scene, "BackGround");
        SpriteRenderer backgroundRenderer = backgroundObject != null
            ? backgroundObject.GetComponent<SpriteRenderer>()
            : null;
        if (backgroundRenderer == null)
            return -7.575f;

        Bounds backgroundBounds = backgroundRenderer.bounds;
        return backgroundBounds.min.x + RoomExtensionLeftWidth +
               BedCenterFromBaseRoomLeft;
    }

    private static void CreateOrRefreshHouseLeftBoundary(Transform parent, float floorLeftX)
    {
        const float wallThickness = 0.4f;
        const float wallHeight = 8f;
        Transform boundary = parent.Find("House Left Boundary");
        if (boundary == null)
        {
            GameObject boundaryObject = new GameObject("House Left Boundary");
            boundaryObject.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(boundaryObject, "Add house left boundary");
            boundary = boundaryObject.transform;
            boundary.gameObject.AddComponent<BoxCollider2D>();
        }

        boundary.position = new Vector3(floorLeftX - wallThickness * 0.5f,
            FloorTop + wallHeight * 0.5f, 0f);
        boundary.localScale = Vector3.one;
        BoxCollider2D collider = boundary.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(wallThickness, wallHeight);
        collider.offset = Vector2.zero;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            boundary.gameObject.layer = groundLayer;
    }

    private static void ApplyRestoredRoomArt(Scene scene, Transform storyRoot, PlayerMovement player)
    {
        GameObject backgroundObject = FindRootObject(scene, "BackGround");
        SpriteRenderer backgroundRenderer = backgroundObject != null
            ? backgroundObject.GetComponent<SpriteRenderer>()
            : null;
        Sprite roomSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Map01BackgroundPath);
        if (backgroundObject == null || backgroundRenderer == null || roomSprite == null)
            throw new FileNotFoundException("Map 01 illustrated home art or the BackGround object is missing.");

        if (backgroundRenderer != null)
        {
            backgroundRenderer.enabled = true;
            backgroundRenderer.sprite = roomSprite;
            backgroundRenderer.sharedMaterial = LoadSpriteUnlitMaterial();
            backgroundRenderer.color = Color.white;
            backgroundRenderer.sortingOrder = -100;
            backgroundRenderer.transform.position = new Vector3(
                RoomBackgroundCenterX, RoomBackgroundCenterY, 10f);
            backgroundRenderer.transform.localScale = Vector3.one;
            ScaleSpriteTo(backgroundRenderer, new Vector2(RoomBackgroundWidth, RoomBackgroundHeight));

            Transform oldDoorArt = backgroundObject.transform.Find("FrontDoorStateHolder");
            if (oldDoorArt != null)
                oldDoorArt.gameObject.SetActive(false);
        }

        Transform floorArt = backgroundObject.transform.Find("House Interior Floor Art");
        if (floorArt != null)
            Undo.DestroyObjectImmediate(floorArt.gameObject);

        Transform oldDetails = storyRoot.Find("Room Graybox Details");
        if (oldDetails != null)
            Undo.DestroyObjectImmediate(oldDetails.gameObject);

        ApplyPixelSnapperDogArt(player);
    }

    private static void EnsureStorybookDogSprites()
    {
        EnsureUserIdleSprites();
        TextureImporter importer = AssetImporter.GetAtPath(StorybookDogPath) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("The new storybook dog sprite sheet is missing.");

        bool importerChanged = importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Multiple ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 160f) ||
            importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
            importer.npotScale != TextureImporterNPOTScale.None ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            !importer.alphaIsTransparency;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 160f;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        if (importerChanged)
            importer.SaveAndReimport();

        importer = AssetImporter.GetAtPath(StorybookDogPath) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException("Unity could not reload the storybook dog importer.");

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null)
            throw new InvalidOperationException("The dog texture importer has no Sprite Editor data provider.");
        provider.InitSpriteEditorDataProvider();
        if (!provider.HasDataProvider(typeof(ISpriteFrameEditCapability)) ||
            !provider.HasDataProvider(typeof(ISpriteNameFileIdDataProvider)))
            throw new InvalidOperationException("Unity's Sprite Editor provider cannot safely create the dog frames.");

        ISpriteFrameEditCapability frameCapability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (frameCapability == null)
            throw new InvalidOperationException("Sprite frame edit capability is unavailable for the dog sheet.");
        EditCapability capability = frameCapability.GetEditCapability();
        if (!capability.HasCapability(EEditCapability.CreateAndDeleteSprite) ||
            !capability.HasCapability(EEditCapability.EditSpriteRect) ||
            !capability.HasCapability(EEditCapability.EditSpriteName) ||
            !capability.HasCapability(EEditCapability.EditPivot))
            throw new InvalidOperationException("Unity does not allow creating, naming, resizing, and anchoring the dog sprites.");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StorybookDogPath);
        if (texture == null || texture.width != 768 || texture.height != 640)
            throw new InvalidOperationException("The user dog sheet must contain six 128px columns and five rows (768 x 640).");

        SpriteRect[] existing = provider.GetSpriteRects();
        SpriteRect[] rects = new SpriteRect[22];
        SpriteNameFileIdPair[] namePairs = new SpriteNameFileIdPair[22];
        for (int i = 0; i < rects.Length; i++)
        {
            // Row 1 has six walking poses; the next four rows each have four poses.
            // Original row 5 was deleted; the final row is the original row 6.
            int column = i < 6 ? i : (i - 6) % 4;
            int rowFromTop = i < 6 ? 0 : 1 + (i - 6) / 4;
            int xMin = column * 128;
            int yMin = texture.height - (rowFromTop + 1) * 128;
            string spriteName = "Dog_Storybook_" + (i + 1).ToString("00");
            SpriteRect spriteRect = new SpriteRect
            {
                name = spriteName,
                rect = new Rect(xMin, yMin, 128, 128),
                alignment = SpriteAlignment.Custom,
                // The sheet has transparent pixels under the paws; anchor the visible feet to the floor.
                pivot = new Vector2(0.5f, 12f / 128f),
                border = Vector4.zero
            };

            bool foundExisting = false;
            for (int j = 0; j < existing.Length; j++)
            {
                if (existing[j].name != spriteName)
                    continue;
                spriteRect.spriteID = existing[j].spriteID;
                foundExisting = true;
                break;
            }
            if (!foundExisting)
                spriteRect.spriteID = GUID.Generate();

            rects[i] = spriteRect;
            namePairs[i] = new SpriteNameFileIdPair(spriteName, spriteRect.spriteID);
        }

        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(namePairs);
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static Sprite LoadStorybookDogSprite(int frameNumber)
    {
        string spriteName = "Dog_Storybook_" + frameNumber.ToString("00");
        Sprite sprite = LoadSpriteSubAsset(StorybookDogPath, spriteName);
        if (sprite == null)
            throw new FileNotFoundException("The storybook dog frame was not imported: " + spriteName);
        return sprite;
    }

    private static void EnsureUserIdleSprites()
    {
        var importer = AssetImporter.GetAtPath(UserDogIdlePath) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("The user's six-frame idle strip is missing.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 160f;
        importer.filterMode = FilterMode.Point;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        importer = AssetImporter.GetAtPath(UserDogIdlePath) as TextureImporter;
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Idle strip has no Sprite Editor provider.");
        provider.InitSpriteEditorDataProvider();
        if (!provider.HasDataProvider(typeof(ISpriteFrameEditCapability)) ||
            !provider.HasDataProvider(typeof(ISpriteNameFileIdDataProvider)))
            throw new InvalidOperationException("Idle strip does not support safe sprite editing.");
        var editing = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (editing == null) throw new InvalidOperationException("Idle strip edit capabilities unavailable.");
        var capability = editing.GetEditCapability();
        if (!capability.HasCapability(EEditCapability.CreateAndDeleteSprite) ||
            !capability.HasCapability(EEditCapability.EditSpriteName) ||
            !capability.HasCapability(EEditCapability.EditSpriteRect) ||
            !capability.HasCapability(EEditCapability.EditPivot))
            throw new InvalidOperationException("Idle strip cannot safely create, name, slice and anchor sprites.");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(UserDogIdlePath);
        if (texture == null || texture.width != 768 || texture.height != 128)
            throw new InvalidOperationException("Expected the user's 768 x 128 six-frame idle strip.");
        var existing = provider.GetSpriteRects();
        var rects = new SpriteRect[6];
        var pairs = new SpriteNameFileIdPair[6];
        for (int i = 0; i < 6; i++)
        {
            string name = "Dog_User_Idle_" + (i + 1).ToString("00");
            GUID id = GUID.Generate();
            foreach (var old in existing) if (old.name == name) { id = old.spriteID; break; }
            rects[i] = new SpriteRect { name = name, spriteID = id, rect = new Rect(i * 128, 0, 128, 128),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, 12f / 128f), border = Vector4.zero };
            pairs[i] = new SpriteNameFileIdPair(name, id);
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static Sprite EnsureCarFrontSprite(bool lightsOn)
    {
        string texturePath = lightsOn ? CarFrontOnTexturePath : CarFrontOffTexturePath;
        string spritePath = lightsOn ? CarFrontOnSpritePath : CarFrontOffSpritePath;
        string spriteName = lightsOn ? "CarFront_LightsOn" : "CarFront_LightsOff";
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("The matching front-facing car sprite sheet is missing: " + texturePath);

        bool importerChanged = importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
            importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            !importer.alphaIsTransparency || !importer.isReadable;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        if (importerChanged)
            importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null || texture.width < 1250 || texture.height < 794)
            throw new InvalidOperationException("The generated front-facing car texture has unexpected dimensions.");
        Sprite sprite = EnsureLayerSprite(spritePath, texture,
            new Rect(280f, 184f, 970f, 610f), new Vector2(0.5f, 0f), 100f);
        sprite.name = spriteName;
        EditorUtility.SetDirty(sprite);
        return sprite;
    }

    private static void ApplyPixelSnapperDogArt(PlayerMovement player)
    {
        if (player == null)
            return;

        Sprite idleSprite = LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_01");

        SpriteRenderer rootRenderer = player.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;

        Transform oldVisual = player.transform.Find("Dog Graybox Visual");
        if (oldVisual != null)
            Undo.DestroyObjectImmediate(oldVisual.gameObject);
        Transform visual = player.transform.Find("PixelSnapper Dog Visual");
        if (visual == null)
        {
            GameObject visualObject = new GameObject("PixelSnapper Dog Visual");
            Undo.RegisterCreatedObjectUndo(visualObject, "Apply storybook dog art");
            visualObject.transform.SetParent(player.transform, false);
            visual = visualObject.transform;
        }
        visual.localPosition = new Vector3(0f, DogVisualFootOffset, -0.1f);
        player.crouchVisualHeightMultiplier = 1f;
        SpriteRenderer dogRenderer = visual.GetComponent<SpriteRenderer>();
        if (dogRenderer == null)
            dogRenderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
        dogRenderer.enabled = true;
        dogRenderer.sprite = idleSprite;
        dogRenderer.sharedMaterial = LoadSpriteUnlitMaterial();
        dogRenderer.color = Color.white;
        dogRenderer.sortingOrder = 10;
        dogRenderer.drawMode = SpriteDrawMode.Simple;
        visual.localScale = Vector3.one;

        Animator animator = player.GetComponent<Animator>();
        if (animator == null)
            animator = Undo.AddComponent<Animator>(player.gameObject);
        animator.runtimeAnimatorController = CreatePixelDogController();
        animator.applyRootMotion = false;
        animator.enabled = true;
    }

    private static Sprite EnsureMovableBedBackground()
    {
        string generatedAbsolutePath = Path.GetFullPath(InteriorBackgroundNoBedPath);
        if (!File.Exists(generatedAbsolutePath))
        {
            byte[] sourceBytes = File.ReadAllBytes(Path.GetFullPath(InteriorBackgroundPath));
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(texture, sourceBytes))
            {
                Object.DestroyImmediate(texture);
                throw new IOException("Could not read PixelSnapper's interior background texture.");
            }

            // Remove only the bed painted into the source background so the matching Bed.png can move with physics.
            Color fill = texture.GetPixel(82, texture.height - 110);
            int patchMinX = 4;
            int patchMaxX = 77;
            int patchMinY = texture.height - 130;
            int patchMaxY = texture.height - 104;
            for (int y = patchMinY; y <= patchMaxY; y++)
            for (int x = patchMinX; x <= patchMaxX; x++)
                texture.SetPixel(x, y, fill);
            texture.Apply(false, false);
            File.WriteAllBytes(generatedAbsolutePath, ImageConversion.EncodeToPNG(texture));
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(InteriorBackgroundNoBedPath, ImportAssetOptions.ForceSynchronousImport);
        }

        TextureImporter importer = AssetImporter.GetAtPath(InteriorBackgroundNoBedPath) as TextureImporter;
        if (importer != null)
        {
            bool needsImport = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = false;
            if (needsImport)
                importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(InteriorBackgroundNoBedPath);
        if (sprite != null)
            return sprite;
        return LoadFirstSprite(InteriorBackgroundNoBedPath);
    }

    private static AnimatorController CreatePixelDogController()
    {
        string animationFolder = Path.GetDirectoryName(Path.GetFullPath(PixelDogControllerPath));
        if (!Directory.Exists(animationFolder))
            Directory.CreateDirectory(animationFolder);

        Sprite idle = LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_01");
        Sprite walkA = LoadStorybookDogSprite(2);
        Sprite walkB = LoadStorybookDogSprite(3);
        Sprite walkC = LoadStorybookDogSprite(4);
        Sprite push = LoadStorybookDogSprite(19);
        Sprite jump = LoadStorybookDogSprite(7);
        if (idle == null || walkA == null || walkB == null || walkC == null ||
            push == null || jump == null)
            throw new FileNotFoundException("One or more storybook dog animation frames are missing.");

        AnimationClip idleClip = CreateSpriteClip(PixelDogIdleClipPath, "PixelSnapperBorderCollie_Idle",
            "PixelSnapper Dog Visual", new[] { idle, LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_02"),
                LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_03"), LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_04"),
                LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_05"), LoadSpriteSubAsset(UserDogIdlePath, "Dog_User_Idle_06") }, .18f, true);
        AnimationClip walkClip = CreateSpriteClip(PixelDogWalkClipPath, "PixelSnapperBorderCollie_Walk",
            "PixelSnapper Dog Visual", new[] { LoadStorybookDogSprite(1), walkA, walkB, walkC, LoadStorybookDogSprite(5), LoadStorybookDogSprite(6) }, 0.10f, true);
        AnimationClip pushClip = CreateSpriteClip(PixelDogPushClipPath, "PixelSnapperBorderCollie_Push",
            "PixelSnapper Dog Visual", new[] { push, LoadStorybookDogSprite(20), LoadStorybookDogSprite(21), LoadStorybookDogSprite(22) }, 0.14f, true);
        AnimationClip jumpClip = CreateSpriteClip(PixelDogJumpClipPath, "PixelSnapperBorderCollie_Jump",
            "PixelSnapper Dog Visual", new[] { jump, LoadStorybookDogSprite(8), LoadStorybookDogSprite(9), LoadStorybookDogSprite(10) }, 0.12f, false);
        AnimationClip crouchClip = CreateSpriteClip(PixelDogCrouchClipPath, "PixelSnapperBorderCollie_Crouch",
            "PixelSnapper Dog Visual", new[] { LoadStorybookDogSprite(11), LoadStorybookDogSprite(12), LoadStorybookDogSprite(13), LoadStorybookDogSprite(14) }, 0.10f, false);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PixelDogControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(PixelDogControllerPath);
        if (controller.layers.Length == 0)
            controller.AddLayer("Base Layer");

        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsPushing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsBiting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("LeashTugSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorStateTransition[] oldAnyStateTransitions = machine.anyStateTransitions;
        for (int i = 0; i < oldAnyStateTransitions.Length; i++)
            machine.RemoveAnyStateTransition(oldAnyStateTransitions[i]);
        ChildAnimatorState[] oldStates = machine.states;
        for (int i = 0; i < oldStates.Length; i++)
            machine.RemoveState(oldStates[i].state);

        AnimatorState idleState = machine.AddState("Idle");
        AnimatorState walkState = machine.AddState("Walk");
        AnimatorState pushState = machine.AddState("Push");
        AnimatorState jumpState = machine.AddState("Jump");
        AnimatorState crouchState = machine.AddState("Crouch");
        AnimatorState biteState = machine.AddState("LeashBite");
        biteState.motion = pushClip;
        biteState.speedParameter = "LeashTugSpeed";
        biteState.speedParameterActive = true;
        idleState.motion = idleClip;
        walkState.motion = walkClip;
        pushState.motion = pushClip;
        jumpState.motion = jumpClip;
        crouchState.motion = crouchClip;
        machine.defaultState = idleState;

        AddTransition(idleState, walkState, AnimatorConditionMode.Greater, 0.05f, "Speed");
        AddTransition(walkState, idleState, AnimatorConditionMode.Less, 0.05f, "Speed");
        AddTransition(machine, pushState, AnimatorConditionMode.If, 0f, "IsPushing");
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            if (transition.destinationState == pushState)
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
                transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBiting");
            }
        AddTransition(machine, biteState, AnimatorConditionMode.If, 0f, "IsBiting");
        AddTransition(biteState, idleState, AnimatorConditionMode.IfNot, 0f, "IsBiting",
            AnimatorConditionMode.Less, .05f, "Speed");
        AddTransition(biteState, walkState, AnimatorConditionMode.IfNot, 0f, "IsBiting",
            AnimatorConditionMode.Greater, .05f, "Speed");
        AddTransition(machine, jumpState, AnimatorConditionMode.If, 0f, "Jump");
        AddTransition(pushState, walkState, AnimatorConditionMode.IfNot, 0f, "IsPushing",
            AnimatorConditionMode.Greater, 0.05f, "Speed");
        AddTransition(pushState, idleState, AnimatorConditionMode.IfNot, 0f, "IsPushing",
            AnimatorConditionMode.Less, 0.05f, "Speed");
        AddTransition(jumpState, walkState, AnimatorConditionMode.If, 0f, "IsGrounded",
            AnimatorConditionMode.Greater, 0.05f, "Speed");
        AddTransition(jumpState, idleState, AnimatorConditionMode.If, 0f, "IsGrounded",
            AnimatorConditionMode.Less, 0.05f, "Speed");
        foreach (AnimatorState state in new[] { idleState, walkState, pushState })
            AddTransition(state, crouchState, AnimatorConditionMode.If, 0f, "IsCrouching");
        AddTransition(crouchState, walkState, AnimatorConditionMode.IfNot, 0f, "IsCrouching",
            AnimatorConditionMode.Greater, 0.05f, "Speed");
        AddTransition(crouchState, idleState, AnimatorConditionMode.IfNot, 0f, "IsCrouching",
            AnimatorConditionMode.Less, 0.05f, "Speed");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip CreateSpriteClip(string assetPath, string clipName, string bindingPath,
        Sprite[] sprites, float secondsPerFrame, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, assetPath);
        }

        clip.name = clipName;
        clip.frameRate = 1f / Mathf.Max(0.01f, secondsPerFrame);
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(bindingPath, typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            frames[i] = new ObjectReferenceKeyframe { time = i * secondsPerFrame, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to,
        AnimatorConditionMode mode, float threshold, string parameter,
        AnimatorConditionMode secondMode = AnimatorConditionMode.If, float secondThreshold = 0f,
        string secondParameter = null)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.06f;
        transition.AddCondition(mode, threshold, parameter);
        if (!string.IsNullOrEmpty(secondParameter))
            transition.AddCondition(secondMode, secondThreshold, secondParameter);
    }

    private static void AddTransition(AnimatorStateMachine machine, AnimatorState to,
        AnimatorConditionMode mode, float threshold, string parameter)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(to);
        transition.canTransitionToSelf = false;
        transition.hasExitTime = false;
        transition.duration = 0.04f;
        transition.AddCondition(mode, threshold, parameter);
    }

    private static Sprite LoadAnySprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return sprite != null ? sprite : LoadFirstSprite(path);
    }

    private static Sprite LoadFirstSprite(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
                return sprite;
        }
        return null;
    }

    private static void ApplyPixelSnapperPropArt(Transform storyRoot)
    {
        OwnerPresentationSetup.EnsureArtworkAssets();
        Sprite bedSprite = LoadAnySprite(BedArtPath);
        Sprite ballSprite = LoadAnySprite(BallArtPath);
        Sprite frontDoorClosed = LoadAnySprite(FrontDoorClosedArtPath);
        Sprite frontDoorOpen = LoadAnySprite(FrontDoorOpenArtPath);
        Sprite ownerWalk = LoadAnySprite(OwnerWalkPath);
        Sprite ownerPet = LoadAnySprite(OwnerPetPath);
        Sprite carHeadlights = EnsureCarFrontSprite(true);
        Sprite carNoLights = EnsureCarFrontSprite(false);
        Sprite gateBound = LoadAnySprite(GateBoundArtPath);
        Sprite gateLoose = LoadAnySprite(GateLooseArtPath);
        Sprite gateBroken = LoadAnySprite(GateBrokenArtPath);
        if (bedSprite == null || ballSprite == null || frontDoorClosed == null ||
            frontDoorOpen == null || ownerWalk == null || ownerPet == null ||
            carHeadlights == null || carNoLights == null || gateBound == null || gateLoose == null ||
            gateBroken == null)
            throw new FileNotFoundException("One or more PixelSnapper story prop sprites are missing.");

        SpriteRenderer backgroundRenderer = FindRootObject(storyRoot.gameObject.scene, "BackGround")
            ?.GetComponent<SpriteRenderer>();
        int sortingLayerId = backgroundRenderer != null ? backgroundRenderer.sortingLayerID : 0;

        Transform bedRoot = storyRoot.Find("Bed Push Target");
        if (bedRoot != null)
        {
            bedRoot.position = new Vector3(GetBedCenterX(storyRoot.gameObject.scene),
                FloorTop + 0.45f, bedRoot.position.z);
            bedRoot.localScale = Vector3.one;
            BoxCollider2D bedCollider = bedRoot.GetComponent<BoxCollider2D>();
            if (bedCollider != null)
            {
                bedCollider.size = new Vector2(2.8f, 0.9f);
                bedCollider.isTrigger = true;
            }
            PushableObject bedInteraction = bedRoot.GetComponent<PushableObject>();
            if (bedInteraction != null)
                bedInteraction.ConfigureInteractionOnlyTrigger(true);
            SpriteRenderer oldBedRenderer = bedRoot.GetComponent<SpriteRenderer>();
            if (oldBedRenderer != null)
                oldBedRenderer.enabled = false;
            SpriteRenderer bedArt = EnsureChildSprite(bedRoot, "PixelSnapper Bed Art", bedSprite,
                new Vector3(0f, -0.08f, -0.1f), new Vector2(2.8f, 1.0f), sortingLayerId, -2);
            bedArt.color = Color.white;
            ConfigureBedBlanket(bedRoot, bedArt, bedSprite, sortingLayerId);
        }

        Transform ownerRoot = storyRoot.Find("Owner Actor (graybox)");
        if (ownerRoot != null)
        {
            ownerRoot.position = new Vector3(GetBedCenterX(storyRoot.gameObject.scene),
                FloorTop, ownerRoot.position.z);
            ownerRoot.localScale = Vector3.one;
            string[] oldParts = { "Body", "Left Leg", "Right Leg", "Head", "Arm" };
            for (int i = 0; i < oldParts.Length; i++)
            {
                Transform oldPart = ownerRoot.Find(oldParts[i]);
                if (oldPart != null)
                    Undo.DestroyObjectImmediate(oldPart.gameObject);
            }
            SpriteRenderer ownerArt = EnsureChildSprite(ownerRoot, "PixelSnapper Owner Visual", ownerWalk,
                new Vector3(0f, -0.24f, -0.2f), new Vector2(1.25f, 1.28f), sortingLayerId, 30);
            ownerArt.color = Color.white;
        }

        DisableRenderer(storyRoot.Find("Bathroom Door Left"));
        DisableRenderer(storyRoot.Find("Bathroom Door Right"));
        DisableRenderer(storyRoot.Find("Bathroom Door Header"));
        DisableRenderer(storyRoot.Find("Bathroom Interior"));
        DisableRenderer(storyRoot.Find("Bathroom Light Marker"));
        Transform duplicateBathroomDoor = storyRoot.Find("Bathroom Door Art");
        if (duplicateBathroomDoor != null)
            Undo.DestroyObjectImmediate(duplicateBathroomDoor.gameObject);

        Transform ballRoot = storyRoot.Find("Rolling Ball");
        if (ballRoot != null)
        {
            ballRoot.localScale = Vector3.one;
            ballRoot.position = new Vector3(BallX, FloorTop + BallDiameter * 0.5f, ballRoot.position.z);
            CircleCollider2D circleCollider = ballRoot.GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.radius = BallDiameter * 0.5f;
                circleCollider.offset = Vector2.zero;
            }
            SpriteRenderer oldBallRenderer = ballRoot.GetComponent<SpriteRenderer>();
            if (oldBallRenderer != null)
                oldBallRenderer.enabled = false;
            SpriteRenderer ballArt = EnsureChildSprite(ballRoot, "PixelSnapper Ball Art", ballSprite,
                new Vector3(0f, -BallDiameter * 0.5f, -0.1f), new Vector2(0.48f, 0.48f), sortingLayerId, 25,
                new Vector2(2.4f, 2.4f));
            ballArt.color = Color.white;
        }

        Transform doorPivot = storyRoot.Find("Front Door Hinge");
        if (doorPivot != null)
        {
            doorPivot.gameObject.SetActive(true);
            doorPivot.position = new Vector3(FrontDoorX, FloorTop, -0.1f);
        // Crop the illustrated door panel so the same art can rotate open and closed.
            Sprite matchingPanel = ConfigureFrontDoorArtwork(doorPivot, backgroundRenderer, sortingLayerId);
            if (matchingPanel != null)
                frontDoorClosed = frontDoorOpen = matchingPanel;
        }

        Transform connector = storyRoot.Find("Exterior Doorway Connector (graybox)");
        if (connector != null)
        {
            SpriteRenderer[] connectorRenderers = connector.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < connectorRenderers.Length; i++)
                connectorRenderers[i].enabled = false;
        }

        ApplyPixelStreetBackgrounds(storyRoot, sortingLayerId);
        SpriteRenderer groundExtension = storyRoot.Find("Street Ground Extension")?.GetComponent<SpriteRenderer>();
        if (groundExtension != null)
            groundExtension.enabled = false;
        for (int i = 0; i < 7; i++)
            DisableRenderer(storyRoot.Find("Street Lane Mark " + i));

        Transform carRoot = storyRoot.Find("PixelSnapper Car No Lights");
        if (carRoot == null)
        {
            GameObject carObject = new GameObject("PixelSnapper Car No Lights");
            carObject.transform.SetParent(storyRoot, false);
            carRoot = carObject.transform;
        }
        carRoot.position = new Vector3(CarX, FloorTop + 1.15f, -0.2f);
        SpriteRenderer carRenderer = carRoot.GetComponent<SpriteRenderer>();
        if (carRenderer == null)
            carRenderer = Undo.AddComponent<SpriteRenderer>(carRoot.gameObject);
        carRenderer.sprite = carNoLights;
        carRenderer.sharedMaterial = LoadSpriteUnlitMaterial();
        carRenderer.color = Color.white;
        carRenderer.flipX = true;
        carRenderer.sortingLayerID = sortingLayerId;
        carRenderer.sortingOrder = 9;
        ScaleSpriteTo(carRenderer, new Vector2(4.8f, 3.15f));

        // Keep the collision and gameplay-light objects, but remove the old graybox car art.
        DisableRenderer(storyRoot.Find("Parked Car Body"));
        DisableRenderer(storyRoot.Find("Parked Car Cabin"));
        DisableRenderer(storyRoot.Find("Car Wheel Rear"));
        DisableRenderer(storyRoot.Find("Car Wheel Front"));

        Transform lightRoot = storyRoot.Find("Car High Beams - Gameplay Light");
        if (lightRoot != null)
        {
            lightRoot.position = new Vector3(CarX, FloorTop + 1.35f, -0.1f);
            lightRoot.localRotation = Quaternion.identity;
            lightRoot.localScale = Vector3.one;
            SpriteRenderer headlightArt = EnsureChildSprite(lightRoot, "PixelSnapper Headlights Art",
                carHeadlights, new Vector3(2.05f, 0.22f, -0.15f),
                new Vector2(4.8f, 3.15f), sortingLayerId, 10);
            headlightArt.flipX = true;
            headlightArt.color = Color.white;
            headlightArt.enabled = false;
            ConfigureSingleCarPresentation(storyRoot);
            EnsureVisibleHeadlightBeam(storyRoot.gameObject);
        }

        Transform gateRoot = storyRoot.Find("Park Iron Gate - Leash Bound");
        if (gateRoot != null)
        {
            gateRoot.position = new Vector3(GateX, FloorTop + 1.18f, -0.1f);
            gateRoot.localScale = Vector3.one;
            SpriteRenderer oldGateRenderer = gateRoot.GetComponent<SpriteRenderer>();
            if (oldGateRenderer != null)
                oldGateRenderer.enabled = false;
            BoxCollider2D gateCollider = gateRoot.GetComponent<BoxCollider2D>();
            if (gateCollider != null)
            {
                gateCollider.size = new Vector2(4.8f, 2.36f);
                gateCollider.offset = Vector2.zero;
            }
            SpriteRenderer gateArt = EnsureChildSprite(gateRoot, "Gate Artwork", gateBound,
                new Vector3(0f, 0.66f, -0.15f), new Vector2(4.8f, 3.68f), sortingLayerId, 14);
            gateArt.color = Color.white;
            DisableRenderer(gateRoot.Find("Visible Leash Knot"));
            for (int i = 0; i < 5; i++)
                DisableRenderer(gateRoot.Find("Gate Bar " + i));
            PushableObject pushable = gateRoot.GetComponent<PushableObject>();
            if (pushable != null)
            {
                SerializedObject serialized = new SerializedObject(pushable);
                SerializedProperty tetherVisual = serialized.FindProperty("tetherVisual");
                if (tetherVisual != null)
                    tetherVisual.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        DisableRenderer(storyRoot.Find("Fence Left Post"));
        DisableRenderer(storyRoot.Find("Fence Right Post"));
        DisableRenderer(storyRoot.Find("Fence Top Rail"));
        DisableRenderer(storyRoot.Find("Fence Bottom Rail"));
        for (int i = 0; i < 13; i++)
            DisableRenderer(storyRoot.Find("Fence Bar " + i));

        DisableRenderer(storyRoot.Find("Park Entry Placeholder Backdrop"));
        DisableRenderer(storyRoot.Find("Park Placeholder Ground"));
        DisableRenderer(storyRoot.Find("Park Entry Marker"));
        for (int i = 0; i < 5; i++)
        {
            DisableRenderer(storyRoot.Find("Park Tree Trunk " + i));
            DisableRenderer(storyRoot.Find("Park Tree Canopy " + i));
        }

        AfterTheGlowStoryDirector director = storyRoot.GetComponent<AfterTheGlowStoryDirector>();
        if (director != null)
        {
            SerializedObject serialized = new SerializedObject(director);
            serialized.FindProperty("ownerWalkSprite").objectReferenceValue = ownerWalk;
            serialized.FindProperty("ownerPetSprite").objectReferenceValue = ownerPet;
            serialized.FindProperty("frontDoorClosedSprite").objectReferenceValue = frontDoorClosed;
            serialized.FindProperty("frontDoorOpenSprite").objectReferenceValue = frontDoorOpen;
            serialized.FindProperty("gateBoundSprite").objectReferenceValue = gateBound;
            serialized.FindProperty("gateLooseSprite").objectReferenceValue = gateLoose;
            serialized.FindProperty("gateBrokenSprite").objectReferenceValue = gateBroken;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EnsureBackgroundPadding(storyRoot);
        ConfigureHouseFlow(storyRoot);
        OwnerPresentationSetup.ApplyIfAvailable(storyRoot);
        ParkGateSetup.ApplyIfAvailable(storyRoot);
        ReferenceStreetLayout.Apply(storyRoot);
    }

    private static void ConfigureHouseFlow(Transform root)
    {
        AfterTheGlowStoryDirector director = root.GetComponent<AfterTheGlowStoryDirector>();
        if (director == null)
            return;
        director.ownerPreparationTargetX = OwnerPreparationProgressX;
        director.minimumOwnerPreparationSeconds = 4f;
        director.ownerDoorStandOffset = OwnerDoorStandOffset;
        director.ownerDoorExitOffset = OwnerDoorExitOffset;
        director.streetEntryPlayerX = StreetEntryX;
        director.streetEntryPlayerY = FloorTop;
        Transform progress = root.Find("Owner Preparation Progress Trigger");
        if (progress == null)
            progress = root.Find("Ball Play Progress Trigger");
        if (progress != null)
        {
            progress.name = "Owner Preparation Progress Trigger";
            progress.position = new Vector3(OwnerPreparationProgressX, progress.position.y, progress.position.z);
        }
        Transform legacyExit = root.Find("House To Street Trigger");
        if (legacyExit != null)
            legacyExit.gameObject.SetActive(false);
        Transform hinge = root.Find("Front Door Hinge");
        if (hinge == null)
            throw new MissingReferenceException("Clickable exit needs the existing front door.");
        HouseExitInteraction exit = hinge.GetComponent<HouseExitInteraction>();
        if (exit == null)
            exit = Undo.AddComponent<HouseExitInteraction>(hinge.gameObject);
        exit.director = director;
        exit.clickArea = hinge.Find("Front Door Dark Opening")?.GetComponent<SpriteRenderer>();
        exit.interactionRange = 1.6f;
        Transform blocker = root.Find("House Exit Click Boundary");
        if (blocker == null)
        {
            GameObject item = new GameObject("House Exit Click Boundary");
            item.transform.SetParent(root, false);
            blocker = item.transform;
            Undo.AddComponent<BoxCollider2D>(item);
        }
        blocker.position = new Vector3(FrontDoorX + 1f, FloorTop + 3f, 0f);
        blocker.localScale = Vector3.one;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            blocker.gameObject.layer = groundLayer;
        BoxCollider2D collider = blocker.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(0.2f, 6f);
        collider.isTrigger = false;
        collider.enabled = true;
        exit.boundaryBlocker = collider;
        if (director.player != null)
        {
            PlayerInteraction interaction = director.player.GetComponent<PlayerInteraction>();
            if (interaction != null)
            {
                SerializedObject serialized = new SerializedObject(interaction);
                serialized.FindProperty("houseExit").objectReferenceValue = exit;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        if (director.cameraFollow != null)
        {
            director.cameraFollow.exteriorMapMinX = RoomBackgroundLeftX + RoomBackgroundWidth + 0.1f;
            director.cameraFollow.constrainHorizontalPosition = true;
        }
        Transform owner = root.Find("Owner Actor (graybox)/PixelSnapper Owner Visual");
        if (owner != null)
            owner.GetComponent<SpriteRenderer>().sortingOrder = 30;
        Transform ballArt = root.Find("Rolling Ball/PixelSnapper Ball Art");
        if (ballArt != null)
        {
            SpriteRenderer renderer = ballArt.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = exit.clickArea.sortingLayerID;
            renderer.sortingOrder = 25; // Ball stays visible in front of the door panel (20).
        }
        OwnerPresentationSetup.ApplyIfAvailable(root);
        HomeRoomLightingSetup.Apply(director);
    }

    [MenuItem("Tools/After the Glow/Refresh House Click Exit")]
    private static void RefreshHouseClickExit()
    {
        if (EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Exit Play Mode before saving house flow.");
        GameObject root = GameObject.Find(StoryRootName);
        if (root == null)
            throw new MissingReferenceException("Open SampleScene first.");
        ConfigureHouseFlow(root.transform);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
    }

    private static void EnsureBackgroundPadding(Transform storyRoot)
    {
        SpriteRenderer room = FindRootObject(storyRoot.gameObject.scene, "BackGround")
            ?.GetComponent<SpriteRenderer>();
        if (room == null || room.sprite == null)
            throw new MissingReferenceException("Background padding needs the existing room artwork.");
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(ShapePath);
        if (square == null)
            throw new MissingReferenceException("Background padding needs the existing white shape sprite.");
        Color skyColor = new Color(168f / 255f, 177f / 255f, 158f / 255f, 1f);
        Color floorColor = new Color(43f / 255f, 66f / 255f, 77f / 255f, 1f);
        const float padding = 32f;
        float centerX = (RoomBackgroundLeftX + StreetGroundRightX) * 0.5f;
        float width = StreetGroundRightX - RoomBackgroundLeftX + padding * 2f;
        SpriteRenderer sky = EnsureChildSprite(storyRoot, "Backdrop Sky Fill", square,
            new Vector3(centerX, FloorTop + padding, 20f),
            new Vector2(width, padding * 2f), room.sortingLayerID, -200);
        sky.color = skyColor;
        SpriteRenderer floor = EnsureChildSprite(storyRoot, "Backdrop Floor Fill", square,
            new Vector3(centerX, FloorTop - padding, 20f),
            new Vector2(width, padding * 2f), room.sortingLayerID, -200);
        floor.color = floorColor;
        ReferenceStreetLayout.BoundLegacyPadding(storyRoot);
        Transform oldCeilingFill = storyRoot.Find("Home Ceiling Fill");
        if (oldCeilingFill != null)
            Undo.DestroyObjectImmediate(oldCeilingFill.gameObject);
        CameraFollow follow = FindRoot<CameraFollow>(storyRoot.gameObject.scene);
        Camera camera = follow != null ? follow.GetComponent<Camera>() : null;
        if (camera != null)
        {
            camera.backgroundColor = skyColor;
            camera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    [MenuItem("Tools/After the Glow/Refresh Background Padding")]
    private static void RefreshBackgroundPadding()
    {
        if (EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Exit Play Mode before saving background padding.");
        GameObject root = GameObject.Find(StoryRootName);
        if (root == null)
            throw new MissingReferenceException("Open SampleScene first.");
        EnsureBackgroundPadding(root.transform);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
    }

    [MenuItem("Tools/After the Glow/Refresh Blanket and Door Interaction")]
    private static void RefreshBlanketAndDoorInteraction()
    {
        if (EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Exit Play Mode before saving interaction artwork.");
        GameObject root = GameObject.Find(StoryRootName);
        if (root == null)
            throw new MissingReferenceException("Open SampleScene first.");
        ApplyPixelSnapperPropArt(root.transform);
        CameraFollow cameraFollow = FindRoot<CameraFollow>(root.scene);
        PlayerMovement player = FindRoot<PlayerMovement>(root.scene);
        if (cameraFollow != null && cameraFollow.target == null && player != null)
            cameraFollow.target = player.transform;
        AfterTheGlowBedWakeSetup.ApplyToScene(root.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
    }

    private static void ConfigureBedBlanket(Transform bedRoot, SpriteRenderer bedArt,
        Sprite source, int sortingLayerId)
    {
        // These rectangles reuse Bed.png's pixels; no repainting or importer slicing.
        Rect blanketRect = new Rect(27f, 19f, 42f, 15f);
        Rect[] strips = {
            new Rect(0f, 0f, blanketRect.xMin, source.rect.height),
            new Rect(blanketRect.xMax, 0f, source.rect.width - blanketRect.xMax, source.rect.height),
            new Rect(blanketRect.xMin, 0f, blanketRect.width, blanketRect.yMin),
            new Rect(blanketRect.xMin, blanketRect.yMax, blanketRect.width, source.rect.height - blanketRect.yMax)
        };
        string[] stripNames = { "Left", "Right", "Bottom", "Top" };
        for (int i = 0; i < strips.Length; i++)
        {
            Rect r = strips[i];
            Sprite strip = EnsureLayerSprite(PixelArtRoot + "Props/Bed_Frame_" + stripNames[i] + ".asset",
                source.texture, r, Vector2.one * 0.5f, source.pixelsPerUnit);
            Vector2 center = (r.center - source.pivot) / source.pixelsPerUnit;
            EnsureChildSprite(bedArt.transform, "Bed Frame " + stripNames[i], strip,
                new Vector3(center.x, center.y, 0f), Vector2.one,
                sortingLayerId, -2, Vector2.one);
        }
        bedArt.enabled = false;
        // OverrideGeometry cannot modify a persisted Sprite asset. The frame uses
        // four ordinary cropped sprites around the independently moving blanket.
        if (AssetDatabase.LoadAssetAtPath<Sprite>(PixelArtRoot + "Props/Bed_Frame.asset") != null)
            AssetDatabase.DeleteAsset(PixelArtRoot + "Props/Bed_Frame.asset");
        Sprite blanket = EnsureLayerSprite(PixelArtRoot + "Props/Bed_Blanket.asset",
            source.texture, blanketRect, Vector2.one * 0.5f, source.pixelsPerUnit);
        Vector2 localCenter = (blanketRect.center - source.pivot) / source.pixelsPerUnit;
        SpriteRenderer moving = EnsureChildSprite(bedArt.transform, "Bed Blanket", blanket,
            new Vector3(localCenter.x, localCenter.y, -0.01f), Vector2.one,
            sortingLayerId, -1, Vector2.one);
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(ShapePath);
        SpriteRenderer mattress = EnsureChildSprite(bedArt.transform, "Mattress Under Blanket", square,
            new Vector3(localCenter.x, (25f - source.pivot.y) / source.pixelsPerUnit, 0.01f),
            new Vector2(blanketRect.width / source.pixelsPerUnit, 12f / source.pixelsPerUnit),
            sortingLayerId, -3);
        mattress.color = new Color(229f / 255f, 219f / 255f, 182f / 255f);
        PushableObject interaction = bedRoot.GetComponent<PushableObject>();
        Undo.RecordObject(interaction, "Use stationary bed blanket interaction");
        interaction.ConfigureVisualInteraction(moving.transform);
        EditorUtility.SetDirty(interaction);
    }

    private static Sprite ConfigureFrontDoorArtwork(Transform hinge, SpriteRenderer background,
        int sortingLayerId)
    {
        if (background == null || background.sprite == null)
            return null;
        Sprite source = background.sprite;
        Rect panelRect = new Rect(1888f, 158f, 230f, 375f);
        Sprite panelSprite = EnsureLayerSprite(PixelArtRoot + "Props/FrontDoor_Illustration_BackgroundPanel.asset",
            source.texture, panelRect, Vector2.one * 0.5f, source.pixelsPerUnit);
        Vector2 sourceCenter = (panelRect.center - source.pivot) / source.pixelsPerUnit;
        Vector3 worldCenter = background.transform.TransformPoint(new Vector3(sourceCenter.x, sourceCenter.y, 0f));
        Vector3 localCenter = hinge.InverseTransformPoint(worldCenter);
        localCenter.z = 0f;
        Vector2 size = new Vector2(panelRect.width / source.pixelsPerUnit * background.transform.lossyScale.x,
            panelRect.height / source.pixelsPerUnit * background.transform.lossyScale.y);
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(ShapePath);
        SpriteRenderer opening = EnsureChildSprite(hinge, "Front Door Dark Opening", square,
            localCenter + new Vector3(0f, 0f, 0.01f), size, sortingLayerId, 19);
        opening.color = new Color(43f / 255f, 66f / 255f, 77f / 255f);
        SpriteRenderer panel = EnsureChildSprite(hinge, "Front Door Panel", panelSprite,
            localCenter, size, sortingLayerId, 20);
        panel.gameObject.SetActive(true);
        opening.gameObject.SetActive(true);
        DisableRenderer(panel.transform.Find("Front Door Handle"));
        DisableRenderer(hinge.Find("HingedDoor_Closed"));
        hinge.localRotation = Quaternion.identity;
        return panelSprite;
    }

    private static Sprite EnsureLayerSprite(string path, Texture2D texture, Rect rect, Vector2 pivot, float ppu)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;
        Sprite sprite = Sprite.Create(texture, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
        sprite.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(sprite, path);
        return sprite;
    }

    private static void ApplyPixelStreetBackgrounds(Transform storyRoot, int sortingLayerId)
    {
        Sprite street = AssetDatabase.LoadAssetAtPath<Sprite>(Map02BackgroundPath);
        Sprite park = AssetDatabase.LoadAssetAtPath<Sprite>(Map03BackgroundPath);
        if (street == null || park == null)
            throw new FileNotFoundException("Map 02 and Map 03 illustrated backgrounds are required.");

        EnsureBackgroundTile(storyRoot, "Street Night Backdrop", street,
            new Vector3(StreetBackdropLeftX + StreetBackdropTileWidth * 0.5f, Map02CenterY, 1f),
            new Vector2(StreetBackdropTileWidth, StreetBackdropTileHeight), sortingLayerId, -101);
        EnsureBackgroundTile(storyRoot, "Park Map Backdrop", park,
            new Vector3(ParkBackdropStartX + ParkBackdropWidth * 0.5f, Map03CenterY, 0.9f),
            new Vector2(ParkBackdropWidth, ParkBackdropHeight), sortingLayerId, -99);

        DisableRenderer(storyRoot.Find("Street Night Backdrop 2"));
        DisableRenderer(storyRoot.Find("Street Night Backdrop 3"));
    }

    private static void EnsureBackgroundTile(Transform parent, string name, Sprite sprite,
        Vector3 position, Vector2 size, int sortingLayerId, int sortingOrder)
    {
        Transform tile = parent.Find(name);
        if (tile == null)
        {
            GameObject tileObject = new GameObject(name);
            tileObject.transform.SetParent(parent, false);
            tile = tileObject.transform;
        }
        tile.position = position;
        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = Undo.AddComponent<SpriteRenderer>(tile.gameObject);
        renderer.sprite = sprite;
        renderer.sharedMaterial = LoadSpriteUnlitMaterial();
        renderer.color = Color.white;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        ScaleSpriteTo(renderer, size);
    }

    private static SpriteRenderer EnsureChildSprite(Transform parent, string name, Sprite sprite,
        Vector3 localPosition, Vector2 visibleSize, int sortingLayerId, int sortingOrder,
        Vector2? customScale = null)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject childObject = new GameObject(name);
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }
        child.localPosition = localPosition;
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = Undo.AddComponent<SpriteRenderer>(child.gameObject);
        renderer.enabled = true;
        renderer.sprite = sprite;
        renderer.sharedMaterial = LoadSpriteUnlitMaterial();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        if (customScale.HasValue)
            child.localScale = new Vector3(customScale.Value.x, customScale.Value.y, 1f);
        else
            ScaleSpriteTo(renderer, visibleSize);
        return renderer;
    }

    private static void DisableRenderer(Transform item)
    {
        if (item == null)
            return;
        SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.enabled = false;
    }

    private static Sprite LoadSpriteSubAsset(string assetPath, string spriteName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (UnityEngine.Object asset in assets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null && sprite.name == spriteName)
                return sprite;
        }
        return null;
    }

    private static void CreateBathroomFrame(Transform parent, Sprite sprite, int sortingLayerId)
    {
        float x = BathroomX;
        CreateRect(parent, sprite, "Bathroom Door Left", new Vector2(x - 0.72f, FloorTop + 1.05f),
            new Vector2(0.13f, 2.1f), new Color(0.34f, 0.2f, 0.13f), sortingLayerId, 2);
        CreateRect(parent, sprite, "Bathroom Door Right", new Vector2(x + 0.72f, FloorTop + 1.05f),
            new Vector2(0.13f, 2.1f), new Color(0.34f, 0.2f, 0.13f), sortingLayerId, 2);
        CreateRect(parent, sprite, "Bathroom Door Header", new Vector2(x, FloorTop + 2.08f),
            new Vector2(1.55f, 0.14f), new Color(0.34f, 0.2f, 0.13f), sortingLayerId, 2);
        CreateRect(parent, sprite, "Bathroom Interior", new Vector2(x, FloorTop + 1.02f),
            new Vector2(1.22f, 1.96f), new Color(0.15f, 0.2f, 0.25f, 0.72f), sortingLayerId, 1);
        CreateRect(parent, sprite, "Bathroom Light Marker", new Vector2(x, FloorTop + 2.25f),
            new Vector2(0.45f, 0.12f), new Color(0.98f, 0.84f, 0.48f), sortingLayerId, 4);
    }

    private static Transform CreateOwner(Transform parent, Sprite square, Sprite circle,
        int sortingLayerId, float bedCenterX)
    {
        GameObject owner = new GameObject("Owner Actor (graybox)");
        owner.transform.SetParent(parent, false);
        owner.transform.position = new Vector3(bedCenterX, FloorTop, -0.1f);
        CreateRect(owner.transform, square, "Body", new Vector2(0f, 0.62f),
            new Vector2(0.58f, 0.9f), new Color(0.42f, 0.31f, 0.26f), sortingLayerId, 5);
        CreateRect(owner.transform, square, "Left Leg", new Vector2(-0.15f, 0.11f),
            new Vector2(0.2f, 0.43f), new Color(0.25f, 0.2f, 0.18f), sortingLayerId, 5);
        CreateRect(owner.transform, square, "Right Leg", new Vector2(0.15f, 0.11f),
            new Vector2(0.2f, 0.43f), new Color(0.25f, 0.2f, 0.18f), sortingLayerId, 5);
        CreateSprite(owner.transform, circle, "Head", new Vector2(0f, 1.25f),
            new Vector2(0.47f, 0.47f), new Color(0.83f, 0.64f, 0.49f), sortingLayerId, 6);
        CreateRect(owner.transform, square, "Arm", new Vector2(0.37f, 0.78f),
            new Vector2(0.18f, 0.62f), new Color(0.72f, 0.49f, 0.34f), sortingLayerId, 5);
        owner.SetActive(false);
        return owner.transform;
    }

    private static Light2D CreateBathroomLight(Transform parent, int sortingLayerId)
    {
        GameObject lightObject = new GameObject("Bathroom Light (decorative only)");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = new Vector3(BathroomX, FloorTop + 1.5f, 0f);
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = 1;
        light.color = new Color(1f, 0.81f, 0.56f);
        light.intensity = 1.05f;
        light.pointLightInnerRadius = 0.6f;
        light.pointLightOuterRadius = 4f;
        light.shadowsEnabled = false;
        lightObject.SetActive(false);
        return light;
    }

    private static void CreateBall(Transform parent, Sprite circle, int sortingLayerId,
        PhysicsMaterial2D zeroFriction)
    {
        GameObject ball = CreateSprite(parent, circle, "Rolling Ball",
            new Vector2(BallX, FloorTop + BallDiameter * 0.5f),
            new Vector2(BallDiameter, BallDiameter), new Color(0.2f, 0.74f, 0.91f), sortingLayerId, 4);
        Rigidbody2D body = ball.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = 0.65f;
        body.gravityScale = 1f;
        body.linearDamping = 1.8f;
        body.angularDamping = 1f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CircleCollider2D collider = ball.AddComponent<CircleCollider2D>();
        collider.sharedMaterial = zeroFriction;
        ball.SetActive(true);
    }

    private static Transform CreateFrontDoor(Transform parent, Sprite square, int sortingLayerId)
    {
        GameObject pivot = new GameObject("Front Door Hinge");
        pivot.transform.SetParent(parent, false);
        pivot.transform.position = new Vector3(FrontDoorX, FloorTop, -0.1f);
        GameObject panel = CreateRect(pivot.transform, square, "Front Door Panel",
            new Vector2(-0.45f, 1.02f), new Vector2(0.9f, 2.04f),
            new Color(0.24f, 0.15f, 0.12f), sortingLayerId, 7);
        CreateRect(panel.transform, square, "Front Door Handle", new Vector2(0.31f, 0.02f),
            new Vector2(0.07f, 0.18f), new Color(0.88f, 0.67f, 0.28f), sortingLayerId, 8);
        return pivot.transform;
    }

    private static void CreateExteriorDoorwayConnector(Transform parent, Sprite square,
        int sortingLayerId)
    {
        GameObject connector = new GameObject("Exterior Doorway Connector (graybox)");
        connector.transform.SetParent(parent, false);

        Color exteriorWall = new Color(0.2f, 0.27f, 0.36f);
        CreateRect(connector.transform, square, "House End Wall Left",
            new Vector2(FrontDoorX - 2.2f, FloorTop + 2.4f), new Vector2(2.0f, 4.8f),
            exteriorWall, sortingLayerId, 1);
        CreateRect(connector.transform, square, "House End Wall Above Door",
            new Vector2(FrontDoorX - 0.45f, FloorTop + 3.35f), new Vector2(1.0f, 2.7f),
            exteriorWall, sortingLayerId, 1);

        Color frame = new Color(0.34f, 0.2f, 0.13f);
        CreateRect(connector.transform, square, "Front Door Frame Left",
            new Vector2(FrontDoorX - 0.96f, FloorTop + 1.04f), new Vector2(0.14f, 2.08f),
            frame, sortingLayerId, 6);
        CreateRect(connector.transform, square, "Front Door Frame Right",
            new Vector2(FrontDoorX + 0.06f, FloorTop + 1.04f), new Vector2(0.14f, 2.08f),
            frame, sortingLayerId, 6);
        CreateRect(connector.transform, square, "Front Door Frame Header",
            new Vector2(FrontDoorX - 0.45f, FloorTop + 2.08f), new Vector2(1.14f, 0.16f),
            frame, sortingLayerId, 6);
        CreateRect(connector.transform, square, "Front Door Threshold",
            new Vector2(FrontDoorX - 0.45f, FloorTop - 0.02f), new Vector2(1.12f, 0.04f),
            new Color(0.5f, 0.34f, 0.21f), sortingLayerId, 5);
        CreateRect(connector.transform, square, "Front Porch Walkway",
            new Vector2(FrontDoorX + 1.45f, FloorTop - 0.02f), new Vector2(3.1f, 0.04f),
            new Color(0.2f, 0.22f, 0.26f), sortingLayerId, 2);
    }

    private static void CreateStreetBackdropAndFloor(Transform parent, Sprite square,
        int sortingLayerId, Scene scene)
    {
        float backdropWidth = StreetGroundRightX - StreetBackdropLeftX;
        float backdropCenterX = StreetBackdropLeftX + backdropWidth * 0.5f;
        GameObject road = CreateRect(parent, square, "Street Night Backdrop",
            new Vector2(backdropCenterX, 1.2f), new Vector2(backdropWidth, 12f),
            new Color(0.12f, 0.17f, 0.25f), sortingLayerId, 1);
        road.transform.position = new Vector3(backdropCenterX, 1.2f, 1f);

        float extensionLeft = StreetGroundLeftX;
        float extensionRight = StreetGroundRightX;
        float width = extensionRight - extensionLeft;
        float center = (extensionLeft + extensionRight) * 0.5f;
        GameObject ground = CreateRect(parent, square, "Street Ground Extension",
            new Vector2(center, FloorCenterY), new Vector2(width, 1f),
            new Color(0.2f, 0.22f, 0.26f), sortingLayerId, 1);
        BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            ground.layer = groundLayer;

        for (int i = 0; i < 7; i++)
        {
            float x = extensionLeft + 3.1f + i * 5.1f;
            CreateRect(parent, square, "Street Lane Mark " + i,
                new Vector2(x, FloorTop + 0.12f), new Vector2(1.5f, 0.07f),
                new Color(0.75f, 0.68f, 0.49f, 0.7f), sortingLayerId, 2);
        }
    }

    private static GameObject CreateCarAndHeadlight(Transform parent, Sprite square, Sprite circle,
        int sortingLayerId, PhysicsMaterial2D zeroFriction)
    {
        float carX = CarX;
        CreateRect(parent, square, "Parked Car Body", new Vector2(carX, FloorTop + 0.7f),
            new Vector2(4.2f, 1f), new Color(0.09f, 0.13f, 0.2f), sortingLayerId, 4);
        CreateRect(parent, square, "Parked Car Cabin", new Vector2(carX + 0.35f, FloorTop + 1.35f),
            new Vector2(2.15f, 0.47f), new Color(0.14f, 0.22f, 0.32f), sortingLayerId, 3);
        CreateSprite(parent, circle, "Car Wheel Rear", new Vector2(carX - 1.25f, FloorTop + 0.22f),
            new Vector2(0.55f, 0.55f), new Color(0.035f, 0.045f, 0.055f), sortingLayerId, 5);
        CreateSprite(parent, circle, "Car Wheel Front", new Vector2(carX + 1.25f, FloorTop + 0.22f),
            new Vector2(0.55f, 0.55f), new Color(0.035f, 0.045f, 0.055f), sortingLayerId, 5);

        GameObject lightObject = new GameObject("Car High Beams - Gameplay Light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = new Vector3(carX, FloorTop + 1.35f, 0f);
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = 1;
        light.color = new Color(1f, 0.88f, 0.64f);
        light.intensity = 1.65f;
        light.pointLightInnerRadius = 0.5f;
        light.pointLightOuterRadius = 4.8f;
        light.shadowsEnabled = false;
        GameplayLight2D gameplay = lightObject.AddComponent<GameplayLight2D>();
        SerializedObject serialized = new SerializedObject(gameplay);
        serialized.FindProperty("gameplayRadius").floatValue = 4.8f;
        serialized.FindProperty("gameplayIntensityMultiplier").floatValue = 1f;
        serialized.FindProperty("falloffExponent").floatValue = 0.72f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CreateRect(lightObject.transform, square, "Visible Headlight Beam",
            new Vector2(-2.4f, 0f), new Vector2(4.8f, 1.8f),
            new Color(1f, 0.78f, 0.36f, 0.18f), sortingLayerId, 3);
        return lightObject;
    }

    private static PushableObject CreateParkGate(Transform parent, Sprite square, Sprite circle,
        int sortingLayerId, PhysicsMaterial2D zeroFriction)
    {
        float gateX = GateX;
        float postY = FloorTop + 1.18f;
        CreateRect(parent, square, "Fence Left Post", new Vector2(35.5f, postY),
            new Vector2(0.22f, 2.5f), new Color(0.32f, 0.39f, 0.42f), sortingLayerId, 3);
        CreateRect(parent, square, "Fence Right Post", new Vector2(40.2f, postY),
            new Vector2(0.22f, 2.5f), new Color(0.32f, 0.39f, 0.42f), sortingLayerId, 3);
        CreateRect(parent, square, "Fence Top Rail", new Vector2(37.85f, FloorTop + 2.35f),
            new Vector2(4.85f, 0.18f), new Color(0.32f, 0.39f, 0.42f), sortingLayerId, 3);
        CreateRect(parent, square, "Fence Bottom Rail", new Vector2(37.85f, FloorTop + 0.12f),
            new Vector2(4.85f, 0.14f), new Color(0.32f, 0.39f, 0.42f), sortingLayerId, 3);
        for (int i = 0; i < 13; i++)
        {
            float x = 35.72f + i * 0.35f;
            CreateRect(parent, square, "Fence Bar " + i, new Vector2(x, postY),
                new Vector2(0.065f, 2.35f), new Color(0.4f, 0.47f, 0.5f), sortingLayerId, 3);
        }

        GameObject gate = new GameObject("Park Iron Gate - Leash Bound");
        gate.transform.SetParent(parent, false);
        gate.transform.position = new Vector3(gateX, postY, -0.1f);
        SpriteRenderer backing = gate.AddComponent<SpriteRenderer>();
        backing.sprite = square;
        backing.sharedMaterial = LoadSpriteUnlitMaterial();
        backing.color = new Color(0.1f, 0.17f, 0.2f, 0.28f);
        backing.sortingLayerID = sortingLayerId;
        backing.sortingOrder = 4;
        ScaleSpriteTo(backing, new Vector2(0.72f, 2.3f));

        for (int i = 0; i < 5; i++)
        {
            float x = -0.27f + i * 0.135f;
            CreateRect(gate.transform, square, "Gate Bar " + i, new Vector2(x, 0f),
                new Vector2(0.07f, 2.2f), new Color(0.57f, 0.64f, 0.65f), sortingLayerId, 5);
        }

        GameObject strap = CreateRect(gate.transform, square, "Visible Leash Knot",
            new Vector2(-0.12f, 0.58f), new Vector2(0.8f, 0.11f),
            new Color(0.75f, 0.48f, 0.28f), sortingLayerId, 7);
        GameObject tiePointObject = new GameObject("Leash Tie Point");
        tiePointObject.transform.SetParent(gate.transform, false);
        tiePointObject.transform.localPosition = new Vector3(-0.32f, 0.58f, 0f);

        Rigidbody2D body = gate.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = 2f;
        body.gravityScale = 1f;
        body.linearDamping = 1.1f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        BoxCollider2D collider = gate.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.72f, 2.3f);
        collider.sharedMaterial = zeroFriction;

        GameObject dustObject = new GameObject("Pixel Dust on Leash Release");
        dustObject.transform.SetParent(gate.transform, false);
        dustObject.transform.localPosition = new Vector3(-0.2f, 0.55f, -0.2f);
        ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();
        ConfigureDust(particles, sortingLayerId);

        PushableObject pushable = gate.AddComponent<PushableObject>();
        SetPushable(pushable, 0.7f, true, 0.95f, true,
            tiePointObject.transform, strap, particles);
        return pushable;
    }

    private static GameObject CreateParkArea(Transform parent, Sprite square, Sprite circle,
        int sortingLayerId, Scene scene)
    {
        GameObject backdrop = CreateRect(parent, square, "Park Entry Placeholder Backdrop",
            new Vector2(48.5f, 1.2f), new Vector2(13f, 12f),
            new Color(0.17f, 0.27f, 0.25f), sortingLayerId, 1);
        backdrop.transform.position = new Vector3(48.5f, 1.2f, 0.9f);
        for (int i = 0; i < 5; i++)
        {
            float x = 43.5f + i * 2.5f;
            CreateRect(parent, square, "Park Tree Trunk " + i, new Vector2(x, FloorTop + 1.2f),
                new Vector2(0.3f, 2.4f), new Color(0.28f, 0.23f, 0.16f), sortingLayerId, 2);
            CreateSprite(parent, circle, "Park Tree Canopy " + i, new Vector2(x, FloorTop + 2.65f),
                new Vector2(1.5f, 1.25f), new Color(0.22f, 0.44f, 0.3f), sortingLayerId, 2);
        }

        CreateRect(parent, square, "Park Placeholder Ground",
            new Vector2(48.5f, FloorCenterY), new Vector2(13f, 1f),
            new Color(0.21f, 0.32f, 0.25f), sortingLayerId, 2);
        GameObject parkGround = FindDirectChild(backdrop.transform.parent, "Park Placeholder Ground");
        BoxCollider2D collider = parkGround.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            parkGround.layer = groundLayer;

        return CreateRect(parent, square, "Park Entry Marker",
            new Vector2(ParkEntryX, FloorTop + 0.1f), new Vector2(0.6f, 0.18f),
            new Color(0.78f, 0.68f, 0.36f), sortingLayerId, 4);
    }

    private static void CreateStoryTrigger(Transform parent, string name, Vector2 position,
        Vector2 size, AfterTheGlowStoryDirector director, AfterTheGlowTriggerKind kind,
        int sortingLayerId)
    {
        GameObject trigger = new GameObject(name);
        trigger.transform.SetParent(parent, false);
        trigger.transform.position = position;
        BoxCollider2D collider = trigger.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = size;
        AfterTheGlowStoryTrigger storyTrigger = trigger.AddComponent<AfterTheGlowStoryTrigger>();
        storyTrigger.director = director;
        storyTrigger.triggerKind = kind;
    }

    private static PushableObject CreatePushable(Transform parent, Sprite sprite, string name,
        Vector2 position, Vector2 size, Color color, int sortingLayerId, int sortingOrder,
        PhysicsMaterial2D material, float pushCompletionDistance, bool requiresBite,
        float pullDistance, bool disableColliderAfterPush, Transform tetherPoint,
        GameObject tetherVisual, ParticleSystem particles)
    {
        GameObject item = CreateSprite(parent, sprite, name, position, size, color,
            sortingLayerId, sortingOrder);
        Rigidbody2D body = item.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = 1.4f;
        body.gravityScale = 1f;
        body.linearDamping = 0.9f;
        body.angularDamping = 0.05f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        BoxCollider2D collider = item.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.sharedMaterial = material;
        PushableObject pushable = item.AddComponent<PushableObject>();
        SetPushable(pushable, pushCompletionDistance, requiresBite, pullDistance,
            disableColliderAfterPush, tetherPoint, tetherVisual, particles);
        return pushable;
    }

    private static void SetPushable(PushableObject target, float pushDistance, bool requiresBite,
        float pullDistance, bool disableCollider, Transform tetherPoint,
        GameObject tetherVisual, ParticleSystem particles)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty("completeAfterPushDistance").floatValue = pushDistance;
        serialized.FindProperty("requiresBiteToRelease").boolValue = requiresBite;
        serialized.FindProperty("pullDistanceToRelease").floatValue = Mathf.Max(0.05f, pullDistance);
        serialized.FindProperty("disableColliderAfterPushCompletion").boolValue = disableCollider;
        serialized.FindProperty("tetherPoint").objectReferenceValue = tetherPoint;
        serialized.FindProperty("tetherVisual").objectReferenceValue = tetherVisual;
        serialized.FindProperty("releaseParticles").objectReferenceValue = particles;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateDebugOverlay(Transform parent, PlayerMovement player)
    {
        GameObject debug = new GameObject("Core Debug Overlay (F1)");
        debug.transform.SetParent(parent, false);
        CoreDebugOverlay overlay = debug.AddComponent<CoreDebugOverlay>();
        SerializedObject serialized = new SerializedObject(overlay);
        serialized.FindProperty("player").objectReferenceValue = player;
        serialized.FindProperty("exposureSensor").objectReferenceValue = player.GetComponent<LightExposureSensor>();
        serialized.FindProperty("interaction").objectReferenceValue = player.GetComponent<PlayerInteraction>();
        serialized.FindProperty("visibleAtStart").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateRect(Transform parent, Sprite sprite, string name,
        Vector2 position, Vector2 size, Color color, int sortingLayerId, int sortingOrder)
    {
        return CreateSprite(parent, sprite, name, position, size, color, sortingLayerId, sortingOrder);
    }

    private static GameObject CreateSprite(Transform parent, Sprite sprite, string name,
        Vector2 position, Vector2 size, Color color, int sortingLayerId, int sortingOrder)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = LoadSpriteUnlitMaterial();
        renderer.color = color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        ScaleSpriteTo(renderer, size);
        return item;
    }

    private static Material LoadSpriteUnlitMaterial()
    {
        string materialPath = AssetDatabase.GUIDToAssetPath(SpriteUnlitMaterialGuid);
        Material material = string.IsNullOrEmpty(materialPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
            throw new MissingReferenceException("URP Sprite-Unlit-Default material was not found.");
        return material;
    }

    private static Material LoadSpriteLitMaterial()
    {
        string materialPath = AssetDatabase.GUIDToAssetPath(SpriteLitMaterialGuid);
        Material material = string.IsNullOrEmpty(materialPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
            throw new MissingReferenceException("URP Sprite-Lit-Default material was not found.");
        return material;
    }

    private static void ApplyGrayboxMaterial(GameObject root)
    {
        if (root == null)
            return;

        Material material = LoadSpriteUnlitMaterial();
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterial = material;
    }

    private static void EnsureVisibleHeadlightBeam(GameObject root)
    {
        Transform light = root.transform.Find("Car High Beams - Gameplay Light");
        if (light == null)
            return;
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(ShapePath);
        if (square == null)
            throw new MissingReferenceException("Headlight display needs the existing white shape sprite.");
        GameObject beamObject = FindDirectChild(light, "Visible Headlight Beam");
        if (beamObject == null)
        {
            beamObject = new GameObject("Visible Headlight Beam");
            beamObject.transform.SetParent(light, false);
        }
        beamObject.SetActive(true);
        beamObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        beamObject.transform.localRotation = Quaternion.identity;
        beamObject.transform.localScale = Vector3.one;
        SpriteRenderer oldBeamRenderer = beamObject.GetComponent<SpriteRenderer>();
        if (oldBeamRenderer != null)
            Undo.DestroyObjectImmediate(oldBeamRenderer);
        MeshFilter filter = beamObject.GetComponent<MeshFilter>();
        if (filter == null)
            filter = Undo.AddComponent<MeshFilter>(beamObject);
        MeshRenderer renderer = beamObject.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = Undo.AddComponent<MeshRenderer>(beamObject);

        const string meshPath = PixelArtRoot + "Props/HeadlightBeam.asset";
        const string materialPath = PixelArtRoot + "Props/HeadlightBeam.mat";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Pixel Headlight Beam" };
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        mesh.Clear();
        GameplayLight2D gameplay = light.GetComponent<GameplayLight2D>();
        gameplay.ConfigureDirectionalBeams(new[] { new Vector2(-1.55f, 0f), new Vector2(1.55f, 0f) },
            Vector2.down, 2.05f, 0.20f, 1.90f, 0.40f);
        EditorUtility.SetDirty(gameplay);
        const int steps = 24;
        Vector3[] vertices = new Vector3[gameplay.BeamCount * steps * 4];
        Vector2[] uv = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[gameplay.BeamCount * steps * 6];
        for (int beam = 0; beam < gameplay.BeamCount; beam++)
        for (int i = 0; i < steps; i++)
        {
            gameplay.GetBeamCrossSection(beam, i / (float)steps, out Vector2 nearLeft, out Vector2 nearRight);
            gameplay.GetBeamCrossSection(beam, (i + 1f) / steps, out Vector2 farLeft, out Vector2 farRight);
            float distance = (i + 1f) / steps;
            int band = beam * steps + i;
            int v = band * 4;
            vertices[v] = farLeft;
            vertices[v + 1] = nearLeft;
            vertices[v + 2] = nearRight;
            vertices[v + 3] = farRight;
            for (int j = 0; j < 4; j++)
            {
                uv[v + j] = Vector2.one * 0.5f;
                colors[v + j] = new Color(1f, 0.89f, 0.61f, Mathf.Lerp(0.42f, 0.09f, distance));
            }
            int t = band * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(LoadSpriteUnlitMaterial()) { name = "Pixel Headlight Beam" };
            material.SetTexture("_MainTex", square.texture);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.enabled = true;
        SpriteRenderer car = root.transform.Find("PixelSnapper Car No Lights")?.GetComponent<SpriteRenderer>();
        renderer.sortingLayerID = car != null ? car.sortingLayerID : 0;
        renderer.sortingOrder = 10;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    [MenuItem("Tools/After the Glow/Refresh Car Headlight Display")]
    private static void RefreshCarHeadlightDisplay()
    {
        if (EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Exit Play Mode before saving the headlight display.");
        GameObject root = GameObject.Find(StoryRootName);
        if (root == null)
            throw new MissingReferenceException("Open SampleScene first.");
        EnsureVisibleHeadlightBeam(root);
        ConfigureSingleCarPresentation(root.transform);
        ReferenceStreetLayout.Apply(root.transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
    }

    private static void ConfigureSingleCarPresentation(Transform root)
    {
        Transform car = root.Find("PixelSnapper Car No Lights");
        Transform light = root.Find("Car High Beams - Gameplay Light");
        if (car == null || light == null)
            throw new MissingReferenceException("Existing car and headlight are required.");
        DisableRenderer(light.Find("PixelSnapper Headlights Art"));
        var presentation = car.GetComponent<CarHeadlightPresentation>();
        if (presentation == null)
            presentation = Undo.AddComponent<CarHeadlightPresentation>(car.gameObject);
        presentation.lightRoot = light.gameObject;
        presentation.lightsOnSprite = EnsureCarFrontSprite(true);
        presentation.lightsOffSprite = EnsureCarFrontSprite(false);
        presentation.worldSize = new Vector2(4.8f, 3.15f);
        presentation.alignWheelsToGround = true;
        presentation.groundY = FloorTop;
        presentation.wheelBottomOffsets = Vector2.zero;
        presentation.Refresh();
        EditorUtility.SetDirty(presentation);
    }

    private static float GetCarWheelBottomOffset(Sprite sprite)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))))
            throw new IOException("Cannot read car sprite pixels.");
        Rect rect = sprite.rect;
        int bottom = -1;
        for (int y = (int)rect.yMin; y < rect.yMax && bottom < 0; y++)
            for (int x = (int)rect.xMin; x < rect.xMax; x++)
                if (texture.GetPixel(x, y).a >= 0.78f) { bottom = y; break; }
        Object.DestroyImmediate(texture);
        if (bottom < 0) throw new System.InvalidOperationException("Car sprite has no visible wheels.");
        return (bottom - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit;
    }

    private static void RefreshExistingStoryDemo(GameObject root)
    {
        ApplyGrayboxMaterial(root);
        Transform oldConnector = root.transform.Find("Exterior Doorway Connector (graybox)");
        if (oldConnector != null)
            Undo.DestroyObjectImmediate(oldConnector.gameObject);
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(ShapePath);
        Sprite circle = EnsureCircleSprite();
        if (square != null)
        {
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            int sortingLayerId = renderers.Length > 0 ? renderers[0].sortingLayerID : 0;
            ApplyRestoredRoomArt(root.scene, root.transform,
                FindRoot<PlayerMovement>(root.scene));
            CreateExteriorDoorwayConnector(root.transform, square, sortingLayerId);
        }

        float bedCenterX = GetBedCenterX(root.scene);
        CreateOrRefreshHouseLeftBoundary(root.transform, GetBackgroundLeftX(root.scene));

        AfterTheGlowStoryDirector director = root.GetComponent<AfterTheGlowStoryDirector>();

        Transform bedTarget = root.transform.Find("Bed Push Target");
        if (bedTarget != null)
        {
            bedTarget.position = new Vector3(bedCenterX, FloorTop + 0.45f, bedTarget.position.z);
            SpriteRenderer bedRenderer = bedTarget.GetComponent<SpriteRenderer>();
            if (bedRenderer != null)
            {
                SpriteRenderer backgroundRenderer = FindRootObject(root.scene, "BackGround")
                    ?.GetComponent<SpriteRenderer>();
                bedRenderer.enabled = false; // Keep the original bed artwork unobstructed.
                bedRenderer.sprite = square;
                bedRenderer.sharedMaterial = LoadSpriteUnlitMaterial();
                bedRenderer.color = new Color(0.54f, 0.31f, 0.16f);
                if (backgroundRenderer != null)
                {
                    bedRenderer.sortingLayerID = backgroundRenderer.sortingLayerID;
                    bedRenderer.sortingOrder = backgroundRenderer.sortingOrder + 103;
                }
            }
        }

        Transform ownerActor = root.transform.Find("Owner Actor (graybox)");
        if (ownerActor != null)
            ownerActor.position = new Vector3(bedCenterX, FloorTop, ownerActor.position.z);

        SetWorldX(root.transform.Find("Bathroom Door Left"), BathroomX - 0.72f);
        SetWorldX(root.transform.Find("Bathroom Door Right"), BathroomX + 0.72f);
        SetWorldX(root.transform.Find("Bathroom Door Header"), BathroomX);
        SetWorldX(root.transform.Find("Bathroom Interior"), BathroomX);
        SetWorldX(root.transform.Find("Bathroom Light Marker"), BathroomX);

        Transform bathroomLight = root.transform.Find("Bathroom Light (decorative only)");
        if (bathroomLight != null)
            bathroomLight.position = new Vector3(BathroomX, FloorTop + 1.5f, bathroomLight.position.z);

        Transform ball = root.transform.Find("Rolling Ball");
        if (ball != null)
        {
            SpriteRenderer ballRenderer = ball.GetComponent<SpriteRenderer>();
            if (ballRenderer != null && circle != null)
            {
                ballRenderer.sprite = circle;
                ballRenderer.color = new Color(0.2f, 0.74f, 0.91f);
                ScaleSpriteTo(ballRenderer, new Vector2(BallDiameter, BallDiameter));
            }
            CircleCollider2D ballCollider = ball.GetComponent<CircleCollider2D>();
            if (ballCollider != null)
            {
                ballCollider.radius = BallDiameter * 0.5f;
                ballCollider.offset = Vector2.zero;
            }
            ball.position = new Vector3(BallX, FloorTop + BallDiameter * 0.5f, ball.position.z);
            ball.gameObject.SetActive(true);
            if (director != null)
                director.rollingBall = ball.gameObject;
        }

        Transform door = root.transform.Find("Front Door Hinge");
        if (door != null)
            door.position = new Vector3(FrontDoorX, FloorTop, -0.1f);

        Transform streetTrigger = root.transform.Find("House To Street Trigger");
        if (streetTrigger != null)
        {
            streetTrigger.position = new Vector3(StreetEntryX, FloorTop + 1.1f, streetTrigger.position.z);
            AfterTheGlowStoryTrigger storyTrigger = streetTrigger.GetComponent<AfterTheGlowStoryTrigger>();
            if (storyTrigger != null)
            {
                storyTrigger.director = director;
                storyTrigger.triggerKind = AfterTheGlowTriggerKind.HouseExit;
            }
        }

        Transform parkTrigger = root.transform.Find("Park Entry Trigger");
        if (parkTrigger != null)
        {
            parkTrigger.position = new Vector3(ParkEntryX, FloorTop + 1.1f, parkTrigger.position.z);
            AfterTheGlowStoryTrigger storyTrigger = parkTrigger.GetComponent<AfterTheGlowStoryTrigger>();
            if (storyTrigger != null)
            {
                storyTrigger.director = director;
                storyTrigger.triggerKind = AfterTheGlowTriggerKind.ParkEntry;
            }
        }

        Transform ownerProgressTrigger = root.transform.Find("Owner Preparation Progress Trigger");
        if (ownerProgressTrigger == null)
            ownerProgressTrigger = root.transform.Find("Ball Play Progress Trigger");
        if (ownerProgressTrigger == null && director != null)
        {
            CreateStoryTrigger(root.transform, "Owner Preparation Progress Trigger",
                new Vector2(OwnerPreparationProgressX, FloorTop + 1.1f), new Vector2(0.55f, 2.2f),
                director, AfterTheGlowTriggerKind.OwnerPreparationTarget, 0);
        }
        else if (ownerProgressTrigger != null)
        {
            ownerProgressTrigger.name = "Owner Preparation Progress Trigger";
            ownerProgressTrigger.position = new Vector3(OwnerPreparationProgressX, FloorTop + 1.1f,
                ownerProgressTrigger.position.z);
            AfterTheGlowStoryTrigger storyTrigger = ownerProgressTrigger.GetComponent<AfterTheGlowStoryTrigger>();
            if (storyTrigger != null)
            {
                storyTrigger.director = director;
                storyTrigger.triggerKind = AfterTheGlowTriggerKind.OwnerPreparationTarget;
            }
        }

        RefreshStreetBackdropAndFloor(root);

        if (director != null)
        {
            director.bathroomX = BathroomX;
            director.frontDoorX = FrontDoorX;
            director.ownerDoorExitOffset = OwnerDoorExitOffset;
            director.streetEntryPlayerX = StreetEntryX;
            director.ownerPreparationTargetX = OwnerPreparationProgressX;
            director.ownerStreetStartX = StreetEntryX + 0.8f;
            director.ownerStreetEndX = 22.5f;
            director.parkEntryX = ParkEntryX;
            director.rollingBall = ball != null ? ball.gameObject : null;
        }

        ApplyPixelSnapperPropArt(root.transform);
    }

    private static void RefreshStreetBackdropAndFloor(GameObject root)
    {
        float floorWidth = StreetGroundRightX - StreetGroundLeftX;
        float floorCenterX = StreetGroundLeftX + floorWidth * 0.5f;
        Transform streetGround = root.transform.Find("Street Ground Extension");
        if (streetGround != null)
        {
            streetGround.position = new Vector3(floorCenterX, FloorCenterY,
                streetGround.position.z);
            ScaleSpriteTo(streetGround.GetComponent<SpriteRenderer>(), new Vector2(floorWidth, 1f));
            BoxCollider2D collider = streetGround.GetComponent<BoxCollider2D>();
            if (collider != null)
                collider.size = Vector2.one;
            SpriteRenderer groundRenderer = streetGround.GetComponent<SpriteRenderer>();
            if (groundRenderer != null)
                groundRenderer.enabled = false;
        }

        SpriteRenderer roomRenderer = FindRootObject(root.scene, "BackGround")
            ?.GetComponent<SpriteRenderer>();
        int sortingLayerId = roomRenderer != null ? roomRenderer.sortingLayerID : 0;
        ApplyPixelStreetBackgrounds(root.transform, sortingLayerId);

        for (int i = 0; i < 7; i++)
        {
            DisableRenderer(root.transform.Find("Street Lane Mark " + i));
        }
    }

    private static void SetWorldX(Transform item, float x)
    {
        if (item == null)
            return;
        Vector3 position = item.position;
        position.x = x;
        item.position = position;
    }

    private static void ScaleSpriteTo(SpriteRenderer renderer, Vector2 size)
    {
        if (renderer == null || renderer.sprite == null)
            return;
        Vector2 bounds = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(
            size.x / Mathf.Max(0.001f, bounds.x),
            size.y / Mathf.Max(0.001f, bounds.y),
            1f);
    }

    private static void ScaleSpriteToWorldSize(SpriteRenderer renderer, Vector2 worldSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;
        Vector2 bounds = renderer.sprite.bounds.size;
        Vector3 parentScale = renderer.transform.parent != null
            ? renderer.transform.parent.lossyScale
            : Vector3.one;
        renderer.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.001f, bounds.x * Mathf.Abs(parentScale.x)),
            worldSize.y / Mathf.Max(0.001f, bounds.y * Mathf.Abs(parentScale.y)),
            1f);
    }

    private static void ConfigureDust(ParticleSystem particles, int sortingLayerId)
    {
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.095f);
        main.startColor = new Color(0.78f, 0.65f, 0.46f, 1f);
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.28f, 0.16f, 0.1f);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = 10;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        GateReleaseParticleSetup.Apply(particles);
    }

    private static Sprite EnsureCircleSprite()
    {
        string absolutePath = Path.GetFullPath(CirclePath);
        string directory = Path.GetDirectoryName(absolutePath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        bool createdFile = false;
        if (!File.Exists(absolutePath))
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.47f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            createdFile = true;
        }

        TextureImporter importer = AssetImporter.GetAtPath(CirclePath) as TextureImporter;
        if (importer != null)
        {
            bool needsReimport = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 64f) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !importer.alphaIsTransparency;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            if (needsReimport)
                importer.SaveAndReimport();
        }
        else if (createdFile)
        {
            AssetDatabase.ImportAsset(CirclePath, ImportAssetOptions.ForceSynchronousImport);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(CirclePath);
        foreach (UnityEngine.Object importedAsset in importedAssets)
        {
            if (importedAsset is Sprite importedSprite)
                return importedSprite;
        }

        return null;
    }

    private static PhysicsMaterial2D FindZeroFrictionMaterial()
    {
        string[] guids = AssetDatabase.FindAssets("ZeroFriction t:PhysicsMaterial2D");
        if (guids.Length == 0)
            return null;
        return AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void SetPrivateFloat(UnityEngine.Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child.gameObject;
        }
        return null;
    }

    private static T FindRoot<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponent<T>();
            if (component != null)
                return component;
        }
        return null;
    }

    private static GameObject FindRootObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
                return roots[i];
        }
        return null;
    }
}
#endif
