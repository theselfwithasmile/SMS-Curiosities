using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AnimationGroup
{
    public List<AnimationClip> clips;
}


public class TokenSpawner : MonoBehaviour
{
    public static TokenSpawner Instance;
    [SerializeField] private Token tokenPrefab;
    [SerializeField] private List<AnimationGroup> animations;
    
    void Awake()
    {
        Instance = this;
    }
    
    public Token SpawnToken(int group, Vector3 worldPosition)
    {
        Token token = Instantiate(tokenPrefab, worldPosition, Quaternion.identity, transform);
        token.Group = group;
        ApplyAnimation(token);
        return token;
    }

    // Every zone spawns a token and immediately applies its group color the same way - this was
    // duplicated line-for-line in all five. Skipped once emoji art is wired up for the group
    // (HasAnimation) - tinting emoji art with the flat placeholder color would just fight its own
    // colors.
    public Token SpawnColoredToken(int group, Vector3 worldPosition)
    {
        Token token = SpawnToken(group, worldPosition);
        if (!token.HasAnimation) token.GetComponent<SpriteRenderer>().color = GameState.Instance.GroupColor(group);
        return token;
    }

    // Resolves this token's current Group/Tier to an AnimationClip (each AnimationGroup is one
    // merge hierarchy; `clips` is indexed by tier within it) and assigns it. Called at spawn, and
    // again by anything that changes a token's Tier after the fact (a merge result), since the
    // clip needs to track whichever tier the token is actually showing.
    public void ApplyAnimation(Token token)
    {
        if (animations == null || token.Group < 0 || token.Group >= animations.Count) return;

        List<AnimationClip> clips = animations[token.Group].clips;
        if (clips == null || clips.Count == 0) return;

        AnimationClip clip = clips[Mathf.Clamp(token.Tier, 0, clips.Count - 1)];
        token.SetAnimationClip(clip);
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
    // container can't both pass a stale capacity check - then commits. Used by Token's multi-cell
    // drop (Block Puzzle pieces).
    //
    // Commits by decomposing into one independent single-cell token per offset, rather than
    // sharing the original multi-cell token across every owning container. Block Puzzle's row and
    // column containers clear independently of each other - a rigid multi-cell piece straddling a
    // completed row and an untouched column would otherwise get destroyed in its entirety just
    // because ONE of its cells sat in the completed row. Once decomposed, each cell only ever
    // answers to its own row/column, exactly like a real placed block. The original token is
    // destroyed as part of this - callers must not touch it afterward on a true return.
    public bool TryClaimFootprint(Token token, Vector2Int anchor, List<Vector2Int> offsets)
    {
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
            }
        }

        int group = token.Group;
        Destroy(token.gameObject);

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = anchor + offset;
            Token cellToken = SpawnColoredToken(group, Grid.Instance.CellToWorld(cell));
            foreach (Container container in ContainerManager.Instance.GetContainersAt(cell))
            {
                container.TryAccept(cellToken);
            }
        }
        return true;
    }
}