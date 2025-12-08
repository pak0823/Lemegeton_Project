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
    float movementLockUntil = 0f;
    int _hardLockTokens = 0; // 무기한 잠금 토큰

    private Direction inputDir;
    public bool isPushMode { private set; get; }
    private Direction pendingDirectionKey = Direction.None;
    private PushObject selectedBox = null;
    private List<PushObject> contactBoxes = new();
    private bool isPerformingPush = false;

    // 마우스 이동 상태
    bool mouseMoveActive = false;
    Vector2 mouseMoveTarget;
    [SerializeField] float clickArrivalThreshold = 0.1f; // 도착 판정
    //[SerializeField] float clickProbeRadius = 0.15f;      // 충돌 예측 반경
    ContactFilter2D _castFilter;
    readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];

    // 타일 경로 기반 이동 상태
    [Header("타일 경로 이동 설정")]
    [SerializeField] private GameObject pathMarkerPrefab;   // 경로 표시용 프리팹

    // 현재 선택된 경로(셀 단위)
    private List<Vector3Int> currentPathCells = new List<Vector3Int>();
    // 현재 선택된 목표 셀 (첫 번째 클릭으로 선택된 타일)
    private Vector3Int? selectedTargetCell = null;
    // 경로를 따라 실제 이동 중인지 여부
    private bool isMovingByPath = false;
    // 경로 이동 코루틴 핸들
    private Coroutine pathMoveRoutine = null;
    // 화면에 찍힌 경로 마커들
    private readonly List<GameObject> activePathMarkers = new List<GameObject>();

    private SpriteRenderer spriterenderer;
    private Animator animator;
    private PlayerDebuffController PlayerDebuffController;

    private BoxInteract highlightedChest = null;
    Collider2D _lastHintTarget;     // 마지막으로 힌트를 띄우게 한 대상
    DescriptionData _lastDescData;  // 그 대상의 설명 데이터(있으면)

    [SerializeField] private KeyCode surveyKey = KeyCode.F; //탐험 조사 키
    [SerializeField] private KeyCode communicationKey = KeyCode.E; //탐험 소통 키
    [SerializeField] private KeyCode upDirectionKey = KeyCode.W; //탐험 위 방향키
    [SerializeField] private KeyCode downDirectionKey = KeyCode.S; //탐험 아래 방향키
    [SerializeField] private KeyCode leftDirectionKey = KeyCode.A; //탐험 왼쪽 방향키
    [SerializeField] private KeyCode rightDirectionKey = KeyCode.D; //탐험 오른쪽 방향키

    void Awake()
    {
        Shared.PlayerMovement = this;
        PlayerDebuffController = GetComponent<PlayerDebuffController>();
        rb = GetComponent<Rigidbody2D>();
        spriterenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        activeMoveSpeed = defaultMoveSpeed;

        // 마우스 이동 충돌 예측 필터 (자기 자신은 자동 제외됨)
        _castFilter.useTriggers = false;
        _castFilter.SetLayerMask(impassableLayerMask);
    }

    void Update()
    {
        // 입력 차단 조건을 한 곳에서 체크
        bool isInputBlocked =
                                (Time.time < movementLockUntil)
                              || (_hardLockTokens > 0)
                              || isPerformingPush
                              || GamePause.IsPaused
                              || (PlayerDebuffController != null && PlayerDebuffController.IsStunned)
                              || (Shared.ObjectGaugeManager != null && Shared.ObjectGaugeManager.IsBattleNoticeActive);

        if (isInputBlocked)
        {
            HaltImmediately();
            return;
        }

        HandleSurveyKeyPreAction();

        inputDir = GetHexDirectionArrowKey();

        if (!isPushMode)
            HandlePushDetection();

        HandleCommunicationKey();

        if (!isPushMode)
        {
            HandleTileClickInput();
        }

        if (isPushMode)
        {
            input = Vector2.zero;

            if (Input.GetKeyDown(surveyKey))
            {
                animator.SetInteger("Move", 0);
                selectedBox?.SetHighlight(false);
                selectedBox = null;
                isPushMode = false;
                pendingDirectionKey = Direction.None;
                animator.SetFloat("PushX", 0f);
                animator.SetFloat("PushY", 0f);
                animator.SetBool("IsPushIdle", false);
                Shared.interactionHintUI?.HideAll();
                Debug.Log("[Push] 밀기 모드 종료");
                return;
            }

            if (selectedBox != null && (Input.GetKeyDown(leftDirectionKey) || Input.GetKeyDown(rightDirectionKey)))
            {
                // 혹시라도 가까이에서 상대 위치가 바뀌었으면, 현재 프레임 기준으로 다시 한 번 보정
                var playerCell = floorTilemap.WorldToCell(rb.position);
                var boxCell = floorTilemap.WorldToCell(selectedBox.transform.position);
                var delta = boxCell - playerCell;
                bool odd = Mathf.Abs(playerCell.y) % 2 == 1;
                pendingDirectionKey = GetDirectionFromDelta(delta, odd);

                if (pendingDirectionKey != Direction.None && IsConfirmKeyFor(pendingDirectionKey))
                {
                    if (selectedBox.TryPush(pendingDirectionKey, out var fromCell, out var toCell))
                    {
                        var (blend, flipX) = GetPushBlend(pendingDirectionKey);
                        animator.SetFloat("PushX", blend.x);
                        animator.SetFloat("PushY", blend.y);
                        spriterenderer.flipX = flipX;

                        StartCoroutine(PerformPush(selectedBox, fromCell, toCell - fromCell));
                    }
                    else
                    {
                        Debug.Log("[Push] 해당 방향으로는 밀 수 없습니다.");
                    }
                }
                else
                {
                    // 잘못된(반대) 키면 무시 (원하면 여기서 효과음/진동/짧은 UI 피드백 가능)
                    // Debug.Log("[Push] 반대 방향 키 입력 무시");
                }
            }

            HandleCommunicationKey();
            return;
        }

        // 밀기 모드 진입
        if (!isPushMode && (Input.GetKeyDown(surveyKey) && selectedBox != null))
        {
            if (contactBoxes.Count > 0)
            {
                Shared.interactionHintUI?.HideBoth();
                selectedBox = contactBoxes.OrderBy(b => Vector2.SqrMagnitude(rb.position - (Vector2)b.transform.position)).First();
                isPushMode = true;
                animator.SetBool("IsPushIdle", true); // 밀기대기모드 애니메이션 시작
                path.Clear();
                Shared.interactionHintUI?.ShowCancelAt(selectedBox.transform);
                Debug.Log("[Push] 밀기 모드 진입");
            }
            return;
        }

        // 선택된 푸시 오브젝트가 없고, 포커스된 상자가 있을 때만 열기
        if (!isPushMode && selectedBox == null && highlightedChest != null
        && Input.GetKeyDown(surveyKey))
        {
            Shared.interactionHintUI?.HideAll();
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

        //animator.SetInteger("Move", (path.Count > 0 || input.sqrMagnitude > 0f) ? 1 : 0);
    }

    // --- 타일 클릭 입력 처리 ---
    void HandleTileClickInput()
    {
        // 이미 경로를 따라 이동 중이면 새로운 입력을 무시
        if (isMovingByPath)
            return;

        if (floorTilemap == null)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;

        float zDist = cam.orthographic ? 0f : (transform.position.z - cam.transform.position.z);
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,
                                                        Input.mousePosition.y,
                                                        zDist));
        wp.z = transform.position.z;

        Vector3Int clickedCell = floorTilemap.WorldToCell(wp);
        Vector3Int currentCell = floorTilemap.WorldToCell(rb.position);

        // 왼쪽 클릭: 경로 프리뷰 or 이동 실행
        if (Input.GetMouseButtonDown(0))
        {
            // 이미 같은 타일이 선택된 상태에서 다시 왼쪽 클릭 → 이동 실행
            if (selectedTargetCell.HasValue
                && selectedTargetCell.Value == clickedCell
                && currentPathCells != null
                && currentPathCells.Count >= 2)
            {
                StartPathMove(currentPathCells);
                return;
            }

            // 새 경로 계산
            var newPath = FindPath(currentCell, clickedCell);

            // 경로가 없거나 1칸(제자리)이면 선택/프리뷰 해제
            if (newPath == null || newPath.Count <= 1)
            {
                selectedTargetCell = null;
                currentPathCells.Clear();
                ClearPathPreview();
                return;
            }

            // 첫 번째 클릭: 경로만 표시
            selectedTargetCell = clickedCell;
            currentPathCells = newPath;
            ShowPathPreview(newPath);
            return;
        }

        // 오른쪽 클릭: 같은 타일을 클릭하면 선택/프리뷰 취소
        if (Input.GetMouseButtonDown(1))
        {
            if (selectedTargetCell.HasValue && selectedTargetCell.Value == clickedCell)
            {
                selectedTargetCell = null;
                currentPathCells.Clear();
                ClearPathPreview();
            }
        }
    }

    // 외부에서 잠금 요청
    public void LockMovementFor(float seconds)
    {
        movementLockUntil = Mathf.Max(movementLockUntil, Time.time + Mathf.Max(0f, seconds));
        HaltImmediately(); // 즉시 멈춤 (애니/속도 초기화)
    }
    public void LockMovementIndefinite()
    {
        _hardLockTokens++;
        HaltImmediately();
    }
    public void UnlockMovementIndefinite()
    {
        _hardLockTokens = Mathf.Max(0, _hardLockTokens - 1);
        HaltImmediately();
    }
    bool IsCommunicationBlocked(Collider2D collider2d)
    {
        // 소통 금지 대상들을 여기서 중앙집중 관리
        return collider2d != null && collider2d.GetComponent<TrapBehavior>() != null;
    }

    #region Movement
    // 화면에서 경로 표시 제거
    void ClearPathPreview()
    {
        if (activePathMarkers.Count > 0)
        {
            foreach (var marker in activePathMarkers)
            {
                if (marker != null)
                    Destroy(marker);
            }
            activePathMarkers.Clear();
        }
    }
    // 경로 프리뷰 생성 (2칸 이상일 때만 표시)
    void ShowPathPreview(List<Vector3Int> cells)
    {
        ClearPathPreview();

        currentPathCells = cells ?? new List<Vector3Int>();
        if (cells == null || cells.Count < 2) return; // 1칸(제자리) 이동이면 표시하지 않음

        for (int i = 1; i < cells.Count; i++) // 시작 셀은 제외
        {
            Vector3Int cell = cells[i];
            Vector3 world = floorTilemap.GetCellCenterWorld(cell);
            world.z = transform.position.z;

            var marker = Instantiate(pathMarkerPrefab, world, Quaternion.identity);
            activePathMarkers.Add(marker);
        }
    }
    // --- 타일 기반 최소 경로 탐색 (BFS) ---

    List<Vector3Int> FindPath(Vector3Int start, Vector3Int goal)
    {
        // 시작과 목표가 같으면 1칸짜리 경로 반환
        if (start == goal)
        {
            return new List<Vector3Int> { start };
        }

        var queue = new Queue<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        queue.Enqueue(start);
        cameFrom[start] = start;

        // 탐색에 사용할 6방향
        Direction[] dirs =
        {
            Direction.West,
            Direction.East,
            Direction.NW,
            Direction.NE,
            Direction.SW,
            Direction.SE
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == goal)
                break;

            bool odd = (current.y & 1) != 0;

            foreach (var dir in dirs)
            {
                Vector3Int offset = GetOffsetForDirection(dir, odd);
                Vector3Int next = current + offset;

                if (cameFrom.ContainsKey(next))
                    continue;

                if (!IsWalkableCell(next))
                    continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        // goal 에 도달하지 못한 경우
        if (!cameFrom.ContainsKey(goal))
        {
            return null;
        }

        // goal 에서 start 까지 역추적 후 뒤집기
        var path = new List<Vector3Int>();
        var cur = goal;
        while (true)
        {
            path.Add(cur);
            if (cur == start) break;
            cur = cameFrom[cur];
        }
        path.Reverse();
        return path;
    }
    // --- 경로를 따라 실제로 이동 ---

    void StartPathMove(List<Vector3Int> cells)
    {
        if (cells == null || cells.Count < 2) return; // 제자리이거나 잘못된 경로

        // 이동 중이면 먼저 정리
        if (pathMoveRoutine != null)
        {
            StopCoroutine(pathMoveRoutine);
            pathMoveRoutine = null;
        }

        isMovingByPath = true;
        // 프리뷰는 이동 시작 시 지움
        ClearPathPreview();

        pathMoveRoutine = StartCoroutine(Co_MoveAlongPath(cells));
    }

    IEnumerator Co_MoveAlongPath(List<Vector3Int> cells)
    {
        // 시작 셀은 현재 위치라고 가정, 1번째 인덱스부터 끝까지 순서대로 이동
        for (int i = 1; i < cells.Count; i++)
        {
            Vector3 start = rb.position;
            Vector3 end = floorTilemap.GetCellCenterWorld(cells[i]);
            end.z = transform.position.z;

            // 방향 벡터 및 거리 계산
            Vector2 to = end - start;
            float dist = to.magnitude;
            if (dist < 0.0001f)
                continue;

            Vector2 dir = to / dist;
            float speed = Mathf.Max(0.01f, defaultMoveSpeed);
            float duration = dist / speed;
            float t = 0f;

            // 스프라이트 방향
            if (dir.sqrMagnitude > 0.0001f)
                spriterenderer.flipX = dir.x > 0f;

            if (animator != null)
                animator.SetInteger("Move", 1);

            while (t < 1f)
            {
                if (GamePause.IsPaused)
                {
                    // 일시정지 중에는 프레임만 넘김
                    yield return null;
                    continue;
                }

                t += Time.deltaTime / duration;
                Vector3 pos = Vector3.Lerp(start, end, Mathf.Clamp01(t));
                rb.MovePosition(pos);
                yield return null;
            }

            rb.MovePosition(end);
        }

        if (animator != null)
            animator.SetInteger("Move", 0);

        isMovingByPath = false;
        selectedTargetCell = null;
        currentPathCells.Clear();
        pathMoveRoutine = null;
    }
    #endregion
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

                Shared.interactionHintUI?.ShowBothAt(box.transform);

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
                Shared.interactionHintUI?.ShowBothAt(closest.transform);
                return;
            }

            if (hit.TryGetComponent<PortalController>(out var potal))
            {
                Shared.interactionHintUI?.ShowBothAt(potal.transform);
                return;
            }
            else if(hit.TryGetComponent<HintAnchor>(out var hint))
            {
                Shared.interactionHintUI?.ShowBothAt(hint.transform);
                return;
            }

            // Fallback으로(푸시/상자 외) 힌트 대상 탐색
            HandleInteractionHintsFallback();

            // 대상이 전혀 없을 때만 힌트 숨김
            Shared.interactionHintUI?.HideAll();
        }
    }
    void HandleInteractionHintsFallback()
    {
        // 이미 기존 로직으로 대상이 정해졌다면(푸시/상자 등) 거기에 맞춰 힌트는 켜져 있을 것.
        // 여기서는 아무 대상도 못 찾았을 때만 "설명 전용" 힌트를 켠다.
        if (selectedBox != null || highlightedChest != null) return;

        var hits = Physics2D.OverlapCircleAll(transform.position, 0.2f);
        Collider2D best = null;
        float bestDist = float.MaxValue;
        DescriptionData bestDesc = null;

        foreach (var h in hits)
        {
            if (IsCommunicationBlocked(h)) continue;    // 함정은 소통 대상에서 제외

            if (h.isTrigger == false && h.attachedRigidbody == null) continue; // 너무 무차별 감지 방지 (필요시 조정)
                                                                               // 조건 1: DescriptionData가 있다면 최우선
            if (h.TryGetComponent<DescriptionData>(out var dd) && (dd.enableHintOnContact))
            {
                float d = Vector2.SqrMagnitude((Vector2)h.bounds.ClosestPoint(transform.position) - (Vector2)transform.position);
                if (d < bestDist) { best = h; bestDist = d; bestDesc = dd; }
                continue;
            }
            //// 조건 2: 태그로만 힌트를 줄 수도 있음
            //if (best == null && h.CompareTag("Interactable"))
            //{
            //    best = h; bestDist = 0.21f; // 가벼운 우선순위
            //    bestDesc = null;
            //}
        }

        if (best != null)
        {
            _lastHintTarget = best;
            _lastDescData = bestDesc;

            // "무엇이든 접촉" → 두 키 모두 보이게
            Shared.interactionHintUI?.ShowBothAt(best.transform);
        }
        else
        {
            _lastHintTarget = null;
            _lastDescData = null;
            Shared.interactionHintUI?.HideAll();
            Shared.descriptionDialogUI?.Hide();
        }
    }

    void HandleCommunicationKey()
    {
        if (!Input.GetKeyDown(communicationKey)) return;

        if (_lastHintTarget != null && IsCommunicationBlocked(_lastHintTarget)) return; // 예외 안전장치: 함정이면 즉시 무시

        string text = null;

        // 상자 포커스 중이면 상자 설명 우선
        if (highlightedChest != null)
        {
            // 상자 기본 문구
            text = "아이템 상자 기본 문구가 비어있음.";
            // 혹시 상자에 DescriptionData가 있다면 그 문구가 우선
            if (highlightedChest.TryGetComponent<DescriptionData>(out var dd) && !string.IsNullOrEmpty(dd.description))
                text = dd.description;
        }
        // 푸시 박스 포커스 중이면
        else if (selectedBox != null)
        {
            if (selectedBox.TryGetComponent<DescriptionData>(out var dd) && !string.IsNullOrEmpty(dd.description))
                text = dd.description;
            else
                text = "밀기 상자 기본 문구가 비어있음.";
        }
        // 그 외 최근 접촉 대상
        else if (_lastHintTarget != null)
        {
            if (_lastDescData != null && !string.IsNullOrEmpty(_lastDescData.description))
                text = _lastDescData.description;
            else
                text = "무언가 상호작용할 수 있을 것 같다.";
        }

        if (!string.IsNullOrEmpty(text))
        {
            Shared.descriptionDialogUI?.Toggle(text);
        }
    }

    // F가 눌릴 때는 설명창을 닫고 기존 로직 수행(진입/종료/오픈 등)
    void HandleSurveyKeyPreAction()
    {
        if (Input.GetKeyDown(surveyKey))
        {
            Shared.descriptionDialogUI?.Hide();
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
        bool up = Input.GetKey(upDirectionKey);
        bool down = Input.GetKey(downDirectionKey);
        bool left = Input.GetKeyDown(leftDirectionKey);
        bool right = Input.GetKeyDown(rightDirectionKey);

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

    bool IsConfirmKeyFor(Direction dir)
    {
        switch (dir)
        {
            // 왼쪽 계열은 A(왼쪽)만 허용
            case Direction.West:
            case Direction.NW:
            case Direction.SW:
                return Input.GetKeyDown(leftDirectionKey);

            // 오른쪽 계열은 D(오른쪽)만 허용
            case Direction.East:
            case Direction.NE:
            case Direction.SE:
                return Input.GetKeyDown(rightDirectionKey);
        }
        return false;
    }

    public void ClearPath()
    {
        // 기존 마우스 경로 리스트 정리
        path.Clear();

        // 타일 경로 이동 상태 정리
        currentPathCells.Clear();
        selectedTargetCell = null;
        isMovingByPath = false;
        ClearPathPreview();

        if (pathMoveRoutine != null)
        {
            StopCoroutine(pathMoveRoutine);
            pathMoveRoutine = null;
        }
    }

    bool IsWalkableCell(Vector3Int cell)
    {
        if (!floorTilemap.HasTile(cell)) return false;
        if (wallTilemap != null && wallTilemap.HasTile(cell)) return false;
        Vector3 world = floorTilemap.GetCellCenterWorld(cell);

        // 자기 자신을 무시하기 위해 All로 받아서 필터링
        var hits = Physics2D.OverlapCircleAll(world, 0.05f, impassableLayerMask);
        foreach (var h in hits)
        {
            if (!h) continue;
            if (h.attachedRigidbody == rb) continue; // 본인 무시
            var hitCell = floorTilemap.WorldToCell(h.bounds.center);
            if (hitCell != cell) continue;
            return false; // 뭔가 걸리면 통행 불가
        }
        return true;
    }

    IEnumerator PerformPush(PushObject box, Vector3Int fromCell, Vector3Int dir)
    {
        isPerformingPush = true;
        float duration = 0.2f;

        var (blend, flipX) = GetPushBlend(pendingDirectionKey);
        animator.SetFloat("PushX", blend.x);
        animator.SetFloat("PushY", blend.y);
        spriterenderer.flipX = flipX;
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

    public void HaltImmediately()
    {
        // 타일 경로 이동 즉시 중단
        if (pathMoveRoutine != null)
        {
            StopCoroutine(pathMoveRoutine);
            pathMoveRoutine = null;
        }
        isMovingByPath = false;

        // 프리뷰 및 경로 정보 초기화
        ClearPathPreview();
        currentPathCells.Clear();
        selectedTargetCell = null;

        // 기존 마우스 이동/키 입력도 모두 정지
        input = Vector2.zero;
        inputDir = Direction.None; // 이미 ResetInput()도 있지만 여기서 직접 처리
        mouseMoveActive = false;
        path.Clear();

        if (animator != null)
            animator.SetInteger("Move", 0);
    }

    public void ResetInput() //디버프 시 이동 방향 초기화
    {
        input = Vector2.zero;
        inputDir = Direction.None;
    }
}
