using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FlowState { Menu, Playing, Paused, Won, Lost }

// Persistent (DontDestroyOnLoad, like GameState) owner of which phase the game is in and how many
// times the player has advanced. A reload is the only way this project resets a zone - BaseZone
// regenerates everything fresh in Start() - so every transition except Pause/Resume goes through
// one, same as Spawner's existing zone-cycling reload. DifficultyLevel survives the reload on this
// object and is read by BaseZone (via Scaled()) to size the next zone instance.
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance;

    public FlowState State { get; private set; } = FlowState.Menu;
    public int DifficultyLevel { get; private set; } = 0;

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
        Reload(FlowState.Playing);
    }

    // Same difficulty, fresh layout - retry doesn't punish or reward the player.
    public void RetryZone() => Reload(FlowState.Playing);

    public void NextLevel()
    {
        DifficultyLevel++;
        Reload(FlowState.Playing);
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
