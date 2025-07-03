using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageBuilder : MonoBehaviour
{
    [Header("프리팹 등록")]
    public MapPiece startRoomPrefab;                // 시작 맵 프리팹 (1개)
    public List<MapPiece> mapPiecePrefabs;          // 그 외 맵 프리팹 (n개)
    public Transform gridParent; // Grid 오브젝트를 Inspector에서 드래그

    // 실제 인스턴스(배치된 오브젝트) 리스트
    private List<MapPiece> placedMaps = new List<MapPiece>();

    // 현재 열린 입구 목록 (맵, 입구 정보)
    private List<(MapPiece map, EntranceInfo entrance)> openEntrances = new List<(MapPiece, EntranceInfo)>();
    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    public TileType[,] logicMap; // public/protected로 선언하면 외부에서 접근 가능
    int minX, minY, maxX, maxY; // 배열-월드셀 변환용

    // 사용 예시: Start에서 자동 실행 (원하면 버튼으로 실행도 가능)
    void Start()
    {
        BuildStage();
    }

    // 맵 스테이지 랜덤 조립 함수
    public void BuildStage()
    {
        // 초기화
        placedMaps.Clear();
        openEntrances.Clear();
        occupiedCells.Clear(); //점유 셀 초기화
        var grid = gridParent.GetComponent<Grid>();

        // 1. StartRoom 배치 (좌표 0,0,0)
        MapPiece start = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity, gridParent);
        placedMaps.Add(start);
        // start의 모든 입구를 openEntrances에 등록
        foreach (var e in start.entrances)
            openEntrances.Add((start, e));

        // StartRoom의 점유 셀 먼저 등록
        var startCells = GetAllOccupiedCellsForMapPiece(startRoomPrefab, Vector3.zero);
        foreach (var cell in startCells)
            occupiedCells.Add(cell);

        // 2. 나머지 맵을 랜덤하게 하나씩 연결
        var unusedPrefabs = new List<MapPiece>(mapPiecePrefabs);

        while (unusedPrefabs.Count > 0 && openEntrances.Count > 0)
        {
            // 랜덤하게 오픈 입구 하나 선택
            var openEntranceIdx = Random.Range(0, openEntrances.Count);
            var open = openEntrances[openEntranceIdx];

            // 연결 가능한 프리팹, 방향 쌍 찾기
            var candidates = new List<(MapPiece prefab, EntranceInfo prefabEntrance, EntranceInfo openEntrance, Vector3 spawnPos)>();
            foreach (var prefab in unusedPrefabs)
            {
                foreach (var entrance in prefab.entrances)
                {
                    if (entrance.direction == GetOppositeDirection(open.entrance.direction))
                    {
                        Vector3 anchorPos = open.map.transform.position +
                            (open.entrance.entranceTransform.position - open.map.transform.position); // 월드 위치
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
                // 이 입구에 연결할 수 있는 맵이 없으면 제거 (길이 끊김 현상 방지)
                openEntrances.RemoveAt(openEntranceIdx);
                continue;
            }

            // 후보 중에서, 오버랩 없는 것만 랜덤 선택
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
                // 모든 후보가 겹침 → 다른 입구로 넘어감
                openEntrances.RemoveAt(openEntranceIdx);
                continue;
            }

            var chosen = validCandidate.Value;

            MapPiece newMap = Instantiate(chosen.prefab, chosen.spawnPos, Quaternion.identity, gridParent);
            placedMaps.Add(newMap);

            // 새 맵이 점유한 셀을 occupiedCells에 추가
            var newCells = GetAllOccupiedCellsForMapPiece(chosen.prefab, chosen.spawnPos);
            foreach (var cell in newCells)
                occupiedCells.Add(cell);

            // 4. 연결된 두 입구를 openEntrances에서 제거 (입구-입구)
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
        Debug.Log($"[StageBuilder] 맵 조립 완료: 배치된 맵 수 = {placedMaps.Count}");

        GenerateLogicMap();
    }

    void GenerateLogicMap()
    {
        var grid = gridParent.GetComponent<Grid>();

        // 1. 모든 점유 셀과 타일 타입 기록
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

                    // --- 여기에 "벽/길" 구분 기준 입력 ---
                    TileBase tile = tilemap.GetTile(cell);
                    // 예시: 타일 이름에 "Wall"이 있으면 벽, 아니면 길
                    TileType type = TileType.Empty;
                    if (tile != null && tile.name.Contains("Wall")) type = TileType.Wall;
                    worldCellDict[worldCell] = type;
                }
            }
        }

        // 2. 전체 범위 구하기
        minX = allCells.Min(c => c.x);
        maxX = allCells.Max(c => c.x);
        minY = allCells.Min(c => c.y);
        maxY = allCells.Max(c => c.y);
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        logicMap = new TileType[width, height];

        // 3. worldCellDict → 배열에 입력
        foreach (var pair in worldCellDict)
        {
            int x = pair.Key.x - minX;
            int y = pair.Key.y - minY;
            logicMap[x, y] = pair.Value;
        }

        // 타일 생성시 타입 확인용
        //for (int x = 0; x < logicMap.GetLength(0); x++)
        //    for (int y = 0; y < logicMap.GetLength(1); y++)
        //        Debug.Log($"logicMap[{x},{y}] = {logicMap[x, y]}");
    }



    // 맵 프리팹, spawnPos(배치 위치), 연결에 쓴 entrance 기준으로
    private List<Vector3Int> GetAllOccupiedCellsForMapPiece(MapPiece prefab, Vector3 spawnPos)
    {
        var tilemap = prefab.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
        var result = new List<Vector3Int>();
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                // 프리팹의 셀 좌표(pos)를 "배치 위치 기준" 월드 셀 좌표로 변환
                // 1. (0,0,0)에 Instantiate했다고 가정하면 pos 그대로
                // 2. spawnPos를 월드좌표에서 grid 셀좌표로 변환
                Vector3Int worldCellPos = gridParent.GetComponent<UnityEngine.Grid>().WorldToCell(spawnPos) + (Vector3Int)pos;
                result.Add(worldCellPos);
            }
        }
        return result;
    }


    // 반대 방향 구하기
    public static HexDirection GetOppositeDirection(HexDirection dir)
    {
        return (HexDirection)(((int)dir + 3) % 6); // 6방향이므로 +3하면 반대
    }

    //길찾기/캐릭터 이동 코드에서 호출
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
