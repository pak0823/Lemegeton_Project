using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum BattleState { Idle, ActionSelect, Moving, Targeting, Resolving, TargetingKnockback, EndTurn }
public enum BattleAction { Move, Attack, Rest, Calm }

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
    bool _isResolvingSelfCast = false;    // Self-cast 재진입 가드

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
    public bool IsKnockbackTargeting => state == BattleState.TargetingKnockback;
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
    public event System.Action<BattleUnit, string> OnUnitPassiveLabel;
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
    private Tilemap customPreviewMap;
    private HashSet<Vector3Int> customPreviewCells;
    public BattleUnit ActingUnit => acting;


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
    public event System.Action<BattleUnit> OnUnitEndTurn;

    [Header("Skill Runtime")]
    public bool isSelectingSkill = false;          // 스킬 선택 패널이 열렸는지
    public SkillDefinition currentSkill;           // 현재 선택된 스킬(선택 전이면 id 미정)
    public Vector3Int selectedCell;                // 타일 스킬용 내부 커서
    public SkillAsset currentSkillSO;                   // 현재 선택된 SO 스킬
    public event System.Action<SkillAsset[]> OnSkillPanelPopulateSO; // SO 목록 UI용

    // UI와 통신용 이벤트
    public event System.Action<bool> OnSkillPanelToggled;  // true=열기/false=닫기
    public event System.Action<string> OnHint;   // UI에 간단한 안내 문구 전달                  
    public event System.Action<BattleUnit> OnUnitTurnLabel;// 유닛 턴 시작 라벨용
    public event System.Action<BattleUnit, string> OnUnitActionLabel; //유닛 스킬 표시용

    // ParametricDamage 전용
    //Knockback
    ParametricDamageSkill _pendingKnockbackSkill;
    BattleUnit _pendingKnockbackTarget;
    Vector3Int _pendingKnockbackDest;
    //skill extra move
    bool _isPostSkillMoveInProgress = false;

    [Header("DBs")]
    [SerializeField] private StateStatModifierDB stateStatDb;


    [Header("Training")]
    public TrainingDB trainingDB;   // 인스펙터로 TrainingDB 할당
    public TrainingDB Training => trainingDB;

    [Header("Projectile/VFX")]
    public GameObject projectilePrefab;     // 투사체

    //점프 애니메이션 속도 및 높이 값
    float jumpDuration = 0.08f;     // 시간 기반
    float jumpArc = 0.2f;

    public static void ClearStatic()
    {
        OnAnyUnitTurnStarted = null;
    }

    public void EmitActionLabel(BattleUnit u, string label) => OnUnitActionLabel?.Invoke(u, label);
    public void EmitPassiveLabel(BattleUnit u, string label) => OnUnitPassiveLabel?.Invoke(u, label);
    public void EmitTurnLabel(BattleUnit u) => OnUnitTurnLabel?.Invoke(u);
    #endregion

    UnitStatusPanelUI StatusPanel
    {
        get
        {
            if (_statusPanel == null) _statusPanel = FindObjectOfType<UnitStatusPanelUI>();
            return _statusPanel;
        }
    }

    [System.Serializable]
    public class BeastDomainZone
    {
        public BattleUnit owner;      // 영역을 만든 유닛
        public Tilemap map;          // 영역이 올라간 타일맵
        public Vector3Int center;    // 생성 시 중심 셀
        public int radius;           // 헥사 거리 반경
        public int remainingTurns;   // 남은 턴 (owner의 차례 기준)

        // 이 영역을 표현하는 Highlighter 그룹 토큰(임시)
        public int highlightToken;
    }
    List<BeastDomainZone> _beastZones = new List<BeastDomainZone>();
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
        var battleUnit = FindObjectsOfType<BattleUnit>(true).ToList();

        float minAGI = battleUnit.Min(unit => unit.EffectiveAGI);
        float maxAGI = battleUnit.Max(unit => unit.EffectiveAGI);
        
        foreach (var unit in battleUnit)
        {
            var map = (unit.team == Team.Player) ? provider.PlayerFloor : provider.EnemyFloor;
            var cell = map.WorldToCell(unit.transform.position);

            unit.Bind(map, cell);
            grid.SetOccupied(unit.team, unit.Cell, true);
            unit.InitializeATB(minAGI, maxAGI);
            unit.InitPassives(this);

            unit.OnDied -= HandleUnitDied;
            unit.OnDied += HandleUnitDied;
        }

        _lastMinAGI = minAGI;
        _lastMaxAGI = maxAGI;
        _lastAGISum = battleUnit.Sum(unit => unit.EffectiveAGI);
        _lastAGICount = battleUnit.Count;

        // 상태가 바뀌면 ATB 재계산
        foreach (var unit in battleUnit)
        {
            var sc = unit.GetComponent<StatusController>();
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
        EmitActionLabel(null, "");   // 전면 리셋 시 라벨도 초기화
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
        EmitActionLabel(null, "");
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
    void StartTurn(BattleUnit _unit)
    {
        if (_unit == null) return;
        acting = _unit;
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        ClearAllPreviews();
        ClearTargetSelection();
        OnAnyUnitTurnStarted?.Invoke(_unit);
        EmitActionLabel(null, "");
        OnHint?.Invoke(string.Empty);
        EmitTurnLabel(_unit);

        Debug.Log($"[Battle] StartTurn -> {acting.name}");

        var sc = _unit.GetComponent<StatusController>();
        var usc = _unit.GetComponent<UnitStateController>();


        // 이 턴 시작 시점에 Fear가 있었는지 먼저 체크
        bool hadFear = (usc != null && usc.Has(UnitStateId.Fear));

        if (sc != null) sc.OnTurnStart();
        if (usc != null) usc?.OnTurnStart();

        var ambushSkill = GetAmbushSkillFor(_unit);
        if (ambushSkill != null)
        {
            TryApplyAmbushTurnStartHeal(_unit, ambushSkill);
        }

        // 공포 상태 처리: 다른 행동보다 우선 적용
        if (hadFear)
        {
            Debug.Log($"[Fear] {_unit.name} 공포 상태 턴 시작 → 강제 후퇴 진행");
            state = BattleState.Resolving;
            remainingActions = 0;
            usedActions.Clear();

            StartCoroutine(Co_HandleFearTurn(_unit));
            return; // 여기서 턴 로직 종료 (이동/공격 선택 화면 안 뜸)
        }


        TickBeastDomainOnTurnStart(_unit);

        // 캐스팅 성공 턴 소비 처리
        if (_unit.team == Team.Enemy)
        {
            var ecs = _unit.GetComponent<EnemyCastState>();
            if (ecs != null && ecs.TryTakeReady(out var pending))   // 준비된 캐스팅 성공 확인
            {
                // 적 행동 루틴 대신, '웹 발사→생성→소비' 코루틴 실행
                StartCoroutine(Co_EnemyFireWebThenConsume(_unit, pending));
                return; // EnemyTurnRoutine 시작하지 않음
            }
        }

        // 모든 ATB 정지
        atbPaused = true;

        if (_unit.team == Team.Player)
        {
            state = BattleState.ActionSelect; // 플레이어 입력 허용
            //Debug.Log($"[PlayerTurn] {unit.name} 턴 시작 → ATB 정지");
        }
        else
        {
            state = BattleState.Resolving; // 입력 잠금
            //Debug.Log($"[EnemyTurn] {unit.name} 턴 시작 → ATB 정지");
            StartCoroutine(EnemyTurnRoutine(_unit));
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
    public void OnClickRest()
    {
        if (acting == null || !IsPlayerTurn) return;
        if (remainingActions <= 0) return;

        // 프리뷰/타겟팅 정리
        ClearAllPreviews();
        ClearTargetSelection();

        // 휴식: 최대 HP의 10% 회복
        float before = acting.HP;
        acting.HealPercent(0.10f);
        float after = acting.HP;

        if (after > before)
        {
            Debug.Log($"[Rest] {acting.name} 휴식: HP {before} -> {after} (+{after - before})");
        }
        else
        {
            Debug.Log($"[Rest] {acting.name} 휴식: 회복 없음 (이미 최대 체력)");
        }

        // 행동 1회 소비 → 지금 구조에서는 baseActionsPerTurn=1이라 사실상 턴 종료와 동일
        OnActionConsumed(BattleAction.Rest);
    }
    public void OnClickCalm()
    {
        if (acting == null || !IsPlayerTurn) return;
        if (remainingActions <= 0) return;

        ClearAllPreviews();
        ClearTargetSelection();

        float maxMP = acting.MaxMP;
        float maxRage = acting.MaxRage;

        float beforeMP = acting.MP;
        float beforeRage = acting.Rage;

        // MP 회복량: MaxMP의 10% (내림, 최소 1 보장)
        int mpGain = Mathf.FloorToInt(maxMP * 0.10f);
        if (mpGain <= 0 && maxMP > 0f)
            mpGain = 1;

        // Rage가 0이어도 최소 MP 회복 0.1 보장
        if (acting.Rage <= 0f)
        {
            acting.GainMP(mpGain);
            Debug.Log($"[Calm] {acting.name} Rage=0 → 최소 회복 MP +0.1");
            OnActionConsumed(BattleAction.Calm);
            return;
        }

        if (mpGain > 0)
        {
            acting.GainMP(mpGain);   // GainMP 안에서 클램프 수행
        }

        float afterMP = acting.MP;

        if (afterMP > beforeMP)
        {
            Debug.Log($"[Calm] {acting.name} 진정: MP {beforeMP} -> {afterMP} (+{afterMP - beforeMP})");
        }
        else
        {
            Debug.Log($"[Calm] {acting.name} 진정: MP 회복 없음 (이미 최대 MP이거나 Gain=0)");
        }

        // 최대 Rage의 10%"를 목표로 사용
        // 현재 Rage가 그보다 적으면 '있던 Rage 전부 소모'
        float rageCostTarget = maxRage * 0.10f;

        // MaxRage가 0이거나 음수면, 현재 Rage 전체를 소모 대상으로 본다 (안전 폴백)
        if (rageCostTarget <= 0f)
            rageCostTarget = beforeRage;

        float spend = Mathf.Min(beforeRage, rageCostTarget);   // 실제로 쓸 Rage 양

        if (spend > 0f)
        {
            acting.AddRage(-spend);    // AddRage 헬퍼 사용 (로그 + 클램프) 
        }

        float afterRage = acting.Rage;

        Debug.Log(
            $"[Calm] {acting.name} 진정: Rage {beforeRage:F2} -> {afterRage:F2} " +
            $"(소모 {spend:F2} / 목표 {rageCostTarget:F2}, MaxRage={maxRage:F2})"
        );

        OnActionConsumed(BattleAction.Calm);
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
                if (_isPostSkillMoveInProgress)
                {
                    // 스킬 사용 후 추가 이동
                    StartCoroutine(Co_MoveAfterSkillThenConsume(acting, clickedMap, clickedCell));
                }
                else
                {
                    // 일반 이동
                    StartCoroutine(Co_MoveThenConsume(acting, clickedMap, clickedCell, BattleAction.Move));
                }
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
        else if (state == BattleState.TargetingKnockback)
        {
            if (_knockbackMap == null || _knockbackCandidates == null || _onKnockbackSelected == null)
                return;

            if (clickedMap != _knockbackMap) return;
            if (!_knockbackCandidates.Contains(clickedCell)) return;

            // 선택 완료
            var cb = _onKnockbackSelected;
            EndKnockbackSelection();

            state = BattleState.Resolving;
            cb?.Invoke(clickedCell);
            return;
        }
    }

    IEnumerator Co_MoveThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell, BattleAction act)
    {
        if (unit == null || map == null) yield break;

        Vector3Int fromCell = unit.Cell;

        // 실제로 이동(애니메이션 + 점유 갱신)
        grid.SetOccupied(unit.team, fromCell, false);
        yield return unit.AnimateMoveTo(map, toCell);
        grid.SetOccupied(unit.team, unit.Cell, true);

        // 야수의 영역 안에서의 이동인지 체크
        bool freeMove = IsBeastDomainFreeMove(unit, map, fromCell, toCell);

        if (freeMove)
        {
            Debug.Log($"[BeastDomain] {unit.name} 야수의 영역 안 이동: 행동 토큰 소비 없음");

            if (unit.team == Team.Player)
            {
                state = BattleState.ActionSelect;
                EmitActionLabel(unit, "");
            }
            yield break;
        }

        // 평소처럼 이동 행동 1회 소비
        OnActionConsumed(act);
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

        bool isAlly = (acting != null && target.team == acting.team);
        bool allowAlly = (currentSkillSO is AllyRetreatSwapSkill);

        // 이 스킬이 아니면 아군은 대상 불가
        if (isAlly && !allowAlly) return;

        // 적을 대상으로 하는 스킬인데 아군이면 막기
        if (!isAlly && allowAlly) return; // (원한다면 아군 전용으로 제한)

        ConfirmSkillOnUnit(target);
    }
    #endregion

    #region Action Consumption
    void OnActionConsumed(BattleAction act)
    {
        if (acting == null)
        {
            // 이미 HandleUnitDied/Retreat에서 턴/ATB 정리 끝난 상태이므로
            return;
        }

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

    public IEnumerable<BattleUnit> GetLivingAlliesOf(BattleUnit unit)   //살아 있는 아군 유닛 확인
    {
        if (unit == null) yield break;

        var all = FindObjectsOfType<BattleUnit>();
        foreach (var u in all)
        {
            if (u == null) continue;
            if (u.IsDead || u.IsRetreated) continue;
            if (u.team != unit.team) continue;

            yield return u;
        }
    }

    public IEnumerable<BattleUnit> GetLivingEnemiesOf(BattleUnit _battleunit)   //살아 있는 적 유닛 확인
    {
        if (_battleunit == null) yield break;

        // 씬에 존재하는 모든 유닛 기준으로 계산
        var currentUnit = FindObjectsOfType<BattleUnit>();

        foreach (var units in currentUnit)
        {
            if (units == null) continue;
            if (units == _battleunit) continue;
            if (units.team == _battleunit.team) continue;
            if (units.IsDead || units.IsRetreated) continue;

            yield return units;
        }
    }

    // 플레이어 턴 종료 처리
    void EndPlayerTurn()
    {
        ClearAllPreviews();
        ClearTargetSelection();

        // ATB 재개(다음 턴은 Update()가 자동 감지)
        OnUnitEndTurn?.Invoke(acting);
        acting.ResetATB(); // ATB와 Overfill 함께 초기화
        acting.TickAllCooldowns();  //재사용 턴 수 감소
        acting = null;
        atbPaused = false;
        state = BattleState.Idle;
    }

    public void CancelCurrentAction()
    {
        // 넉백 타겟팅 취소 처리 우선
        if (state == BattleState.TargetingKnockback && currentSkillSO != null)
        {
            // 코루틴 깨우기: null 선택으로 알림
            if (_onKnockbackSelected != null)
            {
                var cb = _onKnockbackSelected;
                EndKnockbackSelection();   // 내부 필드 정리 + 프리뷰 클리어
                ClearTargetSelection();
                cb(null);                  // done = true, chosen = null
            }
            
            currentSkillSO = null;
            currentSkillTargetMap = null;
            customPreviewCells = null;
            customPreviewMap = null;
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            if (!isSelectingSkill) OpenSkillPanel();
            return;
        }
        // 타겟팅 중(스킬 선택됨) → '스킬만 해제', 패널은 유지
        if (state == BattleState.Targeting && currentSkillSO != null)
        {
            ClearSkillPreview();
            ClearTargetSelection();
            currentSkillSO = null;
            currentSkillTargetMap = null;
            customPreviewCells = null;
            customPreviewMap = null;
            state = BattleState.ActionSelect;
            UpdateTargetingHint();
            if (!isSelectingSkill) OpenSkillPanel();
            return;
        }
        if (state == BattleState.Moving)
        {
            // 스킬 후 보너스 이동을 취소했을 때
            if (_isPostSkillMoveInProgress)
            {
                ClearMovePreview();
                ClearTargetSelection();
                customPreviewCells = null;
                customPreviewMap = null;

                _isPostSkillMoveInProgress = false;

                // 이동은 하지 않고, 스킬은 이미 사용했으므로
                // 공격 토큰을 소비하고 턴을 진행
                OnActionConsumed(BattleAction.Attack);
                return;
            }

            ClearMovePreview();
            ClearTargetSelection();
            customPreviewCells = null;
            customPreviewMap = null;
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
        var all = FindObjectsOfType<BattleUnit>();

        // 현재 선택된 스킬이 AllyRetreatSwapSkill이면 → "아군 리스트"로 타겟 사이클 구성
        if (currentSkillSO is AllyRetreatSwapSkill && acting != null)
        {
            targetCycle = all
                .Where(u => u.team == acting.team          // 같은 팀(아군)
                            && u != acting                 // 자기 자신 제외(원하면 포함도 가능)
                            && !u.IsDead)
                .OrderByDescending(u => u.EffectiveAGI)
                .ToList();
        }
        else
        {
            // 그 외 스킬은 기존처럼 "적 리스트"
            if (acting != null)
            {
                targetCycle = all
                    .Where(u => u.team != acting.team && !u.IsDead)
                    .OrderByDescending(u => u.EffectiveAGI)
                    .ToList();
            }
            else
            {
                targetCycle = all
                    .Where(u => u.team == Team.Enemy && !u.IsDead)
                    .OrderByDescending(u => u.EffectiveAGI)
                    .ToList();
            }
        }
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

        // AllyRetreatSwapSkill는 유닛 확정 시에도 전용 플로우 사용
        if (currentSkillSO is AllyRetreatSwapSkill)
        {
            ConfirmSkillOnUnit(selectedTarget);
            return;
        }

        var skill = currentSkillSO;
        bool doGapClose = skill.ShouldGapCloseToTarget(acting, selectedTarget);

        StartCoroutine(Co_GapCloseThenResolveOnTargetSO(skill, acting, selectedTarget, doGapClose));
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

        // 중복 구독 방지
        dead.OnDied -= HandleUnitDied;
        EmitActionLabel(dead, "");   // 라벨 비우기

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

    bool IsAmbushHiddenTarget(BattleUnit u)
    {
        if (!u) return false;
        var usc = u.GetComponent<UnitStateController>();
        if (usc == null) return false;

        // 잠복 상태면 적이 타겟팅할 수 없음
        return usc.Has(UnitStateId.Ambush);
    }
    SelfAmbushSkill GetAmbushSkillFor(BattleUnit unit)
    {
        if (unit == null || unit.data == null || unit.data.skills == null)
            return null;

        foreach (var s in unit.data.skills)
        {
            if (s is SelfAmbushSkill ambush)
                return ambush;
        }
        return null;
    }

    void TryApplyAmbushTurnStartHeal(BattleUnit unit, SelfAmbushSkill skill)
    {
        if (unit == null || skill == null) return;

        var usc = unit.GetComponent<UnitStateController>();
        if (usc == null || !usc.Has(UnitStateId.Ambush))
            return;

        int route = unit.GetTrainingRouteIndex(skill);

        if (!skill.trainingHealOnTurnStart ||
            skill.routeForHealOnTurnStart < 0 ||
            route != skill.routeForHealOnTurnStart)
            return;

        int amount = skill.ComputeTurnStartHeal(unit);
        if (amount <= 0) return;

        float before = unit.HP;
        unit.Heal(amount);
        float after = unit.HP;

        if (after > before)
        {
            Debug.Log($"[Ambush] Route(Heal={skill.routeForHealOnTurnStart}): {unit.name} 턴 시작 회복 +{after - before} (healPerClv={skill.healPerClv})");
        }
    }

    #region Enemy AI
    IEnumerator EnemyTurnRoutine(BattleUnit enemy)
    {
        yield return new WaitForSeconds(0.5f); // 살짝 텀

        // 생존 플레이어 수집
        var players = FindObjectsOfType<BattleUnit>()
            .Where(u => u.team == Team.Player && !u.IsDead && !IsAmbushHiddenTarget(u))
            .ToList();

        // 대상 지정 가능 플레이어가 아무도 없으면 이번 적 턴은 할 대상이 없다고 보고 종료
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
        if (enemy == null)
        {
            Debug.LogWarning("[Battle] EndEnemyTurn called with null enemy");
            atbPaused = false;
            state = BattleState.Idle;
            return;
        }

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
        OnUnitEndTurn?.Invoke(enemy);
        enemy.TickAllCooldowns();

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
        int percent = Mathf.FloorToInt(successChance01 * 100f);

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
        Debug.Log($"[BattleManager] SelectSkill({index}) 호출");

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
        int effectiveCost = skill.GetEffectiveMpCost(acting);
        if (!acting.HasMP(effectiveCost))
        {
            Debug.Log($"[Skill] MP 부족: {skill.displayName} (필요 {effectiveCost})");
            return; // 타겟팅 진입 안 함
        }

        // 쿨다운 사전 차단
        if (acting.IsSkillOnCooldown(skill))
        {
            Debug.Log($"[Skill] 쿨다운: {skill.displayName} (남은 턴 {acting.GetCooldownRemaining(skill)})");
            return;
        }

        if (skill is ISelfCastSkill self && self.SelfCastOnSelect)
        {
            if (_isResolvingSelfCast)
            {
                Debug.LogWarning($"[SelfCast] 이미 처리 중인 self-cast 스킬입니다. 중복 SelectSkill 무시: {skill.name}");
                return;
            }

            _isResolvingSelfCast = true;

            // 실제 MP 소비는 SelfStateSkill.ResolveOnUnit 내부에서 처리
            bool isFreeAction = false;

            // 이 스킬(=legacy 그룹)에 대해 선택된 훈련 루트
            int route = acting.GetTrainingRouteIndex(skill);

            // Route 2 + 해당 스킬이 "무료턴 사용" 옵션 켜져 있으면 무료턴으로 처리
            if (route == 2)
            {
                if (skill is SelfStateSkill sss && sss.trainingFreeActionOnRoute2)
                    isFreeAction = true;
                else if (skill is SelfStateCleanseSkill scs && scs.trainingFreeActionOnRoute2)
                    isFreeAction = true;
            }

            // HostilitySpikeSkill: routeForFreeAction 기반
            if (!isFreeAction && skill is HostilitySpikeSkill hss)
            {
                if (hss.trainingUseFreeAction &&
                    hss.routeForFreeAction >= 0 &&
                    route == hss.routeForFreeAction)
                {
                    isFreeAction = true;
                }
            }

            // SelfBeastDomainSkill: routeForFreeAction 기반
            if (!isFreeAction && skill is SelfBeastDomainSkill bds)
            {
                if (bds.trainingUseFreeAction &&
                    bds.routeForFreeAction >= 0 &&
                    route == bds.routeForFreeAction)
                {
                    isFreeAction = true;
                }
            }

            // 코루틴으로 처리해서, 끝난 뒤 무료/일반 행동을 분기
            StartCoroutine(Co_ResolveSelfCastThenFinish(skill, acting, isFreeAction));
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

            if (skill is ISkillCustomPreview customPrev)
            {
                var map = customPrev.GetTargetMap(this, acting) ?? currentSkillTargetMap;
                var cells = customPrev.GetPreviewCells(this, acting);

                currentSkillTargetMap = map;
                customPreviewMap = map;
                customPreviewCells = (cells != null) ? new HashSet<Vector3Int>(cells) : new HashSet<Vector3Int>();

                if (map != null && customPreviewCells != null && customPreviewCells.Count > 0)
                    ShowSkillPreview(map, customPreviewCells);
            }
            else
            {
                customPreviewCells = null;
                customPreviewMap = null;

                //if (customPreviewCells != null && customPreviewCells.Count > 0)
                //{
                //    // 커스텀 프리뷰 잠금 상태에서는 호버 프리뷰를 갱신하지 않음
                //    return;
                //}

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
    }

    private void UpdateTargetingHint()
    {
        // 스킬이 확정되어 타게팅 상태일 때만 힌트 노출
        if (state == BattleState.Targeting && currentSkillSO != null)
        {
            if(currentSkillSO)

            if (currentSkillSO.targetMode == SkillTargetMode.Tile)
                OnHint?.Invoke("위치를 선택하세요");
            else
                OnHint?.Invoke("대상을 선택하세요");
        }
        else
            OnHint?.Invoke(string.Empty);
    }

    // 현재 선택된 스킬의 범위를 "유닛 기준"으로 미리보기
    public void PreviewSkillAreaOnUnit(BattleUnit unit)
    {
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) { ClearAllPreviews(); return; }
        if (unit == null) { ClearAllPreviews(); return; }
        if (!(currentSkillSO is AllyRetreatSwapSkill) && acting != null && unit.team == acting.team) // 적 대상 스킬일 때만 아군 프리뷰 금지
        {
            ClearAllPreviews();
            return;
        }

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
        if (customPreviewCells != null) // 커스텀 프리뷰(후보 잠금)가 활성화되어 있다면,
            return;                    // 호버 기반 프리뷰 갱신을 전면 차단


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

        bool isAlly = (acting != null && target.team == acting.team);
        bool allowAlly = (currentSkillSO is AllyRetreatSwapSkill);

        // 플레이어 턴에서 아군을 대상으로 하려면, 이 스킬일 때만 허용
        if (IsPlayerTurn && isAlly && !allowAlly) return;

        // 미리보기 정리
        ClearSkillPreview();

        var skill = currentSkillSO;
        if (skill == null) return;

        bool doGapClose = skill.ShouldGapCloseToTarget(acting, target);

        // 아군 교대/후퇴 스킬이면, 먼저 후퇴 후보 칸부터 선택
        if (skill is AllyRetreatSwapSkill allySkill)
        {
            // 자기 자신은 타겟 불가
            if (target == acting)
                return;

            // 반드시 아군만 대상으로 사용
            if (target.team != acting.team)
                return;

            // target(아군)이 뒤로 물러날 수 있는 칸 계산
            var candidates = allySkill.GetRetreatCandidates(this, target).ToList();

            // 후보가 없으면: 스킬 사용 불가, 바로 리턴 (행동 소비 X)
            if (candidates == null || candidates.Count == 0)
            {
                OnHint?.Invoke("후퇴할 수 있는 칸이 없습니다.");
                return;
            }

            // 후보가 1개 이상이면 후퇴 타일 선택 모드 진입
            StartCoroutine(Co_SelectAllyRetreatThenResolve(allySkill, acting, target, candidates));
            return;
        }

        // === ParametricDamageSkill + 넉백 훈련 루트라면, 넉백 방향 먼저 선택 ===
        if (skill is ParametricDamageSkill dmgSkill)
        {
            int route = acting.GetTrainingRouteIndex(dmgSkill);
            if (dmgSkill.trainingUseKnockback &&
                dmgSkill.routeForKnockback >= 0 &&
                route == dmgSkill.routeForKnockback)
            {
                // 넉백 후보 계산 (최대 2칸)
                var candidates = dmgSkill.GetKnockbackCandidates(this, acting, target);
                if (candidates != null && candidates.Count > 0)
                {
                    StartCoroutine(Co_SelectKnockbackThenResolve(dmgSkill, acting, target, candidates, doGapClose));
                    return;
                }
                // 후보가 전혀 없으면 그냥 평소처럼 공격만 수행
            }
        }

        StartCoroutine(Co_GapCloseThenResolveOnTargetSO(skill, acting, target, doGapClose));
    }
    public void ConfirmSkillOnTile(Tilemap map, Vector3Int originCell)
    {
        if (!IsPlayerTurn || acting == null || map == null) return;
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Tile) return;

        // 커스텀 프리뷰가 잠금돼 있다면: '후보 안'에서만 확정 가능
        if (customPreviewCells != null)
        {
            if (!customPreviewCells.Contains(originCell))
            {
                if (customPreviewCells.Count == 0) return;                // 후보 없음 → 아무 것도 하지 않음
                if (!customPreviewCells.Contains(originCell)) return;      // 후보 밖 클릭 → 무시
            }
            else if (currentSkillSO is ISkillCustomPreview cp)
            {
                var targetMap = (currentSkillSO as ITargetMapProvider)?.GetTargetMap(this, acting) ?? map;
                var candidates = cp.GetPreviewCells(this, acting);
                var set = (candidates != null) ? new HashSet<Vector3Int>(candidates) : new HashSet<Vector3Int>();
                if (set.Count == 0 || !set.Contains(originCell)) return;   // 유효 후보가 아니면 종료 금지
            }
        }

        ClearSkillPreview();
        if (currentSkillSO is IInstantTileSkill)
        {
            // === ParametricDirectionSkill의 Route2 무료턴 처리 여부 판단 ===
            bool freeAction = false;
            if (currentSkillSO is ParametricDirectionSkill dirSkill && acting != null)
            {
                int route = acting.GetTrainingRouteIndex(dirSkill);
                if (route == 2 && dirSkill.trainingFreeActionOnRoute2)
                    freeAction = true;
            }

            // 커스텀 프리뷰 잠금 해제
            customPreviewCells = null;
            customPreviewMap = null;

            int cost = currentSkillSO.GetEffectiveMpCost(acting);
            if (cost > 0 && !acting.TryConsumeMP(cost))
                return;

            if (!freeAction)
            {
                // 기본: 행동 1회 소비(공격으로 간주)
                StartCoroutine(Co_ResolveTileThenFlag(currentSkillSO, map, originCell, acting, () =>
                {
                    acting.ApplyCooldown(currentSkillSO);
                    FinishActionAfterSkill();
                }));
            }
            else
            {
                // === 무료 행동 경로 ===
                StartCoroutine(Co_ResolveTileThenFlag(currentSkillSO, map, originCell, acting, () =>
                {
                    acting.ApplyCooldown(currentSkillSO);

                    // 행동 토큰은 그대로, UI/상태만 정리
                    ClearSkillPreview();
                    CloseSkillPanel();

                    currentSkill = default;
                    currentSkillSO = null;
                    currentSkillTargetMap = null;
                    customPreviewCells = null;
                    customPreviewMap = null;
                    UpdateTargetingHint();

                    if (IsPlayerTurn)
                        state = BattleState.ActionSelect; // 같은 턴에서 다른 행동 이어서 가능
                }));
            }

            //// 무연출 즉시 해결 경로 (MP 차감은 스킬 내부에서 수행)
            //StartCoroutine(Co_ResolveTileThenFlag(currentSkillSO, map, originCell, acting, () => { acting.ApplyCooldown(currentSkillSO); FinishActionAfterSkill(); }));
        }
        else
        {
            // 기존: 투사체/공격 연출 경로
            StartCoroutine(Co_ProjectileSkillThenFinishSO(currentSkillSO, map, originCell, acting));
        }
    }

    IEnumerator Co_GapCloseThenResolveOnTargetSO(
        SkillAsset skill,
        BattleUnit caster,
        BattleUnit target,
        bool doGapClose)
    {
        if (skill == null || caster == null || target == null)
            yield break;

        var originalW = caster.transform.position;

        // 필요하면 대상 앞으로 점프(gap close)
        if (doGapClose && TryGetFrontCellOfTarget(caster, target, out var frontCell))
        {
            var mapForJump = target.CurrentMap ?? provider.EnemyFloor;
            Vector3 frontW = GetCellRightEdgeWorld(mapForJump, frontCell, 0.02f);
            yield return caster.AnimateJumpToWorld(frontW, jumpDuration, null, jumpArc);
        }

        bool resolved = false;

        // 임팩트 타이밍에 스킬 처리 (기존 로직 재사용)
        void OnImpact()
        {
            caster.OnAttackImpact -= OnImpact;
            StartCoroutine(Co_ResolveUnitThenFlag(skill, caster, target, () => { resolved = true; }));
        }

        caster.OnAttackImpact += OnImpact;

        // 공격/시전 모션 (점프를 안 쓰더라도 근접 모션은 그대로 쓸 수 있음)
        yield return caster.AnimateAttack(target);

        // 혹시 애니에서 임팩트 이벤트가 안 들어온 경우 대비해 타임아웃 처리 등 기존 코드 유지
        float timeout = 0.35f;
        while (!resolved && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        caster.OnAttackImpact -= OnImpact;

        // 폴백 처리: 임팩트 이벤트를 못 받았으면 여기서 직접 ResolveOnUnit 실행
        if (!resolved)
        {
            // 애니메이션 이벤트 누락 등 안전망
            yield return skill.ResolveOnUnit(this, caster, target);
        }

        caster.ApplyCooldown(skill);

        // 원위치 복귀
        caster.transform.position = originalW;

        FinishActionAfterSkill();
    }

    // 턴/행동 토큰/스킬 패널에 영향을 주지 않는 "무료 반응 공격"으로 동작
    public Coroutine StartReactiveAttack(BattleUnit caster, BattleUnit target, SkillAsset skill, bool doGapClose)
    {
        if (caster == null || target == null || skill == null) return null;
        return StartCoroutine(Co_ReactiveGapCloseThenResolveOnTargetSO(skill, caster, target, doGapClose));
    }

    IEnumerator Co_ReactiveGapCloseThenResolveOnTargetSO(
        SkillAsset skill,
        BattleUnit caster,
        BattleUnit target,
        bool doGapClose)
    {
        if (skill == null || caster == null || target == null)
            yield break;

        var originalW = caster.transform.position;

        // 필요하면 대상 앞으로 점프
        if (doGapClose && TryGetFrontCellOfTarget(caster, target, out var frontCell))
        {
            var mapForJump = target.CurrentMap ?? provider.EnemyFloor;
            var frontW = GetCellRightEdgeWorld(mapForJump, frontCell, 0.02f);
            yield return caster.AnimateJumpToWorld(frontW, jumpDuration, null, jumpArc);
        }

        bool resolved = false;

        void OnImpact()
        {
            caster.OnAttackImpact -= OnImpact;
            StartCoroutine(Co_ResolveUnitThenFlag(skill, caster, target, () => { resolved = true; }));
        }

        caster.OnAttackImpact += OnImpact;

        // 공격 모션 (애니메이션 이벤트가 있으면 그 타이밍에 Resolve)
        yield return caster.AnimateAttack(target);

        caster.OnAttackImpact -= OnImpact;

        // 폴백: 임팩트 이벤트를 못 받았으면 애니 끝난 시점에 바로 처리
        if (!resolved)
        {
            yield return skill.ResolveOnUnit(this, caster, target);
        }

        // 원위치 복귀 (gap close 했을 때만 의미 있음)
        caster.transform.position = originalW;

        // 여기서는 FinishActionAfterSkill() 호출 안 함
    }

    IEnumerator Co_ResolveSelfCastThenFinish(SkillAsset skill, BattleUnit caster, bool freeAction)
    {
        if (skill == null || caster == null)
        {
            _isResolvingSelfCast = false;
            yield break;
        }

        try
        {
            // 자기 자신 대상으로 Resolve
            yield return skill.ResolveOnUnit(this, caster, caster);

            // ResolveOnUnit 안에서 MP 부족이면 그냥 yield break 해서 아무 변화 없이 끝나므로,
            // 여기서는 쿨다운만 공통 처리
            caster.ApplyCooldown(skill);

            if (!freeAction)
            {
                // 기본: 행동 1회 소비
                FinishActionAfterSkill();
            }
            else
            {
                // ==== 무료 행동 ====
                ClearSkillPreview();
                CloseSkillPanel();

                currentSkill = default;
                currentSkillSO = null;
                currentSkillTargetMap = null;
                customPreviewCells = null;
                customPreviewMap = null;
                UpdateTargetingHint();

                if (IsPlayerTurn)
                    state = BattleState.ActionSelect;
            }
        }
        finally
        {
            _isResolvingSelfCast = false;
        }
    }

    IEnumerator Co_ProjectileSkillThenFinishSO(SkillAsset skill, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        state = BattleState.Resolving;

        bool castEnded = false;
        bool projEnded = false;
        bool fired = false; // 임팩트(발사) 수신 여부

        //훈련 포함 최종 MP코스트를 계산
        int cost = skill.GetEffectiveMpCost(caster);

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
            if (!caster.TryConsumeMP(cost))
            {
                Debug.Log("[Skill] 발사 시 MP 부족 → 취소");
                projEnded = true; // 종료 플래그만 세우고 끝
                return;
            }

            if (projectilePrefab != null)
            {
                var startW = caster.transform.position;
                var targetW = map.GetCellCenterWorld(cell);
                var projectilePrefab = Instantiate(this.projectilePrefab, startW, Quaternion.identity);
                var projectileController = projectilePrefab.GetComponent<ProjectileController>();
                if (projectileController != null)
                {
                    projectileController.Init(startW, targetW,
                                            () => StartCoroutine(Co_ResolveTileThenFlag(skill, map, cell, caster, () =>
                                            {
                                                caster.ApplyCooldown(skill);
                                                projEnded = true;
                                            })),
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
            if (!caster.TryConsumeMP(cost))
            {
                Debug.Log("[Skill] 임팩트 미수신 폴백 시 MP 부족 → 취소");
                projEnded = true;
            }
            else
            {
                yield return skill.ResolveOnTile(this, map, cell, caster);
                caster.ApplyCooldown(skill);
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

    public int GetFinalSkillDamage(BattleUnit caster, BattleUnit target, SkillAsset source, float baseDamage)
    {
        // 기본 방어측 상태 보정이 아예 없을 때를 대비한 최소 처리
        float finalBase = Mathf.Max(0f, baseDamage);

        if (target == null || source == null)
            return Mathf.Max(0, Mathf.FloorToInt(finalBase));

        var stateDb = target.stateStatDB;
        var usc = target.GetComponent<UnitStateController>();
        var sc = target.GetComponent<StatusController>();

        // 1) 대상(UnitState) 기반 기본 배율
        float mul = 1f;
        if (stateDb != null)
            mul *= stateDb.GetDamageTakenMultiplier(usc, source.school);

        // 2) 스택형 상태(탈진/방어/나약/저항) 보정은 기존 그대로 유지
        if (sc != null)
        {
            if (source.school == DamageSchool.Physical)
            {
                int exhaustStacks = sc.GetStacks(StatusId.Exhaustion); // 탈진
                int guardStacks = sc.GetStacks(StatusId.Defense);    // 방어

                mul *= Mathf.Pow(1.20f, exhaustStacks);
                mul *= Mathf.Pow(0.80f, guardStacks);
            }
            else if (source.school == DamageSchool.Magical)
            {
                int weaknessStacks = sc.GetStacks(StatusId.Weakness);   // 나약
                int resistStacks = sc.GetStacks(StatusId.Resistance); // 저항

                mul *= Mathf.Pow(1.20f, weaknessStacks);
                mul *= Mathf.Pow(0.80f, resistStacks);
            }
            // DamageSchool.Composite 인 경우에는 stateStatDB 쪽 설정으로만 처리
            // (필요하면 여기서 탈진/나약을 함께 곱해도 됨)
        }

        // 3) Rage 보정: (1 + 0.01 × 자신의 현재 Rage)
        float rageMult = 1f;
        if (caster != null && caster.Rage > 0f)
        {
            rageMult += 0.01f * caster.Rage;
        }

        float raw = finalBase * rageMult * mul;
        return Mathf.Max(0, Mathf.FloorToInt(raw));
    }

    public void ExecuteSkillDamage(BattleUnit caster, IEnumerable<BattleUnit> victims, SkillAsset source, Tilemap map, Vector3Int originCell)
    {
        if (caster == null || source == null) return;

        bool killRefundDone = false;   // 이번 스킬 사용 중 자원 환급은 1번만

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

                // 기본 대미지 계산
                float baseDamage = source.ComputeDamage(caster, v, ctx);

                // 최종 적용 대미지
                int damage = GetFinalSkillDamage(caster, v, source, baseDamage);

                // === 경계 상태 처리 추가 ===
                var usc = v.GetComponent<UnitStateController>();
                bool hasVigilance = usc != null && usc.Has(UnitStateId.Guard);

                if (hasVigilance && source.school == DamageSchool.Physical)
                {
                    // 이 유닛이 실제로 들고 있는 SelfVigilanceSkill 찾기
                    SelfVigilanceSkill vigilanceSkill = null;
                    var data = v.data;
                    if (data != null && data.skills != null)
                    {
                        for (int i = 0; i < data.skills.Length; i++)
                        {
                            vigilanceSkill = data.skills[i] as SelfVigilanceSkill;
                            if (vigilanceSkill != null)
                                break;
                        }
                    }

                    // 찾은 경계 스킬 기준으로 훈련 루트 조회
                    int routeForVigilance = -1;
                    if (vigilanceSkill != null)
                        routeForVigilance = v.GetTrainingRouteIndex(vigilanceSkill);

                    // 자신을 공격한 적의 통찰 약화 (훈련 옵션 켜져 있고, 루트 일치 시)
                    if (vigilanceSkill != null &&
                        vigilanceSkill.trainingUseInsightDebuff &&
                        vigilanceSkill.routeForInsightDebuff >= 0 &&
                        routeForVigilance == vigilanceSkill.routeForInsightDebuff)
                    {
                        var atkUSC = caster.GetComponent<UnitStateController>();
                        var atkUnit = caster;
                        if (atkUSC != null && atkUnit != null)
                        {
                            float beforeINS = atkUnit.INS;
                            float beforeCrit = atkUnit.CritChance;

                            atkUSC.ApplyBuff(UnitStateBuffId.InsightDown);

                            float afterINS = atkUnit.INS;
                            float afterCrit = atkUnit.CritChance;

                            Debug.Log(
                                $"[Vigilance] 통찰 약화: {atkUnit.name} " +
                                $"INS {beforeINS} -> {afterINS}, " +
                                $"Crit {beforeCrit:P1} -> {afterCrit:P1}"
                            );
                        }
                    }

                    // 이번 물리 피해는 0으로 만들고, 경계를 즉시 제거
                    Debug.Log(
                        $"[Vigilance] {v.name} 이(가) 물리 공격을 경계로 무효화: {damage} -> 0 (skill={source.name}, school={source.school})"
                    );

                    damage = 0;
                    usc.Remove(UnitStateId.Guard);
                }

                // 공통 디버그: 실제로 어떤 피해가 적용되었는지
                Debug.Log(
                    $"[Damage] {caster.name} -> {v.name} / skill={source.name}, school={source.school}, finalDamage={damage}"
                );

                float hpBefore = v.HP;
                v.PlayHit();
                v.TakeDamage(damage);
                bool diedNow = (hpBefore > 0f && v.IsDead);

                // 훈련 강화 처리 (ParametricDamageSkill 전용)
                if (source is ParametricDamageSkill dmgSkill && caster != null)
                {
                    int route = caster.GetTrainingRouteIndex(dmgSkill);

                    // 제압 추가: 캐스팅 중인 적에게 suppressCur 추가 감소
                    if (dmgSkill.trainingSuppressionOnHit > 0 && dmgSkill.routeForSuppression >= 0 && route == dmgSkill.routeForSuppression)
                    {
                        var cast = v.GetComponent<EnemyCastState>();
                        if (cast != null)
                        {
                            cast.TryReduceSuppression(dmgSkill.trainingSuppressionOnHit);
                        }
                    }

                    // 출혈 부여
                    if (dmgSkill.trainingApplyBleed && dmgSkill.routeForBleed >= 0 && route == dmgSkill.routeForBleed)
                    {
                        var sc = v.GetComponent<StatusController>();
                        if (sc != null)
                        {
                            sc.ApplyWithTurnContext(
                                StatusId.Bleeding,
                                Mathf.Max(1, dmgSkill.trainingBleedStacks),
                                Mathf.Max(1, dmgSkill.trainingBleedDurationTurns)
                            );
                            Debug.Log( $"[Bleed] {v.name} ← {dmgSkill.name} " + $"stacks+={dmgSkill.trainingBleedStacks}, duration={dmgSkill.trainingBleedDurationTurns}");
                        }
                    }
                    // 공격받은 대상의 민첩 약화 (UnitStateBuffId 기반 디버프)
                    if (route == dmgSkill.routeForAgiDebuff &&
                        dmgSkill.trainingApplyAgiDebuff &&
                        dmgSkill.targetAgiDebuffId != UnitStateBuffId.None)
                    {
                        var uscTarget = v.GetComponent<UnitStateController>();
                        if (uscTarget != null)
                        {
                            int duration = Mathf.Max(1, dmgSkill.targetAgiDebuffDurationTurns);

                            uscTarget.ApplyBuffForTurns(dmgSkill.targetAgiDebuffId, duration);

                            Debug.Log(
                                $"[ParametricDamage] AGI Debuff Buff: {v.name} buff={dmgSkill.targetAgiDebuffId}, duration={duration}"
                            );
                        }
                    }
                    // 공포 상태 부여
                    if (dmgSkill.trainingApplyFear &&
                        dmgSkill.routeForFear >= 0 &&
                        route == dmgSkill.routeForFear &&
                        !v.IsDead)
                    {
                        var uscFear = v.GetComponent<UnitStateController>();
                        if (uscFear != null)
                        {
                            int fearTurns = Mathf.Max(1, dmgSkill.fearDurationTurns);
                            uscFear.ApplyForTurns(UnitStateId.Fear, fearTurns);

                            Debug.Log(
                                $"[ParametricDamage] Fear: {v.name} 공포 상태 부여 {fearTurns}턴 (route={route})"
                            );
                        }
                    }

                    // 이 히트로 대상이 사망했다면 소비한 자원(MP)을 환급
                    if (diedNow &&
                        !killRefundDone &&
                        route == dmgSkill.routeForRefundOnKill &&
                        dmgSkill.trainingRefundOnKill)
                    {
                        int cost = dmgSkill.GetEffectiveMpCost(caster);
                        if (cost > 0)
                        {
                            caster.GainMP(cost);
                            Debug.Log(
                                $"[ParametricDamage] Kill Refund: {caster.name} MP +{cost} (route={route})"
                            );
                        }

                        killRefundDone = true;
                    }

                    // 넉백 처리
                    if (_pendingKnockbackSkill == dmgSkill &&
                        _pendingKnockbackTarget == v &&
                        !v.IsDead &&
                        v.CurrentMap == map)
                    {
                        var dest = _pendingKnockbackDest;
                        bool canMove = map.HasTile(dest);

                        // 이동 저항 상태가 있으면, 강제 이동 자체를 막는다
                        var sc = v.GetComponent<StatusController>();
                        bool hasMoveResist = sc != null && sc.Has(StatusId.Fixing); // 또는 sc.HasMoveResist()

                        if (canMove && !hasMoveResist)
                        {
                            var units = GetUnitsInArea(map, new[] { dest });
                            foreach (var u in units)
                            {
                                if (u != null && !u.IsDead && u.Cell == dest)
                                {
                                    canMove = false;
                                    break;
                                }
                            }
                        }

                        if (canMove && !hasMoveResist)
                        {
                            // 그리드 점유 갱신
                            if (grid != null)
                                grid.SetOccupied(v.team, v.Cell, false);

                            v.MoveTo(map, dest);

                            if (grid != null)
                                grid.SetOccupied(v.team, v.Cell, true);
                        }

                        // 한 번 사용 후 클리어 (이동은 막혀도 pending은 소비)
                        _pendingKnockbackSkill = null;
                        _pendingKnockbackTarget = null;
                    }
                }

                // 중앙화된 적대감 산출
                float hostilityGained = HostilityRules.FromDamage(damage, caster, v);
                caster.AddHostility(hostilityGained);
                caster.NotifyDealtDamage(v, damage, source);
                //Debug.Log($"[DMG] {caster.name} -> {v.name}: {damage}, Hostility +{hostilityGained:F1}");
            }
        }
    }

    public void SetPendingKnockback(ParametricDamageSkill skill, BattleUnit target, Vector3Int dest)
    {
        _pendingKnockbackSkill = skill;
        _pendingKnockbackTarget = target;
        _pendingKnockbackDest = dest;
    }

    List<Vector3Int> _knockbackCandidates;
    Tilemap _knockbackMap;
    System.Action<Vector3Int?> _onKnockbackSelected;

    void BeginKnockbackSelection(Tilemap map, List<Vector3Int> candidates, System.Action<Vector3Int?> callback)
    {
        _knockbackMap = map;
        _knockbackCandidates = candidates;
        _onKnockbackSelected = callback;

        state = BattleState.TargetingKnockback;

        // 후보 셀 하이라이트
        ShowSkillPreview(map, _knockbackCandidates);
        OnHint?.Invoke("밀어낼 위치를 선택하세요");
    }

    void EndKnockbackSelection()
    {
        _knockbackMap = null;
        _knockbackCandidates = null;
        _onKnockbackSelected = null;

        ClearSkillPreview();
        ClearTargetSelection();
        OnHint?.Invoke(string.Empty);
    }

    bool ShouldOfferPostSkillMove(BattleUnit unit, SkillAsset skill)
    {
        if (unit == null || skill == null) return false;
        if (!IsPlayerTurn) return false; // 적 턴에는 사용하지 않음

        if (skill is ParametricDamageSkill dmg)
        {
            if (!dmg.trainingUsePostMove) return false;

            int route = unit.GetTrainingRouteIndex(dmg);
            if (dmg.routeForPostMove < 0) return false;

            return route == dmg.routeForPostMove;
        }

        return false;
    }

    IEnumerator Co_SelectKnockbackThenResolve(
    ParametricDamageSkill dmgSkill,
    BattleUnit caster,
    BattleUnit target,
    List<Vector3Int> candidates,
    bool doGapClose)
    {
        if (dmgSkill == null || caster == null || target == null)
            yield break;

        // 1) 넉백 타일 선택 모드 진입
        bool done = false;
        Vector3Int? chosen = null;

        BeginKnockbackSelection(target.CurrentMap, candidates, cell =>
        {
            chosen = cell;
            done = true;
        });

        // 플레이어가 클릭할 때까지 대기
        while (!done)
            yield return null;

        // 선택하지 않았으면(취소) 아무 것도 하지 않고 종료
        if (!chosen.HasValue)
        {
            EndKnockbackSelection();
            yield break;
        }
            

        // 선택된 타일을 pending Knockback으로 등록
        SetPendingKnockback(dmgSkill, target, chosen.Value);

        // 원래 공격 흐름 실행
        yield return Co_GapCloseThenResolveOnTargetSO(dmgSkill, caster, target, doGapClose);
    }

    IEnumerator Co_SelectAllyRetreatThenResolve(
    AllyRetreatSwapSkill skill,
    BattleUnit caster,
    BattleUnit ally,
    List<Vector3Int> candidates)
    {
        if (skill == null || caster == null || ally == null)
            yield break;

        // 1) 후퇴 칸 선택 모드 진입 (넉백 인프라 재사용)
        bool done = false;
        Vector3Int? chosen = null;

        BeginKnockbackSelection(ally.CurrentMap, candidates, cell =>
        {
            chosen = cell;
            done = true;
        });

        // 플레이어가 빈 칸 클릭 or 취소(Q/우클릭) 할 때까지 대기
        while (!done)
            yield return null;

        // 취소했으면 아무 것도 하지 않음
        if (!chosen.HasValue)
        {
            EndKnockbackSelection();
            yield break;
        }

        // 선택 끝났으니 하이라이트/힌트 정리
        EndKnockbackSelection();

        // MP 비용 체크 (훈련/기본 반영)
        int cost = skill.GetEffectiveMpCost(caster);
        if (cost > 0 && !caster.TryConsumeMP(cost))
            yield break;

        // 실제 스왑 실행
        yield return skill.ResolveSwapWithDest(this, caster, ally, chosen.Value);

        // 쿨다운 적용 + 행동 1회 소비 (항상 일반 스킬처럼)
        caster.ApplyCooldown(skill);
        FinishActionAfterSkill();
    }
    IEnumerator Co_PostSkillMoveThenConsume(BattleUnit unit)
    {
        if (unit == null || grid == null)
        {
            // 안전상 바로 행동 소비
            OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        _isPostSkillMoveInProgress = true;

        // 이동 후보: 기본 Move와 동일하게 인접 워커블 셀
        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(unit.team, unit.Cell).ToList();

        // 갈 수 있는 칸이 하나도 없으면 → 그냥 행동 소비 후 턴 진행
        if (moveOptions.Count == 0)
        {
            _isPostSkillMoveInProgress = false;
            OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        ShowMovePreview(unit.CurrentMap, moveOptions);

        // 플레이어가 칸을 선택할 때까지 대기
        while (_isPostSkillMoveInProgress)
            yield return null;
    }
    IEnumerator Co_MoveAfterSkillThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell)
    {
        var fromCell = unit.Cell;
        grid.SetOccupied(unit.team, fromCell, false);
        yield return unit.AnimateMoveTo(map, toCell);
        grid.SetOccupied(unit.team, unit.Cell, true);

        // 이동 선택 종료
        _isPostSkillMoveInProgress = false;

        // 이 스킬은 '공격'으로 간주되어 행동 토큰 1개를 소비하고 턴을 마무리
        OnActionConsumed(BattleAction.Attack);
    }

    public Highlighter beastDomainHighlighter; // 야수의 영역 임시 하이라이트

    public void SpawnBeastDomainZone(Tilemap map, BattleUnit owner, Vector3Int centerCell, int radius, int durationTurns)
    {
        if (!owner || !map) return;


        // 같은 유닛이 만든 이전 영역 제거 + 하이라이트도 함께 삭제
        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var old = _beastZones[i];
            if (old.owner != owner) continue;

            if (old.highlightToken != 0 && beastDomainHighlighter != null)
                beastDomainHighlighter.ClearGroup(old.highlightToken);   // Highlighter 그룹 제거

            _beastZones.RemoveAt(i);
        }

        // 이번 영역의 셀 집합 계산
        var cells = new List<Vector3Int>();
        foreach (var c in AreaShapes.BeastDomainArea(centerCell, radius))
            cells.Add(c);

        // 하이라이트 그룹 생성 후 셀 지정
        int token = 0;
        if (cells.Count > 0 && beastDomainHighlighter != null)
        {
            token = beastDomainHighlighter.CreateGroup();
            beastDomainHighlighter.SetGroupCells(token, map, cells);
        }

        // 4) 영역 등록
        var zone = new BeastDomainZone
        {
            owner = owner,
            map = map,
            center = centerCell,
            radius = radius,
            remainingTurns = durationTurns,
            highlightToken = token,
        };
        _beastZones.Add(zone);

        Debug.Log(
            $"[BeastDomain] {owner.name} 야수의 영역 생성 - " +
            $"center:{centerCell}, r:{radius}, turns:{durationTurns}, token:{token}");
    }
    void TickBeastDomainOnTurnStart(BattleUnit unitWhoseTurnStarted)
    {
        if (unitWhoseTurnStarted == null) return;

        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var z = _beastZones[i];
            if (z.owner != unitWhoseTurnStarted)
                continue;

            // 훈련: 영역 주인의 턴 시작에 '자기 자신' Rage 감소
            TryApplyBeastDomainRageTraining(z.owner);

            z.remainingTurns--;
            Debug.Log($"[BeastDomain] {z.owner.name} 턴 시작 - 야수의 영역 남은 턴: {z.remainingTurns}");

            if (z.remainingTurns <= 0)
            {
                Debug.Log($"[BeastDomain] {z.owner.name} 야수의 영역이 사라집니다 (center:{z.center})");

                // 영역 끝날 때 하이라이트 제거
                if (z.highlightToken != 0 && beastDomainHighlighter != null)
                    beastDomainHighlighter.ClearGroup(z.highlightToken);

                _beastZones.RemoveAt(i);
            }
        }
    }
    void TryApplyBeastDomainRageTraining(BattleUnit owner)
    {
        if (owner == null) return;
        if (owner.data == null || owner.data.skills == null) return;

        // 이 유닛이 들고 있는 SelfBeastDomainSkill 찾기
        SelfBeastDomainSkill domainSkill = null;
        foreach (var s in owner.data.skills)
        {
            domainSkill = s as SelfBeastDomainSkill;
            if (domainSkill != null)
                break;
        }
        if (domainSkill == null) return;

        int route = owner.GetTrainingRouteIndex(domainSkill);

        if (!domainSkill.trainingReduceRageOnTurnStart ||
            domainSkill.routeForRageReduceOnTurnStart < 0 ||
            route != domainSkill.routeForRageReduceOnTurnStart)
            return;

        float amount = 0f;
        float clv = owner.MagicDamage; 
        amount = clv * domainSkill.rageReducePerClv;

        if (amount <= 0f) return;

        owner.AddRage(-amount);
        Debug.Log($"[BeastDomain] Rage 훈련: {owner.name} 자신에게 Rage {amount:F2} 감소");
    }

    // 공포(Fear) 턴 처리
    public IEnumerator Co_HandleFearTurn(BattleUnit unit)
    {
        if (unit == null)
            yield break;

        var usc = unit.GetComponent<UnitStateController>();
        if (usc == null)
            yield break;

        var map = unit.CurrentMap;
        if (map == null || grid == null)
        {
            Debug.LogWarning("[Fear] 맵 또는 그리드가 없습니다.");
            yield break;
        }

        // 뒤로 한 칸 이동 가능한 후보들 계산
        var candidates = GetFearRetreatCandidates(unit);
        if (candidates == null || candidates.Count == 0)
        {
            Debug.Log($"[Fear] {unit.name} 공포 상태지만 뒤로 이동할 수 있는 칸이 없습니다.");
        }
        else
        {
            // 후보 중 랜덤 하나 선택
            var dest = candidates[Random.Range(0, candidates.Count)];

            var from = unit.Cell;
            grid.SetOccupied(unit.team, from, false);
            yield return unit.AnimateMoveTo(map, dest);
            grid.SetOccupied(unit.team, unit.Cell, true);

            Debug.Log($"[Fear] {unit.name} 이(가) {from} → {dest} 로 후퇴했습니다.");
        }

        // 이 턴은 공포 때문에 아무 행동도 못 하고 바로 종료
        if (unit.team == Team.Player)
            EndPlayerTurn();
        else
            EndEnemyTurn(unit);
    }
    // 공포 상태일 때 "한 칸 뒤"로 물러날 수 있는 후보 칸들 계산
    List<Vector3Int> GetFearRetreatCandidates(BattleUnit unit)
    {
        var result = new List<Vector3Int>();
        if (unit == null || grid == null)
            return result;

        var map = unit.CurrentMap;
        if (map == null)
            return result;

        var origin = unit.Cell;

        // 팀별 "뒤쪽" 방향 오프셋 정의
        // 예시 기준:
        // - 플레이어: (-1,0), (-1,-1)
        // - 적군:    ( 1,0), ( 0, 1)
        Vector3Int[] offsets;
        if (unit.team == Team.Player)
        {
            offsets = new[]
            {
            new Vector3Int(-1, 0, 0),
            new Vector3Int(-1, -1, 0),
        };
        }
        else // Team.Enemy
        {
            offsets = new[]
            {
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0),
        };
        }

        foreach (var off in offsets)
        {
            var dest = origin + off;

            // 타일이 존재하고, 해당 팀 기준으로 워커블인지 검사
            if (!map.HasTile(dest))
                continue;

            if (grid.IsOccupied(Team.Player, dest) || grid.IsOccupied(Team.Enemy, dest))
                continue;

            result.Add(dest);
        }

        return result;
    }

    int HexDistance(Vector3Int a, Vector3Int b)
    {
        var axA = SkillLibrary.OffsetToAxial(a);
        var axB = SkillLibrary.OffsetToAxial(b);

        int dq = Mathf.Abs(axA.x - axB.x);
        int dr = Mathf.Abs(axA.y - axB.y);
        int ds = Mathf.Abs((-axA.x - axA.y) - (-axB.x - axB.y));

        return (dq + dr + ds) / 2;
    }
    bool IsBeastDomainFreeMove(BattleUnit unit, Tilemap map, Vector3Int fromCell, Vector3Int toCell)
    {
        if (unit == null || map == null) return false;

        foreach (var z in _beastZones)
        {
            if (z.owner != unit) continue;
            if (z.map != map) continue;

            bool fromIn = HexDistance(z.center, fromCell) <= z.radius;
            bool toIn = HexDistance(z.center, toCell) <= z.radius;

            if (fromIn && toIn)
                return true;
        }

        return false;
    }


    bool IsEnemyOf(BattleUnit a, BattleUnit b)
    {
        // 프로젝트에 따라 팀 판별 방법이 다르면 이곳을 연결
        return a != null && b != null && a.team != b.team;
    }

    void FinishActionAfterSkill()
    {
        // 어떤 스킬이었는지, 누구 차례였는지 로컬로 잡아둔다
        var skill = currentSkillSO;
        var unit = acting;

        // 하이라이트/선택 상태 정리
        ClearSkillPreview();
        // 스킬 실행 완료 → 패널 닫기 + 스킬 선택 해제
        CloseSkillPanel();   // 이벤트까지 함께 발행됨

        // 기술 사용 후 1칸 이동
        if (unit != null && skill != null && ShouldOfferPostSkillMove(unit, skill))
        {
            // 스킬 쿨다운/MP는 이미 처리된 상태이므로,
            // 여기서는 '공격 토큰'을 바로 소모하지 않고 이동 기회를 먼저 준다.
            currentSkill = default;
            currentSkillSO = null;
            currentSkillTargetMap = null;
            customPreviewCells = null;
            customPreviewMap = null;
            UpdateTargetingHint();

            StartCoroutine(Co_PostSkillMoveThenConsume(unit));
            return;
        }

        // 스킬은 '공격'으로 간주하여 행동 토큰 소모 로직 재사용
        OnActionConsumed(BattleAction.Attack);

        currentSkill = default;           // 레거시
        currentSkillSO = null;            // SO도 클리어
        currentSkillTargetMap = null;
        customPreviewCells = null;
        customPreviewMap = null;
        UpdateTargetingHint();
    }


    // 타겟팅 취소/종료 시 미리보기 지우기
    public void ShowMovePreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        moveHighlighter?.ShowCells(baseMap, cells);
    }
    public void ShowSkillPreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    {
        if (customPreviewCells != null)
        {
            // 후보가 0개면 아무 것도 그리지 않음
            if (customPreviewCells.Count == 0) return;

            // 락에서 전달한 동일 참조가 아닌 임의 셀은 무시(호버 등 외부 호출 차단)
            if (!object.ReferenceEquals(cells, customPreviewCells)) return;
        }

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
    // “n초 후 자동으로 지운다” 버전
    public void EmitPassiveLabelAutoClear(BattleUnit u, string label, float seconds = 1.0f)
    {
        OnUnitPassiveLabel?.Invoke(u, label);
        StartCoroutine(Co_ClearPassiveLabelAfter(seconds));
    }
    IEnumerator Co_ClearPassiveLabelAfter(float t)
    {
        yield return new WaitForSeconds(t);
        OnUnitPassiveLabel?.Invoke(null, ""); // 빈 라벨 = 클리어 신호
    }
}
