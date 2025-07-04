using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

// Tile 타입 예시
public enum TileType
{
    None,   // 아무것도 없는 공간
    Tile,   // 바닥 타일(이동 가능)
    Wall    // 벽(이동 불가)
}

public class CharacterMove : MonoBehaviour
{
    public float moveSpeed = 3f;

    private Queue<Vector3> pathQueue = new Queue<Vector3>();
    private Vector3 targetPosition;
    private bool isMoving = false;

    private Dictionary<Vector3Int, TileType> logicMap;   // 논리맵을 Dictionary로!
    private StageBuilder stageBuilder;
    private bool mapReady = false;

    private float pathUpdateCooldown = 0.2f;
    private float pathUpdateTimer = 0f;

    private void Start()
    {
        stageBuilder = FindObjectOfType<StageBuilder>();
    }

    void Update()
    {
        // 논리맵 준비 대기
        if (!mapReady)
        {
            if (stageBuilder.logicMap != null && stageBuilder.logicMap.Count > 0)
            {
                logicMap = stageBuilder.logicMap;
                mapReady = true;
                Debug.Log("logicMap 로딩 완료! (Dictionary 구조)");
            }
            else
            {
                return; // logicMap이 생성될 때까지 기다림
            }
        }

        bool canUpdatePath = false;

        // 드래그 중이고 (마우스 우클릭) + 경로 없음 or 목표에 거의 도달한 경우만 경로 갱신
        if (Input.GetMouseButton(1))
        {
            if (!isMoving || Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                canUpdatePath = true;
            }

            pathUpdateTimer += Time.deltaTime;
            if (canUpdatePath)
            {
                Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector3Int goalCell = stageBuilder.WorldToLogicCell(mouseWorldPos);
                Vector3Int startCell = stageBuilder.WorldToLogicCell(transform.position);
                List<Vector3Int> path = FindPathAStar(startCell, goalCell);

                // 원래 목표 셀의 타입
                TileType goalType = logicMap.ContainsKey(goalCell) ? logicMap[goalCell] : TileType.None;

                if (goalType == TileType.None)
                {
                    float minDist = float.MaxValue;
                    Vector3Int bestTile = startCell;

                    // 논리맵 전체에서 reachable(경로가 있는) 타일 중
                    // 클릭(goalCell)과 가장 가까운 타일을 찾음
                    foreach (var pair in logicMap)
                    {
                        if (pair.Value != TileType.Tile)
                            continue;
                        // 본인 셀은 무시
                        if (pair.Key == startCell)
                            continue;

                        // A*로 실제 경로가 있는지 확인 (최단 경로가 있을 때만)
                        var p = FindPathAStar(startCell, pair.Key);
                        if (p.Count > 0)
                        {
                            float dist = Vector3Int.Distance(pair.Key, goalCell);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                bestTile = pair.Key;
                            }
                        }
                    }

                    if (bestTile != startCell)
                    {
                        path = FindPathAStar(startCell, bestTile);
                        goalCell = bestTile;
                    }
                    else
                    {
                        Debug.Log("도달 가능한 타일이 없음");
                        return;
                    }
                }
                else
                {
                    path = FindPathAStar(startCell, goalCell);
                }

                if (!IsWalkable(goalCell) || !IsWalkable(startCell))
                    return;

                
                if (path.Count == 0)
                {
                    Debug.Log("No path found.");
                    return;
                }

                pathQueue.Clear();
                foreach (var cell in path)
                    pathQueue.Enqueue(stageBuilder.LogicCellToWorld(cell)); // 셀의 월드 중심

                if (pathQueue.Count > 0)
                {
                    targetPosition = pathQueue.Dequeue();
                    isMoving = true;
                }

#if UNITY_EDITOR
                VisualizePath(path); // 경로 시각화
#endif
            }
        }
        else
        {
            pathUpdateTimer = pathUpdateCooldown;
        }

        // 경로 따라 이동
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                if (pathQueue.Count > 0)
                {
                    targetPosition = pathQueue.Dequeue();
                }
                else
                {
                    isMoving = false;
                }
            }
        }

        //키입력 이동
        KeyMove();
    }

    //키입력 이동 함수(헥사 이동 가능)
    void KeyMove()
    {
        Vector2 movementInput;
        movementInput.x = Input.GetAxisRaw("Horizontal");
        movementInput.y = Input.GetAxisRaw("Vertical");

        if (movementInput.sqrMagnitude > 0.01f)
        {
            isMoving = false;
            pathQueue.Clear();

            // 현재 위치(셀)
            Vector3Int currentCell = stageBuilder.WorldToLogicCell(transform.position);
            Vector3 moveDir = movementInput.normalized;

            Vector3Int targetCell = stageBuilder.WorldToLogicCell(transform.position + (Vector3)moveDir * moveSpeed * Time.deltaTime);

            if (IsWalkable(targetCell))
            {
                transform.position += (Vector3)moveDir * moveSpeed * Time.deltaTime;
            }
        }
    }


    // A* 알고리즘 (Vector3Int 기준)
    List<Vector3Int> FindPathAStar(Vector3Int start, Vector3Int goal)
    {
        var openSet = new PriorityQueue<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float>();
        var fScore = new Dictionary<Vector3Int, float>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            Vector3Int current = openSet.Dequeue();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GetNeighbors(current))
            {
                // 장애물 체크
                if (!IsWalkable(neighbor))
                    continue;

                float tentativeGScore = gScore[current] + 1;
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goal);
                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }
        return new List<Vector3Int>(); // 경로 없음
    }

    // 맨해튼 거리 (육각형이면 다른 거리 사용 가능)
    float Heuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // 경로 복원
    List<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
    {
        List<Vector3Int> totalPath = new List<Vector3Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Insert(0, current);
        }
        return totalPath;
    }

    // 육각 타일맵 6방향 이웃 (월드셀 기준, 오프셋 구분 없이 단일 배열 사용)
    // Even-R/짝수 행 오프셋 기준
    static readonly Vector3Int[] evenRowDirs = {
    new Vector3Int(+1,  0, 0),
    new Vector3Int(0,  +1, 0),
    new Vector3Int(-1, +1, 0),
    new Vector3Int(-1,  0, 0),
    new Vector3Int(-1, -1, 0),
    new Vector3Int(0,  -1, 0)
};
    static readonly Vector3Int[] oddRowDirs = {
    new Vector3Int(+1,  0, 0),
    new Vector3Int(+1, +1, 0),
    new Vector3Int(0,  +1, 0),
    new Vector3Int(-1,  0, 0),
    new Vector3Int(0,  -1, 0),
    new Vector3Int(+1, -1, 0)
};

    /// 현재 셀의 6방향(육각형) 이웃 반환 (월드셀 기준)
    IEnumerable<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        // cell.y % 2 == 0: 짝수행, cell.y % 2 == 1: 홀수행
        var dirs = (cell.y % 2 == 0) ? evenRowDirs : oddRowDirs;
        foreach (var dir in dirs)
        {
            var neighbor = cell + dir;
            if (logicMap.ContainsKey(neighbor) && IsWalkable(neighbor))
                yield return neighbor;
        }
    }

    // 해당 셀이 "이동 가능 타일(Tile)"인지 판별
    bool IsWalkable(Vector3Int cell)
    {
        if (logicMap == null)
            return false;
        return logicMap.ContainsKey(cell) && logicMap[cell] == TileType.Tile;
    }

#if UNITY_EDITOR
    private List<Vector3> lastPathWorldPoints = new List<Vector3>();

    // 경로 시각화용: A* 경로를 만들 때마다 저장
    void VisualizePath(List<Vector3Int> path)
    {
        lastPathWorldPoints.Clear();
        foreach (var cell in path)
            lastPathWorldPoints.Add(stageBuilder.LogicCellToWorld(cell));
    }

    // OnDrawGizmos로 경로 라인, 점 그리기
    void OnDrawGizmos()
    {
        if (lastPathWorldPoints == null || lastPathWorldPoints.Count == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < lastPathWorldPoints.Count; i++)
        {
            Gizmos.DrawSphere(lastPathWorldPoints[i], 0.18f);
            if (i > 0)
                Gizmos.DrawLine(lastPathWorldPoints[i - 1], lastPathWorldPoints[i]);
        }
    }
#endif
}

// 우선순위 큐
public class PriorityQueue<T>
{
    private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();

    public int Count => elements.Count;
    public void Enqueue(T item, float priority)
    {
        elements.Add(new KeyValuePair<T, float>(item, priority));
    }
    public T Dequeue()
    {
        int bestIndex = 0;
        float bestPriority = elements[0].Value;
        for (int i = 1; i < elements.Count; i++)
        {
            if (elements[i].Value < bestPriority)
            {
                bestPriority = elements[i].Value;
                bestIndex = i;
            }
        }
        T bestItem = elements[bestIndex].Key;
        elements.RemoveAt(bestIndex);
        return bestItem;
    }
    public bool Contains(T item)
    {
        foreach (var element in elements)
            if (EqualityComparer<T>.Default.Equals(element.Key, item)) return true;
        return false;
    }
}
