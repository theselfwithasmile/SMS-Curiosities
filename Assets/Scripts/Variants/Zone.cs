// using System.Collections.Generic;
// using UnityEngine;
//
// // Woodoku-style: one container per row + one per column (overlapping, via the multi-membership
// // lookup), tier-agnostic (geometry only, no entry constraint), clearing on full. Pieces are drawn
// // from a small canonical shape set and staged in the row just below the board, ready to drag.
// public class Zone : MonoBehaviour
// {
//     [SerializeField] int boardSize = 5;
//     [SerializeField] int pieceCount = 3;
//     [SerializeField] Color boardColor = Color.gray;
//
//     private Grid grid;
//     private ContainerManager containerManager;
//
//
//     void Start()
//     {
//         grid = Grid.Instance;
//         containerManager = ContainerManager.Instance;
//         int size = Mathf.Min(boardSize, grid.Columns, grid.Rows - 1);
//         
//         GenerateZone();
//     }
//
//     public void GenerateZone()
//     {
//         GenerateBench();
//         GenerateContainers();
//         GenerateTokens();
//     }
//
//     void GenerateBench()
//     {
//         int totalTokens = tubeCount * tubeCapacity;
//         int benchRows = Mathf.Max(1, Mathf.CeilToInt(totalTokens / (float)grid.Columns));
//         Container bench = containers.CreateFixedContainer(new RectInt(0, 0, grid.Columns, benchRows), Color.gray);
//     }
//
//     void GenerateContainers()
//     {
//         //per row/col
//         //every cell
//         
//         for (int y = 0; y < size; y++)
//         {
//             Container row = containers.CreateFixedContainer(new RectInt(0, y, size, 1), boardColor);
//             row.CompletionPredicate = new FullPredicate();
//             row.Resolution = new ClearResolution();
//         }
//         for (int x = 0; x < size; x++)
//         {
//             Container column = containers.CreateFixedContainer(new RectInt(x, 0, 1, size), boardColor);
//             column.CompletionPredicate = new FullPredicate();
//             column.Resolution = new ClearResolution();
//         }
//     }
//
//     void GenerateTokens()
//     {
//         //singular/grouped
//         //on bench/on grid
//         
//         List<CarSpec> layout = null;
//         
//         Vector2Int exitAnchor = default; //exit anchor is the anchor of the target car, which is also the exit lane's anchor
//
//         for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
//         {
//             List<CarSpec> candidate = GenerateLayout(bounds, out Vector2Int candidateExit);
//             if (candidate != null && IsSolvable(candidate, bounds, candidateExit))
//             {
//                 layout = candidate;
//                 exitAnchor = candidateExit;
//                 break;
//             }
//         }
//         if (layout == null)
//         {
//             Debug.LogWarning("ParkingJamZone: no solvable layout found within the attempt budget; falling back to just the target car.");
//             CarSpec target = CreateRandomTarget(bounds, out exitAnchor);
//             layout = new List<CarSpec> { target };
//         }
//         foreach (CarSpec car in layout)
//         {
//             SpawnCar(car);
//         }
//         
//         
//         
//         
//         int stagingRow = size;
//         int stageX = 0;
//         for (int i = 0; i < pieceCount; i++)
//         {
//             List<Vector2Int> shape = Shapes[Random.Range(0, Shapes.Length)];
//             int group = Random.Range(0, 6);
//             int width = ShapeWidth(shape);
//
//             if (stageX + width > grid.Columns) break;
//
//             Vector3 anchorWorld = grid.CellToWorld(new Vector2Int(stageX, stagingRow));
//             Token piece = containers.SpawnMultiCellToken(group, anchorWorld, shape);
//             piece.GetComponent<SpriteRenderer>().color = GameState.Instance.GroupColor(group);
//
//             stageX += width + 1;
//         }
//     }
//
//     static int ShapeWidth(List<Vector2Int> shape)
//     {
//         int maxX = 0;
//         foreach (Vector2Int cell in shape) maxX = Mathf.Max(maxX, cell.x);
//         return maxX + 1;
//     }
// }
