using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameState: MonoBehaviour
{
    public static GameState Instance;
    
    public Vector2 mousePos;
    

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }
}
