using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class StoryFlowReviewV229
{
    [MenuItem("Tools/After the Glow/Review Full Story v2.29")]
    private static void Run() { Main(); }
    private static AfterTheGlowStoryDirector story;
    private static PlayerMovement player;
    private static PlayerInteraction interaction;
    private static readonly List<string> log = new List<string>();
    private static readonly HashSet<string> shots = new HashSet<string>();
    private static string folder;
    private static float started, lastTouch, readyAt = -1, gateClickAt = -1, petAt;
    private static int frame = -1, petStep;
    private static bool bedClicked, ownerReady, petDone, stayed, exitClicked, autoFollow, retreat, bite, pulled, snap;
    private static bool oldBackground;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static InputSettings.BackgroundBehavior oldInputBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    private static Key[] lastKeys = new Key[0];

    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        story = UnityEngine.Object.FindAnyObjectByType<AfterTheGlowStoryDirector>();
        player = story.player;
        interaction = player.GetComponent<PlayerInteraction>();
        folder = Path.GetFullPath("tmp/story-review-v2.29");
        Directory.CreateDirectory(folder);
        log.Clear(); shots.Clear(); checks.Clear();
        frame = -1; lastKeys = new Key[0]; lastTouch = petAt = 0f;
        readyAt = gateClickAt = -1f; petStep = 0;
        bedClicked = ownerReady = petDone = stayed = exitClicked = autoFollow = retreat = bite = pulled = snap = false;
        File.WriteAllText(Path.Combine(folder,"result.txt"), "RUNNING");
        oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        oldInputBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>("V229ReviewKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("V229ReviewMouse");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        started = Time.realtimeSinceStartup;
        var bedBounds = story.bed.GetComponent<Collider2D>().bounds;
        player.Body.position = new Vector2(bedBounds.max.x + player.BodyCollider.bounds.extents.x + .05f, -3.86f);
        player.transform.position = new Vector3(player.Body.position.x, player.Body.position.y, player.transform.position.z);
        Physics2D.SyncTransforms();
        player.FaceTowards(story.bed.transform.position.x);
        Log("Start: initial position beside bed; remaining flow uses gameplay controls and UI callbacks.");
        EditorApplication.update += Tick;
        return "Review started. Report: " + folder;
    }
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void Log(string value) { log.Add(Time.time.ToString("F2") + " " + value); File.WriteAllLines(Path.Combine(folder,"progress.txt"), log); }
    private static void Check(bool value, string message) { Log((value ? "PASS " : "FAIL ") + message); if (!value) throw new Exception(message); }
    private static void Keys(params Key[] keys)
    {
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        lastKeys = keys;
    }
    private static void Shot(string name)
    {
        if (shots.Add(name)) { ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png")); Log("SCREEN " + name); }
    }
    private static void MouseClick()
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width*.5f,Screen.height*.5f), buttons = 1 });
    }
    private static void MouseRelease() => InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width*.5f,Screen.height*.5f) });
    private static void Finish(bool pass, string reason)
    {
        EditorApplication.update -= Tick;
        Keys(); MouseRelease();
        Application.runInBackground = oldBackground;
        InputSystem.settings.backgroundBehavior = oldInputBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        Log("RESULT " + (pass?"PASS ":"FAIL ") + reason);
        File.WriteAllLines(Path.Combine(folder,"result.txt"),log);
    }
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) { Finish(false,"Play Mode stopped"); return; }
            if (Time.realtimeSinceStartup-started>130f) { Finish(false,"Timeout at " + story.Stage + " x="+player.transform.position.x); return; }
            if (frame==Time.frameCount) return;
            frame=Time.frameCount;
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            if (frame % 120 == 0) Log("STATE " + story.Stage + " x=" + player.transform.position.x.ToString("F2") + " mode=" + player.ControlMode);
            if (gateClickAt>=0 && Time.time-gateClickAt>.05f) MouseRelease();
            switch(story.Stage)
            {
                case AfterTheGlowStoryStage.WakeTheOwner:
                    if (!bedClicked && player.IsGrounded && Time.realtimeSinceStartup-started>.3f)
                    {
                        player.FaceTowards(story.bed.transform.position.x);
                        interaction.RefreshTarget(player.FacingDirection);
                        Check(interaction.CurrentTarget==story.bed,"Bed is click target");
                        bool hint=(bool)typeof(AfterTheGlowStoryDirector).GetMethod("CanShowBedInteractionHint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(story,null);
                        Check(hint,"Bed interaction hint available");
                        interaction.HandleInteractionClick(); bedClicked=true;
                    }
                    break;
                case AfterTheGlowStoryStage.BedWakePuzzle:
                    var wake=story.bedWakePuzzleUI;
                    var button=Field<Button>(wake,"touchButton");
                    if(button.interactable && Time.time-lastTouch>.65f)
                    {
                        Shot("wake-face-target-"+Field<int>(wake,"touchCount"));
                        button.onClick.Invoke();
                        button.onClick.Invoke();
                        Check(Field<Coroutine>(wake,"lickRoutine")!=null,"Wake tap starts lick; duplicate blocked");
                        lastTouch=Time.time;
                    }
                    break;
                case AfterTheGlowStoryStage.OwnerPreparing:
                    CheckOnce("wake",story.BedWakeCount==1,"Four face targets wake owner exactly once");
                    if(player.AllowsPlayerInput) Keys(player.transform.position.x<story.ownerPreparationTargetX+.12f?new[]{Key.D}:new Key[0]);
                    break;
                case AfterTheGlowStoryStage.OwnerAtTheDoor:
                    Keys();
                    if(Field<bool>(story,"waitingForPetPuzzleClick") && !ownerReady)
                    {
                        Check(player.ControlMode==PlayerControlMode.CutsceneLocked,"Pet start position locked");
                        Shot("pet-ready");
                        typeof(AfterTheGlowStoryDirector).GetMethod("HandleOwnerPetPointerEvent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(story,new object[]{new Event{type=EventType.MouseDown,button=0}});
                        ownerReady=true;
                    }
                    break;
                case AfterTheGlowStoryStage.OwnerPetPuzzle:
                    var pet=story.pettingPuzzleUI;
                    if(!pet.IsOpen) break;
                    if(Time.time-petAt<.17f) break;
                    petAt=Time.time;
                    var corners=new Vector3[4]; pet.PortraitRect.GetWorldCorners(corners);
                    Vector2 left=Vector3.Lerp(corners[0],corners[2],.28f), right=Vector3.Lerp(corners[0],corners[2],.72f);
                    left.y=right.y=(corners[0].y+corners[2].y)*.5f;
                    Vector2 p=petStep%2==0?left:right;
                    if(petStep==0) Shot("pet-strong-blur");
                    if(petStep<=7)
                    {
                        pet.HandleMouseEvent(new Event {type=petStep==0?EventType.MouseDown:EventType.MouseDrag,button=0,mousePosition=new Vector2(p.x,Screen.height-p.y)});
                        petStep++;
                    }
                    if(pet.Progress>=1 && !petDone)
                    {
                        pet.HandleMouseEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=new Vector2(p.x,Screen.height-p.y)});
                        Check(petStep>1,"Horizontal petting completes layered portrait"); petDone=true; Shot("pet-clear");
                    }
                    break;
                case AfterTheGlowStoryStage.HouseExitReady:
                    Keys();
                    if(readyAt<0) { readyAt=Time.time; Check(story.PetCount==1&&story.OwnerDepartureCount==1,"Petting and owner exit complete once"); }
                    if(Time.time-readyAt<1.2f) break;
                    if(!stayed) { Check(story.HouseExitCount==0&&player.AllowsPlayerInput,"Dog stays home until door selected"); stayed=true; }
                    var exit=story.frontDoorPivot.GetComponent<HouseExitInteraction>();
                    if(!exit.CanInteract(player)) {Keys(Key.D);break;}
                    if(!exitClicked)
                    {
                        var click=story.cameraFollow.GetComponent<Camera>().WorldToScreenPoint(exit.ClickBounds.center);
                        interaction.HandleInteractionClickAtScreenPosition(click); exitClicked=true;
                    }
                    break;
                case AfterTheGlowStoryStage.StreetDeparture:
                    Keys();
                    CheckOnce("observe",player.ControlMode==PlayerControlMode.CutsceneLocked,"Street owner observation locks player");
                    break;
                case AfterTheGlowStoryStage.FollowOwner:
                    Keys();
                    if(player.ControlMode==PlayerControlMode.AutoMoving) { autoFollow=true; Shot("street-auto-follow"); }
                    break;
                case AfterTheGlowStoryStage.HeadlightRetreat:
                    Keys();retreat=true;Shot("headlight-retreat"); break;
                case AfterTheGlowStoryStage.HeadlightWait:
                    Keys();CheckOnce("retreat",autoFollow&&retreat,"Automatic pursuit triggers forced retreat");break;
                case AfterTheGlowStoryStage.GateBound:
                    CheckOnce("light-off",story.HeadlightOffCount==1&&!story.carHeadlight.activeSelf,"Headlights switch off once");
                    if(interaction.CurrentTarget!=story.parkGate) { Keys(Key.D);break; }
                    Keys();
                    if(!bite) { interaction.HandleInteractionClick(); bite=true; Check(story.Stage==AfterTheGlowStoryStage.GateBitePull,"Gate click latches leash without opening closeup"); }
                    break;
                case AfterTheGlowStoryStage.GateBitePull:
                    Keys(Key.A); Shot("gate-bite-pull"); break;
                case AfterTheGlowStoryStage.GateMemoryPuzzle:
                    Keys();
                    if(!pulled) { Check(bite&&player.ControlMode==PlayerControlMode.CutsceneLocked,"Backward pull enters memory closeup"); pulled=true; }
                    if(story.GateMemoryAwaitingClick && Time.time-gateClickAt>.5f)
                    {
                        gateClickAt=Time.time; MouseClick();
                    }
                    int page=Field<int>(story,"gatePuzzlePage");
                    float elapsed=Time.time-Field<float>(story,"gatePuzzlePageTime");
                    if(page%2==1&&elapsed>.6f) Shot("memory-"+story.GateMemoryClickCount);
                    if(page==3&&elapsed>2.2f) Shot("memory-hand-withdrawn");
                    break;
                case AfterTheGlowStoryStage.GateReleased:
                case AfterTheGlowStoryStage.GatePushed:
                    Keys();
                    var ui=story.GetComponent<LeashBreakUI>();
                    if(ui!=null&&ui.IsPlaying) { snap=true; Shot("leash-snap"); }
                    if(story.Stage==AfterTheGlowStoryStage.GatePushed) Shot("gate-open");
                    break;
                case AfterTheGlowStoryStage.ParkPlaceholder:
                    Keys();
                    Check(story.GateMemoryClickCount==4,"Exactly four clicks play four memories");
                    Check(snap&&story.GateReleaseCount==1,"Animated torn leash releases once");
                    Check(story.GateOpenCount==1&&story.parkGate.GateOpenProgress>=1f,"Both gate leaves open once");
                    Check(!story.parkGate.GetComponent<Collider2D>().enabled,"Open gate no longer blocks dog");
                    Check(player.AllowsPlayerInput&&story.cameraFollow.enabled,"Park arrival restores controls and camera");
                    Shot("park-arrival");Finish(true,"Full flow completed at x="+player.transform.position.x);break;
            }
        }
        catch(Exception ex) { Finish(false,ex.ToString()); }
    }
    private static readonly HashSet<string> checks=new HashSet<string>();
    private static void CheckOnce(string id,bool value,string message) { if(checks.Add(id)) Check(value,message); }
}
