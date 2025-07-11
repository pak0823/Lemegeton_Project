using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 캐릭터 이동 (키, 마우스 A* 경로, Hex 맵 대응, MapGenerator 배치 구조 호환, Index 예외 방지 적용)
/// </summary>
public class CharacterMove : MonoBehaviour
{
    public float moveSpeed = 3f;
    public List<Tilemap> allFloorTilemaps; // MapGenerator에서 할당
    public List<Tilemap> allWallTilemaps;  // MapGenerator에서 할당

    private Queue<Vector3> pathQueue = new Queue<Vector3>();
    private Vector3 physicsTargetPos;
    private bool isMoving = false;
    private bool hasPhysicsTarget = false;
    private Vector2 keyMoveInput = Vector2.zero;

    private Rigidbody2D rigidbody2d;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;

    void Start()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        keyMoveInput.x = Input.GetAxisRaw("Horizontal");
        keyMoveInput.y = Input.GetAxisRaw("Vertical");
        spriteRenderer.flipX = keyMoveInput.x > 0;
        HandleMousePathInput();
    }

    void FixedUpdate()
    {
        if (HandleMousePathMove()) return;
        HandleKeyMovePhysics();
    }

    void HandleMousePathInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (allFloorTilemaps == null || allFloorTilemaps.Count == 0)
                return;
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int goalCell = allFloorTilemaps[0].WorldToCell(mouseWorldPos);
            Vector3Int startCell = allFloorTilemaps[0].WorldToCell(transform.position);
            List<Vector3Int> path = FindPathAStar(startCell, goalCell);
            if (path.Count == 0) return;
            pathQueue.Clear();
            foreach (var cell in path)
                pathQueue.Enqueue(allFloorTilemaps[0].GetCellCenterWorld(cell));
            if (pathQueue.Count > 0)
            {
                physicsTargetPos = pathQueue.Dequeue();
                isMoving = true;
                hasPhysicsTarget = true;
            }
        }
    }

    void HandleKeyMovePhysics()
    {
        if (keyMoveInput.sqrMagnitude > 0.01f)
        {
            animator.SetInteger("Move", 1);
            Vector3 nextPos = transform.position + (Vector3)keyMoveInput.normalized * moveSpeed * Time.fixedDeltaTime;
            Vector3 footPos = nextPos + (Vector3)boxCollider.offset + Vector3.down * boxCollider.size.y * 0.5f;
            // Index 에러 방지: 타일맵 리스트 유효성 체크
            if (allFloorTilemaps != null && allFloorTilemaps.Count > 0)
            {
                Vector3Int targetCell = allFloorTilemaps[0].WorldToCell(footPos);
                if (IsWalkable(targetCell))
                    rigidbody2d.MovePosition(nextPos);
            }
        }
        else
        {
            animator.SetInteger("Move", 0);
        }
    }

    bool HandleMousePathMove()
    {
        if (isMoving && hasPhysicsTarget)
        {
            Vector2 movePos = Vector2.MoveTowards(rigidbody2d.position, physicsTargetPos, moveSpeed * Time.fixedDeltaTime);
            Vector3 footPos = (Vector3)movePos + (Vector3)boxCollider.offset + Vector3.down * boxCollider.size.y * 0.5f;
            if (allFloorTilemaps != null && allFloorTilemaps.Count > 0)
            {
                Vector3Int footCell = allFloorTilemaps[0].WorldToCell(footPos);
                if (IsWalkable(footCell))
                {
                    rigidbody2d.MovePosition(movePos);
                    animator.SetInteger("Move", 1);
                }
            }
            if (Vector2.Distance(rigidbody2d.position, physicsTargetPos) < 0.05f)
            {
                if (pathQueue.Count > 0)
                    physicsTargetPos = pathQueue.Dequeue();
                else
                {
                    isMoving = false;
                    hasPhysicsTarget = false;
                    animator.SetInteger("Move", 0);
                }
            }
            return true;
        }
        return false;
    }

    // MapGenerator에서 MapPiece/CorridorPiece에 있는 모든 Tilemap을 자동 할당
    public void SetTilemaps(List<Tilemap> floors, List<Tilemap> walls)
    {
        allFloorTilemaps = floors;
        allWallTilemaps = walls;
    }

    bool IsWalkable(Vector3Int cell)
    {
        bool onFloor = false;
        if (allFloorTilemaps != null)
        {
            foreach (var tm in allFloorTilemaps)
            {
                if (tm.HasTile(cell)) { onFloor = true; break; }
            }
        }
        if (!onFloor) return false;
        if (allWallTilemaps != null)
        {
            foreach (var wall in allWallTilemaps)
            {
                if (wall.HasTile(cell)) return false;
            }
        }
        return true;
    }

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
            if (current == goal) return ReconstructPath(cameFrom, current);
            foreach (var neighbor in GetNeighbors(current))
            {
                if (!IsWalkable(neighbor)) continue;
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
        return new List<Vector3Int>();
    }

    float Heuristic(Vector3Int a, Vector3Int b)
    {
        // Hex 좌표일 경우 맨해튼 거리가 아니라 헥스 거리로 보정 필요(간단히 abs합으로 둠)
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
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
    IEnumerable<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        Vector3Int[] evenRowDirs = {
            new Vector3Int(+1,  0, 0), new Vector3Int(0,  +1, 0), new Vector3Int(-1, +1, 0),
            new Vector3Int(-1,  0, 0), new Vector3Int(-1, -1, 0), new Vector3Int(0,  -1, 0)
        };
        Vector3Int[] oddRowDirs = {
            new Vector3Int(+1,  0, 0), new Vector3Int(+1, +1, 0), new Vector3Int(0,  +1, 0),
            new Vector3Int(-1,  0, 0), new Vector3Int(0,  -1, 0), new Vector3Int(+1, -1, 0)
        };
        var dirs = (cell.y % 2 == 0) ? evenRowDirs : oddRowDirs;
        foreach (var dir in dirs)
        {
            var neighbor = cell + dir;
            if (IsWalkable(neighbor))
                yield return neighbor;
        }
    }
    public class PriorityQueue<T>
    {
        private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();
        public int Count => elements.Count;
        public void Enqueue(T item, float priority) { elements.Add(new KeyValuePair<T, float>(item, priority)); }
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
}
