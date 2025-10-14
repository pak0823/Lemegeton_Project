// PlayerMovement.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    private Vector2 input = Vector2.zero;

    private Direction inputDir;
    public bool isPushMode { private set; get; }
    private Direction pendingDirectionKey = Direction.None;
    private PushObject selectedBox = null;
    private List<PushObject> contactBoxes = new();
    private bool isPerformingPush = false;

    private SpriteRenderer spriterenderer;
    private Animator animator;
    private PlayerDebuffController PlayerDebuffController;

    private BoxInteract highlightedChest = null;

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
        // 입력 차단 조건을 한 곳에서 체크
        bool isInputBlocked = isPerformingPush
                              || GamePause.IsPaused
                              || (PlayerDebuffController != null && PlayerDebuffController.IsStunned)
                              || (Shared.ObjectGaugeManager != null && Shared.ObjectGaugeManager.IsBattleNoticeActive);

        if (isInputBlocked)
        {
            HaltImmediately();
            return;
        }

        inputDir = GetHexDirectionArrowKey();

        if (!isPushMode)
            HandlePushDetection();

        if (isPushMode)
        {
            input = Vector2.zero;

            if (Input.GetKeyDown(KeyCode.F))
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

        // 키 입력 이동 (화살표 키만 허용)
        float h = Input.GetKey(KeyCode.A) ? -1f : Input.GetKey(KeyCode.D) ? 1f : 0f;
        float v = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;

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
        }

        // 밀기 모드 진입
        if (!isPushMode && (Input.GetKeyDown(KeyCode.F) && selectedBox != null))
        {
            if (contactBoxes.Count > 0)
            {
                selectedBox = contactBoxes.OrderBy(b => Vector2.SqrMagnitude(rb.position - (Vector2)b.transform.position)).First();
                isPushMode = true;
                animator.SetBool("IsPushIdle", true); // 밀기대기모드 애니메이션 시작
                path.Clear();
                Debug.Log("[Push] 밀기 모드 진입");
            }
            return;
        }

        // 선택된 푸시 오브젝트가 없고, 포커스된 상자가 있을 때만 열기
        if (!isPushMode && selectedBox == null && highlightedChest != null
        && Input.GetKeyDown(KeyCode.F))
        {
            highlightedChest.OpenChest();
            highlightedChest = null; // 열렸으니 참조 해제
            return;
        }
    }

    void FixedUpdate()
    {
        if (isPerformingPush) return;
        if (GamePause.IsPaused || (Shared.ObjectGaugeManager != null && Shared.ObjectGaugeManager.IsBattleNoticeActive))
        {
            if (animator != null) animator.SetInteger("Move", 0);
            return;
        }

        animator.SetInteger("Move", (path.Count > 0 || input.sqrMagnitude > 0f) ? 1 : 0);

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
        }

        // 기존에 켜진 상자 하이라이트 해제(포커스 갱신 전 초기화)
        if (highlightedChest != null)
        {
            highlightedChest.SetFocused(false);   // 이전 포커스 해제
            highlightedChest.SetHighlight(false);
            highlightedChest = null;
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

            // 2) 상자 1개만 포커스/하이라이트
            BoxInteract closest = null;
            float bestSqr = float.MaxValue;
            Vector3 p = transform.position;

            foreach (var hitBox in hits)
            {
                if (hitBox.TryGetComponent<BoxInteract>(out var chest) && !chest.IsOpened)// 열린 상자는 제외하고, 닫혀있으면 하이라이트
                {
                    float d = (chest.transform.position - p).sqrMagnitude;
                    if (d < bestSqr)
                    {
                        bestSqr = d;
                        closest = chest;
                    }
                }
            }

            if (closest != null)
            {
                highlightedChest = closest;
                highlightedChest.SetFocused(true);    // 포커스 부여(= UI/입력 허용)
                highlightedChest.SetHighlight(true);  // 선택 1개만 하이라이트
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
        bool up = Input.GetKey(KeyCode.W);
        bool down = Input.GetKey(KeyCode.S);
        bool left = Input.GetKeyDown(KeyCode.A);
        bool right = Input.GetKeyDown(KeyCode.D);

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

    public void HaltImmediately()   //플레이어 이동 강제 즉시 정지(이동 중 생기는 버그를 위한)
    {
        // 입력과 경로를 모두 초기화해서 FixedUpdate가 더 이상 움직이지 않게 함
        input = Vector2.zero;
        inputDir = Direction.None; // 이미 ResetInput()도 있지만 여기서 직접 처리
        ClearPath();
        if (animator != null) animator.SetInteger("Move", 0);
    }

    public void ResetInput() //디버프 시 이동 방향 초기화
    {
        input = Vector2.zero;
        inputDir = Direction.None;
    }
}
