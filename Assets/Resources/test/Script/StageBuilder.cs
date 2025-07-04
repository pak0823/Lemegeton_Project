using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageBuilder : MonoBehaviour
{
    [Header("������ ���")]
    public MapPiece startRoomPrefab;
    public List<MapPiece> mapPiecePrefabs;
    public Transform gridParent;

    private List<MapPiece> placedMaps = new List<MapPiece>();
    private List<(MapPiece map, EntranceInfo entrance)> openEntrances = new List<(MapPiece, EntranceInfo)>();
    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    /// <summary>
    /// ����: ���� ���� ��(Vector3Int) �� Ÿ�� Ÿ��
    /// </summary>
    public Dictionary<Vector3Int, TileType> logicMap = new Dictionary<Vector3Int, TileType>();

    void Start()
    {
        BuildStage();
    }

    public void BuildStage()
    {
        placedMaps.Clear();
        openEntrances.Clear();
        occupiedCells.Clear();
        var grid = gridParent.GetComponent<Grid>();

        // 1. ���� �� ��ġ
        MapPiece start = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity, gridParent);
        placedMaps.Add(start);
        foreach (var e in start.entrances)
            openEntrances.Add((start, e));
        foreach (var cell in GetAllOccupiedCellsForMapPiece(startRoomPrefab, Vector3.zero))
            occupiedCells.Add(cell);

        // 2. ������ �� ���� ��ġ
        var unusedPrefabs = new List<MapPiece>(mapPiecePrefabs);

        while (unusedPrefabs.Count > 0 && openEntrances.Count > 0)
        {
            var openEntranceIdx = Random.Range(0, openEntrances.Count);
            var open = openEntrances[openEntranceIdx];

            var candidates = new List<(MapPiece prefab, EntranceInfo prefabEntrance, EntranceInfo openEntrance, Vector3 spawnPos)>();
            foreach (var prefab in unusedPrefabs)
            {
                foreach (var entrance in prefab.entrances)
                {
                    if (entrance.direction == GetOppositeDirection(open.entrance.direction))
                    {
                        Vector3 anchorPos = open.map.transform.position +
                            (open.entrance.entranceTransform.position - open.map.transform.position);
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
                openEntrances.RemoveAt(openEntranceIdx);
                continue;
            }

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
                openEntrances.RemoveAt(openEntranceIdx);
                continue;
            }

            var chosen = validCandidate.Value;
            MapPiece newMap = Instantiate(chosen.prefab, chosen.spawnPos, Quaternion.identity, gridParent);
            placedMaps.Add(newMap);
            foreach (var cell in GetAllOccupiedCellsForMapPiece(chosen.prefab, chosen.spawnPos))
                occupiedCells.Add(cell);

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

    /// <summary>
    /// ���� ����: ���� ��(Vector3Int) �������� ���
    /// </summary>
    void GenerateLogicMap()
    {
        logicMap.Clear();
        var grid = gridParent.GetComponent<Grid>();

        foreach (var piece in placedMaps)
        {
            var tilemaps = piece.GetComponentsInChildren<Tilemap>();
            foreach (var tilemap in tilemaps)
            {
                foreach (var cell in tilemap.cellBounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cell)) continue;
                    Vector3 worldPos = tilemap.CellToWorld(cell);
                    Vector3Int worldCell = grid.WorldToCell(worldPos);

                    TileBase tile = tilemap.GetTile(cell);
                    TileType type = TileType.None; // �⺻��: �� ����

                    if (tilemap.name.Contains("Wall"))
                        type = TileType.Wall;
                    else if (tilemap.name.Contains("Ground") || tilemap.name.Contains("Tile"))
                        type = TileType.Tile;

                    // Wall �켱���� ����
                    if (!logicMap.ContainsKey(worldCell) || type == TileType.Wall)
                        logicMap[worldCell] = type;
                }
            }
        }

        //foreach (var pair in logicMap)
        //{
        //    Debug.Log($"���� {pair.Key}: {pair.Value}");
        //}
    }

    /// <summary>
    /// MapPiece �������� ���� �����ϴ� ��� ���弿 ��ǥ ��ȯ (�� ��ġ��)
    /// </summary>
    private List<Vector3Int> GetAllOccupiedCellsForMapPiece(MapPiece prefab, Vector3 spawnPos)
    {
        var tilemap = prefab.GetComponentInChildren<Tilemap>();
        var result = new List<Vector3Int>();
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                Vector3Int worldCellPos = gridParent.GetComponent<Grid>().WorldToCell(spawnPos) + (Vector3Int)pos;
                result.Add(worldCellPos);
            }
        }
        return result;
    }

    public static HexDirection GetOppositeDirection(HexDirection dir)
    {
        return (HexDirection)(((int)dir + 3) % 6);
    }

    /// <summary>
    /// ���� ��ǥ���� ���� ���弿(Vector3Int) ��ȯ
    /// </summary>
    public Vector3Int WorldToLogicCell(Vector3 worldPos)
    {
        var grid = gridParent.GetComponent<Grid>();
        return grid.WorldToCell(worldPos);
    }

    /// <summary>
    /// ���� ���弿(Vector3Int)���� �ش� ���� ���� �߾� ��ǥ ��ȯ
    /// </summary>
    public Vector3 LogicCellToWorld(Vector3Int worldCell)
    {
        var grid = gridParent.GetComponent<Grid>();
        return grid.GetCellCenterWorld(worldCell);
    }

    //�ʿ� ��ǥ ǥ�� �� Ÿ�� �� ���� ǥ��
//#if UNITY_EDITOR
//    void OnDrawGizmos()
//    {
//        if (logicMap == null || logicMap.Count == 0) return;
//        var grid = gridParent != null ? gridParent.GetComponent<Grid>() : null;
//        foreach (var pair in logicMap)
//        {
//            Vector3 cellCenter = grid != null ? grid.GetCellCenterWorld(pair.Key) : (Vector3)pair.Key;
//            if (pair.Value == TileType.Tile)
//                Gizmos.color = Color.green;
//            else if (pair.Value == TileType.Wall)
//                Gizmos.color = Color.red;
//            else
//                Gizmos.color = Color.gray;
//            Gizmos.DrawSphere(cellCenter, 0.15f);

//#if UNITY_EDITOR
//            UnityEditor.Handles.Label(cellCenter + Vector3.up * 0.2f, $"{pair.Key}");
//#endif
//        }
//    }
//#endif
}
