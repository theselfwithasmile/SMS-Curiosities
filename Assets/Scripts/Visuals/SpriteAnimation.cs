using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sprite Animation")]
public class SpriteAnimation : ScriptableObject
{
    public Sprite[] frames;
    public float fps = 12f;
    public bool loop = true;
}