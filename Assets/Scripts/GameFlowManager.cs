using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FlowState { Menu, Playing, Paused, Won, Lost }

// Persistent (DontDestroyOnLoad, like GameState) owner of which phase the game is in, how many
// times the player has advanced, and which zone is active. A reload is the only way this project
// resets a zone - BaseZone regenerates everything fresh in Start() - so every transition except
// Pause/Resume goes through one. DifficultyLevel survives the reload and is read by BaseZone (via
// Scaled()) to size the next zone instance. CurrentZoneIndex survives it too and is read by Spawner
// to pick which zone to activate - this is the sole owner of it now; Spawner only ever reads it.
//
// Zone selection is a shuffle-bag: Spawner reports its zone count via RegisterZoneCount every
// Start() (so it's always current before any advance can fire), and each advance draws a random
// index out of the "not yet seen this cycle" pool rather than incrementing sequentially - that
// pool refills once it's exhausted, which is what keeps every zone appearing once before any
// repeat while still making immediate back-to-back repeats unlikely.
public class GameFlowManager : MonoBehaviour
{
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

        // Reload-triggered transitions (Start/Retry/Next Level/Menu) deliberately don't broadcast
        // OnStateChanged themselves mid-Reload, before SceneManager.LoadScene runs - that reload
        // briefly re-instantiates a duplicate of this very GameObject (which self-destroys via the
        // check above), and Unity doesn't strictly guarantee a destroyed-this-frame object's
        // sibling components never get a stray OnEnable/Start first, racing the real listener.
        // Broadcasting only once the new scene has fully finished loading sidesteps that entirely.
        SceneManager.sceneLoaded += (_, _) => OnStateChanged?.Invoke(State);
    }

    // Convenience Escape toggle so Pause works the moment this component exists, even before a
    // dedicated Pause button gets wired up.
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

    // Same zone, fresh layout. Difficulty resets too - a retry follows a loss, so it shouldn't
    // carry the failed difficulty into the next attempt.
    public void RetryZone()
    {
        DifficultyLevel = 0;
        Reload(FlowState.Playing);
    }

    // Win path: harder, and a fresh random zone.
    public void NextLevel()
    {
        DifficultyLevel++;
        PickNextZone();
        Reload(FlowState.Playing);
    }

    // Dev shortcut for cycling zones without going through a win (Spawner's R-key binding) -
    // draws from the same shuffle bag NextLevel uses, just without the difficulty bump.
    public void CycleZone()
    {
        PickNextZone();
        Reload(FlowState.Playing);
    }

    // Called by Spawner every Start() so the count used here always matches what's actually in
    // the scene, even if it changes between edits.
    public void RegisterZoneCount(int count)
    {
        zoneCount = count;
    }

    void PickNextZone()
    {
        if (zoneCount <= 0) return;

        // Bag empty (first pick, or every zone has come up once this cycle) - refill and start over.
        if (unseenZones.Count == 0)
        {
            for (int i = 0; i < zoneCount; i++) unseenZones.Add(i);
        }

        int pick = unseenZones[UnityEngine.Random.Range(0, unseenZones.Count)];
        unseenZones.Remove(pick);
        CurrentZoneIndex = pick;
    }

    public void ReturnToMenu() => Reload(FlowState.Menu);

    // In-place, not a reload - the whole point of Pause is that the board is exactly as the player
    // left it once they Resume, not regenerated. No duplicate-object race here (no scene load
    // involved), so this can safely broadcast immediately.
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

    // Called by BaseZone once its own win/lose check trips. Guarded to Playing only, so a zone
    // whose Update() still runs for a frame after the outcome already landed can't fire twice.
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

    void Reload(FlowState nextState)
    {
        // Belt-and-braces: any reload should resume normal time, in case it was triggered (Retry,
        // Next Level, Menu) from the pause screen while Time.timeScale was still 0.
        Time.timeScale = 1f;
        // Sets State directly rather than through SetState - the sceneLoaded handler above is what
        // broadcasts this one, once the reload actually settles.
        State = nextState;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void SetState(FlowState state)
    {
        State = state;
        OnStateChanged?.Invoke(state);
    }
}
