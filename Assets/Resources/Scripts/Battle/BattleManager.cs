using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum BattleState { Idle, ActionSelect, Moving, Targeting, Resolving, EndTurn }
public enum BattleAction { Move, Attack }

public class BattleManager : MonoBehaviour
{
    #region Variables
    public BattleGridManager grid;
    public TurnOrderManager turn;
    UnitStatusPanelUI _statusPanel;
    public LayerMask unitMask;

    BattleState state = BattleState.Idle;
    bool atbPaused = false; // 턴 중 ATB 충전 멈춤
    BattleUnit acting;
    List<Vector3Int> moveOptions = new();

    // === 추가턴 설계 ===
    [SerializeField] int baseActionsPerTurn = 1; // 기본 행동 토큰(기본 1)
    int remainingActions = 0; // 남은 토큰
    readonly HashSet<BattleAction> usedActions = new(); // 이번 턴에 사용한 행동(중복 금지)

    [Header("Highlighters")]
    public Highlighter moveHighlighter;   // 이동 미리보기
    public Highlighter skillHighlighter;    // 스킬 범위 미리보기
    IBattleMapProvider provider;
    int _skillPreviewHold = 0;            // 웹 캐스팅 등 스킬 프리뷰 유지용

    bool initialized = false; // 중복 Init 방지
    public bool IsPlayerTurn => acting != null && acting.team == Team.Player;
    public bool IsTargeting => state == BattleState.Targeting;
    public BattleUnit SelectedTarget => selectedTarget;
    Coroutine enemyRoutine; // 코루틴 핸들

    [Header("Waves")]
    [SerializeField] WaveSet waveSet;   // 수동 연결 시 우선
    [SerializeField] StageDatabase stageDB;              // 인스펙터에 연결 or Resources 로드
    [SerializeField] bool autoAssignWaveSet = true;      // 자동할당 토글
    [SerializeField] BattleContext debugContext = BattleContext.TrapEncounter; // 에디터 테스트용
    [SerializeField] int currentWaveIndex = -1;
    [SerializeField] int debugStageNumber = -1;          // 에디터 테스트용
    [SerializeField] private Transform enemyRoot;
    public int CurrentWave => currentWaveIndex + 1; //현재 웨이브
    public int TotalWaves => waveSet ? waveSet.waves.Count : 0; //총 웨이브
    private GameObject _spawnedEnemyLayout;
    bool isWaveTransitioning = false;
    public event System.Action<int, int, string> OnWaveChanged; // (cur,total,label)
    public event System.Action OnWaveStarted;   // 웨이브 시작 알림
    public event System.Action<int, int> OnWaveTransition; // 다음 웨이브 전환 안내 (next,total)
    [SerializeField] float waveTransitionDelay = 1.5f;    // 전환 안내 표시 시간(초, 실시간)


    private bool _battleEndedOnce = false; // 중복 승리 처리 방지


    // === 타겟 선택(표시/순환) ===
    [Header("Targeting")]
    public TargetMarker targetMarker; // 인스펙터에 배치한 TargetMarker 할당
    List<BattleUnit> targetCycle = new(); // 적 리스트(AGI desc)
    int targetIndex = -1; // 현재 인덱스
    BattleUnit selectedTarget; // 현재 선택된 대상
    private Tilemap currentSkillTargetMap;  //스킬이 지정한 맵
    public Tilemap CurrentSkillTargetMap => currentSkillTargetMap;

    // === 수동 종료 감지용 ===
    bool manualEndRequested = false;

    // ATB UI 업데이트용 이벤트
    public delegate void OnATBChangedDelegate(BattleUnit unit, float currentATB, float maxATB);
    public event OnATBChangedDelegate OnATBChanged;
    readonly System.Random rng = new System.Random();// 소난수 발생기
    public static event System.Action<BattleUnit> OnAnyUnitTurnStarted;

    // === AGI 변화 감지용 ===
    float _lastMinAGI, _lastMaxAGI, _lastAGISum;
    int _lastAGICount;
    const float AGI_EPS = 0.0001f;
    public event System.Action OnATBReset; // 턴바 강제 초기화 신호

    [Header("Skill Runtime")]
    public bool isSelectingSkill = false;          // 스킬 선택 패널이 열렸는지
    public SkillDefinition currentSkill;           // 현재 선택된 스킬(선택 전이면 id 미정)
    public Vector3Int selectedCell;                // 타일 스킬용 내부 커서
    public SkillAsset currentSkillSO;                   // 현재 선택된 SO 스킬
    public event System.Action<SkillAsset[]> OnSkillPanelPopulateSO; // SO 목록 UI용

    // UI와 통신용 이벤트
    public event System.Action<bool> OnSkillPanelToggled;  // true=열기/false=닫기
    public event System.Action<string> OnHint;   // UI에 간단한 안내 문구 전달

    [Header("Projectile/VFX")]
    public GameObject projectilePrefab;     // 투사체

    //점프 애니메이션 속도 및 높이 값
    [SerializeField] float jumpDuration = 0.08f;     // 시간 기반
    [SerializeField] float jumpArc = 0.15f;

    public static void ClearStatic()
    {
        OnAnyUnitTurnStarted = null;
    }

    //유닛 스킬 표시용
    public event System.Action<BattleUnit, string> OnUnitActionLabel; // (유닛, 라벨)
    public void EmitActionLabel(BattleUnit u, string label) => OnUnitActionLabel?.Invoke(u, label);
    #endregion

    UnitStatusPanelUI StatusPanel
    {
        get
        {
            if (_statusPanel == null) _statusPanel = FindObjectOfType<UnitStatusPanelUI>();
            return _statusPanel;
        }
    }

    #region Unity Callbacks
    void Awake()
    {
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
        if (provider != null) provider.OnMapsReady += Init;
        else { Debug.LogWarning("[BattleManager] BattleMapManager not ready in Awake. Will retry in Start."); }
        if (Shared.BattleManager == null) Shared.BattleManager = this;

        if (autoAssignWaveSet && waveSet == null)
        {
            AutoResolveWaveSet();
        }
    }

    void Start()
    {
        if (provider == null)
        {
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
            if (provider != null) provider.OnMapsReady += Init;
        }

        if (!initialized)
        {
            if (waveSet != null && waveSet.waves != null && waveSet.waves.Count > 0)
            {
                Init();
            }
            else if (provider != null && provider.PlayerFloor != null && provider.EnemyFloor != null)
            {
                Init();
            }
        }

        StartCoroutine(Co_RebindBattleInputWhenMapsReady());
    }

    void OnDisable()
    {
        if (provider != null) provider.OnMapsReady -= Init;
    }
    #endregion

    #region Initialization

    void Init() //초기 세팅
    {

        if (!waveSet || waveSet.waves == null || waveSet.waves.Count == 0)  //웨이브 없을 시 일반 초기화
        {
            RebindAllUnitsAndInitATB();
        }
        else //웨이브 모드일 땐 여기서 바로 0번 웨이브 로드
        {
            LoadWave(0);
        }
    }
    // BattleMapManager(IBattleMapProvider)가 실제로 Floor들을 채운 '이후' 1회만 BattleInput에 통지
    System.Collections.IEnumerator Co_RebindBattleInputWhenMapsReady()
    {
        var provider = Shared.battleMapManager as IBattleMapProvider;
        // provider가 아직 안 잡힌 프레임도 있을 수 있으니 안전 폴링
        while (provider == null)
        {
            yield return null;
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>(true) as IBattleMapProvider;
        }
        // Floor들이 실제로 세팅될 때까지 대기
        while (provider.PlayerFloor == null || provider.EnemyFloor == null)
            yield return null;

        // 맵 준비 완료 → BattleInput에 단 한 번 rebind 지시
        if (Shared.battleInput != null)
            Shared.battleInput.RebindProviders();
    }

    // 씬 내 모든 BattleUnit(플레이어+적)을 재바인드하고 ATB/구독을 초기화
    private void RebindAllUnitsAndInitATB()
    {
        var units = FindObjectsOfType<BattleUnit>(true).ToList();

        float minAGI = units.Min(u => u.EffectiveAGI);
        float maxAGI = units.Max(u => u.EffectiveAGI);
        
        foreach (var u in units)
        {
            var map = (u.team == Team.Player) ? provider.PlayerFloor : provider.EnemyFloor;
            var cell = map.WorldToCell(u.transform.position);

            u.Bind(map, cell);
            grid.SetOccupied(u.team, u.Cell, true);
            u.InitializeATB(minAGI, maxAGI);

            u.OnDied -= HandleUnitDied;
            u.OnDied += HandleUnitDied;
        }

        _lastMinAGI = minAGI;
        _lastMaxAGI = maxAGI;
        _lastAGISum = units.Sum(u => u.EffectiveAGI);
        _lastAGICount = units.Count;

        // 상태가 바뀌면 ATB 재계산
        foreach (var u in units)
        {
            var sc = u.GetComponent<StatusController>();
            if (sc != null)
            {
                sc.OnStatusChanged += RecomputeATBSpeedsFromLiveUnits; // 기존
            }
        }

        initialized = true;
        ParametricDamageSkill.ClearFrontlineCache();    // 전방 집합 캐시 초기화
    }

    /// <summary>모든 유닛 ATB 0, 진행상태/큐 초기화 후 UI에 리셋 신호</summary>
    void ResetATBAndTurnOrder()
    {
        // 1) 모든 유닛 ATB 0
        foreach (var u in FindObjectsOfType<BattleUnit>())
        {
            if (!u || u.IsDead) continue;
            u.ResetATB();
        }

        // 2) 진행 상태 초기화
        acting = null;
        state = BattleState.Idle;
        atbPaused = false; // 리셋 후 즉시 진행

        // 3) 턴 오더 매니저가 있으면 큐/예정행동 초기화
        var tom = FindObjectOfType<TurnOrderManager>();
        if (tom != null)
        {
            tom.Clear();
            tom.RebuildFromScene(); // 웨이브의 새 유닛들 기준으로 큐 재구성
        }

        // 4) UI에 "전체 ATB 리셋" 알림 → 턴바 0으로 재배치
        OnATBReset?.Invoke();
    }

    #endregion

    void Update()
    {
        if (!initialized) return;

        // === AGI 변화 감지(실시간) ===
        {
            var alive = FindObjectsOfType<BattleUnit>().Where(u => !u.IsDead).ToList();
            float curMin = (alive.Count > 0) ? alive.Min(u => u.EffectiveAGI) : 0f;
            float curMax = (alive.Count > 0) ? alive.Max(u => u.EffectiveAGI) : 0f;
            float curSum = (alive.Count > 0) ? alive.Sum(u => u.EffectiveAGI) : 0f;
            int curCnt = alive.Count;

            if (curCnt != _lastAGICount
                || Mathf.Abs(curMin - _lastMinAGI) > AGI_EPS
                || Mathf.Abs(curMax - _lastMaxAGI) > AGI_EPS
                || Mathf.Abs(curSum - _lastAGISum) > AGI_EPS)
            {
                RecomputeATBSpeedsFromLiveUnits();
            }
        }

        if (!atbPaused)
        {
            float delta = Time.deltaTime;
            var allUnits = FindObjectsOfType<BattleUnit>().Where(u => !u.IsDead);
            foreach (var u in allUnits)
            {
                u.UpdateATB(delta);
                OnATBChanged?.Invoke(u, u.ATB, u.MaxATB); // UI 업데이트 이벤트 호출
                //if (u.name == "LuckySix") Debug.Log($"[ATB Emit] {u.name} ATB={u.ATB:F3} / Max={u.MaxATB:F3}");//ActiveIcon의 출력이 이상할 시 테스트용으로 남겨둠
            }

        }

        // ATB 최대 유닛(동시턴 타이브레이커 포함) 찾기
        if (!atbPaused)
        {
            var candidates = FindObjectsOfType<BattleUnit>()
                .Where(u => u.IsTurnReady && !u.IsDead)
                .ToList();

            if (candidates.Count > 0)
            {
                // 우선순위: Overfill(desc) → AGI(desc) → tiny random
                var selected = candidates
                    .OrderByDescending(u => u.Overfill)    // 1) 프레임 내 과충전량이 많은 순
                    .ThenByDescending(u => u.EffectiveAGI)          // 2) AGI 높은 순
                    .ThenBy(u => rng.NextDouble())         // 3) 아주 작은 난수
                    .First();

                acting = selected;
                atbPaused = true;
                StartTurn(acting);
            }
        }
    }

    // Grid 점유 해제 시도(맵/그리드 레퍼런스에 맞춰 구현)
    private void TryReleaseGridOccupy(BattleUnit u)
    {
        if (u == null || grid == null) return;
        var map = grid.GetMap(u.team);                     // 팀에 맞는 타일맵
        if (map == null) return;
        Vector3Int cell = u.Cell;                          // 유닛이 기억하는 셀(없다면 map.WorldToCell(u.transform.position))
        grid.SetOccupied(u.team, cell, false);             // 점유 해제
    }

    // 프리팹 인스턴스 → 유닛 재바인드/ATB 초기화 → UI 이벤트
    private void LoadWave(int index)
    {
        if (waveSet == null || waveSet.waves == null || index < 0 || index >= waveSet.waves.Count)
        {
            Debug.LogWarning($"[Battle] LoadWave({index}) out of range. Treat as final victory.");
            Shared.SceneTransitionManager.ReturnToSavedPoint();
            return;
        }

        CleanupEnemiesAndLayouts();
        currentWaveIndex = index;

        var w = waveSet.waves[index];
        if (w.enemyLayoutPrefab)
        {
            _spawnedEnemyLayout = Instantiate(
            w.enemyLayoutPrefab,
            enemyRoot ? enemyRoot : transform
                        );
        }

        var localProvider = _spawnedEnemyLayout.GetComponentInChildren<BattleMapManager>(true);
        Tilemap waveEnemyFloor = null;
        Tilemap waveEnemyOverlay = null;
        if (localProvider != null)
        {
            waveEnemyFloor = localProvider.EnemyFloor;
        }
        if (waveEnemyFloor == null)
        {
            // 폴백: 이름/태그 규칙 등으로 탐색 (프로젝트 규칙에 맞게 보강)
            waveEnemyFloor = _spawnedEnemyLayout
                .GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(t => t.name.IndexOf("Enemy", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
        if (waveEnemyOverlay == null)
        {
            waveEnemyOverlay = _spawnedEnemyLayout
                .GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(t => t.name.IndexOf("Overlay_Skill", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
        if (waveEnemyFloor != null)
        {
            var mapMgr = Shared.battleMapManager as BattleMapManager ?? FindObjectOfType<BattleMapManager>(true);
            mapMgr.UseEnemyFloor(waveEnemyFloor, waveEnemyOverlay);
            provider = mapMgr; // 이 BattleManager가 쓰는 provider도 최신으로

            Shared.battleInput?.RebindProviders(); // 입력쪽 provider 갱신(이미 존재하는 루틴)
            Shared.battleGridManager?.RebindProvider();
        }

        // 방금 스폰된 적 + 기존 플레이어 유닛까지 모두 다시 바인드/초기화
        RebindAllUnitsAndInitATB();

        Debug.Log("[Battle] waveEnemyFloor: " + (waveEnemyFloor ? waveEnemyFloor.name : "NULL"));   //EnemyFloor가 비어있을 시 출력됨.

        OnWaveStarted?.Invoke();   //웨이브 시작 이벤트 발행

        // UI 알림
        OnWaveChanged?.Invoke(CurrentWave, TotalWaves, w.label);
        Debug.Log($"[Battle] Wave {CurrentWave}/{TotalWaves} 시작 - {w.label}");

        // 웨이브 로드가 끝난 시점에 ATB/턴 상태를 완전 초기화(0에서 출발)
        ResetATBAndTurnOrder();

    }

    // 이전 웨이브 잔재(적 유닛/레이아웃/점유) 정리
    private void CleanupEnemiesAndLayouts()
    {
        // 1) 기존에 스폰한 적 레이아웃 프리팹 제거
        if (_spawnedEnemyLayout)
        {
            // 자식에 BattleUnit이 있을 수 있으므로 먼저 죽이거나 점유 해제
            var enemyUnits = _spawnedEnemyLayout.GetComponentsInChildren<BattleUnit>(true);
            foreach (var u in enemyUnits)
            {
                if (u == null) continue;         // 이미 파괴됨
                if (u.IsDead) continue;          // 죽는 중/죽은 유닛 → Co_DieThenDestroy가 처리
                TryReleaseGridOccupy(u);
                if (u.gameObject != null) Destroy(u.gameObject);
            }
            Destroy(_spawnedEnemyLayout);
            _spawnedEnemyLayout = null;
        }

        // 2) 혹시 남아있는 적 유닛(레이아웃 밖 스폰분)도 정리
        var leftovers = FindObjectsOfType<BattleUnit>()
                    .Where(u => u.team == Team.Enemy).ToList();
        foreach (var u in leftovers)
        {
            TryReleaseGridOccupy(u);
            Destroy(u.gameObject);
        }
    }

    void AdvanceToNextWave()
    {
        if (isWaveTransitioning) return; // 중복 방지
        isWaveTransitioning = true;
        StartCoroutine(Co_NextWave());
    }
    IEnumerator Co_NextWave()
    {
        // 다음 웨이브 인덱스/표시용 번호 계산
        int nextIndex = currentWaveIndex + 1;

        // 유효한 다음 웨이브가 있으면 TurnBar 등에 전환 알림 → 잠깐 대기
        if (waveSet != null && waveSet.waves != null && nextIndex >= 0 && nextIndex < TotalWaves)
        {
            OnWaveTransition?.Invoke(nextIndex + 1, TotalWaves); // UI에 “다음 웨이브 진행” 표시
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, waveTransitionDelay));
        }
        else
        {
            // 기존 폴백 대기(필요 시)
            yield return null;
        }

        if (waveSet == null || waveSet.waves == null || nextIndex < 0 || nextIndex >= TotalWaves)
        {
            isWaveTransitioning = false;
            if (_battleEndedOnce) yield break;          // 이미 종료 처리됐으면 무시
            _battleEndedOnce = true;                    // 가드
            Debug.Log("[Battle] 승리! (모든 웨이브 완료)");
            Shared.SceneTransitionManager.ReturnToSavedPoint();
            yield break;
        }
        LoadWave(nextIndex);
        isWaveTransitioning = false;
    }

    void AutoResolveWaveSet()
    {
        // 1) StageDB 확보 (인스펙터 없으면 Resources: Resources/DB/StageDatabase.asset)
        if (stageDB == null) stageDB = Resources.Load<StageDatabase>("DB/StageDatabase");

        // 2) 스테이지/맥락 결정: 런타임 컨텍스트 → (폴백) 디버그 값
        int stageNo = StageRuntimeContext.Instance != null && StageRuntimeContext.Instance.CurrentStageNumber >= 0
            ? StageRuntimeContext.Instance.CurrentStageNumber
            : debugStageNumber;

        var ctx = StageRuntimeContext.Instance != null
            ? StageRuntimeContext.Instance.CurrentBattleContext
            : debugContext;

        if (stageDB == null || stageNo < 0)
        {
            Debug.LogWarning("[Battle] StageDB or StageNo missing. Use scene-placed units.");
            return;
        }

        // 3) StageNormalMapData 찾기
        StageNormalMapData found = null;
        foreach (var s in stageDB.normalStages)
        { // Database에 배열이 이미 존재합니다 :contentReference[oaicite:3]{index=3}
            if (s != null && s.stageNumber == stageNo) { found = s; break; }
        }

        if (found == null)
        {
            Debug.LogWarning($"[Battle] StageNormalMapData not found for stage {stageNo}");
            return;
        }

        // 4) 맥락에 맞는 WaveSet 선택
        waveSet = (ctx == BattleContext.TrapEncounter) ? found.trapEncounterWave : found.postPuzzleWave;

        if (waveSet != null)
        {
            Debug.Log($"[Battle] WaveSet auto-assigned: Stage {stageNo}, {ctx} → {waveSet.name}");
        }
        else
        {
            Debug.LogWarning($"[Battle] WaveSet not assigned in StageNormalMapData (stage {stageNo}, {ctx}). Fallback to scene-placed units.");
        }
    }


    #region Turn Management
    void StartTurn(BattleUnit unit)
    {
        if (unit == null) return;

        acting = unit;
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        ClearAllPreviews();
        ClearTargetSelection();
        manualEndRequested = false;
        OnAnyUnitTurnStarted?.Invoke(unit);

        Debug.Log($"[Battle] StartTurn -> {acting.name}");

        var sc = unit.GetComponent<StatusController>();
        if (sc != null) sc.OnTurnStart();

        // 캐스팅 성공 턴 소비 처리
        if (unit.team == Team.Enemy)
        {
            var ecs = unit.GetComponent<EnemyCastState>();
            if (ecs != null && ecs.TryTakeReady(out var pending))   // 준비된 캐스팅 성공 확인
            {
                // 적 행동 루틴 대신, '웹 발사→생성→소비' 코루틴 실행
                StartCoroutine(Co_EnemyFireWebThenConsume(unit, pending));
                return; // EnemyTurnRoutine 시작하지 않음
            }
        }

        // 모든 ATB 정지
        atbPaused = true;

        if (unit.team == Team.Player)
        {
            state = BattleState.ActionSelect; // 플레이어 입력 허용
            //Debug.Log($"[PlayerTurn] {unit.name} 턴 시작 → ATB 정지");
        }
        else
        {
            state = BattleState.Resolving; // 입력 잠금
            //Debug.Log($"[EnemyTurn] {unit.name} 턴 시작 → ATB 정지");
            StartCoroutine(EnemyTurnRoutine(unit));
        }
    }

    // === 생존 유닛들의 현재 AGI 범위로 전원의 ATB 속도 재계산 ===
    void RecomputeATBSpeedsFromLiveUnits()
    {
        var alive = FindObjectsOfType<BattleUnit>().Where(u => !u.IsDead).ToList();
        if (alive.Count == 0) return;

        float min = alive.Min(u => u.EffectiveAGI);
        float max = alive.Max(u => u.EffectiveAGI);
        foreach (var u in alive)
            u.InitializeATB(min, max); // atbPerSecond만 갱신(ATB 값은 그대로)

        // 스냅샷 갱신
        _lastMinAGI = min;
        _lastMaxAGI = max;
        _lastAGISum = alive.Sum(u => u.EffectiveAGI);
        _lastAGICount = alive.Count;
    }
    public void OnClickEndTurn()
    {
        if (acting == null || acting.team != Team.Player) return;
        manualEndRequested = true;   // 회복 판정용 플래그만 남김
        EndPlayerTurn();             // 종료 로직은 한 군데로 집약
    }
    #endregion

    #region Movement
    public void OnClickMove()
    {
        if (acting == null || !IsPlayerTurn) return; // 적 턴 금지
        if (usedActions.Contains(BattleAction.Move) || remainingActions <= 0) return; // 중복/토큰 없음

        // 스킬 패널/선택 상태 정리
        CloseSkillPanel();       // 패널 열려있으면 닫기
        ClearAllPreviews();    // 남아있을지 모를 프리뷰 정리
        ClearTargetSelection();  // 타겟 마커 숨김

        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(acting.team, acting.Cell).ToList();
        ShowMovePreview(acting.CurrentMap, moveOptions);
    }

    public void OnTileClicked(Tilemap clickedMap, Vector3Int clickedCell)
    {
        if (!IsPlayerTurn) return;

        if (state == BattleState.Moving)
        {
            if (clickedMap == acting.CurrentMap && moveOptions.Contains(clickedCell))
            {
                state = BattleState.Resolving; // 입력 잠금
                StartCoroutine(Co_MoveThenConsume(acting, clickedMap, clickedCell, BattleAction.Move));
                ClearAllPreviews();
                moveOptions.Clear();
                return;
            }
        }
        else if (state == BattleState.Targeting)
        {
            if (state == BattleState.Targeting
                && currentSkillSO != null
                && currentSkillSO.targetMode == SkillTargetMode.Tile
                && clickedMap == (currentSkillTargetMap ?? provider.EnemyFloor))
            {
                ConfirmSkillOnTile(clickedMap, clickedCell);
            }
        }
    }

    IEnumerator Co_MoveThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell, BattleAction act)
    {
        var fromCell = unit.Cell;
        grid.SetOccupied(unit.team, fromCell, false);
        yield return unit.AnimateMoveTo(map, toCell);
        grid.SetOccupied(unit.team, unit.Cell, true);
        OnActionConsumed(act); // 이동 1회 소비 → 남은 토큰 판단
    }
    #endregion

    #region Attack
    public void OnClickAttack()
    {
        if (!IsPlayerTurn || acting == null) return; // 적 턴 금지
        if (usedActions.Contains(BattleAction.Attack) || remainingActions <= 0) return; // 중복/토큰 없음

        ClearAllPreviews();
        ClearTargetSelection();
        OpenSkillPanel();
    }

    public void OnUnitClicked(BattleUnit target)
    {
        if (!IsPlayerTurn) return;
        if (!IsTargeting || currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;
        if (target == null || target.team == acting.team) return;

        ConfirmSkillOnUnit(target);
    }
    #endregion

    #region Action Consumption
    void OnActionConsumed(BattleAction act)
    {
        usedActions.Add(act);
        remainingActions = Mathf.Max(0, remainingActions - 1);

        // 남은 행동이 있으면 플레이어 입력 대기
        if (remainingActions > 0)
        {
            if (IsPlayerTurn)
            {
                state = BattleState.ActionSelect; // 플레이어 선택 허용
            }
            else
            {
                // 적 턴이면 EnemyTurnRoutine 재개
                if (enemyRoutine != null) StopCoroutine(enemyRoutine);
                enemyRoutine = StartCoroutine(EnemyTurnRoutine(acting));
            }
        }
        else
        {
            // 행동 토큰 모두 소진 → 턴 종료 처리
            if (IsPlayerTurn)
            {
                EndPlayerTurn();
            }
            else
            {
                EndEnemyTurn(acting);
            }
        }
    }

    // 플레이어 턴 종료 처리
    void EndPlayerTurn()
    {
        ClearAllPreviews();
        ClearTargetSelection();

        if (manualEndRequested && usedActions.Count == 0)
        {
            acting.Heal(-1);
            //Debug.Log("[EndPlayerTurn] 행동 없이 수동 종료 → HP회복");
        }

        manualEndRequested = false;

        // ATB 재개(다음 턴은 Update()가 자동 감지)
        acting.ResetATB(); // ATB와 Overfill 함께 초기화
        acting = null;
        atbPaused = false;
        state = BattleState.Idle;
    }

    public void CancelCurrentAction()
    {
        // 타겟팅 중(스킬 선택됨) → '스킬만 해제', 패널은 유지
        if (state == BattleState.Targeting && currentSkillSO != null)
        {
            ClearSkillPreview();
            ClearTargetSelection();
            currentSkillSO = null;
            currentSkillTargetMap = null;
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            if (!isSelectingSkill) OpenSkillPanel();
            return;
        }
        if (state == BattleState.Moving)
        {
            ClearMovePreview();
            ClearTargetSelection();
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            return;
        }
        // 그밖의 상황에서 패널이 열려 있다면(= 취소 2회째) 패널 닫기
        if (isSelectingSkill)
        {
            ClearAllPreviews();
            CloseSkillPanel();
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            return;
        }
    }
    #endregion

    #region Targeting
    void BuildTargetCycle()
    {
        targetCycle = FindObjectsOfType<BattleUnit>()
            .Where(u => u.team == Team.Enemy && !u.IsDead)
            .OrderByDescending(u => u.EffectiveAGI)
            .ToList();
        targetIndex = -1;
        selectedTarget = null;
    }

    // 유닛을 직접 지정하여 TargetCycle에서 선택(없으면 false)
    public bool SelectTargetByUnit(BattleUnit unit)
    {
        if (unit == null || targetCycle == null || targetCycle.Count == 0)
            return false;

        if (selectedTarget == unit)
            return true; // 이미 선택됨

        int idx = targetCycle.IndexOf(unit);
        if (idx < 0) return false; // 사이클에 없는 유닛이면 무시(적만 포함 등 규칙 유지)

        SelectTarget(idx); // SelectTarget 내부에서 마커 + 하이라이트까지 갱신
        return true;
    }

    void SelectTarget(int index)
    {
        if (targetCycle.Count == 0) { ClearTargetSelection(); return; }
        int n = targetCycle.Count;
        targetIndex = ((index % n) + n) % n; // 안전한 모듈로
        selectedTarget = targetCycle[targetIndex];
        targetMarker?.Attach(selectedTarget);

        // 스킬이 Unit형으로 선택된 상태라면, 선택된 타겟 기준으로 범위 미리보기 갱신
        if (currentSkillSO != null && currentSkillSO.targetMode == SkillTargetMode.Unit)
        {
            PreviewSkillAreaOnUnit(selectedTarget);
        }
    }

    public void CycleTarget(int dir)
    {
        if (!IsPlayerTurn || !IsTargeting || targetCycle.Count == 0) return;
        SelectTarget(targetIndex + dir); // dir=+1(→), -1(←)
    }

    public void ConfirmTarget()
    {
        if (!IsPlayerTurn || !IsTargeting || selectedTarget == null) return;
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;

        ClearSkillPreview();
        StartCoroutine(Co_GapCloseThenResolveOnTargetSO(currentSkillSO, acting, selectedTarget));
    }

    void ClearTargetSelection()
    {
        selectedTarget = null;
        targetIndex = -1;
        targetMarker?.Hide();
    }
    #endregion

    #region Death Handling
    void HandleUnitDied(BattleUnit dead)
    {
        grid.SetOccupied(dead.team, dead.Cell, false);

        if (dead == acting)
        {
            acting = null;
            atbPaused = false; // ATB 충전 재개
            state = BattleState.Idle;
        }
        //Debug.Log($"[Die] {dead.name}");

        // 중복 구독 방지
        dead.OnDied -= HandleUnitDied;

        StartCoroutine(Co_DieThenDestroy(dead));
    }

    IEnumerator Co_DieThenDestroy(BattleUnit u)
    {
        if (u == null) { CheckBattleEnd(); yield break; }

        // 1) 사망 연출을 "수동 이터레이션"으로 소비 (중간 파괴에 안전)
        var routine = u.PlayDieAndWait(1.0f);
        if (routine != null)
        {
            // u가 중간에 파괴되거나 routine이 끝나면 루프 종료
            while (u != null)
            {
                // 다음 프레임으로 진행할 수 없으면 종료
                if (!routine.MoveNext()) break;

                // 현재 yield 값 전달
                yield return routine.Current;

                // 중간에 오브젝트가 파괴되면 자연 종료
                if (u == null || u.gameObject == null) break;
            }
        }

        // 2) 최종 파괴(여러 경로로 Destroy가 한 번 더 호출돼도 안전)
        if (u != null && u.gameObject != null)
            Destroy(u.gameObject);

        // 3) 사망 정리가 끝난 뒤 전투 종료 판정
        CheckBattleEnd();
    }
    #endregion

    #region Battle End
    void CheckBattleEnd()
    {
        var units = FindObjectsOfType<BattleUnit>();
        bool anyPlayer = units.Any(u => u.team == Team.Player && !u.IsDead);
        bool anyEnemy = units.Any(u => u.team == Team.Enemy && !u.IsDead);

        if (!anyEnemy)
        {
            // 다음 웨이브가 있으면 진행, 없으면 최종 승리
            if (isWaveTransitioning) return;
            if (waveSet && CurrentWave < TotalWaves)
            {
                AdvanceToNextWave();
                return;
            }

            if (_battleEndedOnce) return;              // 중복 가드
            _battleEndedOnce = true;                   // 가드
            Debug.Log("[Battle] 승리! (최종 웨이브 완료)");

            if (Shared.PuzzleManager.IsPuzzleComplete)
                Shared.SceneTransitionManager.FadeToScene("EndScene");
            else
                Shared.SceneTransitionManager.ReturnToSavedPoint();
        }
        else if (!anyPlayer)
        {
            if (_battleEndedOnce) return;              //  중복 가드
            _battleEndedOnce = true;                   //  가드
            Debug.Log("[Battle] 패배...");

            if (Shared.PuzzleManager.IsPuzzleComplete)
                Shared.SceneTransitionManager.FadeToScene("EndScene");
            else
                Shared.SceneTransitionManager.ReturnToSavedPoint();
        }
    }
    #endregion

    #region Enemy AI
    IEnumerator EnemyTurnRoutine(BattleUnit enemy)
    {
        yield return new WaitForSeconds(0.5f); // 살짝 텀

        // 생존 플레이어 수집
        var players = FindObjectsOfType<BattleUnit>()
            .Where(u => u.team == Team.Player && !u.IsDead)
            .ToList();
        if (players.Count == 0) { EndEnemyTurn(enemy); yield break; }

        BattleUnit target = players[Random.Range(0, players.Count)]; // 랜덤 1인 지정

        // 미리 선정해둔 스킬을 꺼내서 사용
        var ai = enemy.GetComponent<EnemyAI>();
        SkillAsset so = (ai != null) ? ai.ConsumePlannedSkillOrPick() : null;

        // 턴이 시작된 지금, 이 턴에 쓸 스킬명을 방송
        if (so != null) EmitActionLabel(enemy, so.displayName);

        if (so != null)
        {
            // 이미 표시 중인 예정 스킬명이 실행됨
            if (so.targetMode == SkillTargetMode.Unit)
            {
                yield return StartCoroutine(so.ResolveOnUnit(this, enemy, target));
                FinishActionAfterSkill();
                yield break;
            }
            else if(so.targetMode == SkillTargetMode.Tile)
            {
                //Tile지정 스킬 추가 예정
                yield break;
            }
        }

        // so가 없으면 (null) 기본 종료/대기 등
        EndEnemyTurn(enemy);
    }

    // === Enemy AI Helpers ===
    // 적 유닛 대상형 스킬 실행
    IEnumerator Co_EnemyResolveSkillOnUnit_NoMove(SkillDefinition def, BattleUnit caster, BattleUnit target)
    {
        state = BattleState.Resolving;

        bool impactDone = false;
        System.Action impact = null;
        impact = () =>
        {
            caster.OnAttackImpact -= impact;
            impactDone = true;
            // 대상 유닛의 셀을 원점으로 범위 계산/피해 적용
            ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster);
        };
        caster.OnAttackImpact += impact;

        yield return caster.AnimateAttack(target); // 제자리 근접 모션

        if (!impactDone) // 애니 이벤트 누락 대비
            ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster);

        FinishActionAfterSkill(); // 토큰 소모/턴 진행(공격으로 간주)
    }

    IEnumerator Co_EnemyFireWebThenConsume(BattleUnit caster, EnemyCastState.PendingCast p) //실뿜기 스킬 실행 루틴
    {
        state = BattleState.Resolving;

        // 캐스팅 루프 종료 → 발사 애니
        caster.SetCasting(false);

        // 발사 타이밍을 '임팩트 이벤트'로 맞춘다
        bool fired = false;
        bool arrived = false;

        //어떤 투사체를 쓸지 결정
        ProjectileController projPrefab =
              p.projectilePrefab
           ?? caster.defaultProjectilePrefab
           ?? null; // (BM 전역을 유지하고 싶다면 ?? this.projectilePrefab)

        // 투사체 발사 → 도착 시 트랩 생성
        Vector3 startW = caster.transform.position;
        Vector3 targetW = p.map.GetCellCenterWorld(p.cell);

        void FireOnce()
        {
            if (fired) return;
            fired = true;

            void OnArrive()
            {
                if (p.trapPrefab != null && p.map != null)
                {
                    WebTrapController.RemoveAt(p.map, p.cell);
                    var trap = Instantiate(p.trapPrefab, targetW, Quaternion.identity);
                    trap.Init(p.map, p.cell, p.owner);
                }
                arrived = true;
            }

            if (projPrefab != null)
            {
                var go = Instantiate(projPrefab, startW, Quaternion.identity);
                var pc = go.GetComponent<ProjectileController>();
                if (pc != null) pc.Init(startW, targetW, OnArrive, p.projectileSpeed);
                else OnArrive();
            }
            else
            {
                OnArrive();
            }
        }

        System.Action onFire = null;
        onFire = () =>
        {
            caster.OnAttackImpact -= onFire; // 반드시 해제
            FireOnce();
        }
        ;
        caster.OnAttackImpact += onFire;

        // 발사 모션 시작
        yield return caster.AnimateShootWeb();  // 애니 끝까지 대기 (발사는 이미 중간에 실행됨)

        // 애니 중 임팩트 이벤트가 안 왔다면, 애니 종료 시 1회 강제 발사
        if (!fired)
        {
            caster.OnAttackImpact -= onFire; // 혹시 남았으면 해제
            FireOnce();
        }

        // 투사체 도착까지 대기(안전 타임아웃)
        float timeout = 3f;
        while (!arrived && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }

        // 프리뷰 토큰/홀드 해제
        ClearSkillPreview();

        var ecs = caster.GetComponent<EnemyCastState>();
        ecs?.ClearPreviewAndFinalize(this);   // 토큰 삭제 + 홀드 해제 + pending 정리

        // 행동 토큰 소비로 턴 종료
        OnActionConsumed(BattleAction.Attack);
    }

    void EndEnemyTurn(BattleUnit enemy)
    {
        enemyRoutine = null;

        // 캐스팅 중이면 다음 스킬 선점/라벨 갱신 금지
        var ecs = enemy.GetComponent<EnemyCastState>();
        if (ecs == null || !ecs.IsCasting)
        {
            EmitActionLabel(enemy, ""); // 라벨 비우기
            var ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.PlanNextSkill();  // 다음 턴용 스킬 미리 선정
        }

        enemy.ResetATB();
        if (acting == enemy) acting = null;

        atbPaused = false;     // 전체 ATB 재개
        state = BattleState.Idle;
    }
    #endregion

    // 도망가기(버튼/F1 공용)
    public async void OnClickEscape()
    {
        // 플레이어 턴에서만 허용, 해 resolving 중(피해 계산 등)에는 금지
        if (acting == null || acting.team != Team.Player) return;
        if (state == BattleState.Resolving) return;

        // 2) 성공 확률 계산 = (해당 유닛 AGI) / (생존한 적군 전체 AGI 합)
        var aliveEnemies = FindObjectsOfType<BattleUnit>()
            .Where(u => u.team == Team.Enemy && !u.IsDead)
            .ToList();
        float enemyAgiSum = Mathf.Max(0.0001f, aliveEnemies.Sum(u => u.EffectiveAGI));
        float successChance01 = Mathf.Clamp01(acting.EffectiveAGI / enemyAgiSum);

        // 퍼센트 변환
        int percent = Mathf.RoundToInt(successChance01 * 100f);

        // 공통 확인 팝업
        string unitName = GetUnitLabel(acting);
        string safeName = unitName.Replace("<", "&lt;").Replace(">", "&gt;");
        string msg = $"<color=#C60004>{safeName}</color> 유닛을 전투에서 제외합니다. 진행할까요?\n" + 
                     $"(탈출 성공 확률: {percent}%)";

        bool ok = await PopupManager.Instance.ConfirmRetreatAsync(msg, successChance01);
        if (!ok) return;    // 사용자가 취소
        // 최종 실행: 성공/실패 롤
        bool success = (Random.value < successChance01);

        if (success)
        {
            // 성공 메시지 → 곧바로 퇴각 처리
            await PopupManager.Instance.ConfirmAsync("탈출에 성공했습니다.", "확인", ""); // 확인만
            RetreatCurrentUnit(acting); // 기존 퇴각 함수 재사용
        }
        else
        {
            // 실패 메시지 → 행동 없이 '턴만 종료'
            await PopupManager.Instance.ConfirmAsync("탈출에 실패했습니다.", "확인", "");
            EndPlayerTurn(); // 플레이어 턴 종료만 수행
        }

        // 진행 중이던 선택/표시 정리
        CancelCurrentAction();
        ClearAllPreviews();
        ClearTargetSelection();
    }

    private string GetUnitLabel(BattleUnit u)
    {
        // 프로젝트에 따라 이름 필드가 다를 수 있어요:
        // DisplayName / UnitName / CharacterName 등 사용 중인 것을 우선적으로 반환
        if (!string.IsNullOrEmpty(u.name)) return u.name;
        if (!string.IsNullOrEmpty(u.name)) return u.name;
        // 없으면 GameObject 이름 fallback
        return u.name;
    }

    //탈출 실행
    void RetreatCurrentUnit(BattleUnit u)
    {
        if (u == null) return;

        // 1) 그리드 점유 해제
        TryReleaseGridOccupy(u);

        // 2) 턴 오더에서 제거 (현재/이후 턴에서 사라지도록)
        var tom = FindObjectOfType<TurnOrderManager>();
        tom?.Remove(u); // 이미 제공됨

        // 3) HUD/턴바/기타 UI에 알림
        u.Retreat(); // UnitStatusPanelUI / TurnBarUI가 이 이벤트로 자기 UI를 제거

        // 4) 유닛 오브젝트 제거
        Destroy(u.gameObject);

        // 5) 현재 턴 정리 및 ATB 재개
        if (u == acting)
        {
            acting = null;
            atbPaused = false;
            state = BattleState.Idle;
        }

        // 6) 전투 종료 체크(전원 퇴각/전멸 등)
        CheckBattleEnd();
    }


    public void OpenSkillPanel()
    {
        if (!IsPlayerTurn || acting == null) return;
        isSelectingSkill = true;

        var raw = acting?.data?.skills ?? System.Array.Empty<SkillAsset>();
        // 표시용은 상태 기반으로 해석된 SO를 전달 → 버튼 라벨이 즉시 반영됨
        var view = new SkillAsset[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            var s = raw[i];
            if (s is ISkillForStateResolver resolver)
                view[i] = resolver.ResolveForCaster(acting) ?? s;
            else view[i] = s;
        }

        OnSkillPanelPopulateSO?.Invoke(view);
        OnSkillPanelToggled?.Invoke(true);
        UpdateTargetingHint();
    }
    public void CloseSkillPanel()
    {
        isSelectingSkill = false;
        OnSkillPanelToggled?.Invoke(false);
        UpdateTargetingHint();
    }

    public void SelectSkill(int index)
    {
        var list = acting?.data?.skills;
        if (list == null || index < 0 || index >= list.Length) return;

        var picked = list[index];

        // 상태 기반 스킬 치환(어댑터/라우터가 있으면 실제 사용할 SO로 교체)
        if (picked is ISkillForStateResolver resolver)
            picked = resolver.ResolveForCaster(acting) ?? picked;

        currentSkillSO = picked;

        EnterSkillTargeting(currentSkillSO);
    }
    private void EnterSkillTargeting(SkillAsset skill)
    {
        if (skill == null) return;

        // MP 부족 사전 차단
        if (!acting.HasMP(skill.mpCost))
        {
            Debug.Log($"[Skill] MP 부족: {skill.displayName} (필요 {skill.mpCost})");
            //EmitActionLabel?.Invoke(acting, $"MP {skill.mpCost} 필요"); // 카드 라벨 등
            return; // 타겟팅 진입 안 함
        }

        if (skill is ISelfCastSkill self && self.SelfCastOnSelect)
        {
            StartCoroutine(skill.ResolveOnUnit(this, acting, acting));
            FinishActionAfterSkill(); // 프로젝트의 기존 "행동 종료" 루틴 호출
            return;
        }

        // 스킬 타겟팅 모드로 진입
        state = BattleState.Targeting;
        ClearAllPreviews();
        ClearTargetSelection(); // 기존 선택/마커 초기화
        UpdateTargetingHint();

        // Unit 타겟형이면: AGI 내림차순 사이클 구성 후 첫 타겟으로 마커 표시
        if (skill.targetMode == SkillTargetMode.Unit)
        {
            BuildTargetCycle();           // Enemy만, AGI desc
            //if (targetCycle.Count > 0)
            //    SelectTarget(0);          // 첫 타겟(=가장 빠른 AGI)으로 마커/미리보기
        }
        else // Tile 타겟형: 내부 타일 커서를 1회 세팅하고 프리뷰 유지
        {
            currentSkillTargetMap = (skill as ITargetMapProvider)?.GetTargetMap(this, acting) ?? provider?.EnemyFloor;
            var map = currentSkillTargetMap;

            if (map != null)
            {
                var cam = Camera.main;
                var world = cam ? cam.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
                world.z = 0f;
                var hover = map.WorldToCell(world);
                if (map.HasTile(hover))
                {
                    selectedCell = hover;
                    PreviewSkillAreaOnTile(map, selectedCell);
                }
            }
        }
    }

    private void UpdateTargetingHint()
    {
        // 스킬이 확정되어 타게팅 상태일 때만 힌트 노출
        if (state == BattleState.Targeting && currentSkillSO != null)
            OnHint?.Invoke("대상을 선택하세요");
        else
            OnHint?.Invoke(string.Empty);
    }

    // 현재 선택된 스킬의 범위를 "유닛 기준"으로 미리보기
    public void PreviewSkillAreaOnUnit(BattleUnit unit)
    {
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) { ClearAllPreviews(); return; }
        if (unit == null || unit.team == Team.Player) { ClearAllPreviews(); return; } // 아군(플레이어)에는 미리보기 표시하지 않음

        var origin = unit.Cell;                            // 유닛의 현재 셀
        var cells = currentSkillSO.GetAreaCells(origin, SkillLibrary.IsOddColumn(origin));
        var map = unit.CurrentMap;

        // 맵 경계 바깥 셀은 제외
        var validCells = cells.Where(c => map.HasTile(c)).ToList();

        if (validCells.Count == 0)
        {
            // 유효한 셀이 없으면 하이라이트/유닛 강조도 지움
            skillHighlighter?.ClearTransient();
            StatusPanel?.ClearHighlights();
            return;
        }

        // 범위 내 유닛 수집 → 패널 하이라이트
        skillHighlighter.ShowCells(map, validCells);
        var victims = GetUnitsInArea(map, validCells);
        StatusPanel?.HighlightUnits(victims);
    }

    // 현재 선택된 스킬의 범위를 "타일 기준"으로 미리보기
    public void PreviewSkillAreaOnTile(Tilemap map, Vector3Int originCell)
    {
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Tile) { ClearAllPreviews(); return; }
        if (map == null) { ClearAllPreviews(); return; }

        var cells = currentSkillSO.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));

        // 맵 경계 바깥 셀은 제외
        var validCells = cells.Where(c => map.HasTile(c)).ToList();

        if (validCells.Count == 0)
        {
            skillHighlighter?.ClearTransient();
            StatusPanel?.ClearHighlights();
            return;
        }

        skillHighlighter.ShowCells(map, validCells);

        // 범위 내 유닛 수집 → 패널 하이라이트
        var victims = GetUnitsInArea(map, validCells);
        StatusPanel?.HighlightUnits(victims);
    }

    public void ConfirmSkillOnUnit(BattleUnit target)
    {
        if (!IsPlayerTurn || acting == null || target == null) return;
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;
        if (IsPlayerTurn && target.team == Team.Player) return; // 유닛 공격 시 아군을 대상으로 하지 못하게 방지

        // 최종 사전 체크
        //if (!acting.HasMP(currentSkillSO.mpCost)) { EmitActionLabel?.Invoke(acting, "MP 부족"); return; } -- 수정해야함

        // 미리보기 정리
        ClearSkillPreview();
        StartCoroutine(Co_GapCloseThenResolveOnTargetSO(currentSkillSO, acting, target));
    }
    public void ConfirmSkillOnTile(Tilemap map, Vector3Int originCell)
    {
        if (!IsPlayerTurn || acting == null || map == null) return;
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Tile) return;

        ClearSkillPreview();
        StartCoroutine(Co_ProjectileSkillThenFinishSO(currentSkillSO, map, originCell, acting));
    }

    IEnumerator Co_GapCloseThenResolveOnTargetSO(SkillAsset skill, BattleUnit caster, BattleUnit target)
    {
        state = BattleState.Resolving;

        Vector3 originalW = caster.transform.position;

        // 타겟 앞 점프(연출)
        if (TryGetFrontCellOfTarget(caster, target, out var frontCell))
        {
            var mapForJump = target.CurrentMap ?? provider.EnemyFloor;
            Vector3 frontW = GetCellRightEdgeWorld(mapForJump, frontCell, 0.02f);
            yield return caster.AnimateJumpToWorld(frontW, jumpDuration, null, jumpArc);
        }

        // 공격 모션 중 임팩트 타이밍에 해결
        bool impactTriggered = false;
        bool resolved = false;

        System.Action impact = null;
        impact = () =>
        {
            // 중복 방지 플래그 & 구독 해제
            impactTriggered = true;
            caster.OnAttackImpact -= impact;

            // 임팩트 순간 최종 차감
            if (!caster.TryConsumeMP(skill.mpCost))
            {
                Debug.Log("[Skill] 임팩트 시 MP 부족 → 취소");
                // 비용 차감 실패: 아무것도 일으키지 않고 종료
                return;
            }

            // 차감 성공 → 스킬 해결
            StartCoroutine(Co_ResolveUnitThenFlag(skill, caster, target, () => { resolved = true; }));
        };

        caster.OnAttackImpact += impact;
        yield return caster.AnimateAttack(target);

        caster.OnAttackImpact -= impact;    //애니가 끝났는데도 핸들러가 남아있을 수 있으니 한 번 더 해제

        if (!impactTriggered)
        {
            if (!caster.TryConsumeMP(skill.mpCost))
            {
                Debug.Log("[Skill] 임팩트 미수신 폴백 시 MP 부족 → 취소");
            }
            else
            {
                yield return skill.ResolveOnUnit(this, caster, target);
            }

            resolved = true;
        }

        // 임팩트로 시작했다면, 해결 완료까지 대기(타임아웃 가드)
        float timeout = 1.5f;
        while (!resolved && (timeout > 0f)) { timeout -= Time.deltaTime; yield return null; }

        // 3) 원위치 복귀
        caster.transform.position = originalW;
        FinishActionAfterSkill();
    }

    IEnumerator Co_ProjectileSkillThenFinishSO(SkillAsset skill, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        state = BattleState.Resolving;

        bool castEnded = false;
        bool projEnded = false;
        bool fired = false; // 임팩트(발사) 수신 여부

        // 캐스터 모션 종료 훅
        System.Action onCastEnd = null;
        onCastEnd = () => { caster.OnAttackEnded -= onCastEnd; castEnded = true; };
        caster.OnAttackEnded += onCastEnd;

        // 발사 타이밍 훅: 투사체 생성 + 도착 시 SO 해결
        System.Action onFire = null;
        onFire = () =>
        {
            caster.OnAttackImpact -= onFire;
            fired = true; // 임팩트 수신

            // 발사 순간 최종 차감
            if (!caster.TryConsumeMP(skill.mpCost))
            {
                Debug.Log("[Skill] 발사 시 MP 부족 → 취소");
                projEnded = true; // 종료 플래그만 세우고 끝
                return;
            }

            if (projectilePrefab != null)
            {
                var startW = caster.transform.position;
                var targetW = map.GetCellCenterWorld(cell);
                var go = Instantiate(projectilePrefab, startW, Quaternion.identity);
                var pc = go.GetComponent<ProjectileController>();
                if (pc != null)
                {
                    pc.Init(startW, targetW,
                        () => StartCoroutine(Co_ResolveTileThenFlag(skill, map, cell, caster, () => { projEnded = true; })),
                        speedUnitsPerSec: 3f);
                }
                else
                {
                    StartCoroutine(FallbackProjectile(startW, targetW, 0.35f, () =>
                    {
                        StartCoroutine(Co_ResolveTileThenFlag(skill, map, cell, caster, () => { projEnded = true; }));
                    }));
                }
            }
            else
            {
                StartCoroutine(Co_ResolveTileThenFlag(skill, map, cell, caster, () => { projEnded = true; }));
            }
        };
        caster.OnAttackImpact += onFire;

        // 원거리 모션
        yield return caster.AnimateRanged();

        // 임팩트 이벤트를 못 받았을 때: 여기서 직접 MP 차감 후 즉시 해결
        if (!fired && !projEnded)
        {
            if (!caster.TryConsumeMP(skill.mpCost))
            {
                Debug.Log("[Skill] 임팩트 미수신 폴백 시 MP 부족 → 취소");
                projEnded = true;
            }
            else
            {
                yield return skill.ResolveOnTile(this, map, cell, caster);
                projEnded = true;
            }
        }


        // 두 조건 모두 충족 대기
        float timeout = 2f;
        while (!(castEnded && projEnded) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        FinishActionAfterSkill();
    }

    // 스킬 범위를 계산해, 같은 맵에 있는 유닛들 중 해당 셀에 위치한 유닛에게 피해 적용
    public void ResolveSkillAtCell(SkillDefinition def, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        // 1) 범위 셀들 계산 (axial 변환은 SkillLibrary 내부에서 처리됨)
        var area = def.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));

        // 2) 피격 대상 수집 (같은 맵 + 셀 일치)
        var victims = GetUnitsInArea(map, area);

        // 3) 피해 적용 (임시: 적 유닛만 타격, 피해량은 캐스터의 일반 공격력 사용)
        //ExecuteSkillDamage(caster, victims, def);
        // 효과음/VFX 등은 여기에서
    }

    // === 유닛 점유/워커블 헬퍼 ===
    bool IsCellOccupied(Tilemap map, Vector3Int cell)
    {
        // 맵→팀 판별 규칙이 명확하면 팀별로 조회
        var team = (map == provider?.PlayerFloor) ? Team.Player : Team.Enemy;
        return grid != null && grid.IsOccupied(team, cell);
    }
    bool IsWalkableCell(Tilemap map, Vector3Int cell)
    {
        if (!map.HasTile(cell)) return false;
        var team = (map == provider?.PlayerFloor) ? Team.Player : Team.Enemy;
        return grid != null && !grid.IsOccupied(team, cell);
    }

    public IEnumerable<BattleUnit> GetUnitsInArea(Tilemap map, IEnumerable<Vector3Int> cells)
    {
        // 맵 경계 바깥 셀 제외(있으면)
        var valid = new HashSet<Vector3Int>(cells
            .Where(c => map.HasTile(c))); // HasTile 체크가 필요 없다면 이 줄은 빼도 됨

        // 씬의 모든 유닛 중 같은 맵에 있고, 셀 좌표가 area 안에 있는 유닛만
        foreach (var u in FindObjectsOfType<BattleUnit>())
        {
            if (u == null || u.CurrentMap != map) continue;
            if (valid.Contains(u.Cell))
                yield return u;
        }
    }

    public void ExecuteSkillDamage(BattleUnit caster, IEnumerable<BattleUnit> victims, SkillAsset source, Tilemap map, Vector3Int originCell)
    {
        if (caster == null || source == null) return;

        foreach (var v in victims)
        {
            if (v == null) continue;
            if (IsEnemyOf(caster, v))
            {
                var ctx = new SkillRuntime
                {
                    map = map,
                    originCell = originCell,
                    casterCell = caster.Cell,
                    targetCell = v.Cell
                };

                int damage = Mathf.Max(1, source.ComputeDamage(caster, v, ctx));
                v.PlayHit();
                v.TakeDamage(damage);

                // 중앙화된 적대감 산출
                float hostilityGained = HostilityRules.FromDamage(damage, caster, v);
                caster.AddHostility(hostilityGained);
                //Debug.Log($"[DMG] {caster.name} -> {v.name}: {damage}, Hostility +{hostilityGained:F1}");
            }
        }
    }
    bool IsEnemyOf(BattleUnit a, BattleUnit b)
    {
        // 프로젝트에 따라 팀 판별 방법이 다르면 이곳을 연결
        return a != null && b != null && a.team != b.team;
    }

    void FinishActionAfterSkill()
    {
        // 하이라이트/선택 상태 정리
        ClearSkillPreview();
        // 스킬 실행 완료 → 패널 닫기 + 스킬 선택 해제
        CloseSkillPanel();   // 이벤트까지 함께 발행됨
        // 스킬은 '공격'으로 간주하여 행동 토큰 소모 로직 재사용
        OnActionConsumed(BattleAction.Attack);

        currentSkill = default;           // 레거시
        currentSkillSO = null;            // SO도 클리어
        currentSkillTargetMap = null;
        UpdateTargetingHint();
    }


    // 타겟팅 취소/종료 시 미리보기 지우기
    public void ShowMovePreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        moveHighlighter?.ShowCells(baseMap, cells);
    }
    public void ShowSkillPreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        skillHighlighter?.ShowCells(baseMap, cells);
    }

    public void ClearMovePreview()
        => moveHighlighter?.ClearTransient();
    public void HoldSkillPreview()
        => _skillPreviewHold++;
    public void ReleaseSkillPreview()
        => _skillPreviewHold = Mathf.Max(0, _skillPreviewHold - 1);
    public void ClearSkillPreview()
    {
        if (_skillPreviewHold == 0)
            skillHighlighter?.ClearTransient();

        StatusPanel?.ClearHighlights();
    }
    public void ClearAllPreviews()
    {
        ClearMovePreview();
        ClearSkillPreview(); // (hold 중이면 지워지지 않음)
        StatusPanel?.ClearHighlights();
    }

    // === 지속(토큰) 스킬 프리뷰 API ===
    public int CreateSkillPreviewToken() => skillHighlighter != null ? skillHighlighter.CreateGroup() : 0;

    public void SetSkillPreviewForToken(int token, Tilemap map, IEnumerable<Vector3Int> cells)
        => skillHighlighter?.SetGroupCells(token, map, cells);

    public void ClearSkillPreviewToken(int token)
        => skillHighlighter?.ClearGroup(token);


    bool TryGetFrontCellOfTarget(BattleUnit caster, BattleUnit target, out Vector3Int frontCell)
    {
        frontCell = target != null ? target.Cell : default;
        if (target == null || caster == null) return false;

        var targetMap = target.CurrentMap;
        var casterMap = caster.CurrentMap ?? targetMap;

        // --- 1) 좌우 우선 규칙 ---
        var baseCell = target.Cell;
        int dx = caster.Cell.x - target.Cell.x;

        if (dx < 0)
        {
            // 캐스터가 타깃의 '왼쪽'에 있음 → 서쪽 이웃 고정
            frontCell = new Vector3Int(baseCell.x - 1, baseCell.y, baseCell.z);
            return true;
        }
        else if (dx > 0)
        {
            // 캐스터가 타깃의 '오른쪽'에 있음 → 동쪽 이웃 고정
            frontCell = new Vector3Int(baseCell.x + 1, baseCell.y, baseCell.z);
            return true;
        }

        // --- 2) 같은 컬럼(수직 정렬)일 때만 기존 각도 기반 선택 폴백 ---
        // 타겟→시전자 월드 방향
        Vector3 targetW = targetMap.GetCellCenterWorld(target.Cell);
        Vector3 casterW = casterMap.GetCellCenterWorld(caster.Cell);
        Vector2 aimDir = (Vector2)(casterW - targetW);
        if (aimDir.sqrMagnitude < 1e-6f) return false;
        aimDir.Normalize();

        bool oddCol = SkillLibrary.IsOddColumn(baseCell);

        // odd-q 이웃 집합(프로젝트에서 쓰는 체계 그대로)
        Vector3Int[] neighOffsetsEven = {
        new Vector3Int(+1, 0, 0), new Vector3Int( 0,+1,0),
        new Vector3Int(-1,+1, 0), new Vector3Int(-1, 0,0),
        new Vector3Int(-1,-1, 0), new Vector3Int( 0,-1,0)
    };
        Vector3Int[] neighOffsetsOdd = {
        new Vector3Int(+1, 0, 0), new Vector3Int(+1,+1,0),
        new Vector3Int( 0,+1, 0), new Vector3Int(-1, 0,0),
        new Vector3Int( 0,-1, 0), new Vector3Int(+1,-1,0)
    };
        var candidates = oddCol ? neighOffsetsOdd : neighOffsetsEven;

        float bestDot = float.NegativeInfinity;
        float bestDist2 = float.PositiveInfinity;
        const float EPS = 1e-5f;
        Vector3Int best = baseCell;

        foreach (var off in candidates)
        {
            var neigh = new Vector3Int(baseCell.x + off.x, baseCell.y + off.y, baseCell.z);
            var neighW = targetMap.GetCellCenterWorld(neigh);

            Vector2 dir = (Vector2)(neighW - targetW);
            if (dir.sqrMagnitude < 1e-6f) continue;
            dir.Normalize();

            float d = Vector2.Dot(aimDir, dir);
            float dist2 = ((Vector2)(neighW - casterW)).sqrMagnitude;

            if (d > bestDot + EPS || (Mathf.Abs(d - bestDot) <= EPS && dist2 < bestDist2))
            {
                bestDot = d;
                bestDist2 = dist2;
                best = neigh;
            }
        }

        frontCell = best;
        return true;
    }
    Vector3 GetCellRightEdgeWorld(Tilemap map, Vector3Int cell, float margin = 0.02f)   // 점프가 실행되는 좌표의 오른쪽 끝으로 이동
    {
        if (map == null) return Vector3.zero;

        // 기준점: 셀 중심
        var center = map.GetCellCenterWorld(cell);

        // 그리드/셀 크기
        var grid = map.layoutGrid != null ? map.layoutGrid : map.GetComponentInParent<Grid>();
        var cellSize = (grid != null) ? grid.cellSize : Vector3.one;

        // "오른쪽" 방향(그리드 회전 고려)으로 반 셀 + 여유 마진만큼 이동
        // margin은 경계선 살짝 안쪽으로 밀어넣어 스프라이트가 튀어나오지 않게 함
        Vector3 rightDir = (grid != null) ? grid.transform.right : Vector3.right;
        return center + rightDir * (cellSize.x * 0.5f - margin);
    }

    // 유틸 래퍼 추가
    IEnumerator Co_ResolveUnitThenFlag(SkillAsset skill, BattleUnit caster, BattleUnit target, System.Action done)
    {
        yield return skill.ResolveOnUnit(this, caster, target);
        done?.Invoke();
    }
    IEnumerator Co_ResolveTileThenFlag(SkillAsset skill, Tilemap map, Vector3Int cell, BattleUnit caster, System.Action done)
    {
        yield return skill.ResolveOnTile(this, map, cell, caster);
        done?.Invoke();
    }
    IEnumerator FallbackProjectile(Vector3 start, Vector3 end, float time, System.Action done)
    {
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime / Mathf.Max(0.01f, time); yield return null; }
        done?.Invoke();
    }
}
