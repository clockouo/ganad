using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class OwnerPresentationSetup
{
    private const string Folder = "Assets/Art/PixelSnapper/Characters/";
    private const string ActionsTexture = Folder + "Owner_Reference_Actions.png";
    private const string WakePortraitTexture = "Assets/Art/AfterTheGlow/Puzzles/Puzzle01/OwnerWakeCloseup_Reference.png";
    private static readonly string[] OwnerSpriteNames =
    {
        "Owner_01_IdleFront", "Owner_02_IdleSide", "Owner_03_WalkA",
        "Owner_04_WalkB", "Owner_05_Pet", "Owner_06_Goodbye"
    };
    // Tight content rectangles, with a feet-centered pivot for stable ground contact.
    private static readonly Rect[] OwnerSpriteRects =
    {
        new Rect(18f, 93f, 25f, 70f),
        new Rect(74f, 93f, 21f, 71f),
        new Rect(124f, 93f, 32f, 70f),
        new Rect(13f, 9f, 34f, 72f),
        new Rect(66f, 9f, 43f, 67f),
        new Rect(125f, 9f, 33f, 72f)
    };
    private static readonly Vector2[] OwnerSpritePivots =
    {
        new Vector2(.507f, 0f), new Vector2(.476f, 0f), new Vector2(.438f, 0f),
        new Vector2(.447f, 0f), new Vector2(.245f, 0f), new Vector2(.397f, 0f)
    };

    [MenuItem("Tools/After the Glow/Refresh Owner Presentation")]
    private static void Refresh()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before changing owner art.");
        var root=GameObject.Find("AfterTheGlowStoryDemo");
        if (root==null) throw new MissingReferenceException("Open SampleScene first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureArtworkAssets();
        ApplyIfAvailable(root.transform);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        Debug.Log(UserOwnerAnimationSetup.HasSourceSheets
            ? "Owner animation strips applied: pet 5, sleep 6, walk 12; aligned feet and consistent pixel scale."
            : "Owner reference artwork applied: six poses, matching wake portrait, and feet-aligned sprite pivots.");
    }

    public static void EnsureArtworkAssets()
    {
        if (UserOwnerAnimationSetup.HasSourceSheets)
        {
            UserOwnerAnimationSetup.EnsureSprites();
            EnsureWakePortraitSprite();
            AssetDatabase.SaveAssets();
            return;
        }
        EnsureOwnerSpriteAssets();
        EnsureWakePortraitSprite();
        AssetDatabase.SaveAssets();
    }

    public static void ApplyIfAvailable(Transform root)
    {
        if (UserOwnerAnimationSetup.HasSourceSheets)
        {
            UserOwnerAnimationSetup.Apply(root);
            ApplyWakePortrait(root.gameObject.scene);
            return;
        }
        if (!File.Exists(ActionsTexture)) return;
        EnsureOwnerSpriteAssets();
        var story=root.GetComponent<AfterTheGlowStoryDirector>();
        if (story==null||story.owner==null) return;
        story.ownerWalkSprite=LoadOwnerSprite("Owner_03_WalkA");
        story.ownerPetSprite=LoadOwnerSprite("Owner_05_Pet");
        story.ownerStandingHeight=2.05f;story.ownerPetHeight=1.75f;
        story.ownerVisualGroundOffset=-0.24f;
        story.alignOwnerPetWithPlayer=true;story.ownerPetHandNormalized=new Vector2(.90f,.18f);
        var renderer=story.owner.GetComponentInChildren<SpriteRenderer>(true);
        renderer.sprite=story.ownerWalkSprite;renderer.flipX=false;renderer.sortingOrder=30;
        renderer.transform.localPosition=new Vector3(0f,story.ownerVisualGroundOffset,-.2f);
        float scale=story.ownerStandingHeight/story.ownerWalkSprite.bounds.size.y;
        Vector3 parent=renderer.transform.parent.lossyScale;
        renderer.transform.localScale=new Vector3(scale/Mathf.Abs(parent.x),scale/Mathf.Abs(parent.y),1f);
        EditorUtility.SetDirty(story);
        ApplyWakePortrait(root.gameObject.scene);
    }

    private static void EnsureOwnerSpriteAssets()
    {
        if (!File.Exists(ActionsTexture))
            throw new FileNotFoundException("Owner reference sprite sheet is missing.", ActionsTexture);

        TextureImporter importer=AssetImporter.GetAtPath(ActionsTexture) as TextureImporter;
        if (importer==null)
        {
            AssetDatabase.ImportAsset(ActionsTexture,ImportAssetOptions.ForceSynchronousImport);
            importer=AssetImporter.GetAtPath(ActionsTexture) as TextureImporter;
        }
        if (importer==null) throw new InvalidOperationException("Could not import owner sprite sheet: "+ActionsTexture);

        bool changed=importer.textureType!=TextureImporterType.Default || importer.filterMode!=FilterMode.Point ||
            importer.mipmapEnabled || !importer.isReadable || importer.textureCompression!=TextureImporterCompression.Uncompressed ||
            importer.npotScale!=TextureImporterNPOTScale.None || !importer.alphaIsTransparency;
        importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;
        importer.mipmapEnabled=false;importer.isReadable=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
        if (changed) importer.SaveAndReimport();

        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(ActionsTexture);
        if (texture==null) throw new InvalidOperationException("Owner sprite sheet did not load as a texture: "+ActionsTexture);
        for(int i=0;i<OwnerSpriteNames.Length;i++)
        {
            string path=Folder+OwnerSpriteNames[i]+".asset";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path)!=null) continue;
            Rect rect=OwnerSpriteRects[i];
            if (rect.xMin<0f||rect.yMin<0f||rect.xMax>texture.width||rect.yMax>texture.height)
                throw new InvalidOperationException("Owner sprite frame is outside the source image: "+OwnerSpriteNames[i]);
            Sprite sprite=Sprite.Create(texture,rect,OwnerSpritePivots[i],100f,0,SpriteMeshType.FullRect);
            sprite.name=OwnerSpriteNames[i];
            AssetDatabase.CreateAsset(sprite,path);
        }
    }

    private static Sprite LoadOwnerSprite(string name)
    {
        Sprite sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+name+".asset");
        if (sprite==null) throw new InvalidOperationException("Missing generated owner sprite: "+name);
        return sprite;
    }

    private static Sprite EnsureWakePortraitSprite()
    {
        if (!File.Exists(WakePortraitTexture)) return null;
        TextureImporter importer=AssetImporter.GetAtPath(WakePortraitTexture) as TextureImporter;
        if (importer==null)
        {
            AssetDatabase.ImportAsset(WakePortraitTexture,ImportAssetOptions.ForceSynchronousImport);
            importer=AssetImporter.GetAtPath(WakePortraitTexture) as TextureImporter;
        }
        if (importer==null) throw new InvalidOperationException("Could not import owner wake portrait: "+WakePortraitTexture);
        bool changed=importer.textureType!=TextureImporterType.Sprite || importer.spriteImportMode!=SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit,100f) || importer.filterMode!=FilterMode.Point ||
            importer.mipmapEnabled || importer.textureCompression!=TextureImporterCompression.Uncompressed || !importer.alphaIsTransparency;
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100f;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
        if (changed) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(WakePortraitTexture);
    }

    private static void ApplyWakePortrait(Scene scene)
    {
        Sprite portrait=EnsureWakePortraitSprite();
        if (portrait==null||!scene.IsValid()||!scene.isLoaded) return;
        foreach(GameObject sceneRoot in scene.GetRootGameObjects())
        {
            if (sceneRoot.name!="Bed Wake Puzzle Canvas") continue;
            Transform portraitTransform=sceneRoot.transform.Find("Wake Screen/Sleeping Owner Close-up");
            if (portraitTransform==null) return;
            Image image=portraitTransform.GetComponent<Image>();
            if (image==null) return;
            image.sprite=portrait;
            EditorUtility.SetDirty(image);
            return;
        }
    }

    [MenuItem("Tools/After the Glow/Preview Owner Pet Artwork")]
    private static void Preview()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Preview the saved artwork outside Play Mode.");
        var root=GameObject.Find("AfterTheGlowStoryDemo");
        var story=root.GetComponent<AfterTheGlowStoryDirector>();
        var camera=story.cameraFollow.GetComponent<Camera>();
        var renderer=story.owner.GetComponentInChildren<SpriteRenderer>(true);
        Vector3 ownerPosition=story.owner.position,ownerScale=story.owner.localScale;
        Vector3 playerPosition=story.player.transform.position;
        Vector3 visualPosition=renderer.transform.localPosition,visualScale=renderer.transform.localScale;
        Sprite oldSprite=renderer.sprite;bool oldFlip=renderer.flipX,oldActive=story.owner.gameObject.activeSelf;
        RenderTexture oldTarget=camera.targetTexture,oldRenderActive=RenderTexture.active;
        var target=new RenderTexture(1920,1080,24);
        var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        try
        {
            story.owner.gameObject.SetActive(true);story.owner.localScale=Vector3.one;
            story.owner.position=new Vector3(story.OwnerDoorStandX,-3.86f,ownerPosition.z);
            story.player.transform.position=new Vector3(story.OwnerPetPlayerTargetX,-3.86f,playerPosition.z);
            renderer.sprite=story.ownerPetSprite;renderer.flipX=true;
            renderer.transform.localPosition=new Vector3(0,story.ownerVisualGroundOffset,-.2f);
            float scale=story.ownerPetHeight/story.ownerPetSprite.bounds.size.y;
            renderer.transform.localScale=new Vector3(scale,scale,1f);
            Physics2D.SyncTransforms();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.document_work/2026-10-15-owner-revision"));
            Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"owner-pet-scene-preview.png"),image.EncodeToPNG());
            Debug.Log("Owner artwork scene preview saved: "+dir+"; dog target X="+story.OwnerPetPlayerTargetX);
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=oldRenderActive;
            story.owner.position=ownerPosition;story.owner.localScale=ownerScale;
            story.player.transform.position=playerPosition;
            renderer.sprite=oldSprite;renderer.flipX=oldFlip;
            renderer.transform.localPosition=visualPosition;renderer.transform.localScale=visualScale;
            story.owner.gameObject.SetActive(oldActive);Physics2D.SyncTransforms();
            Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);
            EditorSceneManager.SaveScene(root.scene);
        }
    }
}
