using System.Collections.Generic;
using UnityEngine;

public class CharacterMove : MonoBehaviour
{
    public float moveSpeed = 3f;

    private Queue<Vector3> pathQueue = new Queue<Vector3>();
    private Vector3 targetPosition;
    private bool isMoving = false;

    private TileType[,] logicMap;
    private StageBuilder stageBuilder;
    private bool mapReady = false;

    private void Start()
    {
        stageBuilder = FindObjectOfType<StageBuilder>();
    }

    void Update()
    {
        
        //아직 logicMap이 준비되지 않았으면 대기
        if (!mapReady)
        {
            if (stageBuilder.logicMap != null)
            {
                logicMap = stageBuilder.logicMap;
                mapReady = true;
                Debug.Log("logicMap 로딩 완료!");
                Debug.Log("stageBuilder.logicMap:" + stageBuilder.logicMap);
            }
            else
            {
                return; // logicMap이 생성될 때까지 기다림
            }
        }

        // 마우스 우클릭 시 목표 경로 탐색
        if (Input.GetMouseButton(1))
        {
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int goalIdx = stageBuilder.WorldToLogicIndex(mouseWorldPos);
            Vector2Int startIdx = stageBuilder.WorldToLogicIndex(transform.position);

            if (goalIdx.x < 0 || goalIdx.x >= logicMap.GetLength(0) || goalIdx.y < 0 || goalIdx.y >= logicMap.GetLength(1))
            {
                Debug.LogWarning("Goal index is out of logicMap bounds!");
            }
            else
            {
                Debug.Log($"logicMap[goalIdx.x, goalIdx.y]: {logicMap[goalIdx.x, goalIdx.y]}");
            }

            if (!IsWalkable(goalIdx))
            {
                Debug.Log("Goal tile is not walkable!");
                return;
            }

            if (!IsWalkable(goalIdx))
            {
                Debug.Log("Goal tile is not walkable!");
                return;
            }
            if (!IsWalkable(startIdx))
            {
                Debug.Log("Start tile is not walkable!");
                return;
            }

            List<Vector2Int> path = FindPathAStar(startIdx, goalIdx);

            Debug.Log("Path count: " + path.Count);

            if (path.Count == 0)
            {
                Debug.Log("No path found.");
                return;
            }

            pathQueue.Clear();
            foreach (var tile in path)
            {
                Vector3 worldPos = stageBuilder.LogicIndexToWorld(tile);
                pathQueue.Enqueue(stageBuilder.LogicIndexToWorld(tile));
                Debug.Log($"Path idx: {tile}, worldPos: {worldPos}");
            }

            if (pathQueue.Count > 0)
            {
                targetPosition = pathQueue.Dequeue();
                isMoving = true;
            }

        }
        // 경로 따라 이동 중
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            // 목표에 거의 도달하면 다음 경로로 넘어감
            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                if (pathQueue.Count > 0)
                {
                    // 다음 타일 목표로 이동
                    targetPosition = pathQueue.Dequeue();
                    // isMoving true 유지
                }
                else
                {
                    isMoving = false; // 경로 끝, 이동 완료
                }
            }
        }
        else //pathQueue가 없고, 이동 중도 아니면 WASD 이동 가능
        {
            Vector2 movementInput;
            movementInput.x = Input.GetAxisRaw("Horizontal");
            movementInput.y = Input.GetAxisRaw("Vertical");
            movementInput.Normalize();
            transform.position += (Vector3)movementInput * moveSpeed * Time.deltaTime;
        }
    }

    // [A* 알고리즘 구현]
    List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int goal)
    {
        var openSet = new PriorityQueue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var fScore = new Dictionary<Vector2Int, float>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();

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

        return new List<Vector2Int>(); // 경로 없음
    }

    float Heuristic(Vector2Int a, Vector2Int b)
    {
        // 맨해튼 거리 (직선 거리로 바꾸고 싶으면 Vector2Int.Distance 사용)
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> totalPath = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Insert(0, current);
        }
        return totalPath;
    }

    IEnumerable<Vector2Int> GetNeighbors(Vector2Int tile)
    {
        int width = logicMap.GetLength(0);
        int height = logicMap.GetLength(1);
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        for (int i = 0; i < 8; i++)
        {
            int nx = tile.x + dx[i];
            int ny = tile.y + dy[i];
            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                yield return new Vector2Int(nx, ny);
        }
    }


    bool IsWalkable(Vector2Int tile)
    {
        if (logicMap == null)
        {
            Debug.LogError("logicMap이 null!");
            return false;
        }
        if (tile.x < 0 || tile.x >= logicMap.GetLength(0) ||
            tile.y < 0 || tile.y >= logicMap.GetLength(1))
        {
            Debug.LogWarning($"IsWalkable: Out of bounds! tile=({tile.x},{tile.y})");
            return false;
        }
        return logicMap[tile.x, tile.y] != TileType.Wall;
    }


    // [타일 좌표 <-> 월드 좌표 변환 함수는 프로젝트에 맞게 구현 필요]
    Vector2Int WorldToTilePosition(Vector2 worldPos)
    {
        // 타일 크기 1 기준 (예시)
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
    }
    Vector3 TileToWorldPosition(Vector2Int tilePos)
    {
        return new Vector3(tilePos.x, tilePos.y, 0);
    }
}

// Tile 타입 예시
public enum TileType
{
    Empty, // 이동 가능
    Wall   // 이동 불가
}

// 간단한 PriorityQueue 구현 예시 (또는 C#의 SortedSet 등 사용 가능)
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
