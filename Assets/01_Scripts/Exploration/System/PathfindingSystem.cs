
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PathfindingSystem : MonoBehaviour
{
    public static PathfindingSystem Instance { get; private set; }

    [Header("타일맵 설정")]
    public List<Tilemap> floorMaps = new List<Tilemap>();
    public List<Tilemap> wallMaps = new List<Tilemap>();
    public List<Tilemap> obstacleMaps = new List<Tilemap>();
    
    [SerializeField] private LayerMask impassableLayerMask;

    public Tilemap floorTilemap => (floorMaps != null && floorMaps.Count > 0) ? floorMaps[0] : null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }
    
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- 초기화 ---
    public void Initialize(List<Tilemap> floors, List<Tilemap> obstacles, List<Tilemap> walls, LayerMask impassableMask)
    {
        floorMaps = floors;
        obstacleMaps = obstacles;
        wallMaps = walls;
        impassableLayerMask = impassableMask;
    }


    // --- 타일맵 유틸리티 ---
    
    public Tilemap GetWalkableMapAt(Vector3Int cell)
    {
        if (floorMaps == null) return null;

        // 리스트의 뒤쪽(높이 높은 부분)부터 검사해서 겹쳤을 때 높은 타일을 가져옴
        for (int i = floorMaps.Count - 1; i >= 0; i--)
        {
            if (floorMaps[i].HasTile(cell)) return floorMaps[i];
        }
        return null;
    }
    
    // 논리적 판단을 위한 정확한 월드 좌표 반환
    public Vector3 GetWorldPosForLogic(Vector3Int cell)
    {
        Tilemap map = GetWalkableMapAt(cell);
        if (map == null) map = floorTilemap; // 없으면 바닥 기준

        // 해당 맵의 앵커가 적용된 월드 중심 좌표
        Vector3 worldPos = map.GetCellCenterWorld(cell);
        worldPos.z = 0; // 거리는 2D 평면(XY) 기준으로만 재거나 Z 무시
        return worldPos;
    }

    public Vector3Int GetCellFromWorldPos(Vector3 worldPos)
    {
        // Pure Grid Logic: 모든 Floor 맵을 순회하며 수학적으로 좌표 계산
        Tilemap bestMap = null;
        Vector3Int bestCell = Vector3Int.zero;
        
        // 정렬 기준:
        // 1. Sorting Order (높을수록 위)
        // 2. 리스트 인덱스 (뒤쪽일수록 위)
        int bestOrder = int.MinValue;
        int bestIndex = -1;

        if (floorMaps != null)
        {
            for (int i = 0; i < floorMaps.Count; i++)
            {
                Tilemap map = floorMaps[i];
                if (map == null) continue;

                // 1. 맵의 Grid 정보를 통해 Anchor 오프셋 계산
                // (TileAnchor가 (0.5, 0.5, 0)이면 타일 중심이 그만큼 이동되어 보임 -> 역산해야 그리드 좌표)
                Grid grid = map.layoutGrid;
                
                // 타일의 보이는 위치 = Grid위치 + AnchorOffset
                // 따라서 Grid위치 = 보이는 위치 - AnchorOffset
                Vector3 anchorOffset = grid.LocalToWorld(grid.CellToLocalInterpolated(map.tileAnchor))
                                     - grid.LocalToWorld(grid.CellToLocalInterpolated(Vector3.zero));
                
                Vector3 correctedPos = worldPos - anchorOffset;
                Vector3Int cell = map.WorldToCell(correctedPos);
                cell.z = 0; // 2D 평면

                // 2. 해당 셀에 실제로 타일이 있는지 확인
                if (map.HasTile(cell))
                {
                    var renderer = map.GetComponent<TilemapRenderer>();
                    int order = renderer != null ? renderer.sortingOrder : 0;

                    // 3. 우선순위 비교 (더 위에 있는 맵 찾기)
                    bool isBetter = false;
                    
                    if (bestMap == null) isBetter = true;
                    else if (order > bestOrder) isBetter = true;
                    else if (order == bestOrder && i > bestIndex) isBetter = true;

                    if (isBetter)
                    {
                        bestMap = map;
                        bestCell = cell;
                        bestOrder = order;
                        bestIndex = i;
                    }
                }
            }
        }

        if (bestMap != null)
        {
            return bestCell;
        }

        // 바닥 맵을 못 찾았을 경우 Fallback
        if (floorTilemap != null)
        {
            // 혹시 모를 기본 바닥 앵커값도 빼줌
            Vector3 correctedPos = worldPos;
            correctedPos -= floorTilemap.tileAnchor;
            return floorTilemap.WorldToCell(correctedPos);
        }

        return Vector3Int.zero;
    }


    // --- 타일 판정 ---

    public bool IsWalkableCell(Vector3Int cell)
    {
        // 1. 바닥 타일 존재 체크
        var targetMap = GetWalkableMapAt(cell);
        if (targetMap == null) return false;

        // 2. 장애물 타일이 있는지 체크
        // 2. 장애물 타일이 있는지 체크
        if (obstacleMaps != null)
        {
            foreach (var obs in obstacleMaps)
            {
                if (obs.HasTile(cell)) return false;
            }
        }

        // 2-1. 벽 타일이 있는지 체크 (추가됨)
        if (wallMaps != null)
        {
            foreach (var wall in wallMaps)
            {
                if (wall.HasTile(cell)) return false;
            }
        }

        // 3. 물리적 충돌체(LayerMask) 확인 (OverlapBox/Point)
        Vector3 worldPos = targetMap.GetCellCenterWorld(cell);
        // 타일 크기 고려해서 약간 작게 잡음
        Collider2D hit = Physics2D.OverlapBox(worldPos, new Vector2(0.8f, 0.8f), 0f, impassableLayerMask);
        if (hit != null)
        {
            // Trigger가 아닌 실제 Collider만 장애물로 취급
            if (!hit.isTrigger) return false;
        }

        return true;
    }

    // 이동 가능한 타일의 높이 차이가 이동 가능한 범위인지 확인 (3칸 이상 불가)
    public bool IsHeightDiffValid(Vector3Int from, Vector3Int to)
    {
        Tilemap fromMap = GetWalkableMapAt(from);
        Tilemap toMap = GetWalkableMapAt(to);

        // 맵을 못찾으면 바닥(0)으로 간주
        float fromH = (fromMap != null) ? fromMap.tileAnchor.y : 0f;
        float toH = (toMap != null) ? toMap.tileAnchor.y : 0f;

        float diff = Mathf.Abs(toH - fromH);    // (도착 - 출발)

        // 위로 가거나 아래로 가거나 차이가 0.6f 미만이어야 함
        if (Mathf.Abs(diff) < 0.55f)
        {
            return true;
        }

        return false;
    }


    // --- 경로 탐색 (BFS) ---

    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int goal)
    {
        if (start == goal) return new List<Vector3Int> { start };

        var queue = new Queue<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        queue.Enqueue(start);
        cameFrom[start] = start;

        Direction[] dirs =
        {
            Direction.West, Direction.East,
            Direction.NW, Direction.NE,
            Direction.SW, Direction.SE
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal) break;

            bool odd = (current.y & 1) != 0;
            Vector3 currentWorldPos = GetWorldPosForLogic(current);

            foreach (var dir in dirs)
            {
                Vector3Int offset = GetOffsetForDirection(dir, odd);
                Vector3Int next = current + offset;

                if (cameFrom.ContainsKey(next)) continue;

                // 1. 바닥 타일 없으면 스킵
                if (!IsWalkableCell(next)) continue;

                // 2. 높이 차이 안맞으면 스킵
                if (!IsHeightDiffValid(current, next)) continue;

                // 3. 물리적 거리가 너무 멀어서 헥사 타일이 아닌 다른 타일이면 스킵
                // 안전하게 2.0f로 둠
                Vector3 nextWorldPos = GetWorldPosForLogic(next);
                float dist = Vector2.Distance(currentWorldPos, nextWorldPos);
                if (dist > 2.0f) continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            return null; // 경로 없음
        }

        // 경로 재구성
        var path = new List<Vector3Int>();
        var cur = goal;
        while (true)
        {
            path.Add(cur);
            if (cur == start) break;
            cur = cameFrom[cur];
        }
        path.Reverse();
        return path;
    }

    // 유틸리티: 방향에 따른 오프셋 (Hex Top-Flat 기준 가정)
    // 기존 PlayerMovement에 있던 로직 그대로 가져옴
    public Vector3Int GetOffsetForDirection(Direction dir, bool oddRow)
    {
        // 짝수 행 (y%2==0)
        // NW(-1,1), NE(0,1), W(-1,0), E(1,0), SW(-1,-1), SE(0,-1)
        
        // 홀수 행 (y%2==1)
        // NW(0,1), NE(1,1), W(-1,0), E(1,0), SW(0,-1), SE(1,-1)

        switch (dir)
        {
            case Direction.West: return new Vector3Int(-1, 0, 0);
            case Direction.East: return new Vector3Int(1, 0, 0);
            
            case Direction.NW: return oddRow ? new Vector3Int(0, 1, 0) : new Vector3Int(-1, 1, 0);
            case Direction.NE: return oddRow ? new Vector3Int(1, 1, 0) : new Vector3Int(0, 1, 0);
            
            case Direction.SW: return oddRow ? new Vector3Int(0, -1, 0) : new Vector3Int(-1, -1, 0);
            case Direction.SE: return oddRow ? new Vector3Int(1, -1, 0) : new Vector3Int(0, -1, 0);
            
            default: return Vector3Int.zero;
        }
    }
    
    // 이웃 타일 최단 경로 찾기
    public List<Vector3Int> FindPathToAdjacentCell(Vector3Int start, Vector3Int objectCell)
    {
        Direction[] dirs =
        {
            Direction.West, Direction.East,
            Direction.NW, Direction.NE,
            Direction.SW, Direction.SE
        };

        List<Vector3Int> bestPath = null;
        bool odd = (objectCell.y & 1) != 0;

        foreach (var dir in dirs)
        {
            Vector3Int offset = GetOffsetForDirection(dir, odd);
            Vector3Int adj = objectCell + offset;

            if (!IsWalkableCell(adj))
                continue;

            var path = FindPath(start, adj);
            if (path == null || path.Count < 2)
                continue;

            if (bestPath == null || path.Count < bestPath.Count)
                bestPath = path;
        }

        return bestPath;
    }
}
