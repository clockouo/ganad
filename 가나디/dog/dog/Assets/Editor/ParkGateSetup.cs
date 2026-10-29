using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ParkGateSetup
{
    private const string Props = "Assets/Art/PixelSnapper/Props/";
    private const float Floor = -3.86f;
    private const float PixelScale = 9f;

    [MenuItem("Tools/After the Glow/Refresh Hinged Park Gate")]
    private static void Refresh()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var root=GameObject.Find("AfterTheGlowStoryDemo");
        if(root==null)throw new MissingReferenceException("Open SampleScene first.");
        NormalizePair("Gate_Open_Double");
        NormalizePair("Gate_Closed_Double");
        ApplyIfAvailable(root.transform);
        ReferenceStreetLayout.Apply(root.transform);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        Debug.Log("Park gate fixed in place: pull the leash, push the left leaf open, walk through.");
    }

    public static void ApplyIfAvailable(Transform storyRoot)
    {
        var openTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Props+"Gate_Open_Double.png");
        if(openTexture==null)return;
        var story=storyRoot.GetComponent<AfterTheGlowStoryDirector>();
        var gate=story.parkGate.transform;
        var closed=AssetDatabase.LoadAssetAtPath<Texture2D>(Props+"Gate_LeashBound.png");
        var releasedTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Props+"Gate_Closed_Double.png");
        if(releasedTexture==null)return;
        var fullOld=gate.Find("Gate Artwork").GetComponent<SpriteRenderer>();
        int layer=fullOld.sortingLayerID;fullOld.enabled=false;
        gate.position=new Vector3(31.2f,Floor,-.1f);gate.localScale=Vector3.one;
        float origin=46.5f;
        Rect left=new Rect(8,16,14,53),right=new Rect(70,16,15,53),panel=new Rect(22,16,48,42);
        var leftArt=Child(gate,"Fixed Left Gate Post",SpriteAsset("Gate_Fixed_Left_Post",closed,left,new Vector2(0,0)),layer);
        var rightArt=Child(gate,"Fixed Right Gate Post",SpriteAsset("Gate_Fixed_Right_Post",closed,right,new Vector2(0,0)),layer);
        leftArt.transform.localPosition=new Vector3((left.x-origin)/100f*PixelScale,0,-.15f);
        rightArt.transform.localPosition=new Vector3((right.x-origin)/100f*PixelScale,0,-.15f);
        leftArt.transform.localScale=rightArt.transform.localScale=new Vector3(PixelScale,PixelScale,1f);
        Rect leftPanel=new Rect(22,16,24,42),rightPanel=new Rect(46,16,24,42);
        Sprite boundLeft=SpriteAsset("Gate_Double_Closed_Left",closed,leftPanel,new Vector2(0,0));
        Sprite boundRight=SpriteAsset("Gate_Double_Closed_Right",closed,rightPanel,new Vector2(1,0));
        Sprite releasedLeft=SpriteAsset("Gate_Closed_Double_Left",releasedTexture,
            Tight(releasedTexture,new Rect(0,0,releasedTexture.width/2,releasedTexture.height)),new Vector2(0,0));
        Sprite releasedRight=SpriteAsset("Gate_Closed_Double_Right",releasedTexture,
            Tight(releasedTexture,new Rect(releasedTexture.width/2,0,releasedTexture.width/2,releasedTexture.height)),new Vector2(1,0));
        Rect openLeftRect=Tight(openTexture,new Rect(0,0,openTexture.width/2,openTexture.height));
        Rect openRightRect=Tight(openTexture,new Rect(openTexture.width/2,0,openTexture.width/2,openTexture.height));
        Sprite openedLeft=SpriteAsset("Gate_Open_Double_Left",openTexture,openLeftRect,new Vector2(0,0));
        Sprite openedRight=SpriteAsset("Gate_Open_Double_Right",openTexture,openRightRect,new Vector2(1,0));
        var leftLeaf=Child(gate,"Left Hinged Gate Leaf",boundLeft,layer);
        var rightLeaf=Child(gate,"Right Hinged Gate Leaf",boundRight,layer);
        // A bottom outer pivot keeps each leaf attached to its own stationary post.
        leftLeaf.transform.localPosition=new Vector3((leftPanel.x-origin)/100f*PixelScale,0,-.2f);
        rightLeaf.transform.localPosition=new Vector3((rightPanel.xMax-origin)/100f*PixelScale,0,-.2f);
        var view=gate.GetComponent<GateHingePresentation>();
        if(view==null)view=gate.gameObject.AddComponent<GateHingePresentation>();
        view.leftLeaf=leftLeaf;view.rightLeaf=rightLeaf;
        view.boundLeftSprite=boundLeft;view.boundRightSprite=boundRight;
        view.releasedLeftSprite=releasedLeft;view.releasedRightSprite=releasedRight;
        view.openedLeftSprite=openedLeft;view.openedRightSprite=openedRight;
        view.closedMaterial=rightLeaf.sharedMaterial;
        view.closedSortingLayerID=layer;view.closedSortingOrder=14;
        view.opensIntoGarden=true;
        view.closedScale=new Vector3(PixelScale,PixelScale,1f);
        view.openedWidthFraction=.4f;
        float closedWidth=leftPanel.width/100f*PixelScale;
        float width=closedWidth*view.openedWidthFraction;
        float height=panel.height/100f*PixelScale;
        view.releasedLeftScale=new Vector3(closedWidth/releasedLeft.bounds.size.x,height/releasedLeft.bounds.size.y,1f);
        view.releasedRightScale=new Vector3(closedWidth/releasedRight.bounds.size.x,height/releasedRight.bounds.size.y,1f);
        view.openedLeftScale=new Vector3(width/openedLeft.bounds.size.x,height/openedLeft.bounds.size.y,1f);
        view.openedRightScale=new Vector3(width/openedRight.bounds.size.x,height/openedRight.bounds.size.y,1f);
        view.ResetPose();
        var collider=gate.GetComponent<BoxCollider2D>();
        collider.size=new Vector2(77f/100f*PixelScale,panel.height/100f*PixelScale);
        collider.offset=new Vector2((panel.center.x-origin)/100f*PixelScale,collider.size.y*.5f);
        collider.isTrigger=false;collider.enabled=true;
        var tie=gate.Find("Leash Tie Point");tie.localPosition=new Vector3(.08f,.58f,-.3f);
        var dust=gate.Find("Pixel Dust on Leash Release");if(dust!=null)dust.localPosition=tie.localPosition;
        story.parkGate.ConfigureHingedGate(view);story.gateOpenSprite=openedLeft;
        EditorUtility.SetDirty(story.parkGate);EditorUtility.SetDirty(view);EditorUtility.SetDirty(story);
    }

    private static void NormalizePair(string name)
    {
        if(!File.Exists(Props+name+".png"))
        {
            var raw=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(raw,File.ReadAllBytes(Props+name+"_Source.png")))throw new IOException("Gate open source.");
            var pixels=new Texture2D(90,69,TextureFormat.RGBA32,false);
            for(int y=0;y<69;y++)for(int x=0;x<90;x++)
            {
                Color c=raw.GetPixel(Mathf.Min(raw.width-1,Mathf.FloorToInt((x+.5f)*raw.width/90f)),
                    Mathf.Min(raw.height-1,Mathf.FloorToInt((y+.5f)*raw.height/69f)));
                if(c.a<.75f)c=Color.clear;else c.a=1f;
                pixels.SetPixel(x,y,c);
            }
            pixels.Apply();File.WriteAllBytes(Props+name+".png",pixels.EncodeToPNG());
            Object.DestroyImmediate(raw);Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(Props+name+".png",ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(Props+name+".png");
            importer.textureType=TextureImporterType.Default;importer.isReadable=true;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
        }
    }

    private static SpriteRenderer Child(Transform root,string name,Sprite sprite,int layer)
    {
        var child=root.Find(name);
        if(child==null){var item=new GameObject(name);item.transform.SetParent(root,false);child=item.transform;}
        var renderer=child.GetComponent<SpriteRenderer>();if(renderer==null)renderer=child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite=sprite;renderer.color=Color.white;renderer.enabled=true;renderer.sortingLayerID=layer;renderer.sortingOrder=14;
        renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        return renderer;
    }

    private static Sprite SpriteAsset(string name,Texture2D texture,Rect rect,Vector2 pivot)
    {
        string path=Props+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null)return existing;
        var sprite=Sprite.Create(texture,rect,pivot,100,0,SpriteMeshType.FullRect);sprite.name=name;
        AssetDatabase.CreateAsset(sprite,path);return sprite;
    }

    private static Rect Tight(Texture2D t,Rect area)
    {
        int minX=(int)area.xMax,minY=(int)area.yMax,maxX=-1,maxY=-1;
        for(int y=(int)area.y;y<area.yMax;y++)for(int x=(int)area.x;x<area.xMax;x++)
            if(t.GetPixel(x,y).a>.75f){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
        if(maxX<minX)throw new InvalidOperationException("Empty open gate leaf.");
        return new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
    }

    [MenuItem("Tools/After the Glow/Preview Park Gate Artwork")]
    private static void Preview()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Preview artwork outside Play Mode.");
        var root=GameObject.Find("AfterTheGlowStoryDemo");var story=root.GetComponent<AfterTheGlowStoryDirector>();
        var view=story.parkGate.GetComponent<GateHingePresentation>();
        var camera=story.cameraFollow.GetComponent<Camera>();
        Vector3 position=camera.transform.position;float size=camera.orthographicSize;
        RenderTexture previous=camera.targetTexture,active=RenderTexture.active;
        var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
        try
        {
            camera.transform.position=new Vector3(31.2f,Floor+1.65f,-10);camera.orthographicSize=3.1f;camera.targetTexture=target;
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.document_work/2026-10-15-double-gate-revision"));
            Directory.CreateDirectory(dir);
            for(int i=0;i<4;i++)
            {
                view.SetReleased(i>0);view.SetOpenProgress(Mathf.Max(0,(i-1)*.5f));camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(dir,i==0?"park-gate-closed-preview.png":i==1?"park-gate-unbound-preview.png":i==2?"park-gate-half-open-preview.png":"park-gate-open-preview.png"),pixels.EncodeToPNG());
            }
            Debug.Log("Double gate artwork previews saved: "+dir);
        }
        finally
        {
            view.ResetPose();camera.transform.position=position;camera.orthographicSize=size;
            camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            EditorSceneManager.SaveScene(root.scene);
        }
    }
}
