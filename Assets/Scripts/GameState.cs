using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameState: MonoBehaviour
{
    public static GameState Instance;
    
    public Vector2 mousePos;
    
    [SerializeField] Color[] groupColors;

    public int GroupCount => groupColors.Length;


    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
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
