using UnityEngine;


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

    //covers the very first frame this object exists, before the manager's own sceneLoaded
    //subscription has had a chance to broadcast anything
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
