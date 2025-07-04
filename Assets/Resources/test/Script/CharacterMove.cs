using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

// Tile Ÿ�� ����
public enum TileType
{
    None,   // �ƹ��͵� ���� ����
    Tile,   // �ٴ� Ÿ��(�̵� ����)
    Wall    // ��(�̵� �Ұ�)
}

public class CharacterMove : MonoBehaviour
{
    public float moveSpeed = 3f;

    private Queue<Vector3> pathQueue = new Queue<Vector3>();
    private Vector3 targetPosition;
    private bool isMoving = false;

    private Dictionary<Vector3Int, TileType> logicMap;   // ������ Dictionary��!
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
        // ���� �غ� ���
        if (!mapReady)
        {
            if (stageBuilder.logicMap != null && stageBuilder.logicMap.Count > 0)
            {
                logicMap = stageBuilder.logicMap;
                mapReady = true;
                Debug.Log("logicMap �ε� �Ϸ�! (Dictionary ����)");
            }
            else
            {
                return; // logicMap�� ������ ������ ��ٸ�
            }
        }

        bool canUpdatePath = false;

        // �巡�� ���̰� (���콺 ��Ŭ��) + ��� ���� or ��ǥ�� ���� ������ ��츸 ��� ����
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

                // ���� ��ǥ ���� Ÿ��
                TileType goalType = logicMap.ContainsKey(goalCell) ? logicMap[goalCell] : TileType.None;

                if (goalType == TileType.None)
                {
                    float minDist = float.MaxValue;
                    Vector3Int bestTile = startCell;

                    // ���� ��ü���� reachable(��ΰ� �ִ�) Ÿ�� ��
                    // Ŭ��(goalCell)�� ���� ����� Ÿ���� ã��
                    foreach (var pair in logicMap)
                    {
                        if (pair.Value != TileType.Tile)
                            continue;
                        // ���� ���� ����
                        if (pair.Key == startCell)
                            continue;

                        // A*�� ���� ��ΰ� �ִ��� Ȯ�� (�ִ� ��ΰ� ���� ����)
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
                        Debug.Log("���� ������ Ÿ���� ����");
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
                    pathQueue.Enqueue(stageBuilder.LogicCellToWorld(cell)); // ���� ���� �߽�

                if (pathQueue.Count > 0)
                {
                    targetPosition = pathQueue.Dequeue();
                    isMoving = true;
                }

#if UNITY_EDITOR
                VisualizePath(path); // ��� �ð�ȭ
#endif
            }
        }
        else
        {
            pathUpdateTimer = pathUpdateCooldown;
        }

        // ��� ���� �̵�
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

        //Ű�Է� �̵�
        KeyMove();
    }

    //Ű�Է� �̵� �Լ�(��� �̵� ����)
    void KeyMove()
    {
        Vector2 movementInput;
        movementInput.x = Input.GetAxisRaw("Horizontal");
        movementInput.y = Input.GetAxisRaw("Vertical");

        if (movementInput.sqrMagnitude > 0.01f)
        {
            isMoving = false;
            pathQueue.Clear();

            // ���� ��ġ(��)
            Vector3Int currentCell = stageBuilder.WorldToLogicCell(transform.position);
            Vector3 moveDir = movementInput.normalized;

            Vector3Int targetCell = stageBuilder.WorldToLogicCell(transform.position + (Vector3)moveDir * moveSpeed * Time.deltaTime);

            if (IsWalkable(targetCell))
            {
                transform.position += (Vector3)moveDir * moveSpeed * Time.deltaTime;
            }
        }
    }


    // A* �˰��� (Vector3Int ����)
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
                // ��ֹ� üũ
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
        return new List<Vector3Int>(); // ��� ����
    }

    // ����ư �Ÿ� (�������̸� �ٸ� �Ÿ� ��� ����)
    float Heuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // ��� ����
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

    // ���� Ÿ�ϸ� 6���� �̿� (���弿 ����, ������ ���� ���� ���� �迭 ���)
    // Even-R/¦�� �� ������ ����
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

    /// ���� ���� 6����(������) �̿� ��ȯ (���弿 ����)
    IEnumerable<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        // cell.y % 2 == 0: ¦����, cell.y % 2 == 1: Ȧ����
        var dirs = (cell.y % 2 == 0) ? evenRowDirs : oddRowDirs;
        foreach (var dir in dirs)
        {
            var neighbor = cell + dir;
            if (logicMap.ContainsKey(neighbor) && IsWalkable(neighbor))
                yield return neighbor;
        }
    }

    // �ش� ���� "�̵� ���� Ÿ��(Tile)"���� �Ǻ�
    bool IsWalkable(Vector3Int cell)
    {
        if (logicMap == null)
            return false;
        return logicMap.ContainsKey(cell) && logicMap[cell] == TileType.Tile;
    }

#if UNITY_EDITOR
    private List<Vector3> lastPathWorldPoints = new List<Vector3>();

    // ��� �ð�ȭ��: A* ��θ� ���� ������ ����
    void VisualizePath(List<Vector3Int> path)
    {
        lastPathWorldPoints.Clear();
        foreach (var cell in path)
            lastPathWorldPoints.Add(stageBuilder.LogicCellToWorld(cell));
    }

    // OnDrawGizmos�� ��� ����, �� �׸���
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

// �켱���� ť
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
