// PlayerMovement.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

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

    public bool isPushMode { private set; get; }
    private Direction pendingDirectionKey = Direction.None;
    private PushObject selectedBox = null;
    private List<PushObject> contactBoxes = new();
    private bool isPerformingPush = false;

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

    // 경로 도착 시 실행할 콜백 (예: 상자 열기)
    private Action pathArrivalCallback = null;

    // 상호작용 이동용으로 선택된 상자(있다면)
    private BoxInteract pendingChest = null;

    // 현재 상호작용 대상(관찰/조사 버튼이 가리키는 대상)
    private Collider2D currentInteractTarget = null;
    private DescriptionData currentDescData = null;

    [SerializeField] private LayerMask encounterLayerMask;
    [SerializeField] private string battleSceneName = "BattleScene"; // 실제 전투씬 이름으로

    private SpriteRenderer spriterenderer;
    private Animator animator;
    private PlayerDebuffController PlayerDebuffController;

    [SerializeField] private KeyCode surveyKey = KeyCode.F; //탐험 조사 키
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
        // 관찰/대화 Dialog가 열려 있을 때: 
        // 좌클릭으로 닫기
        // 닫히기 전까지는 이동/타일 클릭을 전부 막는다
        if (Shared.descriptionDialogUI != null && Shared.descriptionDialogUI.IsOpen)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Shared.descriptionDialogUI.Hide();
            }

            HaltImmediately();
            return;
        }

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

        if (!isPushMode)
        {
            HandlePushDetection();      // 푸시 박스 탐지만 남김 (힌트 UI 없이)
            HandleTileClickInput();     // 타일 클릭/상자 클릭 이동
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
    }

    void FixedUpdate()
    {
        if (isPerformingPush) return;
        if (GamePause.IsPaused || (Shared.ObjectGaugeManager != null && Shared.ObjectGaugeManager.IsBattleNoticeActive))
        {
            if (animator != null) animator.SetInteger("Move", 0);
            return;
        }
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

        // UI 위를 클릭한 경우에는 타일 입력을 처리하지 않음
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        float zDist = cam.orthographic ? 0f : (transform.position.z - cam.transform.position.z);
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,
                                                        Input.mousePosition.y,
                                                        zDist));
        wp.z = transform.position.z;

        Vector3Int clickedCell = floorTilemap.WorldToCell(wp);
        Vector3Int currentCell = floorTilemap.WorldToCell(rb.position);

        // 먼저, 클릭 지점에 상호작용 가능한 오브젝트가 있는지 검사
        BoxInteract clickedChest = null;
        Collider2D clickedCollider = null;
        DescriptionData clickedDesc = null;

        var hits = Physics2D.OverlapPointAll(wp);
        foreach (var h in hits)
        {
            // 상자(부모 포함) 검사
            var chest = h.GetComponentInParent<BoxInteract>();
            if (chest != null)
            {
                // 이미 열린 상자는 완전히 무시 (모든 콜라이더 포함)
                if (chest.IsOpened)
                    continue;

                // 닫힌 상자라면 상자 정보만 저장 (클릭 대상 우선)
                if (clickedChest == null)
                    clickedChest = chest;

                // 상자 콜라이더도 상호작용 대상 콜라이더로 인정
                if (!clickedCollider)
                    clickedCollider = h;
            }

            // 설명 데이터는 상자/기타 공통으로 가져온다
            if (clickedDesc == null && h.TryGetComponent<DescriptionData>(out var descriptiondata))
            {
                clickedDesc = descriptiondata;
                if (!clickedCollider)
                    clickedCollider = h;
            }
        }

        // 왼쪽 클릭: 경로 프리뷰 or 이동 실행
        if (Input.GetMouseButtonDown(0))
        {
            // 1) 오브젝트(상자, NPC, 기타) 클릭
            if (clickedChest != null || clickedCollider != null)
            {
                // 목표가 될 Transform 결정 (상자 우선, 아니면 해당 콜라이더)
                Transform targetTr = clickedChest ? clickedChest.transform : clickedCollider.transform;

                // 대상 셀
                Vector3Int targetCell = floorTilemap.WorldToCell(targetTr.position);
                

                // ▶ 현재 셀이 대상 셀과 같은 셀이거나, 인접 6칸 중 하나라면 "이동 없이 상호작용 가능"
                bool isAdjacentOrSame = false;
                {
                    if (currentCell == targetCell)
                    {
                        isAdjacentOrSame = true;
                    }
                    else
                    {
                        Direction[] dirs =
                        {
                            Direction.West,
                            Direction.East,
                            Direction.NW,
                            Direction.NE,
                            Direction.SW,
                            Direction.SE
                        };

                        bool odd = (targetCell.y & 1) != 0;
                        foreach (var dir in dirs)
                        {
                            Vector3Int offset = GetOffsetForDirection(dir, odd);
                            Vector3Int adj = targetCell + offset;
                            if (adj == currentCell)
                            {
                                isAdjacentOrSame = true;
                                break;
                            }
                        }
                    }
                }

                if (isAdjacentOrSame)
                {
                    // 이동 없이 바로 상호작용할 수 있는 거리
                    selectedTargetCell = currentCell;
                    currentPathCells = new List<Vector3Int> { currentCell };

                    // 현재 상호작용 대상/관찰 대상 저장
                    currentInteractTarget = clickedCollider ?? (clickedChest ? clickedChest.GetComponent<Collider2D>() : null);
                    currentDescData = clickedDesc;
                    pendingChest = clickedChest;

                    // 제자리에선 경로 프리뷰는 필요 없으니 호출해도 표시가 안 됨(Count < 2라서)
                    ShowPathPreview(currentPathCells);

                    // HintUI를 대상 위치에 표시 (조사/관찰/취소 버튼 모두)
                    Shared.interactionHintUI?.ShowBothAt(targetTr);
                    Shared.interactionHintUI?.ShowCancel();
                    return;
                }


                var newPath = FindPathToAdjacentCell(currentCell, targetCell);

                if (newPath == null || newPath.Count < 2)
                {
                    // 도달 불가 → 선택/프리뷰 해제
                    selectedTargetCell = null;
                    currentPathCells.Clear();
                    ClearPathPreview();
                    pendingChest = null;
                    pathArrivalCallback = null;
                    currentInteractTarget = null;
                    currentDescData = null;
                    Shared.interactionHintUI?.HideAll();
                    return;
                }

                selectedTargetCell = newPath[newPath.Count - 1];
                currentPathCells = newPath;

                // 현재 상호작용 대상/관찰 대상 저장
                currentInteractTarget = clickedCollider ?? (clickedChest ? clickedChest.GetComponent<Collider2D>() : null);
                currentDescData = clickedDesc;

                pendingChest = clickedChest;

                ShowPathPreview(newPath);

                // HintUI를 대상 위치에 표시 (조사/관찰/취소 버튼 모두)
                Shared.interactionHintUI?.ShowBothAt(targetTr); // 조사 + 관찰
                Shared.interactionHintUI?.ShowCancel();         // 취소 버튼 추가
                return;
            }

            // 2) 오브젝트가 아닌 "그냥 타일" 클릭인 경우
            // 원거리 상자 상호작용이 예약된 상황에서는
            // 타일 더블클릭으로는 이동을 시작하지 않는다.
            if (pendingChest != null)
            {
                // 왼쪽 클릭은 무시 (버튼으로만 이동 시작)
                return;
            }

            // 이미 같은 타일이 선택된 상태에서 다시 왼쪽 클릭 → 이동 실행
            if (selectedTargetCell.HasValue
                && selectedTargetCell.Value == clickedCell
                && currentPathCells != null
                && currentPathCells.Count >= 2)
            {
                StartPathMove(currentPathCells, pathArrivalCallback);
                return;
            }

            // 새 경로 계산
            var newPath2 = FindPath(currentCell, clickedCell);

            // 경로가 없거나 1칸(제자리)이면 선택/프리뷰 해제
            if (newPath2 == null || newPath2.Count <= 1)
            {
                selectedTargetCell = null;
                currentPathCells.Clear();
                ClearPathPreview();
                pendingChest = null;
                pathArrivalCallback = null;
                currentInteractTarget = null;
                currentDescData = null;
                Shared.interactionHintUI?.HideAll();
                return;
            }

            // 첫 번째 클릭: 경로만 표시 (이 경우 관찰 대상은 없음)
            selectedTargetCell = clickedCell;
            currentPathCells = newPath2;
            pendingChest = null;
            pathArrivalCallback = null;
            currentInteractTarget = null;
            currentDescData = null;
            ShowPathPreview(newPath2);
            Shared.interactionHintUI?.HideAll();
            return;
        }

        // 오른쪽 클릭: 같은 타일을 클릭하면 선택/프리뷰 취소
        if (Input.GetMouseButtonDown(1))
        {
            if (selectedTargetCell.HasValue || (currentPathCells != null && currentPathCells.Count > 0))
            {
                CancelSelectionAndHint();
            }
        }
    }
    //HintUi 공통 취소 메서드
    void CancelSelectionAndHint()
    {
        selectedTargetCell = null;
        currentPathCells.Clear();
        ClearPathPreview();
        pendingChest = null;
        pathArrivalCallback = null;
        currentInteractTarget = null;
        currentDescData = null;
        Shared.interactionHintUI?.HideAll();
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

        // 플레이어 현재 위치(시작 셀)도 포함해서 전부 표시
        for (int i = 0; i < cells.Count; i++)
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

    // 특정 오브젝트 셀 주변(인접 6칸) 중 하나까지의 최단 경로를 찾는다.
    List<Vector3Int> FindPathToAdjacentCell(Vector3Int start, Vector3Int objectCell)
    {
        Direction[] dirs =
        {
            Direction.West,
            Direction.East,
            Direction.NW,
            Direction.NE,
            Direction.SW,
            Direction.SE
        };

        List<Vector3Int> bestPath = null;

        bool odd = (objectCell.y & 1) != 0;

        foreach (var dir in dirs)
        {
            Vector3Int offset = GetOffsetForDirection(dir, odd);
            Vector3Int adj = objectCell + offset;

            if (!IsWalkableCell(adj))
                continue;

            var path = FindPath(start, adj);
            if (path == null || path.Count < 2)
                continue;

            if (bestPath == null || path.Count < bestPath.Count)
                bestPath = path;
        }

        return bestPath;
    }

    //심볼 인카운터 몬스터 체크
    bool TryGetEncounterAtCell(Vector3Int cell, out EncounterMonster monster)
    {
        monster = null;
        var world = floorTilemap.GetCellCenterWorld(cell);
        var hits = Physics2D.OverlapCircleAll(world, 0.05f, encounterLayerMask);
        foreach (var h in hits)
        {
            if (!h) continue;
            var m = h.GetComponentInParent<EncounterMonster>();
            if (m != null && m.IsActive)
            {
                monster = m;
                return true;
            }
        }
        return false;
    }

    // 경로를 따라 실제로 이동
    void StartPathMove(List<Vector3Int> cells, Action onArrive = null)
    {
        if (cells == null || cells.Count < 2) return; // 제자리이거나 잘못된 경로

        // 이동 중이면 먼저 정리
        if (pathMoveRoutine != null)
        {
            StopCoroutine(pathMoveRoutine);
            pathMoveRoutine = null;
        }

        isMovingByPath = true;

        // 도착 콜백 설정
        pathArrivalCallback = onArrive;

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
            // 도착 셀에서 인카운터 체크
            if (TryGetEncounterAtCell(cells[i], out var monster))
            {
                // 남은 경로 구성: 현재(몬스터 셀)부터 끝까지
                var remaining = new List<Vector3Int>();
                for (int k = i; k < cells.Count; k++)
                    remaining.Add(cells[k]);

                // 잠시 멈춤(연출용)
                if (animator != null) animator.SetInteger("Move", 0);

                // 이동 상태 정리(코루틴 종료)
                isMovingByPath = false;
                pathMoveRoutine = null;

                // 복귀 컨텍스트 저장
                var stm = Shared.SceneTransitionManager;
                if (stm != null)
                {
                    stm.SetResumePath(remaining);

                    // "전투씬으로 이동하기 전 타일(몬스터 타일)"을 복귀지점으로 저장
                    var returnPos = floorTilemap.GetCellCenterWorld(cells[i]);
                    stm.SaveReturnPoint(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, returnPos);

                    // 탐험 스냅샷 저장
                    stm.SaveExplorationSnapshot(stm.BuildExplorationSnapshotFromScene());

                    // 몬스터는 중복 인카운터 방지 처리(필요)
                    monster.MarkConsumed();

                    // 전투 씬으로
                    stm.FadeToScene(battleSceneName);
                }

                yield break;
            }
        }

        if (animator != null)
            animator.SetInteger("Move", 0);

        isMovingByPath = false;
        selectedTargetCell = null;
        currentPathCells.Clear();
        pathMoveRoutine = null;

        // 도착 후 콜백 실행
        var cb = pathArrivalCallback;

        // 이동 상태 초기화
        isMovingByPath = false;
        selectedTargetCell = null;
        currentPathCells.Clear();
        pathMoveRoutine = null;

        // 콜백을 먼저 실행
        cb?.Invoke();

        // 마지막으로 필드 정리
        pathArrivalCallback = null;
        pendingChest = null;
    }

    //탐험씬 복귀 후 남은 경로 이동
    public void ResumePathAfterBattle(List<Vector3Int> resumeCells)
    {
        if (resumeCells == null || resumeCells.Count < 2) return;
        if (isMovingByPath) return;

        // 복귀 직후 다른 선택/힌트 상태 정리
        CancelSelectionAndHint();

        // 콜백은 기본 이동으로 취급(필요하면 나중에 확장)
        StartPathMove(resumeCells, null);
    }
    #endregion
    void HandlePushDetection()
    {
        if (isPushMode) return;

        // 이전에 선택된 박스 하이라이트 해제
        if (selectedBox != null)
        {
            selectedBox.SetHighlight(false);
            selectedBox = null;
        }

        contactBoxes.Clear();
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.15f);

        PushObject closestBox = null;
        float bestSqr = float.MaxValue;
        Vector3 p = transform.position;

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<PushObject>(out var box))
            {
                float d = (box.transform.position - p).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    closestBox = box;
                }
            }
        }

        if (closestBox != null)
        {
            selectedBox = closestBox;
            contactBoxes.Add(closestBox);
            closestBox.SetHighlight(true);

            Vector3Int playerCell = floorTilemap.WorldToCell(rb.position);
            Vector3Int boxCell = floorTilemap.WorldToCell(closestBox.transform.position);
            Vector3Int delta = boxCell - playerCell;

            bool odd = Mathf.Abs(playerCell.y) % 2 == 1;
            pendingDirectionKey = GetDirectionFromDelta(delta, odd);

            var (blend, flipX) = GetPushBlend(pendingDirectionKey);
            animator.SetFloat("PushX", blend.x);
            animator.SetFloat("PushY", blend.y);
            spriterenderer.flipX = flipX;
        }
        else
        {
            pendingDirectionKey = Direction.None;
        }
    }
    // === Hint UI 버튼용 콜백 ===
    public void OnClickSurveyButton()
    {
        Shared.descriptionDialogUI?.Hide();

        if (isMovingByPath)
            return;

        // 이동 없이 즉시 실행해야 하는 경우 (한 칸 이내)
        if (currentPathCells == null || currentPathCells.Count < 2)
        {
            if (pendingChest != null)
            {
                pendingChest.OpenChest();
                Shared.interactionHintUI?.HideAll();
            }
            // 나중에 조사 대상이 더 생기면 여기에 else-if로 추가

            // 상태 정리
            ClearPath();
            return;
        }

        // 여기부터는 "이동 후 실행" 케이스
        if (currentPathCells != null && currentPathCells.Count >= 2)
        {
            Action onArrive = null;
            if (pendingChest != null)
                onArrive = () => pendingChest.OpenChest();

            StartPathMove(currentPathCells, onArrive);
        }
    }
    public void OnClickCommunicationButton()
    {
        if (isMovingByPath)
            return;

        // 한 칸 이내면 이동 없이 즉시 관찰
        if (currentPathCells == null || currentPathCells.Count < 2)
        {
            if (currentDescData != null && !string.IsNullOrWhiteSpace(currentDescData.description))
            {
                Shared.descriptionDialogUI?.Toggle(currentDescData.description);
                Shared.interactionHintUI?.HideAll();
            }
            return;
        }

        // 이동 후 관찰
        if (currentPathCells != null && currentPathCells.Count >= 2)
        {
            Action onArrive = null;
            if (currentDescData != null && !string.IsNullOrWhiteSpace(currentDescData.description))
            {
                onArrive = () =>
                {
                    Shared.descriptionDialogUI?.Toggle(currentDescData.description);
                    Shared.interactionHintUI?.HideAll();
                };
            }

            StartPathMove(currentPathCells, onArrive);
        }
    }
    public void OnClickCancelButton()
    {
        // 경로/상호작용 예약만 취소 (이동 중이 아닐 때)
        if (!isMovingByPath)
        {
            CancelSelectionAndHint();
        }

        // 밀기 모드라면 종료 처리
        if (isPushMode)
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
            Debug.Log("[Push] 밀기 모드 종료 (버튼)");
        }
    }
    public void TeleportTo(Vector3 worldPos)
    {
        rb.position = worldPos;
        transform.position = worldPos;
        ClearPath();
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

        pathArrivalCallback = null;
        pendingChest = null;
        currentInteractTarget = null;
        currentDescData = null;
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
        pathArrivalCallback = null;
        pendingChest = null;
        currentInteractTarget = null;
        currentDescData = null;

        // 기존 마우스 이동/키 입력도 모두 정지
        path.Clear();

        if (animator != null)
            animator.SetInteger("Move", 0);
    }
}
