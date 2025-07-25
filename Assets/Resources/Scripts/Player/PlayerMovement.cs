using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerMovement : MonoBehaviour
{
    [Header("타일맵 설정")]
    public Tilemap floorTilemap;     // 바닥 타일맵
    public Tilemap wallTilemap;      // 벽 타일맵 (이동 불가)
    public float moveSpeed = 2f;

    [Header("경로 방해 오브젝트 레이어")]
    public LayerMask impassableLayerMask;

    private Rigidbody2D rb;
    private Vector2 input;
    private bool isMouseMove = false;
    private List<Vector3> path = new List<Vector3>();
    private int currentPathIndex = 0;

    private bool pushRequested = false;
    private Vector3Int pushDir;

    SpriteRenderer spriterenderer;
    Animator animator;

    void Awake()
    {
        Shared.PlayerMovement = this;
        rb = GetComponent<Rigidbody2D>();
        spriterenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        Vector2 keyInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (isMouseMove && keyInput.sqrMagnitude > 0f)
        {
            spriterenderer.flipX = keyInput.x < 0f;
            isMouseMove = false;
            path.Clear();
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 screenPos = Input.mousePosition;
            screenPos.z = -Camera.main.transform.position.z;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            SetTargetPosition(worldPos);
        }

        if (Shared.PuzzleManager != null
            && Shared.PuzzleManager.IsPuzzleActive
            && Input.GetKey(KeyCode.F))
        {
            Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (raw.sqrMagnitude > 0f)
            {
                // 대각선도 ±1로 정규화
                pushDir = new Vector3Int((int)Mathf.Sign(raw.x), (int)Mathf.Sign(raw.y), 0);
                pushRequested = true;
            }
        }

        input = !isMouseMove ? keyInput.normalized : Vector2.zero;
    }

    void FixedUpdate()
    {
        // 1) 현재 셀 좌표
        Vector3Int playerCell = floorTilemap.WorldToCell(rb.position);

        // 현재 이동 상태에 따른 애니메이션 실행
        bool isMovingNow = (path.Count > 0) || (input.sqrMagnitude > 0f);
        animator.SetInteger("Move", isMovingNow ? 1 : 0);

        if (pushRequested && Shared.PuzzleManager != null && Shared.PuzzleManager.IsPuzzleActive)
        {
            // PuzzleManager.TryPush 시도
            if (Shared.PuzzleManager.TryPush(playerCell, pushDir))
            {
                // 밀기에 성공하면 플레이어도 한 칸 전진
                Vector3 newWorld = floorTilemap.GetCellCenterWorld(playerCell + pushDir);
                rb.MovePosition(newWorld);
            }
            // 밀기 요청 소멸, 기존 경로·라인 지우기
            pushRequested = false;
            path.Clear();
            //ClearPathVisualization();
            return;
        }


        // 방향 벡터 계산
        Vector2 moveDir = Vector2.zero;

        if (path.Count > 0)
        {
            Vector3 target = path[currentPathIndex];
            Vector2 newPos = Vector2.MoveTowards(rb.position, (Vector2)target, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            // 이동하는 방향으로 이미지 방향 설정
            moveDir = ((Vector2)target - rb.position).normalized;

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
            //입력된 방향으로 이미지 방향 설정
            moveDir = input;

            Vector2 targetPos = rb.position + input * moveSpeed * Time.fixedDeltaTime;
            Vector3Int targetCell = floorTilemap.WorldToCell(targetPos);
            Vector3Int dir = targetCell - playerCell;

            // 3) 밀기 시도
            if (Shared.PuzzleManager != null
             && Shared.PuzzleManager.TryPush(playerCell, dir))
            {
                // 상자 밀기에 성공했으니 플레이어도 한 칸 전진
                Vector3 worldPos = floorTilemap.GetCellCenterWorld(playerCell + dir);
                rb.MovePosition(worldPos);
                return;
            }


            if (IsWalkableCell(targetCell))
                rb.MovePosition(targetPos);
        }

        // flipX 토글 (기본 스프라이트가 왼쪽 바라보므로, x>0일 때 뒤집기)
        if (moveDir.x != 0)
            spriterenderer.flipX = (moveDir.x > 0);
    }

    void SetTargetPosition(Vector3 worldTarget)
    {
        Vector3Int cell = floorTilemap.WorldToCell(worldTarget);
        if (!floorTilemap.cellBounds.Contains(cell) || !IsWalkableCell(cell))
            return;

        Vector3 cellCenter = floorTilemap.GetCellCenterWorld(cell);
        path = FindPath(transform.position, cellCenter);
        currentPathIndex = 0;
        isMouseMove = path.Count > 0;
    }

    public void ClearPath()
    {
        isMouseMove = false;
        path.Clear();
    }


    List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Vector3Int start = floorTilemap.WorldToCell(startPos);
        Vector3Int goal = floorTilemap.WorldToCell(targetPos);

        var openList = new List<Vector3Int> { start };
        var closedList = new HashSet<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float> { [start] = 0f };
        var fScore = new Dictionary<Vector3Int, float> { [start] = HexDistance(start, goal) };

        while (openList.Count > 0)
        {
            Vector3Int current = openList.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();

            if (current == goal)
            {
                var newPath = new List<Vector3>();
                while (cameFrom.ContainsKey(current))
                {
                    newPath.Insert(0, floorTilemap.GetCellCenterWorld(current));
                    current = cameFrom[current];
                }
                return newPath;
            }

            openList.Remove(current);
            closedList.Add(current);

            foreach (Vector3 neighborWorld in GetNeighbors(current))
            {
                Vector3Int neighbor = floorTilemap.WorldToCell(neighborWorld);
                if (closedList.Contains(neighbor))
                    continue;

                float moveCost = (Mathf.Abs(neighbor.x - current.x) + Mathf.Abs(neighbor.y - current.y) > 1) ? 1.4f : 1f;
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

    List<Vector3> GetNeighbors(Vector3Int current)
    {
        var results = new List<Vector3>();
        Vector3Int[] dirs = {
        new Vector3Int( 1,  0, 0),
        new Vector3Int(-1,  0, 0),
        new Vector3Int( 0,  1, 0),
        new Vector3Int( 0, -1, 0),
        new Vector3Int( 1, -1, 0),  // 대각선
        new Vector3Int(-1,  1, 0),  // 대각선
    };

        foreach (var d in dirs)
        {
            var cell = current + d;

            // 1) 바운드 & 바닥/벽/오브젝트 검사
            if (!floorTilemap.cellBounds.Contains(cell)
             || !IsWalkableCell(cell)
             || HasImpassableObject(cell))
                continue;

            // 2) 대각선 코너컷 방지
            if (Mathf.Abs(d.x) == 1 && Mathf.Abs(d.y) == 1)
            {
                var c1 = current + new Vector3Int(d.x, 0, 0);
                var c2 = current + new Vector3Int(0, d.y, 0);

                // c1, c2 중 하나라도 걸을 수 없으면 대각선 이동 금지
                if (!floorTilemap.cellBounds.Contains(c1)
                 || !IsWalkableCell(c1)
                 || HasImpassableObject(c1)
                 || !floorTilemap.cellBounds.Contains(c2)
                 || !IsWalkableCell(c2)
                 || HasImpassableObject(c2))
                    continue;
            }

            // 3) 모든 검사를 통과했을 때만 추가
            results.Add(floorTilemap.GetCellCenterWorld(cell));
        }

        return results;
    }

    int HexDistance(Vector3Int a, Vector3Int b)
    {
        return (Mathf.Abs(a.x - b.x)
              + Mathf.Abs(a.y - b.y)
              + Mathf.Abs((a.x + a.y) - (b.x + b.y))) / 2;
    }

    /// <summary>
    /// 바닥 타일맵에 Floor 타일이 있고, 벽 타일맵에 타일이 없으며,
    /// 경로 방해 오브젝트가 없는 셀만 걷기 가능
    /// </summary>
    bool IsWalkableCell(Vector3Int cell)
    {
        if (!floorTilemap.HasTile(cell) || !floorTilemap.GetTile(cell).name.Contains("Floor"))
            return false;
        if (wallTilemap != null && wallTilemap.HasTile(cell))
            return false;
        return true;
    }

    bool HasImpassableObject(Vector3Int cell)
    {
        Vector3 worldCenter = floorTilemap.GetCellCenterWorld(cell);
        Collider2D hit = Physics2D.OverlapCircle(worldCenter, 0.1f, impassableLayerMask);
        return hit != null;
    }

    // floor와 wall 타일맵을 한 번에 세팅하고, 경로도 초기화
    public void SetTilemap(Tilemap map, Tilemap wallMap)
    {
        floorTilemap = map;
        wallTilemap = wallMap;
        ClearPath();
    }

    void OnDrawGizmos() //이동 경로 표시용
    {
        if (path == null || path.Count < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < path.Count - 1; i++)
            Gizmos.DrawLine(path[i], path[i + 1]);
    }
}
