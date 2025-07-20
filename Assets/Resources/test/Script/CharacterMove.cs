using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerMovement : MonoBehaviour
{
    [Header("타일맵 설정")]
    public Tilemap tilemap;
    public float moveSpeed = 2f;

    [Header("경로 방해 오브젝트 레이어")]
    public LayerMask impassableLayerMask;

    private Rigidbody2D rb;
    private Vector2 input;
    private bool isMouseMove = false;
    private List<Vector3> path = new List<Vector3>();
    private int currentPathIndex = 0;

    SpriteRenderer spriterenderer;
    Animator animator;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriterenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 키 입력 감지 시 마우스 이동 취소
        Vector2 keyInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (isMouseMove && keyInput.sqrMagnitude > 0f)
        {
            if(keyInput.x > 0f)
            {
                spriterenderer.flipX = false;
            }
            else
            {
                spriterenderer.flipX = true;
            }
            animator.SetInteger("Move", 1);

            isMouseMove = false;
            path.Clear();
        }

        // 우클릭을 누르고 있는 동안 목표 갱신
        if (Input.GetMouseButton(1))
        {
            Vector3 screenPos = Input.mousePosition;
            screenPos.z = -Camera.main.transform.position.z;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            SetTargetPosition(worldPos);
        }

        // 마우스 이동 중이 아니면 키 입력 반영
        input = !isMouseMove ? keyInput.normalized : Vector2.zero;
    }

    void FixedUpdate()
    {
        if (path.Count > 0)
        {
            Vector3 target = path[currentPathIndex];
            Vector2 newPos = Vector2.MoveTowards(rb.position, (Vector2)target, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            if (Vector2.Distance(rb.position, (Vector2)target) < 0.05f)
            {
                currentPathIndex++;
                if (currentPathIndex >= path.Count)
                {
                    path.Clear();
                    isMouseMove = false;
                }
            }
        }
        else if (input.sqrMagnitude > 0f)
        {
            Vector2 targetPos = rb.position + input * moveSpeed * Time.fixedDeltaTime;
            if (IsValidMove(targetPos))
                rb.MovePosition(targetPos);
        }
    }

    void SetTargetPosition(Vector3 worldTarget)
    {
        Vector3Int cell = tilemap.WorldToCell(worldTarget);
        if (!tilemap.cellBounds.Contains(cell) || !IsValidCell(cell))
            return;

        Vector3 cellCenter = tilemap.GetCellCenterWorld(cell);
        path = FindPath(transform.position, cellCenter);
        currentPathIndex = 0;
        isMouseMove = path.Count > 0;
    }

    List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Vector3Int start = tilemap.WorldToCell(startPos);
        Vector3Int goal = tilemap.WorldToCell(targetPos);

        var openList = new List<Vector3Int> { start };
        var closedList = new HashSet<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float> { [start] = 0f };
        var fScore = new Dictionary<Vector3Int, float> { [start] = HexDistance(start, goal) }; // ★ 수정됨

        while (openList.Count > 0)
        {
            Vector3Int current = openList.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();

            if (current == goal)
            {
                var newPath = new List<Vector3>();
                while (cameFrom.ContainsKey(current))
                {
                    newPath.Insert(0, tilemap.GetCellCenterWorld(current));
                    current = cameFrom[current];
                }
                return newPath;
            }

            openList.Remove(current);
            closedList.Add(current);

            foreach (Vector3 neighborWorld in GetNeighbors(current))
            {
                Vector3Int neighbor = tilemap.WorldToCell(neighborWorld);
                if (closedList.Contains(neighbor))
                    continue;

                // 이동 비용 조정: 대각선(헥스 비직교)일 때 가중치 높임
                Vector3Int dir = neighbor - current;
                float moveCost = (Mathf.Abs(dir.x) + Mathf.Abs(dir.y) > 1) ? 1.4f : 1f;
                float tentativeG = gScore[current] + moveCost;

                if (!openList.Contains(neighbor))
                    openList.Add(neighbor);
                else if (tentativeG >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + HexDistance(neighbor, goal);
            }
        }

        Debug.LogWarning("경로를 찾지 못했습니다.");
        return new List<Vector3>();
    }

    // 6방향(헥스) 이웃 추출
    List<Vector3> GetNeighbors(Vector3Int current)
    {
        var results = new List<Vector3>();
        Vector3Int[] dirs = {
            new Vector3Int( 1,  0, 0),
            new Vector3Int(-1,  0, 0),
            new Vector3Int( 0,  1, 0),
            new Vector3Int( 0, -1, 0),
            new Vector3Int( 1, -1, 0),
            new Vector3Int(-1,  1, 0),
        };

        foreach (var d in dirs)
        {
            var cell = current + d;
            // 타일+오브젝트 검사
            if (tilemap.cellBounds.Contains(cell)
             && IsValidCell(cell)
             && !HasImpassableObject(cell))
            {
                results.Add(tilemap.GetCellCenterWorld(cell));
            }
        }
        return results;
    }

    // 헥스 전용 휴리스틱(허용된) 거리 계산
    int HexDistance(Vector3Int a, Vector3Int b)
    {
        return (Mathf.Abs(a.x - b.x)
              + Mathf.Abs(a.y - b.y)
              + Mathf.Abs((a.x + a.y) - (b.x + b.y))) / 2;
    }

    bool IsValidMove(Vector3 worldPos)
    {
        Vector3Int cell = tilemap.WorldToCell(worldPos);
        return tilemap.GetTile(cell) != null && tilemap.GetTile(cell).name.Contains("Floor");
    }

    bool IsValidCell(Vector3Int cell)
    {
        return tilemap.GetTile(cell) != null && tilemap.GetTile(cell).name.Contains("Floor");
    }

    bool HasImpassableObject(Vector3Int cell)
    {
        Vector3 worldCenter = tilemap.GetCellCenterWorld(cell);
        // 작은 반지름으로 오브젝트 충돌 체크
        Collider2D hit = Physics2D.OverlapCircle(worldCenter, 0.1f, impassableLayerMask);
        return hit != null;
    }

    public void SetTilemap(Tilemap map)
    {
        tilemap = map;
    }
}
