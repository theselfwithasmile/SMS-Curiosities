using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameState: MonoBehaviour
{
    public static GameState Instance;
    
    public Vector2 mousePos;
    [SerializeField] private TextMeshProUGUI scoreText;
    [HideInInspector] public int CurrScore=0; 
    
    [SerializeField] Color[] groupColors;
    public Color BubbleColor = new Color(0f, 0.5f, 1f, 1f);
    static readonly List<Vector2Int>[] Shapes =
    {
        new List<Vector2Int> { Vector2Int.zero },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.up + Vector2Int.right },
    };

    
    
    public int GroupCount => groupColors.Length;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // scoreText lives in the scene, not on this persistent object, so a scene reload
        // (see GameFlowManager.Reload) destroys the old one and leaves this reference stale.
        // Re-find it lazily instead of relying on a one-time Inspector wire.
        if (scoreText == null)
        {
            var scoreObj = GameObject.Find("Score");
            if (scoreObj != null) scoreText = scoreObj.GetComponent<TextMeshProUGUI>();
        }

        if (scoreText != null) scoreText.text = CurrScore.ToString();
    }
    
    public Color GroupColor(int group)
    {
        if (groupColors.Length == 0) return Color.white;

        Color color = groupColors[group % groupColors.Length];
        color.a = 1;
        return color;
    }

    public Color SecondaryGroupColor(int group)
    {
        if (groupColors.Length == 0) return Color.white;

        Color color = groupColors[group % groupColors.Length] * 0.5f;
        color.a = 1;
        return color;
    }
}
