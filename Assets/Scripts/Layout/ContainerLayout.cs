using System.Collections.Generic;
using UnityEngine;

//what containers exist when a zone starts
public interface IContainerLayout
{
    List<Container> Build(RectInt bounds, Color color);
}

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

//one container per row plus one per column
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

// A single rect-shaped container placed at a random free position (one of Water Sort's
// tubes/shelves). axisBias (e.g. Vector2Int.up) produces a 1-wide line along that axis (capacity
// cells long) - no bias produces the closest-to-square rect instead.
public class RegionGrowthLayout : IContainerLayout
{
    readonly int capacity;
    readonly Vector2Int axisBias;

    public RegionGrowthLayout(int capacity, Vector2Int axisBias = default)
    {
        this.capacity = capacity;
        this.axisBias = axisBias;
    }

    //builds exactly one container per call rather than deriving
    public List<Container> Build(RectInt bounds, Color color)
    {
        Container container = ContainerManager.Instance.GenerateContainer(capacity, color, axisBias);
        return container != null ? new List<Container> { container } : new List<Container>();
    }
}

