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
    private List<PuzzleBox> contactBoxes = new List<PuzzleBox>();
    private bool isPerformingPush = false; //이미 상자를 밀고 있는지 체크

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

        // 상호작용키를 눌러 박스 선택 모드 진입
        if (!isPushMode && Shared.PuzzleManager.IsPuzzleActive && !Shared.PuzzleManager.IsPuzzleComplete
            && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space)) && selectedBox != null)
        {
            if(contactBoxes.Count > 0)
            {
                // 접촉 중인 박스들 중 가장 가까운 것 선택
                selectedBox = contactBoxes
                    .OrderBy(b => Vector2.SqrMagnitude(
                        (Vector2)transform.position
                        - (Vector2)b.transform.position))
                    .First();
                isPushMode = true;
                path.Clear();
                Debug.Log($"[Select] 박스 선택: {selectedBox.name}");
            }
            return;
        }

        if (isPushMode)
        {
            animator.SetInteger("Move",0);
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

        if (isPerformingPush)
        {
            return;   //상자 밀기모드라면 이동 무시
        }
        else
        {
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
        }

        input = !isMouseMove ? keyInput.normalized : Vector2.zero;
    }

    void FixedUpdate()
    {
        if (isPerformingPush) return;

        // 캐싱
        int pathCount = path.Count;
        float inputSq = input.sqrMagnitude;
        var puzzleMgr = Shared.PuzzleManager;
        bool puzzleActive = puzzleMgr != null && puzzleMgr.IsPuzzleActive;

        // pendingDirectionKey 에 대응하는 반대 키
        Direction oppositeKey = pendingDirectionKey switch
        {
            Direction.West => Direction.East,
            Direction.East => Direction.West,
            Direction.NW => Direction.SE,
            Direction.NE => Direction.SW,
            Direction.SW => Direction.NE,
            Direction.SE => Direction.NW,
            _=> Direction.None
        };

        // 애니메이션 업데이트
        bool isMoving = pathCount > 0 || inputSq > 0f;
        animator.SetInteger("Move", isMoving ? 1 : 0);

        // 푸시 모드 처리 (선택된 박스에만 적용)
        if (isPushMode && selectedBox != null && pendingDirectionKey != Direction.None && puzzleActive && !Shared.PuzzleManager.IsPuzzleComplete)
        {
            // 선택된 박스 셀
            Vector3Int boxCell = selectedBox.CurrentCell;
            bool boxOdd = (boxCell.y & 1) == 1;

            // 뒤 오프셋 계산
            Vector3Int backOffset = GetOffsetForKey(oppositeKey, boxOdd);

            // 방향 오프셋과 목표 셀 계산
            Vector3Int dir = GetOffsetForKey(pendingDirectionKey, boxOdd);
            Vector3Int targetCell = boxCell + dir;

            // 플레이어 셀
            Vector3Int playerCell = floorTilemap.WorldToCell(rb.position);

            //플레이어가 박스 뒤쪽 방향에 있는지 확인
            if (playerCell != boxCell + backOffset)
            {
                Debug.Log("[Push] 실패: 박스 뒤에서만 밀 수 있습니다.");
                isPushMode = false;
                pendingDirectionKey = Direction.None;
                selectedBox = null;
                return;
            }

            // 밀기 시도
            if (puzzleMgr.TryPush(boxCell, dir))
            {
                StartCoroutine(PerformPush(selectedBox, boxCell, dir));
            }
            else
            {
                Debug.Log($"[Push] 실패: 이동 불가 target={targetCell}");
            }

            // 푸시 모드 초기화
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
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<PuzzleBox>(out var box))
        {
            selectedBox = box;
            contactBoxes.Add(box);
            Debug.Log($"[SelectTrigger] 박스 접촉: {box.name}");
        }
            
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<PuzzleBox>(out var box) && selectedBox == box)
        {
            selectedBox = null;
            contactBoxes.Remove(box);
            Debug.Log($"[SelectTrigger] 박스 이탈: {box.name}");
        }
            
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

    public void ClearPath() //마우스 이동 경로 초기화
    {
        isMouseMove = false;
        path.Clear();
    }

    //상자 밀기 전용 함수
    IEnumerator PerformPush(PuzzleBox box, Vector3Int boxCell, Vector3Int dir)
    {
        Debug.Log($"[PerformPush] 시작 → box={box.name}, boxCell={boxCell}, dir={dir}");
        if (isPerformingPush) yield break;
        isPerformingPush = true;

        // 시작/목표 위치 계산
        Vector3 fromBox = box.transform.position;
        Vector3 toBox = floorTilemap.GetCellCenterWorld(boxCell + dir);
        Vector3 fromPlayer = rb.position;
        Vector3 toPlayer = fromBox;

        Vector3 moveDir = (toBox - fromBox).normalized;
        spriterenderer.flipX = (moveDir.x > 0);

        // Push 애니메이션 관리
        // PushType 결정
        int pushType = dir.y > 0 ? 2     // y + : 아래 -> 위 대각
                     : dir.y < 0 ? 1    // y - : 위 -> 아래 대각
                     : 0;               // y == 0 : 좌/우 수평
 
        // 파라미터 세팅 & 트리거
        animator.SetInteger("PushDir", pushType);
        animator.SetTrigger("Push");

        // 애니메이션 타이밍에 맞춘 duration
        float duration = 0.35f;

        // 동시에 Lerp
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            box.transform.position = Vector3.Lerp(fromBox, toBox, t);
            rb.MovePosition(Vector3.Lerp(fromPlayer, toPlayer, t));
            yield return null;
        }

        // 최종 위치 보정
        box.transform.position = toBox;
        rb.MovePosition(toPlayer);

        Debug.Log($"[PerformPush] 이동 완료 → box={box.name}, targetCell={boxCell + dir}");
        Shared.PuzzleManager.ExecutePush(box, boxCell, boxCell + dir);
        Shared.PuzzleManager.NotifyGoalChanged();

        // Push 모드 해제 등
        isPushMode = false;
        pendingDirectionKey = Direction.None;
        selectedBox = null;
        isPerformingPush = false;
        if (!contactBoxes.Contains(box))
            contactBoxes.Add(box);
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

    // 바닥 타일맵에 Floor 타일이 있고, 벽 타일맵에 타일이 없으며,
    // 경로 방해 오브젝트가 없는 셀만 걷기 가능
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
