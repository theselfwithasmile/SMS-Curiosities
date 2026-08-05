using UnityEngine;

// Minimal, unstyled state -> panel-visibility wiring for the four flow screens. Assign the panel
// GameObjects in the Inspector and hook each button's OnClick to the matching On*Clicked method -
// no layout, styling, or transitions here on purpose; this only has to prove the flow works end to
// end (Menu -> Playing -> Paused -> Playing, Won/Lost -> Retry/Next Level) so the actual screens
// can be built on top.
public class GameFlowUI : MonoBehaviour
{
    [SerializeField] GameObject menuPanel;
    [SerializeField] GameObject inGamePanel;
    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;

    void OnEnable()
    {
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.OnStateChanged += Refresh;
    }

    void OnDisable()
    {
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.OnStateChanged -= Refresh;
    }

    // Covers the very first frame this object exists, before the manager's own sceneLoaded
    // subscription (see GameFlowManager.Awake) has had a chance to broadcast anything - every
    // transition after that, including reload-based ones, comes through OnStateChanged instead.
    void Start() => Refresh(GameFlowManager.Instance != null ? GameFlowManager.Instance.State : FlowState.Menu);

    void Refresh(FlowState state)
    {
        if (menuPanel != null) menuPanel.SetActive(state == FlowState.Menu);
        if (inGamePanel != null) inGamePanel.SetActive(state == FlowState.Playing);
        if (pausePanel != null) pausePanel.SetActive(state == FlowState.Paused);
        if (winPanel != null) winPanel.SetActive(state == FlowState.Won);
        if (losePanel != null) losePanel.SetActive(state == FlowState.Lost);
    }

    public void OnStartClicked() => GameFlowManager.Instance.StartGame();
    public void OnRetryClicked() => GameFlowManager.Instance.RetryZone();
    public void OnNextLevelClicked() => GameFlowManager.Instance.NextLevel();
    public void OnMenuClicked() => GameFlowManager.Instance.ReturnToMenu();
    public void OnPauseClicked() => GameFlowManager.Instance.PauseGame();
    public void OnResumeClicked() => GameFlowManager.Instance.ResumeGame();
}
