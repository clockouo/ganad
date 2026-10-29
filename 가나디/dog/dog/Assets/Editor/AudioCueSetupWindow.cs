using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Lets non-programmers assign clips to the story's existing audio situations.</summary>
public sealed class AudioCueSetupWindow : EditorWindow
{
    private const string CatalogPath = "Assets/Resources/Audio/GameAudioCatalog.asset";
    private const string ImportFolder = "Assets/Resources/Audio/UserAdded";
    private GameAudioCueCatalog catalog;

    private struct DefaultCue
    {
        public string key;
        public string situation;
        public GameAudioBus bus;

        public DefaultCue(string key, string situation, GameAudioBus bus)
        {
            this.key = key;
            this.situation = situation;
            this.bus = bus;
        }
    }

    private static readonly DefaultCue[] Defaults =
    {
        new DefaultCue("AtmosphericEmotionalPiano", "전체 장면 배경 음악", GameAudioBus.Background),
        new DefaultCue("RoomTone", "집 안 환경음", GameAudioBus.Background),
        new DefaultCue("ParkBirds", "공원에 들어왔을 때 환경음", GameAudioBus.Background),
        new DefaultCue("StreetTraffic", "길 장면 환경음", GameAudioBus.Background),
        new DefaultCue("DieselIdle", "길에서 들리는 자동차 공회전", GameAudioBus.Background),
        new DefaultCue("BedBlanket", "침대에서 움직일 때", GameAudioBus.Effects),
        new DefaultCue("DogSniff", "강아지가 냄새를 맡을 때", GameAudioBus.Effects),
        new DefaultCue("LightSwitchOn", "벽 스위치를 켤 때", GameAudioBus.Effects),
        new DefaultCue("LightSwitchOff", "벽 스위치를 끌 때", GameAudioBus.Effects),
        new DefaultCue("GateRattle", "철문을 흔들 때", GameAudioBus.Effects),
        new DefaultCue("GateClank", "철문 걸쇠가 움직일 때", GameAudioBus.Effects),
        new DefaultCue("IronDoor", "철문이 열릴 때", GameAudioBus.Effects),
        new DefaultCue("WoodSteps", "나무 바닥을 걸을 때", GameAudioBus.Effects),
        new DefaultCue("ClothRubbing", "쓰다듬기 퍼즐에서 손을 움직일 때", GameAudioBus.Effects),
        new DefaultCue("BouncyBall", "공이 굴러가기 시작할 때", GameAudioBus.Effects)
    };

    [MenuItem("Tools/After the Glow/Audio Connections")]
    public static void Open()
    {
        var window = GetWindow<AudioCueSetupWindow>();
        window.titleContent = new GUIContent("Audio Connections");
        window.minSize = new Vector2(510f, 560f);
        window.Show();
    }

    private void OnEnable() => EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    private void OnDisable() => EditorApplication.playModeStateChanged -= HandlePlayModeChanged;

    private void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (rootVisualElement != null)
            CreateGUI();
    }

    public void CreateGUI()
    {
        catalog = LoadOrCreateCatalog();
        rootVisualElement.Clear();
        rootVisualElement.style.paddingLeft = 12;
        rootVisualElement.style.paddingRight = 12;
        rootVisualElement.style.paddingTop = 10;
        rootVisualElement.style.paddingBottom = 10;

        rootVisualElement.Add(new Label("상황별 오디오 연결")
        {
            style = { fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
        });
        rootVisualElement.Add(new Label("각 상황 칸에 AudioClip을 끌어 놓으세요. 외부 파일은 ‘가져오기’로 프로젝트에 복사하면서 바로 연결할 수 있습니다.")
        {
            style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8 }
        });

        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.marginBottom = 8;
        var folderButton = new Button(OpenAudioFolder) { text = "오디오 폴더 열기" };
        toolbar.Add(folderButton);
        var catalogButton = new Button(() => Selection.activeObject = catalog) { text = "연결표 선택" };
        toolbar.Add(catalogButton);
        rootVisualElement.Add(toolbar);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        rootVisualElement.Add(scroll);
        AddGroup(scroll, "배경 음악과 환경음", GameAudioBus.Background);
        AddGroup(scroll, "효과음", GameAudioBus.Effects);

        rootVisualElement.Add(new HelpBox(
            "연결을 바꾸면 즉시 저장됩니다. 실행 중 소리를 확인하려면 Play 모드에서 각 행의 ‘재생’을 누르세요. 새 장면 상황을 추가하려면 먼저 그 장면에 재생 이벤트를 연결해야 합니다.",
            HelpBoxMessageType.Info));
    }

    private void AddGroup(VisualElement parent, string title, GameAudioBus bus)
    {
        var group = new VisualElement();
        group.style.marginBottom = 10;
        var heading = new Label(title);
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        heading.style.fontSize = 14;
        heading.style.marginTop = 6;
        heading.style.marginBottom = 5;
        group.Add(heading);

        foreach (DefaultCue definition in Defaults)
        {
            if (definition.bus != bus) continue;
            GameAudioCueCatalog.Cue cue = FindOrAddCue(definition);
            group.Add(BuildCueRow(cue));
        }

        parent.Add(group);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private VisualElement BuildCueRow(GameAudioCueCatalog.Cue cue)
    {
        var row = new VisualElement();
        row.style.paddingLeft = 8;
        row.style.paddingRight = 8;
        row.style.paddingTop = 5;
        row.style.paddingBottom = 6;
        row.style.marginBottom = 4;
        row.style.backgroundColor = new Color(.16f, .17f, .18f);

        var situation = new Label(cue.situation);
        situation.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(situation);

        var controls = new VisualElement();
        controls.style.flexDirection = FlexDirection.Row;
        controls.style.alignItems = Align.Center;
        var clipField = new ObjectField("오디오")
        {
            objectType = typeof(AudioClip),
            allowSceneObjects = false,
            value = cue.clip
        };
        clipField.style.flexGrow = 1;
        clipField.RegisterValueChangedCallback(evt => Assign(cue, evt.newValue as AudioClip));
        controls.Add(clipField);

        var importButton = new Button(() => ImportAndAssign(cue, clipField)) { text = "가져오기" };
        importButton.tooltip = "컴퓨터의 오디오 파일을 프로젝트에 복사하고 이 상황에 연결합니다.";
        controls.Add(importButton);

        var playButton = new Button(() => PlayCue(cue)) { text = "재생" };
        playButton.SetEnabled(EditorApplication.isPlaying);
        playButton.tooltip = "Play 모드에서 이 오디오를 한 번 재생합니다.";
        controls.Add(playButton);
        row.Add(controls);

        row.Add(new Label("상황 키: " + cue.key)
        {
            style = { fontSize = 10, color = new Color(.68f, .70f, .72f) }
        });
        return row;
    }

    private GameAudioCueCatalog.Cue FindOrAddCue(DefaultCue definition)
    {
        foreach (GameAudioCueCatalog.Cue cue in catalog.Cues)
            if (cue != null && cue.key == definition.key)
                return cue;

        var created = new GameAudioCueCatalog.Cue
        {
            key = definition.key,
            situation = definition.situation,
            bus = definition.bus
        };
        catalog.Cues.Add(created);
        return created;
    }

    private void Assign(GameAudioCueCatalog.Cue cue, AudioClip clip)
    {
        Undo.RecordObject(catalog, "Assign story audio");
        cue.clip = clip;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private void ImportAndAssign(GameAudioCueCatalog.Cue cue, ObjectField clipField)
    {
        string source = EditorUtility.OpenFilePanel("가져올 오디오 선택", "", "wav,mp3,ogg,aiff,aif");
        if (string.IsNullOrEmpty(source)) return;

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Audio/UserAdded"));
        string destination = AssetDatabase.GenerateUniqueAssetPath(
            ImportFolder + "/" + Path.GetFileName(source));
        File.Copy(source, destination, false);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
        if (clip == null)
        {
            EditorUtility.DisplayDialog("오디오를 가져오지 못했습니다", "Unity가 이 파일을 AudioClip으로 읽지 못했습니다.", "확인");
            return;
        }

        Assign(cue, clip);
        clipField.SetValueWithoutNotify(clip);
    }

    private static void PlayCue(GameAudioCueCatalog.Cue cue)
    {
        GameAudioManager manager = GameAudioManager.Instance;
        if (manager == null || cue.clip == null) return;
        manager.PlayClip(cue.clip, cue.bus);
    }

    private static void OpenAudioFolder()
    {
        string folder = Path.Combine(Application.dataPath, "Resources/Audio");
        Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }

    private static GameAudioCueCatalog LoadOrCreateCatalog()
    {
        GameAudioCueCatalog existing = AssetDatabase.LoadAssetAtPath<GameAudioCueCatalog>(CatalogPath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Audio"));
        AssetDatabase.Refresh();
        var created = CreateInstance<GameAudioCueCatalog>();
        foreach (DefaultCue definition in Defaults)
        {
            created.Cues.Add(new GameAudioCueCatalog.Cue
            {
                key = definition.key,
                situation = definition.situation,
                bus = definition.bus
            });
        }
        AssetDatabase.CreateAsset(created, CatalogPath);
        AssetDatabase.SaveAssets();
        return created;
    }
}
