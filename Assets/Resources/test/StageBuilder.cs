using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
}
