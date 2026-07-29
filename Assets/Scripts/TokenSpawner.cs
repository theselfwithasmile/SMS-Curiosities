using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TokenSpawner : MonoBehaviour
{
    public static TokenSpawner Instance;
    [SerializeField] private Token tokenPrefab;
    
    void Awake()
    {
        Instance = this;
    }
    
    public Token SpawnToken(int group, Vector3 worldPosition)
    {
        Token token = Instantiate(tokenPrefab, worldPosition, Quaternion.identity);
        token.Group = group;
        return token;
    }

    // Every zone spawns a token and immediately applies its group color the same way - this was
    // duplicated line-for-line in all five.
    public Token SpawnColoredToken(int group, Vector3 worldPosition)
    {
        Token token = SpawnToken(group, worldPosition);
        token.GetComponent<SpriteRenderer>().color = GameState.Instance.GroupColor(group);
        return token;
    }
    
        // Multi-cell pieces (Block Puzzle, Parking Jam) are still one Token with a longer
    // CellOffsets list - the extra cells just need a visual, so this bolts on a plain child
    // sprite per extra offset (reusing the base token's sprite/color), parented with
    // worldPositionStays so it lands correctly regardless of the prefab's own nested scale.
    // Uses SpawnColoredToken (not the raw SpawnToken) specifically so the base sprite already has
    // its correct color before children copy it - spawning uncolored here would let children copy
    // Token.Awake()'s stale Group-0 color, since .Group isn't assigned until after Instantiate.
    public Token SpawnMultiCellToken(int group, Vector3 anchorWorldPosition, List<Vector2Int> offsets)
    {
        Token token = SpawnColoredToken(group, anchorWorldPosition);
        token.CellOffsets = new List<Vector2Int>(offsets);

        SpriteRenderer baseRenderer = token.GetComponent<SpriteRenderer>();
        for (int i = 1; i < offsets.Count; i++)
        {
            var child = new GameObject($"Cell{i}");
            child.transform.position = anchorWorldPosition + (Vector3)((Vector2)offsets[i] * Grid.Instance.CellSize);
            child.transform.SetParent(token.transform, true);

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = baseRenderer.sprite;
            renderer.color = baseRenderer.color;
            renderer.sortingOrder = baseRenderer.sortingOrder;
        }
        return token;
    }

    // Checks every offset cell (anchored at a given cell) against all of its owning containers
    // atomically - tracking pending claims per container so two offset cells landing in the same
    // container can't both pass a stale capacity check - then commits via TryAccept if the whole
    // footprint is legal. Used by Token's multi-cell drop (Block Puzzle pieces).
    public bool TryClaimFootprint(Token token, Vector2Int anchor, List<Vector2Int> offsets)
    {
        var claims = new List<Container>();
        var pendingCounts = new Dictionary<Container, int>();  //tracks pending claims for container

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = anchor + offset;
            IReadOnlyList<Container> owners = ContainerManager.Instance.GetContainersAt(cell);
            if (owners.Count == 0) return false;

            foreach (Container container in owners)
            {
                //ensures multiple offset cells landing in the same container can't both pass a stale capacity check
                pendingCounts.TryGetValue(container, out int pending);
                if (container.Members.Count + pending >= container.Capacity || !container.CanAccept(token))
                {
                    return false;
                }
                
                //registers claim
                pendingCounts[container] = pending + 1; 
                claims.Add(container);
            }
        }

        //commit via TryAccept()
        foreach (Container container in claims)
        {
            container.TryAccept(token);
        }
        return true;
    }
}