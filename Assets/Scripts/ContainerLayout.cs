using System.Collections.Generic;
using UnityEngine;

// What containers exist when a zone starts - called once, upfront, from a zone's Start().
public interface IContainerLayout
{
    List<Container> Build(RectInt bounds, Color color);
}

// One container per cell in bounds (Merge, Toon Blast).
public class PerTileLayout : IContainerLayout
{
    public List<Container> Build(RectInt bounds, Color color)
    {
        ContainerManager containers = ContainerManager.Instance;
        var result = new List<Container>(bounds.width * bounds.height);
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                result.Add(containers.CreateFixedContainer(new RectInt(x, y, 1, 1), color));
            }
        }
        return result;
    }
}

// One container per row plus one per column, overlapping via multi-membership (Block Puzzle).
public class OrthogonalLayout : IContainerLayout
{
    public List<Container> Build(RectInt bounds, Color color)
    {
        ContainerManager containers = ContainerManager.Instance;
        var result = new List<Container>(bounds.width + bounds.height);
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            result.Add(containers.CreateFixedContainer(new RectInt(bounds.xMin, y, bounds.width, 1), color));
        }
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            result.Add(containers.CreateFixedContainer(new RectInt(x, bounds.yMin, 1, bounds.height), color));
        }
        return result;
    }
}

// A single container grown via randomized region growth (one of Water Sort's tubes/shelves).
// axisBias (e.g. Vector2Int.up) with biasStrength > 0 skews growth along an axis - strong bias
// produces tube/lane shapes, no bias produces blobs.
//
// Unlike the other two layouts, this builds exactly one container per call rather than deriving
// a count from bounds - callers needing several (e.g. one tube per color) call Build once per
// container, since each one typically wants its own distinct color anyway.
//
// Known gap: ContainerManager.GenerateContainer always searches the whole grid internally, not
// this bounds parameter - fine today since every zone using this owns the whole grid anyway, but
// worth fixing if a future zone needs region growth confined to a sub-region of a shared board.
public class RegionGrowthLayout : IContainerLayout
{
    readonly int capacity;
    readonly Vector2Int axisBias;
    readonly float biasStrength;

    public RegionGrowthLayout(int capacity, Vector2Int axisBias = default, float biasStrength = 0f)
    {
        this.capacity = capacity;
        this.axisBias = axisBias;
        this.biasStrength = biasStrength;
    }

    public List<Container> Build(RectInt bounds, Color color)
    {
        Container container = ContainerManager.Instance.GenerateContainer(capacity, color, axisBias, biasStrength);
        return container != null ? new List<Container> { container } : new List<Container>();
    }
}

