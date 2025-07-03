using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageBuilder : MonoBehaviour
{
    [Header("������ ���")]
    public MapPiece startRoomPrefab;                // ���� �� ������ (1��)
    public List<MapPiece> mapPiecePrefabs;          // �� �� �� ������ (n��)
    public Transform gridParent; // Grid ������Ʈ�� Inspector���� �巡��

    // ���� �ν��Ͻ�(��ġ�� ������Ʈ) ����Ʈ
    private List<MapPiece> placedMaps = new List<MapPiece>();

    // ���� ���� �Ա� ��� (��, �Ա� ����)
    private List<(MapPiece map, EntranceInfo entrance)> openEntrances = new List<(MapPiece, EntranceInfo)>();
    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    public TileType[,] logicMap; // public/protected�� �����ϸ� �ܺο��� ���� ����
    int minX, minY, maxX, maxY; // �迭-���弿 ��ȯ��

    // ��� ����: Start���� �ڵ� ���� (���ϸ� ��ư���� ���൵ ����)
    void Start()
    {
        BuildStage();
    }

    // �� �������� ���� ���� �Լ�
    public void BuildStage()
    {
        // �ʱ�ȭ
        placedMaps.Clear();
        openEntrances.Clear();
        occupiedCells.Clear(); //���� �� �ʱ�ȭ
        var grid = gridParent.GetComponent<Grid>();

        // 1. StartRoom ��ġ (��ǥ 0,0,0)
        MapPiece start = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity, gridParent);
        placedMaps.Add(start);
        // start�� ��� �Ա��� openEntrances�� ���
        foreach (var e in start.entrances)
            openEntrances.Add((start, e));

        // StartRoom�� ���� �� ���� ���
        var startCells = GetAllOccupiedCellsForMapPiece(startRoomPrefab, Vector3.zero);
        foreach (var cell in startCells)
            occupiedCells.Add(cell);

        // 2. ������ ���� �����ϰ� �ϳ��� ����
        var unusedPrefabs = new List<MapPiece>(mapPiecePrefabs);

        while (unusedPrefabs.Count > 0 && openEntrances.Count > 0)
        {
            // �����ϰ� ���� �Ա� �ϳ� ����
            var openEntranceIdx = Random.Range(0, openEntrances.Count);
            var open = openEntrances[openEntranceIdx];

            // ���� ������ ������, ���� �� ã��
            var candidates = new List<(MapPiece prefab, EntranceInfo prefabEntrance, EntranceInfo openEntrance, Vector3 spawnPos)>();
            foreach (var prefab in unusedPrefabs)
            {
                foreach (var entrance in prefab.entrances)
                {
                    if (entrance.direction == GetOppositeDirection(open.entrance.direction))
                    {
                        Vector3 anchorPos = open.map.transform.position +
                            (open.entrance.entranceTransform.position - open.map.transform.position); // ���� ��ġ
                        Vector3 newPieceOffset = entrance.entranceTransform.localPosition;
                        Vector3 spawnPos = anchorPos - newPieceOffset;

                        Vector3Int gridCell = grid.WorldToCell(spawnPos);
                        Vector3 snappedPos = grid.GetCellCenterWorld(gridCell);

                        candidates.Add((prefab, entrance, open.entrance, spawnPos));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                // �� �Ա��� ������ �� �ִ� ���� ������ ���� (���� ���� ���� ����)
                openEntrances.RemoveAt(openEntranceIdx);
                continue;
            }

            // �ĺ� �߿���, ������ ���� �͸� ���� ����
            (MapPiece prefab, EntranceInfo prefabEntrance, EntranceInfo openEntrance, Vector3 spawnPos)? validCandidate = null;
            var shuffled = candidates.OrderBy(_ => Random.value);
            foreach (var cand in shuffled)
            {
                var candCells = GetAllOccupiedCellsForMapPiece(cand.prefab, cand.spawnPos);
                bool overlap = candCells.Any(cell => occupiedCells.Contains(cell));
                if (!overlap)
                {
                    validCandidate = cand;
                    break;
                }
            }

            if (validCandidate == null)
            {
                // ��� �ĺ��� ��ħ �� �ٸ� �Ա��� �Ѿ
                openEntrances.RemoveAt(openEntranceIdx);
                continue;
            }

            var chosen = validCandidate.Value;

            MapPiece newMap = Instantiate(chosen.prefab, chosen.spawnPos, Quaternion.identity, gridParent);
            placedMaps.Add(newMap);

            // �� ���� ������ ���� occupiedCells�� �߰�
            var newCells = GetAllOccupiedCellsForMapPiece(chosen.prefab, chosen.spawnPos);
            foreach (var cell in newCells)
                occupiedCells.Add(cell);

            // 4. ����� �� �Ա��� openEntrances���� ���� (�Ա�-�Ա�)
            openEntrances.Remove(open);
            foreach (var e in newMap.entrances)
            {
                if (e.direction == chosen.prefabEntrance.direction) continue;
                openEntrances.Add((newMap, e));
            }

            unusedPrefabs.Remove(chosen.prefab);

            
            Vector3Int cellSet = grid.WorldToCell(newMap.transform.position);
            newMap.transform.position = grid.GetCellCenterWorld(cellSet);
        }
        Debug.Log($"[StageBuilder] �� ���� �Ϸ�: ��ġ�� �� �� = {placedMaps.Count}");

        GenerateLogicMap();
    }

    void GenerateLogicMap()
    {
        var grid = gridParent.GetComponent<Grid>();

        // 1. ��� ���� ���� Ÿ�� Ÿ�� ���
        HashSet<Vector3Int> allCells = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, TileType> worldCellDict = new Dictionary<Vector3Int, TileType>();
        foreach (var piece in placedMaps)
        {
            var tilemap = piece.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(cell))
                {
                    Vector3 worldPos = tilemap.CellToWorld(cell);
                    Vector3Int worldCell = grid.WorldToCell(worldPos);
                    allCells.Add(worldCell);

                    // --- ���⿡ "��/��" ���� ���� �Է� ---
                    TileBase tile = tilemap.GetTile(cell);
                    // ����: Ÿ�� �̸��� "Wall"�� ������ ��, �ƴϸ� ��
                    TileType type = TileType.Empty;
                    if (tile != null && tile.name.Contains("Wall")) type = TileType.Wall;
                    worldCellDict[worldCell] = type;
                }
            }
        }

        // 2. ��ü ���� ���ϱ�
        minX = allCells.Min(c => c.x);
        maxX = allCells.Max(c => c.x);
        minY = allCells.Min(c => c.y);
        maxY = allCells.Max(c => c.y);
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        logicMap = new TileType[width, height];

        // 3. worldCellDict �� �迭�� �Է�
        foreach (var pair in worldCellDict)
        {
            int x = pair.Key.x - minX;
            int y = pair.Key.y - minY;
            logicMap[x, y] = pair.Value;
        }

        // Ÿ�� ������ Ÿ�� Ȯ�ο�
        //for (int x = 0; x < logicMap.GetLength(0); x++)
        //    for (int y = 0; y < logicMap.GetLength(1); y++)
        //        Debug.Log($"logicMap[{x},{y}] = {logicMap[x, y]}");
    }



    // �� ������, spawnPos(��ġ ��ġ), ���ῡ �� entrance ��������
    private List<Vector3Int> GetAllOccupiedCellsForMapPiece(MapPiece prefab, Vector3 spawnPos)
    {
        var tilemap = prefab.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
        var result = new List<Vector3Int>();
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                // �������� �� ��ǥ(pos)�� "��ġ ��ġ ����" ���� �� ��ǥ�� ��ȯ
                // 1. (0,0,0)�� Instantiate�ߴٰ� �����ϸ� pos �״��
                // 2. spawnPos�� ������ǥ���� grid ����ǥ�� ��ȯ
                Vector3Int worldCellPos = gridParent.GetComponent<UnityEngine.Grid>().WorldToCell(spawnPos) + (Vector3Int)pos;
                result.Add(worldCellPos);
            }
        }
        return result;
    }


    // �ݴ� ���� ���ϱ�
    public static HexDirection GetOppositeDirection(HexDirection dir)
    {
        return (HexDirection)(((int)dir + 3) % 6); // 6�����̹Ƿ� +3�ϸ� �ݴ�
    }

    //��ã��/ĳ���� �̵� �ڵ忡�� ȣ��
    public Vector2Int WorldToLogicIndex(Vector3 worldPos)
    {
        var grid = gridParent.GetComponent<Grid>();
        Vector3Int worldCell = grid.WorldToCell(worldPos);
        int x = worldCell.x - minX;
        int y = worldCell.y - minY;
        return new Vector2Int(x, y);
    }

    public Vector3 LogicIndexToWorld(Vector2Int logicIndex)
    {
        var grid = gridParent.GetComponent<Grid>();
        Vector3Int cell = new Vector3Int(logicIndex.x + minX, logicIndex.y + minY, 0);
        return grid.GetCellCenterWorld(cell);
    }
}
