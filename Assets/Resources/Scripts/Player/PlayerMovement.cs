// PlayerMovement.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class PlayerMovement : MonoBehaviour
{
    [Header("타일맵 설정")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public float defaultMoveSpeed = 2f;
    public LayerMask impassableLayerMask;

    public float activeMoveSpeed;   //버프로 적용되는 이동속도

    private Rigidbody2D rb;
    private List<Vector3> path = new();
    private int currentPathIndex = 0;
    private Vector2 input = Vector2.zero;

    private Direction inputDir;
    private bool isMouseMove = false;
    public bool isPushMode { private set; get; }
    private Direction pendingDirectionKey = Direction.None;
    private PushObject selectedBox = null;
    private List<PushObject> contactBoxes = new();
    private bool isPerformingPush = false;

    private SpriteRenderer spriterenderer;
    private Animator animator;
    private PlayerDebuffController PlayerDebuffController;

    void Awake()
    {
        Shared.PlayerMovement = this;
        PlayerDebuffController = GetComponent<PlayerDebuffController>();
        rb = GetComponent<Rigidbody2D>();
        spriterenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        activeMoveSpeed = defaultMoveSpeed;
    }

    void Update()
    {
        if (isPerformingPush) return;

        if (PlayerDebuffController != null && PlayerDebuffController.IsStunned)
        {
            animator.SetInteger("Move", 0);
            return; // 키입력, 마우스 이동, FlipX 처리 전부 건너뜀
        }

        inputDir = GetHexDirectionArrowKey();

        if (!isPushMode)
            HandlePushDetection();

        if (isPushMode)
        {
            input = Vector2.zero;

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space))
            {
                animator.SetInteger("Move", 0);
                selectedBox?.SetHighlight(false);
                selectedBox = null;
                isPushMode = false;
                pendingDirectionKey = Direction.None;
                animator.SetFloat("PushX", 0f);
                animator.SetFloat("PushY", 0f);
                animator.SetBool("IsPushIdle", false);
                Debug.Log("[Push] 밀기 모드 종료");
                return;
            }

            if (inputDir != Direction.None && inputDir == pendingDirectionKey && selectedBox != null)
            {
                if (selectedBox.TryPush(pendingDirectionKey, out var fromCell, out var toCell))
                {
                    StartCoroutine(PerformPush(selectedBox, fromCell, toCell - fromCell));
                }
                else
                {
                    Debug.Log("[Push] 해당 방향으로는 밀 수 없습니다.");
                }
            }
            return;
        }

        // 마우스 우클릭 이동
        if (Input.GetMouseButton(1))
        {
            Vector3 screenPos = Input.mousePosition;
            screenPos.z = -Camera.main.transform.position.z;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            SetTargetPosition(worldPos);
        }

        // 키 입력 이동 (화살표 키만 허용)
        float h = Input.GetKey(KeyCode.LeftArrow) ? -1f : Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;
        float v = Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;

        input = new Vector2(h, v).normalized;

        float x = input.x;
        float y = input.y;

        if (input != Vector2.zero)
        {
            if (Mathf.Abs(x) > 0.01f)
            {
                spriterenderer.flipX = x > 0;
            }
            else if (Mathf.Abs(y) > 0.01f)
            {
                spriterenderer.flipX = y > 0;
            }

            path.Clear();
            isMouseMove = false;
        }

        // 밀기 모드 진입
        if (!isPushMode && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space)) && selectedBox != null)
        {
            if (contactBoxes.Count > 0)
            {
                selectedBox = contactBoxes.OrderBy(b => Vector2.SqrMagnitude(rb.position - (Vector2)b.transform.position)).First();
                isPushMode = true;
                animator.SetBool("IsPushIdle", true); // 밀기대기모드 애니메이션 시작
                //spriterenderer.flipX = (selectedBox.transform.position.x - rb.position.x) > 0;
                path.Clear();
                Debug.Log("[Push] 밀기 모드 진입");
            }
            return;
        }
    }

    void FixedUpdate()
    {
        if (isPerformingPush) return;

        animator.SetInteger("Move", (path.Count > 0 || input.sqrMagnitude > 0f) ? 1 : 0);

        // 마우스 이동 처리
        if (path.Count > 0)
        {
            Vector3 targetWorld = path[currentPathIndex];
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetWorld, defaultMoveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            // 마우스 이동 flipX 적용
            float dx = targetWorld.x - rb.position.x;
            if (Mathf.Abs(dx) > 0.01f)
                spriterenderer.flipX = dx > 0;

            if (Vector2.Distance(rb.position, targetWorld) < 0.05f)
            {
                currentPathIndex++;
                if (currentPathIndex >= path.Count)
                {
                    path.Clear();
                    isMouseMove = false;
                }
            }
            return;
        }

        // 키 이동 처리
        if (input.sqrMagnitude > 0f)
        {
            Vector2 newPos = rb.position + input * defaultMoveSpeed * Time.fixedDeltaTime;
            Vector3Int tgtCell = floorTilemap.WorldToCell(newPos);
            if (IsWalkableCell(tgtCell))
            {
                rb.MovePosition(newPos);
            }
        }
    }

    void HandlePushDetection()
    {
        if (isPushMode) return;

        if (selectedBox != null)
        {
            selectedBox.SetHighlight(false);
            selectedBox = null;
            Debug.Log("하이라이트 종료됨.");
        }

        contactBoxes.Clear();
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.15f);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<PushObject>(out var box))
            {
                selectedBox = box;
                contactBoxes.Add(box);
                box.SetHighlight(true);
                Debug.Log("하이라이트 작동함.");

                Vector3Int playerCell = floorTilemap.WorldToCell(rb.position);
                Vector3Int boxCell = floorTilemap.WorldToCell(box.transform.position);
                Vector3Int delta = boxCell - playerCell;

                bool odd = Mathf.Abs(playerCell.y) % 2 == 1;
                pendingDirectionKey = GetDirectionFromDelta(delta, odd);

                var (blend, flipX) = GetPushBlend(pendingDirectionKey);
                animator.SetFloat("PushX", blend.x);
                animator.SetFloat("PushY", blend.y);
                spriterenderer.flipX = flipX;

                return;
            }
        }
    }

    public void TeleportTo(Vector3 worldPos)
    {
        rb.position = worldPos;
        transform.position = worldPos;
        ClearPath();
    }

    Direction GetHexDirectionArrowKey()
    {
        bool up = Input.GetKey(KeyCode.UpArrow);
        bool down = Input.GetKey(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);

        if (up && left) return Direction.NW;
        if (up && right) return Direction.NE;
        if (down && left) return Direction.SW;
        if (down && right) return Direction.SE;
        if (left) return Direction.West;
        if (right) return Direction.East;
        return Direction.None;
    }

    Direction GetDirectionFromDelta(Vector3Int delta, bool odd)
    {
        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            if (dir == Direction.None) continue;
            if (GetOffsetForDirection(dir, odd) == delta)
                return dir;
        }
        return Direction.None;
    }

    Vector3Int GetOffsetForDirection(Direction dir, bool odd)
    {
        return dir switch
        {
            Direction.West => new Vector3Int(-1, 0, 0),
            Direction.East => new Vector3Int(1, 0, 0),
            Direction.NW => odd ? new Vector3Int(0, 1, 0) : new Vector3Int(-1, 1, 0),
            Direction.NE => odd ? new Vector3Int(1, 1, 0) : new Vector3Int(0, 1, 0),
            Direction.SW => odd ? new Vector3Int(0, -1, 0) : new Vector3Int(-1, -1, 0),
            Direction.SE => odd ? new Vector3Int(1, -1, 0) : new Vector3Int(0, -1, 0),
            _ => Vector3Int.zero,
        };
    }

    void SetTargetPosition(Vector3 worldTarget)
    {
        Vector3Int cell = floorTilemap.WorldToCell(worldTarget);
        if (!floorTilemap.cellBounds.Contains(cell) || !IsWalkableCell(cell)) return;

        Vector3 cellCenter = floorTilemap.GetCellCenterWorld(cell);
        path = FindPath(transform.position, cellCenter);
        currentPathIndex = 0;
        isMouseMove = path.Count > 0;
    }

    public void ClearPath() => path.Clear();

    bool IsWalkableCell(Vector3Int cell)
    {
        if (!floorTilemap.HasTile(cell)) return false;
        if (wallTilemap != null && wallTilemap.HasTile(cell)) return false;
        Vector3 world = floorTilemap.GetCellCenterWorld(cell);
        Collider2D block = Physics2D.OverlapCircle(world, 0.1f, impassableLayerMask);
        return block == null;
    }

    IEnumerator PerformPush(PushObject box, Vector3Int fromCell, Vector3Int dir)
    {
        isPerformingPush = true;
        float duration = 0.2f;

        var (blend, flipX) = GetPushBlend(pendingDirectionKey);
        animator.SetFloat("PushX", blend.x);
        animator.SetFloat("PushY", blend.y);
        spriterenderer.flipX = flipX;
        //animator.SetBool("IsPushIdle", false); // 밀기 애니메이션 시작
        animator.SetBool("IsPushing", true); // 밀기 애니메이션 시작

        Vector3 fromBox = box.transform.position;
        Vector3 toBox = floorTilemap.GetCellCenterWorld(fromCell + dir);
        Vector3 fromPlayer = rb.position;

        // 박스 이동 방향
        Vector3 pushDir = (fromBox - toBox).normalized;

        // 셀 중심보다 박스 쪽으로 조금 더 붙임
        float offset = 0.15f; // 조정 가능
        Vector3 toPlayer = fromBox - pushDir * offset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            box.transform.position = Vector3.Lerp(fromBox, toBox, t);
            rb.MovePosition(Vector3.Lerp(fromPlayer, toPlayer, t));
            yield return null;
        }

        box.transform.position = toBox;
        rb.MovePosition(toPlayer);
        animator.SetBool("IsPushing", false); // 밀기 애니메이션 시작

        // 퍼즐 박스 위치 갱신 및 목표 체크 호출
        Shared.PuzzleManager?.ExecutePush(box, fromCell, fromCell + dir);
        isPerformingPush = false;
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
            Vector3Int current = openList.OrderBy(n => fScore.GetValueOrDefault(n, float.MaxValue)).First();
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
                if (closedList.Contains(neighbor)) continue;

                float moveCost = (Mathf.Abs(neighbor.x - current.x) + Mathf.Abs(neighbor.y - current.y) > 1) ? 1.4f : 1f;
                float tentativeG = gScore[current] + moveCost;

                if (!openList.Contains(neighbor)) openList.Add(neighbor);
                else if (tentativeG >= gScore.GetValueOrDefault(neighbor, float.MaxValue)) continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + HexDistance(neighbor, goal);
            }
        }

        Debug.LogWarning("경로를 찾을 수 없습니다.");
        return new List<Vector3>();
    }

    List<Vector3> GetNeighbors(Vector3Int current)
    {
        var results = new List<Vector3>();
        Vector3Int[] dirs = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(1, -1, 0), new Vector3Int(-1, 1, 0),
        };

        foreach (var d in dirs)
        {
            var cell = current + d;
            if (!floorTilemap.cellBounds.Contains(cell) || !IsWalkableCell(cell) || HasImpassableObject(cell))
                continue;

            if (Mathf.Abs(d.x) == 1 && Mathf.Abs(d.y) == 1)
            {
                var c1 = current + new Vector3Int(d.x, 0, 0);
                var c2 = current + new Vector3Int(0, d.y, 0);
                if (!floorTilemap.cellBounds.Contains(c1) || !IsWalkableCell(c1) || HasImpassableObject(c1)
                    || !floorTilemap.cellBounds.Contains(c2) || !IsWalkableCell(c2) || HasImpassableObject(c2))
                    continue;
            }
            results.Add(floorTilemap.GetCellCenterWorld(cell));
        }
        return results;
    }

    int HexDistance(Vector3Int a, Vector3Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs((a.x + a.y) - (b.x + b.y))) / 2;
    }

    bool HasImpassableObject(Vector3Int cell)
    {
        Vector3 worldCenter = floorTilemap.GetCellCenterWorld(cell);
        Collider2D hit = Physics2D.OverlapCircle(worldCenter, 0.1f, impassableLayerMask);
        return hit != null;
    }
    public void SetTilemap(Tilemap floor, Tilemap wall)
    {
        floorTilemap = floor;
        wallTilemap = wall;
        ClearPath();
        Debug.Log("[PlayerMovement] SetTilemap 정상 세팅 완료");

        // PushObject들에게 타일맵 자동 할당
        foreach (var push in FindObjectsOfType<PushObject>())
            push.SetTilemaps(floor, wall);
    }
    (Vector2 blend, bool flipX) GetPushBlend(Direction dir)
    {
        return dir switch
        {
            Direction.NW => (new Vector2(1f, 1f), false),
            Direction.NE => (new Vector2(1f, 1f), true),
            Direction.SW => (new Vector2(1f, -1f), false),
            Direction.SE => (new Vector2(1f, -1f), true),
            Direction.West => (new Vector2(1f, 0f), false),
            Direction.East => (new Vector2(1f, 0f), true),
            _ => (Vector2.zero, false)
        };
    }

    public void ResetInput() //디버프 시 이동 방향 초기화
    {
        input = Vector2.zero;
        inputDir = Direction.None;
    }
}
