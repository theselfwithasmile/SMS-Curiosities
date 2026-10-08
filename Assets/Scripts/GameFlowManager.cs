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

    int zoneCount = 0;
    readonly List<int> unseenZones = new List<int>();

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
        if (State == FlowState.Playing) SpawnChatbox();
    }
    
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (State == FlowState.Playing) PauseGame();
        else if (State == FlowState.Paused) ResumeGame();
    }

    public void StartGame()
    {
        DifficultyLevel = 0;
        PickNextZone();
        Reload(FlowState.Playing);
    }
    
    public void RetryZone()
    {
        DifficultyLevel = 0;
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
        PickNextZone();
        Reload(FlowState.Playing);
    }
    
    public void RegisterZoneCount(int count)
    {
        zoneCount = count;
    }

    void PickNextZone()
    {
        if (zoneCount <= 0) return;

        //first pick, or every zone has come up once this cycle
        if (unseenZones.Count == 0)
        {
            for (int i = 0; i < zoneCount; i++) unseenZones.Add(i);
        }

        int pick = unseenZones[UnityEngine.Random.Range(0, unseenZones.Count)];
        unseenZones.Remove(pick);
        CurrentZoneIndex = pick;
    }

    public void ReturnToMenu() => Reload(FlowState.Menu);
    
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
        SetState(FlowState.Won);
    }

    public void ReportLose()
    {
        if (State != FlowState.Playing) return;
        SetState(FlowState.Lost);
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
