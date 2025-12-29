// PlayerMovement.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using TMPro;

public class PlayerMovement : MonoBehaviour
{
    [Header("타일맵 설정")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public float defaultMoveSpeed = 2f;
    public LayerMask impassableLayerMask;

    private Rigidbody2D rb;
    private List<Vector3> path = new();
    float movementLockUntil = 0f;
    int _hardLockTokens = 0; // 무기한 잠금 토큰

    public bool isPushMode { private set; get; }
    private Direction pendingDirectionKey = Direction.None;
    private bool isPerformingPush = false;

    ContactFilter2D _castFilter;
    readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];

    // 타일 경로 기반 이동 상태
    [Header("타일 경로 이동 설정")]
    [SerializeField] private GameObject pathMarkerPrefab;   // 경로 표시용 프리팹
    [SerializeField] private GameObject pushMarkerPrefab; // Push 후보 타일 표시 전용
    [SerializeField] private GameObject goalMarkerPrefab; // 목표 지점(끝부분) 전용 마커(노란 테두리)

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
    private PortalController pendingPortal = null;
    private TestNpc pendingNpc = null;

    // 현재 상호작용 대상(관찰/조사 버튼이 가리키는 대상)
    private Collider2D currentInteractTarget = null;
    private DescriptionData currentDescData = null;

    private int _pendingMoveVigorCost = 0;  //이동 비용

    // Push 선택(타일 클릭) 모드
    private bool isPushSelectMode = false;
    private PushObject pendingPushBox = null;

    // 박스가 이동할 수 있는 목표 타일만 허용
    private HashSet<Vector3Int> pushValidTargetCells = new HashSet<Vector3Int>();

    // 표시용 마커(기존 pathMarkerPrefab 재사용 가능)
    private readonly List<GameObject> activePushMarkers = new List<GameObject>();

    [Header("Path Cost Label (TMP)")]
    [SerializeField] private float pathCostLabelScale = 0.05f;
    private TextMeshPro _pathCostTMP = null;
    private GameObject _pathCostLabelGO = null;

    [SerializeField] private LayerMask encounterLayerMask;
    [SerializeField] private string battleSceneName = "BattleScene"; // 실제 전투씬 이름으로

    private SpriteRenderer spriterenderer;
    private Animator animator;

    void Awake()
    {
        Shared.PlayerMovement = this;
        rb = GetComponent<Rigidbody2D>();
        spriterenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

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
                              || GamePause.IsPaused;

        if (isInputBlocked)
        {
            if (!isMovingByPath)
                HaltImmediately();

            return;
        }

        if (!isPushSelectMode && pendingPushBox == null)
        {
            HandleTileClickInput(); // 타일 클릭/상자 클릭 이동
        }

        if (isPushSelectMode)
        {
            // RMB 취소(요구사항 5)
            if (Input.GetMouseButtonDown(1))
            {
                ExitPushSelectMode();
                return;
            }

            // LMB: 허용 타일만
            if (Input.GetMouseButtonDown(0))
            {
                var cam = Camera.main;
                float zDist = cam.orthographic ? 0f : (transform.position.z - cam.transform.position.z);
                var wp = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDist));
                wp.z = transform.position.z;

                var clickedCell = floorTilemap.WorldToCell(wp);

                if (!pushValidTargetCells.Contains(clickedCell))
                    return;

                // “목표 셀”만 넘기고, 실제 연속/단발 푸시는 내부에서 처리
                StartPushToCell(pendingPushBox, clickedCell);

                ExitPushSelectMode(keepBoxHighlight: false);
                return;
            }

            return; // pushSelectMode 중에는 일반 이동/상호작용 입력 차단
        }

        if (!isPushSelectMode && pendingPushBox != null && Input.GetMouseButtonDown(1))
        {
            pendingPushBox.SetHighlight(false);
            pendingPushBox = null;
            Shared.interactionHintUI?.HideAll();
            return;
        }
    }

    void FixedUpdate()
    {
        if (isPerformingPush) return;
        if (GamePause.IsPaused)
        {
            if (animator != null)
                animator.SetInteger("Move", 0);
            return;
        }
    }

    void ExitPushSelectMode(bool keepBoxHighlight = false)
    {
        ClearPushTargets();
        pushValidTargetCells.Clear();

        if (animator != null)
        {
            animator.SetInteger("Move", 0);
            animator.SetBool("IsPushIdle", false);
        }

        isPushSelectMode = false;
        isPushMode = false;

        if (!keepBoxHighlight)
            pendingPushBox?.SetHighlight(false);

        pendingPushBox = null;
        pendingDirectionKey = Direction.None;

        Shared.interactionHintUI?.HideAll();
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
        PushObject clickedPush = null;
        PortalController clickedPortal = null;
        TestNpc clickedNpc = null;

        var hits = Physics2D.OverlapPointAll(wp);
        foreach (var h in hits)
        {
            // PushObject 감지
            var push = h.GetComponentInParent<PushObject>();
            if (push != null)
            {
                clickedPush = push;
                if (!clickedCollider) clickedCollider = h;
            }

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

            // Portal 감지
            var portal = h.GetComponentInParent<PortalController>();
            if (portal != null)
            {
                if (clickedPortal == null) clickedPortal = portal;
                if (!clickedCollider) clickedCollider = h;
            }

            // NPC 감지
            var npc = h.GetComponentInParent<TestNpc>();
            if (npc != null)
            {
                if (clickedNpc == null) clickedNpc = npc;
                if (!clickedCollider) clickedCollider = h;
            }
        }

        // 왼쪽 클릭: 경로 프리뷰 or 이동 실행
        if (Input.GetMouseButtonDown(0))
        {
            if (clickedPush != null)
            {
                CancelSelectionAndHint(); // 기존 선택/경로 정리:contentReference[oaicite:9]{index=9}

                pendingPushBox = clickedPush;
                pendingPushBox.SetHighlight(true);

                // 밀기/취소 2버튼만 표시
                Shared.interactionHintUI?.ShowPushCancelAt(pendingPushBox.transform);
                return;
            }

            // 오브젝트(상자, NPC, 기타) 클릭
            if (clickedChest != null || clickedPortal != null || clickedNpc != null || clickedCollider != null)
            {
                // 목표가 될 Transform 결정 (상자 우선, 아니면 해당 콜라이더)
                Transform targetTr = clickedChest ? clickedChest.transform :
                                    (clickedPortal != null ? clickedPortal.transform :
                                    (clickedNpc != null ? clickedNpc.transform :
                                     clickedCollider.transform));

                // 대상 셀
                Vector3Int targetCell = floorTilemap.WorldToCell(targetTr.position);
                

                // 현재 셀이 대상 셀과 같은 셀이거나, 인접 6칸 중 하나라면 "이동 없이 상호작용 가능"
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
                    pendingPortal = clickedPortal;
                    pendingNpc = clickedNpc;

                    // 제자리에선 경로 프리뷰는 필요 없으니 호출해도 표시가 안 됨(Count < 2라서)
                    ShowPathPreview(currentPathCells);

                    // HintUI를 대상 위치에 표시 (조사/관찰/취소 버튼 모두)
                    Shared.interactionHintUI?.HideAll();

                    if (clickedPortal != null)
                    {
                        Shared.interactionHintUI?.ShowSurveyAt(targetTr, clickedPortal.GetHintLabel()); // "이동"
                                                                                                        // Portal은 관찰 버튼 불필요하면 생략
                    }
                    else if (clickedNpc != null)
                    {
                        Shared.interactionHintUI?.ShowSurveyAt(targetTr, clickedNpc.GetHintLabel());     // "대화"
                                                                                                         // 필요하면 관찰 버튼도:
                        if (clickedDesc != null && !string.IsNullOrWhiteSpace(clickedDesc.description))
                            Shared.interactionHintUI?.ShowCommunicationAt(targetTr, "관찰");
                    }
                    else
                    {
                        Shared.interactionHintUI?.ShowBothAt(targetTr); // 기존 상자/기타
                    }

                    Shared.interactionHintUI?.ShowCancelAt(targetTr);
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
                    pendingPortal = null;
                    pendingNpc = null;
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
                pendingPortal = clickedPortal;
                pendingNpc = clickedNpc;

                ShowPathPreview(newPath);

                // HintUI를 대상 위치에 표시 (조사/관찰/취소 버튼 모두)
                Shared.interactionHintUI?.ShowBothAt(targetTr); // 조사 + 관찰
                Shared.interactionHintUI?.ShowCancel();         // 취소 버튼 추가
                return;
            }

            // 오브젝트가 아닌 그냥 타일 클릭인 경우
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
        pendingPortal = null;
        pendingNpc = null;
        Shared.interactionHintUI?.HideAll();
    }

    // 외부에서 잠금 요청
    public void LockMovementFor(float seconds)
    {
        movementLockUntil = Mathf.Max(movementLockUntil, Time.time + Mathf.Max(0f, seconds));

        // 경로 이동 중에는 코루틴을 끊지 말고 입력만 잠금
        if (!isMovingByPath)
        {
            HaltImmediately();
        }
        else
        {
            if (animator != null) animator.SetInteger("Move", 0);
        }
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
        if (_pathCostLabelGO != null)
        {
            Destroy(_pathCostLabelGO);
            _pathCostLabelGO = null;
            _pathCostTMP = null;
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

            bool isGoal = (i == cells.Count - 1);

            GameObject prefabToUse = isGoal && goalMarkerPrefab != null
                ? goalMarkerPrefab
                : pathMarkerPrefab;

            if (prefabToUse == null) continue;

            var marker = Instantiate(prefabToUse, world, Quaternion.identity);
            activePathMarkers.Add(marker);
        }

        // === 목표 지점에 총 필요 활기 표시 ===
        var vigor = Shared.VigorManager;
        if (vigor != null && floorTilemap != null && cells != null && cells.Count > 0)
        {
            int steps = Mathf.Max(0, cells.Count - 1);
            int cost = steps * Mathf.Max(0, vigor.costMovePerTile);

            Vector3Int goalCell = cells[cells.Count - 1];
            Vector3 goalWorld = floorTilemap.GetCellCenterWorld(goalCell);
            goalWorld.z = transform.position.z;

            if (_pathCostLabelGO == null)
            {
                _pathCostLabelGO = new GameObject("PathCostLabel_TMP");
                _pathCostLabelGO.transform.localScale = Vector3.one * pathCostLabelScale;

                _pathCostTMP = _pathCostLabelGO.AddComponent<TextMeshPro>();
                _pathCostTMP.text = $"-{cost}";
                _pathCostTMP.alignment = TextAlignmentOptions.Center;
                _pathCostTMP.fontSize = 25;                // 스케일과 함께 튜닝
                _pathCostTMP.enableWordWrapping = false;
                _pathCostTMP.sortingOrder = 1000;          // 타일/마커 위로
                _pathCostTMP.outlineWidth = 0.2f;          // 가독성 (필요 시)
                _pathCostTMP.color = (cost <= vigor.CurrentVigor) ? Color.white : Color.red;
            }

            _pathCostLabelGO.transform.position = goalWorld;
            _pathCostTMP.text = $"-{cost}";
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

    List<Vector3Int> FindPathToPushReadyCell(Vector3Int playerCell, Vector3Int boxCell, PushObject box)
    {
        Direction[] dirs =
        {
        Direction.West, Direction.East,
        Direction.NW, Direction.NE,
        Direction.SW, Direction.SE
    };

        List<Vector3Int> best = null;
        bool oddBox = (boxCell.y & 1) != 0;

        foreach (var d in dirs)
        {
            var adj = boxCell + GetOffsetForDirection(d, oddBox);
            if (!IsWalkableCell(adj)) continue;

            // adj(플레이어 위치)에서 box를 밀 때의 “플레이어→박스 상대 방향”을 dirKey로 산출
            var delta = boxCell - adj;
            bool oddAdj = (adj.y & 1) != 0;
            var dirKey = GetDirectionFromDelta(delta, oddAdj);
            if (dirKey == Direction.None) continue;

            // 그 자리에서 실제로 1칸이라도 밀 수 있어야 “push-ready”
            // (후보 타일 계산 함수는 아래 2)에서 추가할 BuildPushLineTargets를 사용)
            var line = BuildPushLineTargets(box, boxCell, dirKey);
            if (line.Count == 0) continue;

            var path = FindPath(playerCell, adj);
            if (path == null || path.Count < 2) continue;

            if (best == null || path.Count < best.Count) best = path;
        }

        return best;
    }

    //심볼 인카운터 몬스터 타일 체크
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
    //함정 타일 체크
    void TryTriggerTrapAtCell(Vector3Int cell)
    {
        if (floorTilemap == null) return;

        var traps = TrapBehavior.allTraps;
        for (int i = 0; i < traps.Count; i++)
        {
            var trap = traps[i];
            if (!trap) continue;
            trap.TryTriggerByPlayer(floorTilemap, cell);
        }
    }
    void TryConsumeTrapByBoxAtCell(Vector3Int cell)
    {
        if (floorTilemap == null) return;

        var traps = TrapBehavior.allTraps;
        for (int i = 0; i < traps.Count; i++)
        {
            var trap = traps[i];
            if (!trap) continue;
            if (!trap.gameObject.activeInHierarchy) continue; // 비활성 트랩 스킵(권장)

            trap.TryConsumeByBox(floorTilemap, cell);
        }
    }

    void StartPushToCell(PushObject box, Vector3Int targetCell)
    {
        if (box == null || floorTilemap == null) return;

        // 시퀀스 시작 시점의 “방향”은 EnterPushSelectMode에서 결정된 pendingDirectionKey를 사용
        // (해당 모드에서는 방향이 하나로 고정되는 설계)
        if (pendingDirectionKey == Direction.None) return;

        // 입력 잠금은 시퀀스에서만 관리
        StartCoroutine(PerformPushToTarget(box, pendingDirectionKey, targetCell));
    }

    // 경로를 따라 실제로 이동
    void StartPathMove(List<Vector3Int> cells, Action onArrive = null, int? overrideVigorCost = null)
    {
        if (cells == null || cells.Count < 2) return; // 제자리이거나 잘못된 경로

        // 이동 비용 계산
        _pendingMoveVigorCost = 0;
        var vigor = Shared.VigorManager;

        if (overrideVigorCost.HasValue)
        {
            // 복귀 후 이어서 이동: "총 예정 비용"을 그대로 이어받는다
            _pendingMoveVigorCost = Mathf.Max(0, overrideVigorCost.Value);
        }
        else if (vigor != null)
        {
            int steps = Mathf.Max(0, cells.Count - 1);
            int cost = steps * Mathf.Max(0, vigor.costMovePerTile);

            // 선 차감 대신 가능 여부만 확인
            if (cost > 0 && !vigor.CanSpend(cost))
            {
                Shared.explorationLogUI?.Push($"활기가 부족합니다. 이동 필요: {cost}, 현재: {vigor.CurrentVigor}");
                CancelSelectionAndHint();
                return;
            }
            _pendingMoveVigorCost = cost;
        }

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

        var moveCells = new List<Vector3Int>(cells);   // 복사본 생성
        pathMoveRoutine = StartCoroutine(Co_MoveAlongPath(moveCells));
    }

    void EndPathMove()
    {
        isMovingByPath = false;
        selectedTargetCell = null;
        currentPathCells.Clear();
        pathMoveRoutine = null;

        // 경로 프리뷰/마커 정리
        ClearPathPreview();
    }

    IEnumerator Co_MoveAlongPath(List<Vector3Int> cells)
    {
        // 안전하게 로컬로 복사(중간에 외부에서 리스트가 바뀌는 경우 방지)
        if (cells == null || cells.Count < 2)
        {
            EndPathMove();
            yield break;
        }

        Action cb = pathArrivalCallback;

        try
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
                        if (animator != null) animator.SetInteger("Move", 0);
                        yield return null;
                        continue;
                    }

                    if (Time.time < movementLockUntil)
                    {
                        if (animator != null) animator.SetInteger("Move", 0);
                        yield return null;
                        continue;
                    }

                    t += Time.deltaTime / duration;
                    Vector3 pos = Vector3.Lerp(start, end, Mathf.Clamp01(t));
                    rb.MovePosition(pos);
                    yield return null;
                }

                rb.MovePosition(end);

                // 도착 셀 함정 체크
                TryTriggerTrapAtCell(cells[i]);

                // 도착 셀에서 인카운터 체크
                if (TryGetEncounterAtCell(cells[i], out var monster))
                {
                    // 남은 경로 구성: 현재(몬스터 셀)부터 끝까지
                    var remaining = new List<Vector3Int>();
                    for (int k = i; k < cells.Count; k++)
                        remaining.Add(cells[k]);

                    if (animator != null) animator.SetInteger("Move", 0);

                    var stm = Shared.SceneTransitionManager;
                    if (stm != null && Shared.VigorManager != null)
                    {
                        stm.SetDeferredMoveCost(_pendingMoveVigorCost);
                        stm.SetResumePath(remaining);

                        var returnPos = floorTilemap.GetCellCenterWorld(cells[i]);
                        stm.SaveReturnPoint(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, returnPos);

                        stm.SaveExplorationSnapshot(stm.BuildExplorationSnapshotFromScene());

                        monster.MarkConsumed();
                        stm.SaveVigor(Shared.VigorManager.CurrentVigor);

                        string monsterName = monster != null ? monster.gameObject.name : "몬스터";
                        stm.EnterBattleWithEncounterBanner(monsterName, battleSceneName);

                        yield break;
                    }

                    _pendingMoveVigorCost = 0;
                    yield break;
                }
            }

            // 이동 비용 결제(도착까지 완료된 경우에만)
            if (Shared.VigorManager != null && _pendingMoveVigorCost > 0)
            {
                if (!Shared.VigorManager.TrySpend(_pendingMoveVigorCost, VigorSpendReason.MoveTile))
                {
                    Shared.VigorManager.FailExploration(
                        $"탐색을 실패했습니다. (이동 결제 실패 / 필요 {_pendingMoveVigorCost}, 현재 {Shared.VigorManager.CurrentVigor})"
                    );
                    yield break;
                }
            }

            _pendingMoveVigorCost = 0;

            if (animator != null)
                animator.SetInteger("Move", 0);

            // 정상 도착 콜백 실행(상자 열기 등)
            cb?.Invoke();
        }
        finally
        {
            EndPathMove();  // 이동 상태 정리

            // 이동 후 상호작용 예약은 한 번 쓰고 비움
            pathArrivalCallback = null;
            pendingChest = null;
            currentInteractTarget = null;
            currentDescData = null;
        }
    }

    //탐험씬 복귀 후 남은 경로 이동
    public void ResumePathAfterBattle(List<Vector3Int> resumeCells)
    {
        if (resumeCells == null || resumeCells.Count < 2) return;
        if (isMovingByPath) return;

        CancelSelectionAndHint();

        // 전투 전에 저장해 둔 총 예정 이동 비용을 이어받는다
        int plannedCost = 0;
        var stm = Shared.SceneTransitionManager;
        if (stm != null) plannedCost = stm.ConsumeDeferredMoveCost();

        StartPathMove(resumeCells, null, plannedCost);
    }
    #endregion
    // === Hint UI 버튼용 콜백 ===
    public void OnClickSurveyButton()
    {
        Shared.descriptionDialogUI?.Hide();
        Shared.interactionHintUI?.HideAll();

        if (isMovingByPath)
            return;

        // Portal/NPC 이동 후 실행 또는 즉시 실행
        if (pendingPortal != null || pendingNpc != null)
        {
            var portal = pendingPortal;
            var npc = pendingNpc;

            // 이동 없이 즉시 실행(인접/제자리)
            if (currentPathCells == null || currentPathCells.Count < 2)
            {
                if (portal != null) portal.UsePortal();
                else if (npc != null) npc.Talk();

                ClearPath();
                pendingPortal = null;
                pendingNpc = null;
                return;
            }

            // 이동 후 실행
            Action onArrive = () =>
            {
                if (portal != null) portal.UsePortal();
                else if (npc != null) npc.Talk();
            };

            StartPathMove(currentPathCells, onArrive);

            // 예약 정리(한 번만)
            pendingPortal = null;
            pendingNpc = null;
            return;
        }

        // Push 전용 분기
        if (pendingPushBox != null)
        {
            var box = pendingPushBox;
            var playerCell = floorTilemap.WorldToCell(rb.position);
            var boxCell = floorTilemap.WorldToCell(box.transform.position);

            // 이미 인접이면 즉시 진입
            if (IsAdjacentOrSame(playerCell, boxCell))
            {
                EnterPushSelectMode(box);
                return;
            }

            // 멀리 있으면: “밀기 가능한 인접 타일”까지 이동 후 진입
            var pathToReady = FindPathToPushReadyCell(playerCell, boxCell, box);
            if (pathToReady == null || pathToReady.Count < 2)
            {
                Shared.explorationLogUI?.Push("해당 상자를 밀 수 있는 위치로 이동할 수 없습니다.");
                box.SetHighlight(false);
                pendingPushBox = null;
                Shared.interactionHintUI?.HideAll();
                return;
            }

            // 이동 시작 즉시 UI 닫기
            Shared.interactionHintUI?.HideAll();

            // 도착 후 밀기 모드 진입
            Action onArrive = () =>
            {
                // 도착 시점에 박스가 아직 존재/유효한지 확인
                if (box == null) return;

                // 도착 후에는 인접일 것이며, 이때 EnterPushSelectMode가 다칸 후보까지 표시(2번 수정)
                EnterPushSelectMode(box);
            };

            StartPathMove(pathToReady, onArrive);
            return;
        }
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

    void EnterPushSelectMode(PushObject box)
    {
        if (box == null || floorTilemap == null) return;

        // 경로/예약 상호작용 정리
        ClearPathPreview();
        currentPathCells.Clear();

        isPushSelectMode = true;
        isPushMode = true;

        if (animator != null)
            animator.SetBool("IsPushIdle", true); // 밀기 준비 애니메이션

        // 플레이어 위치/박스 위치로 "밀 수 있는 방향 1개" 계산(플레이어 인접 기준)
        var playerCell = floorTilemap.WorldToCell(rb.position);
        var boxCell = floorTilemap.WorldToCell(box.transform.position);

        var delta = boxCell - playerCell;
        bool odd = (playerCell.y & 1) != 0;
        pendingDirectionKey = GetDirectionFromDelta(delta, odd);

        pushValidTargetCells.Clear();

        //현재 플레이어가 서 있는 방향으로 밀수 있는 후보 생성
        var startBoxCell = floorTilemap.WorldToCell(box.transform.position);
        var line = BuildPushLineTargets(box, startBoxCell, pendingDirectionKey);

        for (int i = 0; i < line.Count; i++)
            pushValidTargetCells.Add(line[i]);

        if (pushValidTargetCells.Count == 0)
        {
            Shared.explorationLogUI?.Push("이 위치에서는 밀 수 없습니다.");
            ExitPushSelectMode(keepBoxHighlight: false);
            return;
        }

        ShowPushTargets(pushValidTargetCells);
    }

    //연속적으로 목적지까지 밀기
    List<Vector3Int> BuildPushLineTargets(PushObject box, Vector3Int startBoxCell, Direction dirKey)
    {
        var results = new List<Vector3Int>();
        if (box == null || box.floorTilemap == null) return results;
        if (dirKey == Direction.None) return results;

        // 다른 PushObject 점유 셀(가상 충돌 체크)
        var occupied = new HashSet<Vector3Int>();
        foreach (var po in FindObjectsOfType<PushObject>())
        {
            if (po == null || po == box) continue;
            occupied.Add(box.floorTilemap.WorldToCell(po.transform.position));
        }

        var cur = startBoxCell;

        while (true)
        {
            bool odd = (cur.y & 1) != 0;
            var offset = GetOffsetForDirection(dirKey, odd);
            var next = cur + offset;

            // 바닥/벽 체크
            bool hasFloor = box.floorTilemap.HasTile(next);
            bool hasWall = (box.wallTilemap != null && box.wallTilemap.HasTile(next));
            if (!hasFloor || hasWall) break;

            // 장애물 레이어(박스 스스로 쓰는 obstacleLayer 사용)
            var world = box.floorTilemap.GetCellCenterWorld(next);
            var obstacle = Physics2D.OverlapCircle(world, 0.1f, box.obstacleLayer);
            if (obstacle != null) break;

            // 다른 PushObject 점유
            if (occupied.Contains(next)) break;

            // 여기까지 통과하면 “next는 밀기 가능한 목적지”
            results.Add(next);

            // 다음 반복을 위해 박스 위치를 next로 가정
            cur = next;
        }

        return results;
    }

    public void OnClickCancelButton()
    {
        // 경로/상호작용 예약만 취소 (이동 중이 아닐 때)
        if (!isMovingByPath)
            CancelSelectionAndHint();
        if (isPushSelectMode) 
            ExitPushSelectMode();
        else if (pendingPushBox != null)    //푸시 타겟 선택 상태 정리
        {
            pendingPushBox.SetHighlight(false);
            pendingPushBox = null;
            Shared.interactionHintUI?.HideAll();
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
    void ShowPushTargets(IEnumerable<Vector3Int> cells)
    {
        ClearPushTargets();

        if (pushMarkerPrefab == null) return;

        foreach (var c in cells)
        {
            var world = floorTilemap.GetCellCenterWorld(c);
            world.z = transform.position.z;

            var marker = Instantiate(pushMarkerPrefab, world, Quaternion.identity);
            activePushMarkers.Add(marker);
        }
    }

    void ClearPushTargets()
    {
        for (int i = 0; i < activePushMarkers.Count; i++)
            if (activePushMarkers[i]) Destroy(activePushMarkers[i]);

        activePushMarkers.Clear();
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

    // 밀기 상자용 인접 판정
    bool IsAdjacentOrSame(Vector3Int a, Vector3Int b)
    {
        if (a == b) return true;

        Direction[] dirs =
        {
        Direction.West, Direction.East,
        Direction.NW, Direction.NE,
        Direction.SW, Direction.SE
    };

        bool odd = (b.y & 1) != 0;
        foreach (var dir in dirs)
        {
            var off = GetOffsetForDirection(dir, odd);
            if (b + off == a) return true;
        }
        return false;
    }

    IEnumerator PerformPush(PushObject box, Vector3Int fromCell, Vector3Int dir)
    {
        float duration = 0.2f;

        var from = fromCell;
        var to = fromCell + dir;
        var boxOdd = (from.y & 1) != 0;
        var key = GetDirectionFromDelta(to - from, boxOdd);
        var (blend, flipX) = GetPushBlend(key);

        if (animator != null)
        {
            animator.SetFloat("PushX", blend.x);
            animator.SetFloat("PushY", blend.y);
            spriterenderer.flipX = flipX;
        }
        

        Vector3 fromBox = box.transform.position;
        Vector3 toBox = floorTilemap.GetCellCenterWorld(fromCell + dir);
        Vector3 fromPlayer = rb.position;

        Vector3 moveDir = (toBox - fromBox).normalized;      // 박스 이동 방향
        float offset = 0.15f;
        Vector3 pushVisualTarget = fromBox - moveDir * offset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            box.transform.position = Vector3.Lerp(fromBox, toBox, t);
            rb.MovePosition(Vector3.Lerp(fromPlayer, pushVisualTarget, t));
            yield return null;
        }

        box.transform.position = toBox;
        var logicalPlayerCellCenter = floorTilemap.GetCellCenterWorld(fromCell);
        logicalPlayerCellCenter.z = transform.position.z;
        rb.MovePosition(logicalPlayerCellCenter);

        // 상자가 최종적으로 도착한 셀
        var boxArrivedCell = fromCell + dir;

        // 상자에 의해 함정 제거
        TryConsumeTrapByBoxAtCell(boxArrivedCell);

        // 퍼즐 박스 위치 갱신 및 목표 체크 호출
        Shared.PuzzleManager?.ExecutePush(box, fromCell, fromCell + dir);
    }

    //연속 밀기 기능
    // PerformPushToTarget에서 연속으로 밀 수 있는지 확인 후 이동은 PerformPush로 진행됨
    IEnumerator PerformPushToTarget(PushObject box, Direction dirKey, Vector3Int targetCell)
    {
        int pushedTiles = 0;
        bool reachedTarget = false;

        isPerformingPush = true;

        if (animator != null)
            animator.SetBool("IsPushing", true);

        try
        {
            while (true)
            {
                if (box == null) yield break;

                var curCell = floorTilemap.WorldToCell(box.transform.position);

                // 이미 목표 도달
                if (curCell == targetCell)
                {
                    reachedTarget = true;
                    yield break;
                }

                // 현재 위치에서 가능한 라인을 다시 계산해서 1칸이라도 가능한지 확인
                var line = BuildPushLineTargets(box, curCell, dirKey);
                if (line.Count == 0)
                    yield break; // 더 이상 못 밈(중간에 막힘)

                // 다음 1칸 목적지(라인의 첫 칸)
                var nextCell = line[0];

                if (targetCell != nextCell && !line.Contains(targetCell))
                    yield break;

                var stepDir = nextCell - curCell;

                // 1칸 스텝 실행(연출/이동/퍼즐 갱신)
                yield return StartCoroutine(PerformPush(box, curCell, stepDir));

                pushedTiles++;
            }
        }
        finally
        {
            if (animator != null)
                animator.SetBool("IsPushing", false);

            isPerformingPush = false;

            if (reachedTarget && pushedTiles > 0 && Shared.VigorManager != null)
            {
                int cost = pushedTiles * Shared.VigorManager.costPushBoxPerTile;
                if (cost > 0)
                    Shared.VigorManager.TrySpend(cost, VigorSpendReason.PushBox);
            }
        }
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
        // Push 상태 강제 정리 (안전망)
        if (isPushSelectMode)
            ExitPushSelectMode(keepBoxHighlight: false);
        else if (pendingPushBox != null)
        {
            pendingPushBox.SetHighlight(false);
            pendingPushBox = null;
            ClearPushTargets();
            pushValidTargetCells.Clear();
            isPushMode = false;
        }

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
