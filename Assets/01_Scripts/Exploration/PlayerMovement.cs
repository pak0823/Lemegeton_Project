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



    private Rigidbody2D rb;

    private List<Vector3> path = new();

    float movementLockUntil = 0f;

    int _hardLockTokens = 0; // 무기한 잠금 토큰



    public bool isPushMode { private set; get; }

    private Direction pendingDirectionKey = Direction.None;

    private bool isPerformingPush = false;





    readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];



    // 자동경로 기반 이동 상태

    [Header("자동경로 이동 설정")]

    [SerializeField] private GameObject pathMarkerPrefab;   // 경로 표시용 프리팹

    [SerializeField] private GameObject pushMarkerPrefab; // Push 후보 타일 표시용

    [SerializeField] private GameObject goalMarkerPrefab; // 목표 지점 표시용 마커(최종 도착)



    // 현재 선택된 경로(셀 단위)

    private List<Vector3Int> currentPathCells = new List<Vector3Int>();

    // 현재 선택된 목표 셀 (첫 번째 클릭으로 선택된 상태)

    private Vector3Int? selectedTargetCell = null;

    // 경로를 따라 실제 이동 중인지 여부

    private bool isMovingByPath = false;

    // 경로 이동 코루틴 핸들

    private Coroutine pathMoveRoutine = null;

    // 화면에 찍힌 경로 마커들

    private readonly List<GameObject> activePathMarkers = new List<GameObject>();



    // 경로 도착 후 실행할 콜백 (예: 상자 열기)

    private Action pathArrivalCallback = null;



    // 상호작용 의도로 선택된 상자(또는 다른 것)

    private IInteractable pendingInteractable = null;

    private PortalController pendingPortal = null;



    // 현재 상호작용 타겟(관찰/조사 버튼이 가리키는 대상)

    private Collider2D currentInteractTarget = null;

    private DescriptionData currentDescData = null;



    private int _pendingMoveVigorCost = 0;  //이동 비용



    // Push 선택(우클릭) 모드

    private bool isPushSelectMode = false;

    private PushObject pendingPushBox = null;



    // 박스가 이동해야 하는 목표 타일만 사용

    private HashSet<Vector3Int> pushValidTargetCells = new HashSet<Vector3Int>();



    // 임시 마커(기존 pathMarkerPrefab 복사해서 사용)

    private readonly List<GameObject> activePushMarkers = new List<GameObject>();



    [Header("Path Cost Label (TMP)")]

    [SerializeField] private float pathCostLabelScale = 0.05f;

    private TextMeshPro _pathCostTMP = null;

    private GameObject _pathCostLabelGO = null;



    [Header("점프 이동 설정")]

    public AnimationCurve jumpCurve;      // 점프 곡선

    public float jumpHeightMultiplier = 0.5f; // 점프 높이 배율



    [SerializeField] private LayerMask encounterLayerMask;

    [SerializeField] private string battleSceneName = "BattleScene"; // 현재 전투 씬 이름으로



    private SpriteRenderer spriterenderer;

    private Animator animator;



    void Awake()
    {
        if(Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject); // 중복 방지 강화

        rb = GetComponent<Rigidbody2D>();
        spriterenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

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

    private bool IsInputBlocked => (Time.time < movementLockUntil) || (_hardLockTokens > 0) || isPerformingPush || GamePause.IsPaused;

    // 공통적으로 클릭 시 다이얼로그가 열려있으면 닫고 입력을 소비함
    private bool HandleGlobalClickBlocking()
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

        if (isPushSelectMode)
        {
            ExitPushSelectMode();
            return;
        }

        if (pendingPushBox != null)
        {
            pendingPushBox.SetHighlight(false);
            pendingPushBox = null;
            InteractionHintUI.Instance?.HideAll();
            return;
        }
        
        // 일반 이동/상호작용 취소
        if (selectedTargetCell.HasValue || (currentPathCells != null && currentPathCells.Count > 0))
        {
            CancelSelectionAndHint();
        }
    }

    public void ProcessPushObjectClick(PushObject push)
    {
        if (HandleGlobalClickBlocking()) return;
        if (isMovingByPath) return;

        CancelSelectionAndHint(); // 기존 선택/경로 정리

        pendingPushBox = push;
        pendingPushBox.SetHighlight(true);

        // 밀기/취소 2버튼 표시
        InteractionHintUI.Instance?.ShowPushCancelAt(pendingPushBox.transform);
    }

    public void ProcessInteractionClick(Vector3Int clickedCell, Transform targetTr, IInteractable interactable, PortalController portal, Collider2D collider, DescriptionData desc)
    {
        if (HandleGlobalClickBlocking()) return;
        if (isMovingByPath) return;

        // 현재 타일과 같거나 인접 6방향 중 하나라면 "이동 없이 상호작용 가능"
        Vector3Int currentCell = PathfindingSystem.Instance.GetCellFromWorldPos(rb.position);
        currentCell.z = 0;
        
        bool isAdjacentOrSame = false;
        if (currentCell == clickedCell)
        {
            isAdjacentOrSame = true;
        }
        else
        {
             // 인접 체크 (PathfindingSystem 메서드 활용)
            var path = PathfindingSystem.Instance.FindPathToAdjacentCell(currentCell, clickedCell);
             // 인접하면 경로 길이가 2 (start, end)가 나옴. 
             // 다만 여기서는 'targetCell'이 'clickedCell'임.
             // 기존 로직: clickedCell이 targetCell임.
             // 인접 체크를 단순 거리나 오프셋으로 먼저 하고, 안되면 경로 탐색
             
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

            currentInteractTarget = collider ?? (interactable != null ? interactable.GetTransform().GetComponent<Collider2D>() : null);
            currentDescData = desc;
            pendingInteractable = interactable;
            pendingPortal = portal;

            ShowPathPreview(currentPathCells); 

            InteractionHintUI.Instance?.HideAll();

            if (portal != null)
                InteractionHintUI.Instance?.ShowSurveyAt(targetTr, portal.GetHintLabel());
            else
                InteractionHintUI.Instance?.ShowBothAt(targetTr);

            InteractionHintUI.Instance?.ShowCancelAt(targetTr);
            return;
        }

        // 인접하지 않으면 이동 경로 계산
        var newPath = PathfindingSystem.Instance.FindPathToAdjacentCell(currentCell, clickedCell);

        if (newPath == null || newPath.Count < 2)
        {
            ClearAllSelection();
            return;
        }

        selectedTargetCell = newPath[newPath.Count - 1];
        currentPathCells = newPath;

        currentInteractTarget = collider ?? (interactable != null ? interactable.GetTransform().GetComponent<Collider2D>() : null);
        currentDescData = desc;
        pendingInteractable = interactable;
        pendingPortal = portal;

        ShowPathPreview(newPath);

        InteractionHintUI.Instance?.ShowBothAt(targetTr);
        InteractionHintUI.Instance?.ShowCancel();
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
        
        pendingInteractable = null;
        pendingPortal = null;
        pathArrivalCallback = null;
        currentInteractTarget = null;
        currentDescData = null;

        ShowPathPreview(newPath);
        InteractionHintUI.Instance?.HideAll();
    }

    private void ClearAllSelection()
    {
        selectedTargetCell = null;
        currentPathCells.Clear();
        ClearPathPreview();
        pendingInteractable = null;
        pendingPortal = null;
        pathArrivalCallback = null;
        currentInteractTarget = null;
        currentDescData = null;
        InteractionHintUI.Instance?.HideAll();
    }

    //HintUi 공통 취소 메서드

    void CancelSelectionAndHint()

    {

        selectedTargetCell = null;

        currentPathCells.Clear();

        ClearPathPreview();

        pendingInteractable = null;

        pathArrivalCallback = null;

        currentInteractTarget = null;

        currentDescData = null;

        pendingPortal = null;

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

            var adj = boxCell + PathfindingSystem.Instance.GetOffsetForDirection(d, oddBox);

            if (!PathfindingSystem.Instance.IsWalkableCell(adj)) continue;



            // adj(플레이어 위치)에서 box를 밀 수 있는지 여부와 플레이어→박스 이동 방향을 dirKey로 추출

            var delta = boxCell - adj;

            bool oddAdj = (adj.y & 1) != 0;

            var dirKey = GetDirectionFromDelta(delta, oddAdj);

            if (dirKey == Direction.None) continue;



            // 이 자리에서 최소 1칸이라도 밀 수 있어야 push-ready임

            // (이 부분은 계산 함수인 아래 BuildPushLineTargets를 이용)

            var line = BuildPushLineTargets(box, boxCell, dirKey);

            if (line.Count == 0) continue;



            var path = PathfindingSystem.Instance.FindPath(playerCell, adj);

            if (path == null || path.Count < 2) continue;



            if (best == null || path.Count < best.Count) best = path;

        }



        return best;

    }



    //인카운터 몬스터 유무 체크

    bool TryGetEncounterAtCell(Vector3Int cell, out EncounterMonster monster)

    {

        monster = null;

        var world = floorTilemap.GetCellCenterWorld(cell);

        var hits = Physics2D.OverlapCircleAll(world, 0.4f, encounterLayerMask);



        // 오브젝트 개수 확인

        if (hits.Length == 0)

        {

            Debug.Log($"[Encounter] 해당 타일({cell}) 중심에서 반경 0.4f 내에 'Water' 레이어 감지 안됨. 위치: {world}");

            return false;

        }



        foreach (var h in hits)

        {

            if (!h) continue;

            var m = h.GetComponentInParent<EncounterMonster>();

            if (m != null)

            {

                if (!m.IsActive) Debug.Log($"[Encounter] 몬스터 감지됨({m.name}) 그러나 IsActive가 false임");

                else

                {

                    Debug.Log($"[Encounter] 몬스터 감지 성공! 전투 진입 시도.");

                    monster = m;

                    return true;

                }

                   

            }

        }

        return false;

    }

    //함정 발동 체크

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

            if (!trap.gameObject.activeInHierarchy) continue; // 비활성 함정 스킵(권장)



            trap.TryConsumeByBox(floorTilemap, cell);

        }

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



                        stm.SaveReturnPoint(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, returnPos);



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

            pendingInteractable = null;

            currentInteractTarget = null;

            currentDescData = null;

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



    // === Hint UI 버튼 콜백 ===

    public void OnClickSurveyButton()

    {

        DescriptionDialogUI.Instance?.Hide();

        InteractionHintUI.Instance?.HideAll();



        if (isMovingByPath)

            return;



        // Portal/NPC 이동 후 실행 또는 즉시 실행

        if (pendingPortal != null)

        {

            var portal = pendingPortal;



            // 이동 없이 즉시 실행(인접/상자 등)

            if (currentPathCells == null || currentPathCells.Count < 2)

            {

                if (portal != null) portal.UsePortal();



                ClearPath();

                pendingPortal = null;

                return;

            }



            // 이동 후 실행

            Action onArrive = () =>

            {

                if (portal != null) portal.UsePortal();

            };



            StartPathMove(currentPathCells, onArrive);



            // 예약 정리(한 번만)

            pendingPortal = null;

            return;

        }



        // Push 사용 분기

        if (pendingPushBox != null)

        {

            var box = pendingPushBox;

            var playerCell = floorTilemap.WorldToCell(rb.position);

            var boxCell = floorTilemap.WorldToCell(box.transform.position);



            // 이미 인접하면 즉시 진입

            if (IsAdjacentOrSame(playerCell, boxCell))

            {

                EnterPushSelectMode(box);

                return;

            }



            // 멀리 있으므로 인접한 '준비 위치'까지 이동 후 진입

            var pathToReady = FindPathToPushReadyCell(playerCell, boxCell, box);

            if (pathToReady == null || pathToReady.Count < 2)

            {

                ExplorationLogUI.Instance?.Push("해당 상자를 밀 수 있는 위치로 이동할 수 없습니다.");

                box.SetHighlight(false);

                pendingPushBox = null;

                InteractionHintUI.Instance?.HideAll();

                return;

            }



            // 이동 시작 즉시 UI 닫기

            InteractionHintUI.Instance?.HideAll();



            // 도착 후 밀기모드 진입

            Action onArrive = () =>

            {

                // 도착 시점에 박스가 아직 존재/유효한지 확인

                if (box == null) return;



                // 도착 후에는 인접한 것이므로, 이때 EnterPushSelectMode가 타일 정보까지 표시(2차 보정)

                EnterPushSelectMode(box);

            };



            StartPathMove(pathToReady, onArrive);

            return;

        }

        // 이동 없이 즉시 실행해야 하는 경우 (사거리 내)

        if (currentPathCells == null || currentPathCells.Count < 2)

        {

            if (pendingInteractable != null)

            {

                pendingInteractable.OnInteract();

                InteractionHintUI.Instance?.HideAll();

            }

            // 나중에 조사 기능이 추가되면 여기에 else-if로 추가



            // 상태 정리

            ClearPath();

            return;

        }



        // 여기부터는 "이동 후 실행" 케이스

        if (currentPathCells != null && currentPathCells.Count >= 2)

        {

            Action onArrive = null;

            if (pendingInteractable != null)

                onArrive = () => pendingInteractable.OnInteract();



            StartPathMove(currentPathCells, onArrive);

        }

    }

    public void OnClickCommunicationButton()

    {

        if (isMovingByPath)

            return;



        // 사거리 내면 이동 없이 즉시 관찰

        if (currentPathCells == null || currentPathCells.Count < 2)

        {

            if (currentDescData != null && !string.IsNullOrWhiteSpace(currentDescData.description))

            {

                DescriptionDialogUI.Instance?.Toggle(currentDescData.description);

                InteractionHintUI.Instance?.HideAll();

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

                    DescriptionDialogUI.Instance?.Toggle(currentDescData.description);

                    InteractionHintUI.Instance?.HideAll();

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



        // 플레이어 위치/박스 위치로 "밀 수 있는 방향" 1차 계산(플레이어 인접 기준)

        var playerCell = floorTilemap.WorldToCell(rb.position);

        var boxCell = floorTilemap.WorldToCell(box.transform.position);



        var delta = boxCell - playerCell;

        bool odd = (playerCell.y & 1) != 0;

        pendingDirectionKey = GetDirectionFromDelta(delta, odd);



        pushValidTargetCells.Clear();



        // 현재 플레이어가 보고 있는 방향으로 밀 수 있는 후보 생성

        var startBoxCell = floorTilemap.WorldToCell(box.transform.position);

        var line = BuildPushLineTargets(box, startBoxCell, pendingDirectionKey);



        for (int i = 0; i < line.Count; i++)

            pushValidTargetCells.Add(line[i]);



        if (pushValidTargetCells.Count == 0)

        {

            ExplorationLogUI.Instance?.Push("이 위치에서는 밀 수 없습니다.");

            ExitPushSelectMode(keepBoxHighlight: false);

            return;

        }



        ShowPushTargets(pushValidTargetCells);

    }



    // 연속적으로 목적지까지 밀기

    List<Vector3Int> BuildPushLineTargets(PushObject box, Vector3Int startBoxCell, Direction dirKey)

    {

        var results = new List<Vector3Int>();



        // [체크] box에 MainFloorMap이 없으면 계산 불가

        if (box == null || box.MainFloorMap == null) return results;

        if (dirKey == Direction.None) return results;



        // 다른 PushObject 점유 체크(가장 간단한 충돌 체크)

        var occupied = new HashSet<Vector3Int>();

        foreach (var po in FindObjectsOfType<PushObject>())

        {

            if (po == null || po == box) continue;



            // [수정] box.floorTilemap은 리스트이므로 WorldToCell을 바로 쓸 수 없음 -> MainFloorMap 사용

            if (box.MainFloorMap != null)

            {

                occupied.Add(box.MainFloorMap.WorldToCell(po.transform.position));

            }

        }



        var cur = startBoxCell;



        while (true)

        {

            bool odd = (cur.y & 1) != 0;

            var offset = PathfindingSystem.Instance.GetOffsetForDirection(dirKey, odd);

            var next = cur + offset;



            // 바닥 확인 메서드 사용

            bool hasFloor = box.HasFloorAt(next);



            bool hasWall = false;

            if (PathfindingSystem.Instance.wallMaps != null)

            {

                foreach (var wall in PathfindingSystem.Instance.wallMaps)

                {

                    if (wall.HasTile(next))

                    {

                        hasWall = true;

                        break;

                    }

                }

            }



            if (!hasFloor || hasWall) break;



            // 장애물 레이어(기둥 등 용도)

            var world = box.MainFloorMap.GetCellCenterWorld(next);

            var obstacle = Physics2D.OverlapCircle(world, 0.1f, box.obstacleLayer);

            if (obstacle != null) break;



            // 다른 PushObject 점유

            if (occupied.Contains(next)) break;



            // 여기까지 통과하면 next는 밀기 가능한 목적지임

            results.Add(next);



            // 다음 반복을 위해 박스 위치를 next로 가정

            cur = next;

        }



        return results;

    }



    public void OnClickCancelButton()

    {

        // 경로/상호작용 예약 취소 (이동 중이 아닐 때)

        if (!isMovingByPath)

            CancelSelectionAndHint();

        if (isPushSelectMode) 

            ExitPushSelectMode();

        else if (pendingPushBox != null)    // 임시 선택 상태 정리

        {

            pendingPushBox.SetHighlight(false);

            pendingPushBox = null;

            InteractionHintUI.Instance?.HideAll();

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

            if (PathfindingSystem.Instance.GetOffsetForDirection(dir, odd) == delta)

                return dir;

        }

        return Direction.None;

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

        pendingInteractable = null;

        currentInteractTarget = null;

        currentDescData = null;

    }







    // 밀기 상자와의 인접 판정

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

            var off = PathfindingSystem.Instance.GetOffsetForDirection(dir, odd);

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



        // 상자가 최종적으로 도착한 곳

        var boxArrivedCell = fromCell + dir;



        // 상자에 의해 함정 제거

        TryConsumeTrapByBoxAtCell(boxArrivedCell);



        // 퍼즐 박스 위치 갱신 및 목표 체크 호출

        // PuzzleManager removed
        // PuzzleManager.Instance?.ExecutePush(box, fromCell, fromCell + dir);

    }



    // 연속 밀기 기능

    // PerformPushToTarget에서 연속으로 밀 수 있는지 확인 후 이동은 PerformPush로 진행함

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

                    yield break; // 더 이상 불가(중간에 막힘)



                // 다음 1칸 목적지(라인의 첫번째)

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



            if (reachedTarget && pushedTiles > 0 && VigorManager.Instance != null)

            {

                int cost = pushedTiles * VigorManager.Instance.costPushBoxPerTile;

                if (cost > 0)

                    VigorManager.Instance.TrySpend(cost, VigorSpendReason.PushBox);

            }

        }

    }

    public bool IsPushSelectMode => isPushSelectMode;

    public void ExitPushSelectMode(bool keepBoxHighlight = false)
    {
        isPushSelectMode = false;
        isPushMode = false;
        
        if (animator != null)
            animator.SetBool("IsPushIdle", false);

        ClearPushTargets();
        pushValidTargetCells.Clear();

        if (pendingPushBox != null)
        {
            if (!keepBoxHighlight)
            {
                pendingPushBox.SetHighlight(false);
                pendingPushBox = null;
            }
        }
        
        InteractionHintUI.Instance?.HideAll();
    }

    public void ProcessPushTargetClick(Vector3Int clickedCell)
    {
        if (HandleGlobalClickBlocking()) return;
        
        if (!isPushSelectMode) return;
        
        if (!pushValidTargetCells.Contains(clickedCell)) return;

        StartPushToCell(pendingPushBox, clickedCell);
        
        ExitPushSelectMode(keepBoxHighlight: false);
    }

    void StartPushToCell(PushObject box, Vector3Int targetCell)
    {
        Vector3Int currentCell = PathfindingSystem.Instance.GetCellFromWorldPos(rb.position);
        currentCell.z = 0;

        var boxCell = floorTilemap.WorldToCell(box.transform.position);

        // Find best path to a "push ready" cell
        var pathToReady = FindPathToPushReadyCell(currentCell, boxCell, box);

        if (pathToReady != null)
        {
            if (pathMoveRoutine != null) StopCoroutine(pathMoveRoutine);
            pathMoveRoutine = StartCoroutine(Co_MoveToPushReadyAndPush(pathToReady, box, targetCell));
        }
    }

    IEnumerator Co_MoveToPushReadyAndPush(List<Vector3Int> pathToReady, PushObject box, Vector3Int targetCell)
    {
        isMovingByPath = true;
        isPushMode = true;

        // 1. 준비 지점까지 이동
        if (pathToReady.Count > 1) // 이미 인접해있지 않다면
        {
            // ready position은 path의 마지막 지점
            // Co_MoveAlongPath는 0번째(현재위치) 제외하고 1번째부터 이동함.
            // pathToReady[0] == currentCell.
            yield return StartCoroutine(Co_MoveAlongPath(pathToReady));
        }

        // 2. 밀기 방향 결정
        // 플레이어는 이제 pathToReady의 마지막 지점에 있음
        Vector3Int readyCell = pathToReady[pathToReady.Count - 1];
        Vector3Int boxPos = floorTilemap.WorldToCell(box.transform.position);

        Vector3Int dirVec = boxPos - readyCell;
        bool odd = (readyCell.y & 1) != 0;
        Direction pushDir = GetDirectionFromDelta(dirVec, odd);

        if (pushDir != Direction.None)
        {
            yield return StartCoroutine(PerformPushToTarget(box, pushDir, targetCell));
        }

        isMovingByPath = false;
        isPushMode = false;
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

        // Push 상태 강제 정리 (안전하게)

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

        pendingInteractable = null;

        currentInteractTarget = null;

        currentDescData = null;



        // 기존 마우스 이동/예약 입력 등 모두 해제

        path.Clear();



        if (animator != null)

            animator.SetInteger("Move", 0);

    }

}




