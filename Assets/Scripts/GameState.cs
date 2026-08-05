using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameState: MonoBehaviour
{
    public static GameState Instance;
    
    public Vector2 mousePos;
    [SerializeField] private TextMeshProUGUI guideText;
    [SerializeField] private string[] guideSpec;
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
        
        GetValidComponent(ref scoreText, "Score");
        if (scoreText != null) scoreText.text = CurrScore.ToString();

        GetValidComponent(ref guideText, "Guide");
        if (guideText != null) guideText.text = guideSpec[GameFlowManager.Instance.CurrentZoneIndex];
    }

    void GetValidComponent(ref TextMeshProUGUI text, string name)
    {
        if (text == null)
        {
            var obj = GameObject.Find(name);
            if (obj != null) text = obj.GetComponent<TextMeshProUGUI>();
        }
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
