using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.GraphicsBuffer;

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

    private bool isPushMode = false;
    private Direction pendingDirectionKey = Direction.None;
    private PuzzleBox selectedBox = null;       // 현재 선택된 박스
    public float selectRadius = 0.6f;   //박스 선택 범위

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

        // 1) F키 눌러 “박스 선택 모드” 진입
        if (!isPushMode
         && Shared.PuzzleManager.IsPuzzleActive
         && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space)))
        {
            // 플레이어 근처의 박스 탐색
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                selectRadius,
                LayerMask.GetMask("Box")
            );
            if (hits.Length > 0)
            {
                // 첫 번째 박스를 선택
                selectedBox = hits[0].GetComponent<PuzzleBox>();
                if (selectedBox != null)
                {
                    isPushMode = true;
                    path.Clear();
                    Debug.Log($"[Select] 박스 선택: {selectedBox.name}");
                }
            }
        }

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

        if (isPushMode)
        {
            // 화살표 조합 검사
            bool up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            bool down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);

            if (up && left) pendingDirectionKey = Direction.NW;
            else if (up && right) pendingDirectionKey = Direction.NE;
            else if (down && left) pendingDirectionKey = Direction.SW;
            else if (down && right) pendingDirectionKey = Direction.SE;
            else if (left) pendingDirectionKey = Direction.West;
            else if (right) pendingDirectionKey = Direction.East;
            else return;  // 다른 입력 무시

            return;  // FixedUpdate로 넘김
        }

        input = !isMouseMove ? keyInput.normalized : Vector2.zero;
    }

    void FixedUpdate()
    {
        // 캐싱
        int pathCount = path.Count;
        float inputSq = input.sqrMagnitude;
        var puzzleMgr = Shared.PuzzleManager;
        bool puzzleActive = puzzleMgr != null && puzzleMgr.IsPuzzleActive;

        // 플레이어가 있는 셀과 패리티 계산
        Vector3Int playerCell = floorTilemap.WorldToCell(rb.position);
        bool playerOdd = (playerCell.y & 1) == 1;

        // 애니메이션 업데이트
        bool isMoving = pathCount > 0 || inputSq > 0f;
        animator.SetInteger("Move", isMoving ? 1 : 0);

        // 푸시 모드 처리 (선택된 박스에만 적용)
        if (isPushMode && selectedBox != null && pendingDirectionKey != Direction.None && puzzleActive)
        {
            // 플레이어 → 박스
            Vector3Int d1 = GetOffsetForKey(pendingDirectionKey, playerOdd);
            Vector3Int boxCell = playerCell + d1;
            bool boxOdd = (boxCell.y & 1) == 1;

            // 박스 → 목표
            Vector3Int d2 = GetOffsetForKey(pendingDirectionKey, boxOdd);
            Vector3Int targetCell = boxCell + d2;

            Debug.Log($"[Push] 시도: Key={pendingDirectionKey}, playerCell={playerCell}, boxCell={boxCell}, targetCell={targetCell}");

            // 밀기 시도
            if (puzzleMgr.TryPush(boxCell, d2))
            {
                Debug.Log($"[Push] 성공 → 상자 이동 {boxCell} → {targetCell}");
                //// 플레이어를 박스 원래 위치로 이동
                //Vector3 newPlayerPos = floorTilemap.GetCellCenterWorld(boxCell);
                //rb.MovePosition(newPlayerPos);
                StartCoroutine(PerformPush(selectedBox, boxCell, d2));
            }
            else
            {
                Debug.Log($"[Push] 실패: 밀 수 없는 방향 또는 장애물 존재");
            }

            // 푸시 모드 해제
            isPushMode = false;
            pendingDirectionKey = Direction.None;
            selectedBox = null;
            return;
        }

        // 5. 일반 이동 로직
        Vector2 moveDir = Vector2.zero;

        if (pathCount > 0)
        {
            // A* 경로 따라 이동
            Vector3 targetWorld = path[currentPathIndex];
            Vector2 newPos = Vector2.MoveTowards(rb.position, (Vector2)targetWorld, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            moveDir = ((Vector2)targetWorld - rb.position).normalized;

            if (Vector2.Distance(rb.position, (Vector2)targetWorld) < 0.05f)
            {
                currentPathIndex++;
                if (currentPathIndex >= pathCount)
                {
                    path.Clear();
                    isMouseMove = false;
                }
            }
        }
        else if (inputSq > 0f)
        {
            // 키보드 직접 이동
            moveDir = input;
            Vector2 newPos = rb.position + input * moveSpeed * Time.fixedDeltaTime;
            Vector3Int tgtCell = floorTilemap.WorldToCell(newPos);
            if (IsWalkableCell(tgtCell))
                rb.MovePosition(newPos);
        }

        // 스프라이트 좌우 뒤집기
        if (moveDir.x != 0f)
            spriterenderer.flipX = moveDir.x > 0f;
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

    IEnumerator PerformPush(PuzzleBox box, Vector3Int boxCell, Vector3Int dir)
    {
        // 1) Push 애니메이션 트리거
        //animator.SetTrigger("Push");

        // 2) 애니메이션 타이밍에 맞춘 duration
        float duration = 0.2f;

        // 3) 시작/목표 위치 계산
        Vector3 fromBox = box.transform.position;
        Vector3 toBox = floorTilemap.GetCellCenterWorld(boxCell + dir);
        Vector3 fromPlayer = rb.position;
        Vector3 toPlayer = fromBox;

        // 4) 동시에 Lerp
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            box.transform.position = Vector3.Lerp(fromBox, toBox, t);
            rb.MovePosition(Vector3.Lerp(fromPlayer, toPlayer, t));
            yield return null;
        }
        // 5) 최종 위치 보정
        box.transform.position = toBox;
        rb.MovePosition(toPlayer);


        Shared.PuzzleManager.ExecutePush(boxCell, boxCell + dir);

        // 7) Push 모드 해제 등
        isPushMode = false;
        pendingDirectionKey = Direction.None;
        selectedBox = null;
    }


    //퀴즈맵에서 리셋 실행 시 초기화 될 플레이어 위치 함수
    public void TeleportTo(Vector3 worldPos)
    {
        rb.position = worldPos;
        transform.position = worldPos;
        ClearPath();          // 남아 있는 경로 초기화
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

    // 오프셋 계산 헬퍼 (짝/홀 행에 따라 달라짐)
    Vector3Int GetOffsetForKey(Direction dir, bool oddRow)
    {
        switch (dir)
        {
            case Direction.West: return new Vector3Int(-1, 0, 0);
            case Direction.East: return new Vector3Int(1, 0, 0);

            case Direction.NW:
                return oddRow
                    ? new Vector3Int(0, 1, 0)  // 홀수 행
                    : new Vector3Int(-1, 1, 0); // 짝수 행

            case Direction.NE:
                return oddRow
                    ? new Vector3Int(1, 1, 0)
                    : new Vector3Int(0, 1, 0);

            case Direction.SW:
                return oddRow
                    ? new Vector3Int(0, -1, 0)
                    : new Vector3Int(-1, -1, 0);

            case Direction.SE:
                return oddRow
                    ? new Vector3Int(1, -1, 0)
                    : new Vector3Int(0, -1, 0);

            default: return Vector3Int.zero;
        }
    }




    void OnDrawGizmos() //이동 경로 표시용
    {
        if (path == null || path.Count < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < path.Count - 1; i++)
            Gizmos.DrawLine(path[i], path[i + 1]);
    }
}
