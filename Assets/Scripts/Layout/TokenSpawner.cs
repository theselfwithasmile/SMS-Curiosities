using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class AnimationGroup
{
    public List<AnimationClip> clips;
}


public class TokenSpawner : MonoBehaviour
{
    public static TokenSpawner Instance;
    [SerializeField] private Token tokenPrefab;
    [SerializeField] private Transform chatbot;
    [SerializeField] private List<AnimationGroup> animations;
    [SerializeField] private AnimationGroup directionAnimations;

    void Awake()
    {
        Instance = this;
    }
    
    public Token SpawnToken(int group, Vector3 worldPosition, bool byTier = false)
    {
        Token token = Instantiate(tokenPrefab, worldPosition, Quaternion.identity, chatbot);
        token.Group = group;
        ScaleToCell(token.transform);
        ApplyAnimation(token, byTier);
        return token;
    }
    
    static void ScaleToCell(Transform tokenTransform)
    {
        tokenTransform.localScale *= Grid.Instance.CellSize / Grid.Instance.ReferenceCellSize;
    }
    
    public Token SpawnColoredToken(int group, Vector3 worldPosition, bool byTier = false)
    {
        Token token = SpawnToken(group, worldPosition, byTier);
        if (!token.HasAnimation) token.GetComponent<Image>().color = GameState.Instance.GroupColor(group);
        return token;
    }
    
    public void ApplyAnimation(Token token, bool byTier = false)
    {
        if (animations == null || token.Group < 0 || token.Group >= animations.Count) return;

        List<AnimationClip> clips = animations[token.Group].clips;
        if (clips == null || clips.Count == 0) return;

        //arbitrary entry if tier level does not matter
        int index = byTier ? Mathf.Clamp(token.Tier, 0, clips.Count - 1) : Random.Range(0, clips.Count);
        token.SetAnimationClip(clips[index]);
    }
    
    //for parking jam direction rendering
    public void ApplyDirectionAnimation(Token token, Vector2Int direction)
    {
        if (directionAnimations?.clips == null) return;

        int index = DirectionIndex(direction);
        if (index < 0 || index >= directionAnimations.clips.Count) return;

        token.SetAnimationClip(directionAnimations.clips[index]);
    }
    
    static int DirectionIndex(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return 0;
        if (direction == Vector2Int.down) return 1;
        if (direction == Vector2Int.left) return 2;
        if (direction == Vector2Int.right) return 3;
        return -1;
    }

    //multi-cell pieces are one Token with a longer CellOffsets list
    public Token SpawnMultiCellToken(int group, Vector3 anchorWorldPosition, List<Vector2Int> offsets)
    {
        Token token = SpawnColoredToken(group, anchorWorldPosition);
        token.CellOffsets = new List<Vector2Int>(offsets);

        Image baseRenderer = token.GetComponent<Image>();
        for (int i = 1; i < offsets.Count; i++)
        {
            var child = new GameObject($"Cell{i}");
            child.transform.position = anchorWorldPosition + (Vector3)((Vector2)offsets[i] * Grid.Instance.CellSize);
            child.transform.SetParent(token.transform, true);

            //resetting to 1,1,1 here means "match the root", not "keep this object's old world scale"
            child.transform.localScale = Vector3.one;

            var renderer = child.AddComponent<Image>();
            renderer.sprite = baseRenderer.sprite;
            renderer.color = baseRenderer.color;
            //renderer.sortingOrder = baseRenderer.sortingOrder;

            token.CellParts.Add(child.transform);
        }
        return token;
    }

    //checks every offset cell against all of its owning containers
    //atomically via tracking pending claims per container so two offset cells landing in the same
    //container can't both pass a stale capacity check
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

        //commits footprint
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