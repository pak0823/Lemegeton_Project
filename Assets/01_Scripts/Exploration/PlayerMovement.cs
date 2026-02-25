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

    public static PlayerMovement Instance {  get; private set; }







    public float defaultMoveSpeed = 2f;

    public Tilemap floorTilemap => PathfindingSystem.Instance != null ? PathfindingSystem.Instance.floorTilemap : null;



    public Rigidbody2D rb;

    private List<Vector3> path = new();

    float movementLockUntil = 0f;

    int _hardLockTokens = 0; // 무기한 잠금 토큰



    public PlayerPushHandler PushHandler { get; private set; }





    readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];



    // 자동경로 기반 이동 상태

    [Header("자동경로 이동 설정")]

    [SerializeField] private GameObject pathMarkerPrefab;   // 경로 표시용 프리팹



    [SerializeField] private GameObject goalMarkerPrefab; // 목표 지점 표시용 마커(최종 도착)



    // 현재 선택된 경로(셀 단위)

    public List<Vector3Int> currentPathCells = new List<Vector3Int>();

    // 현재 선택된 목표 셀 (첫 번째 클릭으로 선택된 상태)

    private Vector3Int? selectedTargetCell = null;

    // 경로를 따라 실제 이동 중인지 여부

    public bool isMovingByPath = false;

    // 경로 이동 코루틴 핸들

    public Coroutine pathMoveRoutine = null;

    // 화면에 찍힌 경로 마커들

    private readonly List<GameObject> activePathMarkers = new List<GameObject>();



    /// <summary>
    /// 플레이어가 타일 위에 다다르때 발생하는 이벤트.
    /// HiddenPortalController등이 구독하여 특정 타일 감지에 활용합니다.
    /// </summary>
    public static System.Action<Vector3Int> OnTileStepped;


// 경로 도착 후 실행할 콜백 (예: 상자 열기)

    private Action pathArrivalCallback = null;



    // 외부로 이관된 상호작용 관련 로직 핸들러
    [HideInInspector] public PlayerInteractionHandler InteractionHandler { get; private set; }



    private int _pendingMoveVigorCost = 0;  //이동 비용







    [Header("Path Cost Label (TMP)")]

    [SerializeField] private float pathCostLabelScale = 0.05f;

    private TextMeshPro _pathCostTMP = null;

    private GameObject _pathCostLabelGO = null;



    [Header("점프 이동 설정")]

    public AnimationCurve jumpCurve;      // 점프 곡선

    public float jumpHeightMultiplier = 0.5f; // 점프 높이 배율



    [SerializeField] private LayerMask encounterLayerMask;

    [SerializeField] private SceneName battleSceneName = SceneName.BattleScene; // 현재 전투 씬 이름으로



    public SpriteRenderer spriterenderer;
    public Animator animator;



    void Awake()
    {
        if(Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject); // 중복 방지 강화

        rb = GetComponent<Rigidbody2D>();
        spriterenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        InteractionHandler = GetComponent<PlayerInteractionHandler>();
        if (InteractionHandler == null) InteractionHandler = gameObject.AddComponent<PlayerInteractionHandler>();
        InteractionHandler.Initialize(this);

        PushHandler = GetComponent<PlayerPushHandler>();
        if (PushHandler == null) PushHandler = gameObject.AddComponent<PlayerPushHandler>();
        PushHandler.Initialize(this);

        // 마우스 이동 충돌 예측 필터 (자기 자신은 충돌 제외)

        // Controller 자동 부착 및 초기화
        var interactionCtrl = GetComponent<ExplorationInteractionController>();
        if (interactionCtrl == null) interactionCtrl = gameObject.AddComponent<ExplorationInteractionController>();

        var inputCtrl = GetComponent<PlayerInputController>();
        if (inputCtrl == null) inputCtrl = gameObject.AddComponent<PlayerInputController>();

        interactionCtrl.Initialize(this);
        inputCtrl.Initialize(interactionCtrl);
    }



    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    public bool IsMoving => isMovingByPath;
    private bool IsInputBlocked => (Time.time < movementLockUntil) || (_hardLockTokens > 0) || PushHandler.IsPerformingPush || GamePause.IsPaused;

    // 버튼 대응 (관찰/획득)
    public void OnClickSurveyButton()
    {
        DescriptionDialogUI.Instance?.Hide();
        InteractionHintUI.Instance?.HideAll();

        if (IsInputBlocked) return;
        if (isMovingByPath) return;

        // Push 상자 관련 처리는 PlayerMovement > PlayerPushHandler로 이관됨.
        if (PushHandler.PendingPushBox != null)
        {
            var box = PushHandler.PendingPushBox;
            var playerCell = floorTilemap.WorldToCell(rb.position);
            var boxCell = floorTilemap.WorldToCell(box.transform.position);

            if (PushHandler.IsAdjacentOrSame(playerCell, boxCell))
            {
                PushHandler.EnterPushSelectMode(box);
                return;
            }

            var pathToReady = PushHandler.FindPathToPushReadyCell(playerCell, boxCell, box);
            if (pathToReady == null || pathToReady.Count < 2)
            {
                ExplorationLogUI.Instance?.Push("해당 상자를 밀 수 있는 위치로 이동할 수 없습니다.");
                box.SetHighlight(false);
                PushHandler.HaltPushImmediately();
                InteractionHintUI.Instance?.HideAll();
                return;
            }

            InteractionHintUI.Instance?.HideAll();

            System.Action onArrive = () =>
            {
                if (box == null) return;
                PushHandler.EnterPushSelectMode(box);
            };

            StartPathMove(pathToReady, onArrive);
            return;
        }

        // 상호작용 지점이 가깝거나 사거리 내인 경우 즉시 실행
        if (currentPathCells == null || currentPathCells.Count < 2)
        {
            InteractionHandler.ExecuteSurvey();
            ClearPath();
            return;
        }

        // 목표 지점까지 이동 후 상호작용 실행
        System.Action onArriveSurvey = () =>
        {
            InteractionHandler.ExecuteSurvey();
        };

        StartPathMove(currentPathCells, onArriveSurvey);
    }

    public void OnClickCommunicationButton()
    {
        if (IsInputBlocked) return;
        if (isMovingByPath) return;

        // 사거리 내 즉시 실행
        if (currentPathCells == null || currentPathCells.Count < 2)
        {
            InteractionHandler.ExecuteCommunication();
            return;
        }

        // 타겟까지 이동 후 실행
        System.Action onArriveComm = () =>
        {
            InteractionHandler.ExecuteCommunication();
        };

        StartPathMove(currentPathCells, onArriveComm);
    }
    // 공통적으로 클릭 시 다이얼로그가 열려있으면 닫고 입력을 소비함
    public bool HandleGlobalClickBlocking()
    {
        if (DescriptionDialogUI.Instance != null && DescriptionDialogUI.Instance.IsOpen)
        {
            DescriptionDialogUI.Instance.Hide();
            HaltImmediately();
            return true; // 입력 소비됨
        }

        if (IsInputBlocked)
        {
            if (!isMovingByPath) HaltImmediately();
            return true;
        }
        return false;
    }

    void Update()
    {
        // 입력 로직 제거됨 (PlayerInputController 사용)
    }

    // --- Controller에서 호출하는 메서드들 ---

    public void ProcessRightClick()
    {
        // 우클릭은 다이얼로그 등을 닫는 동작으로 사용될 수도 있지만,
        // 기존 로직에서는 좌클릭으로 닫았음.
        // 우클릭 시에도 일단 블락 체크
        if (IsInputBlocked) return;

        if (PushHandler.IsPushSelectMode)
        {
            PushHandler.ExitPushSelectMode();
            return;
        }

        if (PushHandler.PendingPushBox != null)
        {
            PushHandler.HaltPushImmediately();
            InteractionHintUI.Instance?.HideAll();
            return;
        }

        // 일반 이동/상호작용 취소
        if (selectedTargetCell.HasValue || (currentPathCells != null && currentPathCells.Count > 0))
        {
            CancelSelectionAndHint();
        }
    }

    public void ProcessInteractionClick(Vector3Int clickedCell, Transform targetTr, IInteractable interactable, Collider2D collider, DescriptionData desc)
    {
        if (HandleGlobalClickBlocking()) return;
        if (isMovingByPath) return;

        Vector3Int currentCell = PathfindingSystem.Instance.GetCellFromWorldPos(rb.position);
        currentCell.z = 0;

        bool isAdjacentOrSame = false;
        if (currentCell == clickedCell)
        {
            isAdjacentOrSame = true;
        }
        else
        {
             Direction[] dirs = { Direction.West, Direction.East, Direction.NW, Direction.NE, Direction.SW, Direction.SE };
             bool odd = (clickedCell.y & 1) != 0;
             foreach (var dir in dirs)
             {
                 Vector3Int offset = PathfindingSystem.Instance.GetOffsetForDirection(dir, odd);
                 if (clickedCell + offset == currentCell) { isAdjacentOrSame = true; break; }
             }
        }

        if (isAdjacentOrSame)
        {
            selectedTargetCell = currentCell;
            currentPathCells = new List<Vector3Int> { currentCell };

            InteractionHandler.SetPendingInteraction(interactable, collider ?? (interactable != null ? interactable.GetTransform().GetComponent<Collider2D>() : null), desc);

            ShowPathPreview(currentPathCells);

            InteractionHintUI.Instance?.HideAll();

            if (interactable != null && desc != null)
                InteractionHintUI.Instance?.ShowBothAt(targetTr, interactable.GetInteractLabel());
            else if (interactable != null)
                InteractionHintUI.Instance?.ShowSurveyAt(targetTr, interactable.GetInteractLabel());
            else if (desc != null)
                InteractionHintUI.Instance?.ShowBothAt(targetTr);
            else
                InteractionHintUI.Instance?.ShowSurveyAt(targetTr);

            InteractionHintUI.Instance?.ShowCancelAt(targetTr);
            return;
        }

        var newPath = PathfindingSystem.Instance.FindPathToAdjacentCell(currentCell, clickedCell);

        if (newPath == null || newPath.Count < 2)
        {
            ClearAllSelection();
            return;
        }

        selectedTargetCell = newPath[newPath.Count - 1];
        currentPathCells = newPath;

        InteractionHandler.SetPendingInteraction(interactable, collider ?? (interactable != null ? interactable.GetTransform().GetComponent<Collider2D>() : null), desc);

        ShowPathPreview(newPath);

        if (interactable != null && desc != null)
            InteractionHintUI.Instance?.ShowBothAt(targetTr, interactable.GetInteractLabel());
        else if (interactable != null)
            InteractionHintUI.Instance?.ShowSurveyAt(targetTr, interactable.GetInteractLabel());
        else if (desc != null)
            InteractionHintUI.Instance?.ShowBothAt(targetTr);
        else
            InteractionHintUI.Instance?.ShowSurveyAt(targetTr);

        InteractionHintUI.Instance?.ShowCancelAt(targetTr);
    }

    public void ProcessMoveClick(Vector3Int clickedCell)
    {
        if (HandleGlobalClickBlocking()) return;
        if (isMovingByPath) return;

        if (selectedTargetCell.HasValue
            && selectedTargetCell.Value == clickedCell
            && currentPathCells != null
            && currentPathCells.Count >= 2)
        {
            StartPathMove(currentPathCells, pathArrivalCallback);
            return;
        }

        Vector3Int currentCell = PathfindingSystem.Instance.GetCellFromWorldPos(rb.position);
        currentCell.z = 0;

        var newPath = PathfindingSystem.Instance.FindPath(currentCell, clickedCell);

        if (newPath == null || newPath.Count <= 1)
        {
             ClearAllSelection();
             return;
        }

        selectedTargetCell = clickedCell;
        currentPathCells = newPath;

        InteractionHandler.ClearInteractTargets(); // 기존 상호작용 타겟 초기화

        ShowPathPreview(newPath);
        InteractionHintUI.Instance?.HideAll();
    }

    private void ClearAllSelection()
    {
        selectedTargetCell = null;
        currentPathCells.Clear();
        ClearPathPreview();
        pathArrivalCallback = null;
        InteractionHandler.ClearInteractTargets();
        InteractionHintUI.Instance?.HideAll();
    }

    //HintUi 공통 취소 메서드
    public void CancelSelectionAndHint()
    {
        selectedTargetCell = null;
        currentPathCells.Clear();
        ClearPathPreview();
        pathArrivalCallback = null;
        InteractionHandler.ClearInteractTargets();
        InteractionHintUI.Instance?.HideAll();
    }


    // 외부에서 잠금 요청
    public void LockMovementFor(float seconds)
    {
        movementLockUntil = Mathf.Max(movementLockUntil, Time.time + Mathf.Max(0f, seconds));

        // 경로 이동 중에도 코루틴을 멈추지 말고 입력을 잠금
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


    // 화면에서 경로 표시 제거
    public void ClearPathPreview()
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
        if (cells == null || cells.Count < 2) return;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];

            // 해당 타일의 소속 타일맵을 찾음 (높이 정보 포함)
            Tilemap targetMap = PathfindingSystem.Instance.GetWalkableMapAt(cell);
            if (targetMap == null) targetMap = floorTilemap;

            // 해당 타일맵 기준으로 월드 좌표를 가져옴 (Anchor 값 자동 반영)
            Vector3 world = targetMap.GetCellCenterWorld(cell);

            // Z축 정렬
            world.z = 0;

            bool isGoal = (i == cells.Count - 1);

            GameObject prefabToUse = isGoal && goalMarkerPrefab != null
                ? goalMarkerPrefab
                : pathMarkerPrefab;

            if (prefabToUse == null) continue;

            var marker = Instantiate(prefabToUse, world, Quaternion.identity);

            // 마커가 타일에 묻히지 않게 Sorting Order 적절 조절
            var mapRenderer = targetMap.GetComponent<TilemapRenderer>();
            var markerRenderer = marker.GetComponent<SpriteRenderer>();

            if (mapRenderer != null && markerRenderer != null)
            {
                // 타일맵과 같은 Sorting Layer를 쓰고,
                markerRenderer.sortingLayerID = mapRenderer.sortingLayerID;

                // 그 중에서 타일맵보다 1 높게 설정 (무조건 타일 위에 그려지도록)
                markerRenderer.sortingOrder = mapRenderer.sortingOrder + 1;
            }

            activePathMarkers.Add(marker);
        }

        // 목표 지점에 소모 기력 표시
        var vigor = VigorManager.Instance;
        if (vigor != null && cells.Count > 0)
        {
            int steps = Mathf.Max(0, cells.Count - 1);
            int cost = steps * Mathf.Max(0, vigor.costMovePerTile);

            Vector3Int goalCell = cells[cells.Count - 1];
            Tilemap goalMap = PathfindingSystem.Instance.GetWalkableMapAt(goalCell);
            if (goalMap == null) goalMap = floorTilemap;

            Vector3 goalWorld = goalMap.GetCellCenterWorld(goalCell);
            goalWorld.z = 0;

            if (_pathCostLabelGO == null)
            {
                _pathCostLabelGO = new GameObject("PathCostLabel_TMP");
                _pathCostLabelGO.transform.localScale = Vector3.one * pathCostLabelScale;

                _pathCostTMP = _pathCostLabelGO.AddComponent<TextMeshPro>();
                _pathCostTMP.text = $"-{cost}";
                _pathCostTMP.alignment = TextAlignmentOptions.Center;
                _pathCostTMP.fontSize = 30;                // 가독성과 함께 튜닝
                _pathCostTMP.enableWordWrapping = false;

                // 목표 지점 맵의 Order + 2 정도로 설정
                var renderer = goalMap.GetComponent<TilemapRenderer>();
                int baseOrder = renderer != null ? renderer.sortingOrder : 0;
                _pathCostTMP.sortingOrder = baseOrder + 2; // 텍스트는 확실하게 위로
                _pathCostTMP.outlineWidth = 0.2f;          // 가시성
                _pathCostTMP.color = (cost <= vigor.CurrentVigor) ? Color.white : Color.red;
            }

            _pathCostLabelGO.transform.position = goalWorld;
            _pathCostTMP.text = $"-{cost}";
        }
    }

    // --- 타일 기반 최소 경로 탐색 (BFS) ---
    // (PathfindingSystem으로 이관됨)

    //인카운터 몬스터 유무 체크
    bool TryGetEncounterAtCell(Vector3Int cell, out EncounterMonster monster)
    {
        return InteractionHandler.TryGetEncounterAtCell(cell, out monster);
    }

    //함정 발동 체크
    void TryTriggerTrapAtCell(Vector3Int cell)
    {
        InteractionHandler.TryTriggerTrapAtCell(cell);
    }

    void TryConsumeTrapByBoxAtCell(Vector3Int cell)
    {
        InteractionHandler.TryConsumeTrapByBoxAtCell(cell);
    }

    // 경로를 따라 실제 이동
    void StartPathMove(List<Vector3Int> cells, Action onArrive = null, int? overrideVigorCost = null)
    {
        if (cells == null || cells.Count < 2) return; // 제자리이거나 잘못된 경로



        // 이동 비용 계산

        _pendingMoveVigorCost = 0;

        var vigor = VigorManager.Instance;



        if (overrideVigorCost.HasValue)

        {

            // 복귀 시 이어지는 이동: "지정 비용"을 그대로 이어받는다

            _pendingMoveVigorCost = Mathf.Max(0, overrideVigorCost.Value);

        }

        else if (vigor != null)

        {

            int steps = Mathf.Max(0, cells.Count - 1);

            int cost = steps * Mathf.Max(0, vigor.costMovePerTile);



            // 비용 차감 가능 여부 확인

            if (cost > 0 && !vigor.CanSpend(cost))

            {

                ExplorationLogUI.Instance?.Push($"기력이 부족합니다. 이동 필요: {cost}, 현재: {vigor.CurrentVigor}");

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



        // 프리뷰는 이동 시작 즉시 지움

        ClearPathPreview();



        var moveCells = new List<Vector3Int>(cells);   // 복사해서 사용

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



    // 플레이어(또는 특정 월드 좌표)가 밟고 있는 타일의 정확한 셀 좌표 구하기

    // public Vector3Int GetCellFromWorldPos(Vector3 worldPos) ... PathfindingSystem 사용

    public IEnumerator Co_MoveAlongPath(List<Vector3Int> cells)

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

            // 시작 점(현재 위치) 빼고 1번째 인덱스부터 끝까지 순서대로 이동

            for (int i = 1; i < cells.Count; i++)

            {

                // 이전 셀(출발)과 현재 셀(도착)의 층수(Index) 구하기

                Vector3Int startCell = cells[i - 1];

                Vector3Int endCell = cells[i];



                // 각 타일이 소속된 맵을 찾아서 실제 월드 좌표를 가져옴
                Tilemap startMap = PathfindingSystem.Instance.GetWalkableMapAt(startCell);
                Tilemap endMap = PathfindingSystem.Instance.GetWalkableMapAt(endCell);



                // 맵을 못 찾으면 기본 맵 사용 (방어 코드)

                Vector3 startPos = (startMap != null ? startMap : floorTilemap).GetCellCenterWorld(startCell);

                Vector3 endPos = (endMap != null ? endMap : floorTilemap).GetCellCenterWorld(endCell);



                // Z축 고정

                startPos.z = transform.position.z;

                endPos.z = transform.position.z;



                // 타일맵의 고유 높이(Anchor.y) 차이가 있을 때만 점프

                float startHeight = (startMap != null) ? startMap.tileAnchor.y : 0f;

                float endHeight = (endMap != null) ? endMap.tileAnchor.y : 0f;



                // 높이 차이 계산

                float heightDiff = Mathf.Abs(endHeight - startHeight);



                // 높이 차이가 미세하게라도 있으면 점프 (0.001f 오차 허용)

                bool isJump = heightDiff > 0.001f;



                // 방향 벡터 및 거리 계산 (2D 평면 거리 기준)

                float dist = Vector2.Distance(startPos, endPos);

                if (dist < 0.0001f) continue;



                Vector2 dir = (endPos - startPos).normalized;

                float speedMultiplier = ExplorationStatusManager.Instance != null ? ExplorationStatusManager.Instance.GetMoveSpeedMultiplier() : 1f;
                float speed = Mathf.Max(0.01f, defaultMoveSpeed * speedMultiplier);

                float duration = dist / speed;

                float t = 0f;



                // 스프라이트 방향

                if (Mathf.Abs(dir.x) > 0.0001f) spriterenderer.flipX = dir.x > 0f;

                if (animator != null) animator.SetInteger("Move", 1);



                // 점프 높이 누적 계산

                float currentJumpMultiplier = jumpHeightMultiplier;



                if (isJump)

                {

                    // 1칸 차이 vs 2칸 차이 구분

                    // 0.18(대략 1.5층)보다 크면 2단 점프로 간주하여 높이를 키움

                    if (heightDiff > 0.26f)

                    {

                        currentJumpMultiplier *= 1.5f; // 2칸일 때 1.6배 높게 점프 (취향껏 조절)

                    }



                    // 내려가는 점프는 살짝 낮춤 (기존 로직 유지)

                    if (endHeight < startHeight)

                    {

                        currentJumpMultiplier *= 0.6f;

                    }

                }



                while (t < 1f)

                {

                    if (GamePause.IsPaused || Time.time < movementLockUntil)

                    {

                        if (animator != null) animator.SetInteger("Move", 0);

                        yield return null;

                        continue;

                    }



                    t += Time.deltaTime / duration;

                    float percent = Mathf.Clamp01(t);



                    // 기본 선형 이동 (높이 차이가 있으면 대각선으로 이동)

                    Vector3 currentPos = Vector3.Lerp(startPos, endPos, percent);



                    // 점프 연출 (Y축 차이가 있을 때만)

                    if (isJump && jumpCurve != null)

                    {

                        float curveValue = jumpCurve.Evaluate(percent);

                        currentPos.y += curveValue * currentJumpMultiplier;

                    }



                    rb.MovePosition(currentPos);

                    yield return null;

                }



                rb.MovePosition(endPos); // 최종 위치 보정



                // 도착 후 함정 체크

                TryTriggerTrapAtCell(cells[i]);

                // 타일 도착 이벤트 발생 (HiddenPortalController 등이 구독)
                OnTileStepped?.Invoke(cells[i]);


                // 도착 지점에서 인카운터 체크

                if (TryGetEncounterAtCell(cells[i], out var monster))

                {

                    // 남은 경로 구성: 현재(몬스터 만남)부터 끝까지

                    var remaining = new List<Vector3Int>();

                    for (int k = i; k < cells.Count; k++)

                        remaining.Add(cells[k]);



                    if (animator != null) animator.SetInteger("Move", 0);



                    var stm = SceneTransitionManager.Instance;

                    if (stm != null && VigorManager.Instance != null)

                    {

                        stm.SetDeferredMoveCost(_pendingMoveVigorCost);

                        stm.SetResumePath(remaining);



                        Tilemap encounterMap = PathfindingSystem.Instance.GetWalkableMapAt(cells[i]);

                        if (encounterMap == null) encounterMap = floorTilemap;



                        Vector3 returnPos = encounterMap.GetCellCenterWorld(cells[i]);

                        // Z축을 0으로 맞추거나 필요 시 transform.position.z 사용

                        returnPos.z = 0;



                        stm.SaveReturnPoint(SceneName.ExplorationScene, returnPos);



                        // 중요: 몬스터를 먼저 소비 처리하고 스냅샷 찍음

                        monster.MarkConsumed();



                        // 몬스터가 비활성화된 상태로 스냅샷 저장

                        stm.SaveExplorationSnapshot(stm.BuildExplorationSnapshotFromScene());

                        stm.SaveVigor(VigorManager.Instance.CurrentVigor);



                        string monsterName = monster != null ? monster.gameObject.name : "몬스터";

                        stm.EnterBattleWithEncounterBanner(monsterName, battleSceneName);



                        yield break;

                    }



                    _pendingMoveVigorCost = 0;

                    yield break;

                }

            }



            // 이동 비용 결제(도착까지 완료된 경우에만)

            if (VigorManager.Instance != null && _pendingMoveVigorCost > 0)

            {

                if (!VigorManager.Instance.TrySpend(_pendingMoveVigorCost, VigorSpendReason.MoveTile))

                {

                    VigorManager.Instance.FailExploration(

                        $"탐색에 실패했습니다. (이동 결제 실패 / 필요 {_pendingMoveVigorCost}, 현재 {VigorManager.Instance.CurrentVigor})"

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



            // 이동 후 상호작용 예약 초기화

            pathArrivalCallback = null;

            InteractionHandler.ClearInteractTargets();

        }

    }



    // 탐험에서 복귀 후 이어지는 경로 이동

    public void ResumePathAfterBattle(List<Vector3Int> resumeCells)

    {

        if (resumeCells == null || resumeCells.Count < 2) return;

        if (isMovingByPath) return;



        CancelSelectionAndHint();



        // 전투 전에 예약해 둔(유예된) 이동 비용을 이어받는다

        int plannedCost = 0;

        var stm = SceneTransitionManager.Instance;

        if (stm != null) plannedCost = stm.ConsumeDeferredMoveCost();



        StartPathMove(resumeCells, null, plannedCost);

    }



    // === Hint UI 버튼 콜백 제거 (상단으로 이관됨) ===







    public void OnClickCancelButton()

    {

        // 경로/상호작용 예약 취소 (이동 중이 아닐 때)

        if (!isMovingByPath)

            CancelSelectionAndHint();

        if (PushHandler.IsPushMode)
            PushHandler.HaltPushImmediately();

    }

    public void TeleportTo(Vector3 worldPos)

    {

        rb.position = worldPos;

        transform.position = worldPos;

        ClearPath();

        // 텔레포트 시 카메라가 부드럽게 따라오지 않고 즉시 스냅하도록 처리 (페이드 인 시 화면 팝핑 방지)
        UnityEngine.Object.FindAnyObjectByType<CameraFollow2D>()?.SnapToTarget();

    }



    public void ClearPath()

    {

        // 기존 마우스/경로 리스트 정리

        path.Clear();



        // 현재 경로 이동 상태 정리

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

        InteractionHandler.ClearInteractTargets();

    }









    public void HaltImmediately()

    {

        // Push 상태 강제 정리 (안전하게)
        PushHandler?.HaltPushImmediately();



        // 현재 경로 이동 즉시 중단

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

        InteractionHandler.ClearInteractTargets();



        // 기존 마우스 이동/예약 입력 등 모두 해제

        path.Clear();



        if (animator != null)

            animator.SetInteger("Move", 0);

    }

}
