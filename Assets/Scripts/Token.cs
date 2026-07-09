using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Draggable))]
public class Token : MonoBehaviour
{
    public Container CurrentContainer;
    public Color currColor;
    public int Group;
    public int Tier; // merge level only (2048-style) - never used for group/type matching

    // Relative footprint in cells. Size 1 (the default) behaves exactly like a normal single-cell
    // token; a multi-cell piece (Block Puzzle, Parking Jam) just has more entries here - any
    // extra visuals for those cells are child sprites baked into the prefab, which move for free
    // since they're parented under this Transform.
    public List<Vector2Int> CellOffsets = new List<Vector2Int> { Vector2Int.zero };

    void Awake()
    {
        GetComponent<SpriteRenderer>().color = GameState.Instance.SecondaryGroupColor(Group);
    }
}
