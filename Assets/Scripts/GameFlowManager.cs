using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FlowState { Menu, Playing, Paused, Won, Lost }

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private Transform chatboxPrefab;
    
    public static GameFlowManager Instance;

    public FlowState State { get; private set; } = FlowState.Menu;
    public int DifficultyLevel { get; private set; } = 0;
    public int CurrentZoneIndex { get; private set; } = 0;

    readonly List<string> zoneNames = new List<string>();
    readonly List<int> unseenZones = new List<int>();

    //analytics bookkeeping: a run is one unbroken climb from difficulty 0, since Retry resets difficulty
    int attempt = 1;
    float levelTime;
    bool runActive;
    int levelsCleared;

    string Genre => CurrentZoneIndex < zoneNames.Count ? zoneNames[CurrentZoneIndex] : "unknown";

    public event Action<FlowState> OnStateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Analytics.Initialize();

        //broadcasting only once the new scene has fully finished loading
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    //sceneLoaded is static, so with domain reload disabled a handler from a previous play session would
    //otherwise stay subscribed and fire again on every load
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        OnStateChanged?.Invoke(State);
        if (State != FlowState.Playing) return;

        SpawnChatbox();
        levelTime = 0f;
        Analytics.LogEvent("level_start", LevelParams());
    }

    void Update()
    {
        //scaled time, so pausing (timeScale 0) doesn't count toward level duration
        if (State == FlowState.Playing) levelTime += Time.deltaTime;

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (State == FlowState.Playing) PauseGame();
        else if (State == FlowState.Paused) ResumeGame();
    }

    public void StartGame()
    {
        DifficultyLevel = 0;
        PickNextZone();
        BeginRun();
        Reload(FlowState.Playing);
    }

    public void RetryZone()
    {
        DifficultyLevel = 0;
        attempt++;
        BeginRun();
        Reload(FlowState.Playing);
    }

    public void NextLevel()
    {
        DifficultyLevel++;
        PickNextZone();
        Reload(FlowState.Playing);
    }

    //dev build shortcut for cycling zones without going through a win
    public void CycleZone()
    {
        LogQuitIfMidLevel("skip");
        PickNextZone();
        Reload(FlowState.Playing);
    }

    public void RegisterZones(List<string> names)
    {
        zoneNames.Clear();
        zoneNames.AddRange(names);
    }

    void PickNextZone()
    {
        if (zoneNames.Count <= 0) return;

        //first pick, or every zone has come up once this cycle
        if (unseenZones.Count == 0)
        {
            for (int i = 0; i < zoneNames.Count; i++) unseenZones.Add(i);
        }

        int pick = unseenZones[UnityEngine.Random.Range(0, unseenZones.Count)];
        unseenZones.Remove(pick);
        CurrentZoneIndex = pick;
        attempt = 1;
    }

    public void ReturnToMenu()
    {
        LogQuitIfMidLevel("menu");
        EndRun();
        Reload(FlowState.Menu);
    }
    
    public void PauseGame()
    {
        if (State != FlowState.Playing) return;
        Time.timeScale = 0f;
        SetState(FlowState.Paused);
    }

    public void ResumeGame()
    {
        if (State != FlowState.Paused) return;
        Time.timeScale = 1f;
        SetState(FlowState.Playing);
    }
    
    public void ReportWin()
    {
        if (State != FlowState.Playing) return;
        levelsCleared++;
        LogLevelEnd(true);
        SetState(FlowState.Won);
    }

    public void ReportLose()
    {
        if (State != FlowState.Playing) return;
        LogLevelEnd(false);
        //Retry restarts from difficulty 0, so a loss always ends the run
        EndRun();
        SetState(FlowState.Lost);
    }

    (string, object)[] LevelParams() => new (string, object)[]
    {
        ("level_name", $"{Genre}_{DifficultyLevel}"),
        ("genre", Genre),
        ("difficulty", DifficultyLevel),
        ("attempt", attempt),
    };

    void LogLevelEnd(bool success)
    {
        var p = new List<(string, object)>(LevelParams()) { ("success", success), ("duration_sec", Mathf.RoundToInt(levelTime)) };
        Analytics.LogEvent("level_end", p.ToArray());
    }

    void LogQuitIfMidLevel(string reason)
    {
        if (State != FlowState.Playing && State != FlowState.Paused) return;
        var p = new List<(string, object)>(LevelParams()) { ("reason", reason), ("duration_sec", Mathf.RoundToInt(levelTime)) };
        Analytics.LogEvent("level_quit", p.ToArray());
    }

    void BeginRun()
    {
        runActive = true;
        levelsCleared = 0;
    }

    void EndRun()
    {
        if (!runActive) return;
        runActive = false;
        Analytics.LogEvent("run_end", ("levels_cleared", levelsCleared), ("max_difficulty", DifficultyLevel), ("last_genre", Genre));
    }

    void SpawnChatbox()
    {
        Transform chatbox = Instantiate(chatboxPrefab, Grid.Instance.Board);
        //Message (the zone's visualParent) is authored in the scene under Board, so it already exists here;
        //first sibling keeps the chatbox ahead of it in the vertical layout order
        chatbox.SetAsFirstSibling();
        TweenRunner.Instance.GrowIn(chatbox);
    }

    void Reload(FlowState nextState)
    {
        Time.timeScale = 1f;
        State = nextState;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void SetState(FlowState state)
    {
        State = state;
        OnStateChanged?.Invoke(state);
    }
}
