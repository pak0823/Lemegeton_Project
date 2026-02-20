using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Cysharp.Threading.Tasks;

public class BattleManager : MonoBehaviour
{
    #region Variables
    public static BattleManager Instance { get; private set; }

    // Core Modules (하위 시스템 연결)
    [Header("Core Modules")]
    [SerializeField] private BattleGridManager gridManager;         // 그리드 조회 및 유닛 위치 관리
    [SerializeField] private BattleFieldManager fieldManager;       // 장판 및 환경 효과 담당
    [SerializeField] private BattleTurnManager turnManager;
    [SerializeField] private BattleInputHandler inputHandler;       // 입력 및 시각적 피드백 담당
    [SerializeField] private BattleMapManager mapManager;
    [SerializeField] private BattleSkillProcessor skillProcessor;   // 스킬 효과 및 데미지 계산 담당
    [SerializeField] private BattleWaveManager waveManager;        // 웨이브 스폰 및 스테이지 관리

    public IGridProvider Grid => gridManager;
    public IFieldController Field => fieldManager;

    [Header("Controllers")]
    public ATBTurnController turnController;                       // 턴 순서(ATB) 관리자
    public BattleInput battleInput;

    // Battle State (전투 핵심 상태)
    public event System.Action<BattleState, BattleState> OnStateChanged; // 이전 상태, 현재 상태

    [Header("FSM")]
    public BattleStateMachine fsm; // FSM 참조

    // Battle State (전투 핵심 상태)
    // Legacy support: We still keep this for external readers, but FSM drives logic
    private BattleState _state = BattleState.Idle;
    public BattleState state
    {
        get => _state;
        set // Changed to public set for FSM to write to it, context dependent
        {
            if (_state != value)
            {
                var oldState = _state;
                _state = value;
                OnStateChanged?.Invoke(oldState, _state);
            }
        }
    }
    private bool initialized = false;                                  // 초기화 여부 플래그
    private bool _battleEndedOnce = false;                             // 전투 종료 처리 중복 방지

    // Action & Turn Rules (행동력 및 턴 규칙)
    private BattleUnit acting;
    public BattleUnit ActingUnit => acting;
    //public BattleUnit ActingUnit => turnManager.ActingUnit;
    public bool IsPlayerTurn => turnManager.IsPlayerTurn;

    // Skill & Input Context (스킬 시전 및 입력 관련 상태)
    // 스킬 선택 상태
    public bool isSelectingSkill = false;                // 스킬 패널이 열려있는지 여부
    public SkillDefinition currentSkill;                 // (Legacy) 선택된 스킬 구조체
    public SkillAsset currentSkillSO;                    // 현재 선택된 스킬 에셋 (메인)

    // 실행 제어 플래그
    bool _skillConfirmLocked = false;                    // 스킬 확정 대기 락
    bool _isPostSkillMoveInProgress = false;             // 스킬 후 이동(Hit & Run) 진행 중 여부
    private int _reactionLocks = 0;                      // 리액션(반격 등)으로 인한 턴 진행 일시 정지 카운트

    // 이동/타겟팅 데이터
    private List<Vector3Int> moveOptions = new();        // 현재 이동 가능한 타일 목록 캐싱

    // Unit Registry (유닛 중앙 관리)
    private HashSet<BattleUnit> _activeUnits = new();
    public IEnumerable<BattleUnit> ActiveUnits => _activeUnits;

    public void RegisterUnit(BattleUnit unit)
    {
        if (unit != null && !_activeUnits.Contains(unit))
        {
            _activeUnits.Add(unit);
            // Debug.Log($"[BattleManager] Unit Registered: {unit.name} (Total: {_activeUnits.Count})");
        }
    }

    public void UnregisterUnit(BattleUnit unit)
    {
        if (unit != null && _activeUnits.Contains(unit))
        {
            _activeUnits.Remove(unit);
            // Debug.Log($"[BattleManager] Unit Unregistered: {unit.name} (Total: {_activeUnits.Count})");
        }
    }

    // StatusPanel Registry
    public void RegisterStatusPanel(UnitStatusPanelUI panel)
    {
        _statusPanel = panel;
    }

    // [Optimization] Helper methods for unit retrieval
    public List<BattleUnit> GetAllUnits() => _activeUnits.ToList(); // 사본 반환 안전

    public List<BattleUnit> GetAliveUnits(Team team)
    {
        return _activeUnits
               .Where(u => u != null && !u.IsDead && u.data.team == team)
               .ToList();
    }
    public List<BattleUnit> GetAliveUnits()
    {
         return _activeUnits
               .Where(u => u != null && !u.IsDead)
               .ToList();
    }

    // Databases (데이터 참조)
    [Header("Databases")]
    [SerializeField] private StateStatModifierDB stateStatDb; // 상태이상 스탯 보정 DB
    public TrainingDB trainingDB;                             // 훈련/특성 DB

    // Internal References (내부 참조 및 유틸)
    private IBattleMapProvider provider;                 // 맵 정보 제공자 (GridManager로 대체 중이나 초기화 의존성으로 유지)
    private UnitStatusPanelUI _statusPanel;              // UI 패널 참조 (Lazy Load)

    // Events (외부 알림)
    // Wave Events
    public event System.Action<int, int, string> OnWaveChanged;  // 웨이브 정보 갱신 (현재, 총, 라벨)
    public event System.Action OnWaveStarted;                    // 웨이브 시작 시점
    public event System.Action<int, int> OnWaveTransition;       // 웨이브 전환 연출 시작

    // Unit/Turn Events
    public static event System.Action<BattleUnit> OnAnyUnitTurnStarted; // (Static) 유닛 턴 시작 전역 알림
    public event System.Action<BattleUnit> OnUnitEndTurn;               // 유닛 턴 종료
    public event System.Action<BattleUnit> OnOverworkTriggered;         // 과로(Overwork) 발동 알림

    // UI Label Events
    public event System.Action<string> OnHint;                          // 상단 힌트 텍스트 갱신
    public event System.Action<BattleUnit> OnUnitTurnLabel;             // 턴 시작 라벨 표시
    public event System.Action<BattleUnit, string> OnUnitActionLabel;   // 액션(스킬명) 라벨 표시
    public event System.Action<BattleUnit, string> OnUnitPassiveLabel;  // 패시브 발동 라벨 표시

    // Skill Panel Events
    public event System.Action<bool> OnSkillPanelToggled;               // 스킬 패널 열림/닫힘
    public event System.Action<SkillAsset[]> OnSkillPanelPopulateSO;    // 스킬 패널 내용 갱신 요청

    // ATB Events
    public delegate void OnATBChangedDelegate(BattleUnit unit, float currentATB, float maxATB);
    public event OnATBChangedDelegate OnATBChanged;      // ATB 게이지 변경 알림
    public event System.Action OnATBReset;               // ATB 초기화 알림

    // Public Properties (외부 접근자)
    // 상태 확인
    public bool IsTargeting => state == BattleState.Targeting;
    public bool IsKnockbackTargeting => state == BattleState.TargetingKnockback;

    // 데이터 접근
    public SkillAsset CurrentSkillSO => currentSkillSO; // 필드와 프로퍼티 연결
    public TrainingDB Training => trainingDB;

    // 웨이브 정보 연결
    public int CurrentWave => waveManager.CurrentWave;
    public int TotalWaves => waveManager.TotalWaves;

    #endregion

    public static void ClearStatic()
    {
        OnAnyUnitTurnStarted = null;
    }
    public void RegisterReactionLock()
    {
        _reactionLocks++;
    }

    public void UnregisterReactionLock()
    {
        _reactionLocks = Mathf.Max(0, _reactionLocks - 1);
    }
    public void SetState(BattleState newState) => state = newState;
    public static void EmitGlobalTurnStart(BattleUnit u) => OnAnyUnitTurnStarted?.Invoke(u);

    public void EmitActionLabel(BattleUnit u, string label) => OnUnitActionLabel?.Invoke(u, label);
    public void EmitPassiveLabel(BattleUnit u, string label) => OnUnitPassiveLabel?.Invoke(u, label);
    public void EmitTurnLabel(BattleUnit u) => OnUnitTurnLabel?.Invoke(u);
    public void SetHint(string msg) => OnHint?.Invoke(msg);


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
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (!turnController) turnController = FindObjectOfType<ATBTurnController>();
        if (!waveManager) waveManager = GetComponentInChildren<BattleWaveManager>();
        if (!skillProcessor) skillProcessor = GetComponentInChildren<BattleSkillProcessor>();
        if (!inputHandler) inputHandler = GetComponentInChildren<BattleInputHandler>();
        if (!turnManager) turnManager = GetComponentInChildren<BattleTurnManager>();

        if (fsm == null) fsm = GetComponent<BattleStateMachine>() ?? gameObject.AddComponent<BattleStateMachine>();
        fsm.Initialize(this);

        gridManager.Initialize(mapManager);
        fieldManager.Initialize(this, gridManager, inputHandler);
        inputHandler.Initialize(this);
        turnManager.Initialize(this, gridManager, inputHandler, fieldManager);
        waveManager.Initialize(this, gridManager, mapManager);
        skillProcessor.Initialize(this, gridManager);

        // MapReady 이벤트
        if (mapManager != null) mapManager.OnMapsReady += Init;

        // TurnController 이벤트
        if (turnController != null) turnController.OnTurnReady += HandleTurnReady;

        // WaveManager 이벤트
        if (waveManager != null)
        {
            waveManager.OnWaveLoaded += HandleWaveLoaded;
            waveManager.OnWaveInfoUpdated += (cur, tot, lbl) => OnWaveChanged?.Invoke(cur, tot, lbl);
            waveManager.OnWaveTransitionStarted += (next, tot) => OnWaveTransition?.Invoke(next, tot);
            waveManager.OnAllWavesCleared += HandleVictory;
        }

        if (turnManager != null)
        {
            turnManager.OnUnitEndTurn += (u) => OnUnitEndTurn?.Invoke(u);
            turnManager.OnOverworkTriggered += (u) => OnOverworkTriggered?.Invoke(u);
        }

        battleInput = FindObjectOfType<BattleInput>();
        if (battleInput != null) battleInput.Initialize(this, gridManager, mapManager);
    }

    void Start()
    {
        if (provider == null)
        {
            provider = BattleMapManager.Instance as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
            if (provider != null) provider.OnMapsReady += Init;
        }

        SpawnPlayerUnits();

        if (!initialized)
        {
            waveManager.StartFirstWave();
        }

        StartCoroutine(Co_RebindBattleInputWhenMapsReady());
    }

    private void OnDestroy()
    {
        ClearStatic();
    }

    // 특정 스킬(skill)을 기준으로 타겟(target)이 유효한지 검사하는 헬퍼 함수
    private bool IsTargetValidForSkill(BattleUnit target, SkillAsset skill, BattleUnit caster)
    {
        if (target == null || caster == null || skill == null) return false;

        // 1. 부활 스킬이 아니면 죽은 자는 제외
        bool isReviveSkill = false;
        if (skill is ParametricSupportSkill supportSkill && supportSkill.mode == SupportSkillMode.Revive)
            isReviveSkill = true;

        if (!isReviveSkill && target.IsDead) return false;

        // 2. 타겟팅 불가 상태(잠복/연막) 체크 (적군이 플레이어를 노릴 때만 적용되지만, 안전상 체크)
        if (caster.data.team != target.data.team && SkillAsset.IsUntargetableByEnemy(target)) return false;

        // 3. 타겟 성향(Alignment) 체크
        switch (skill.targetAlignment)
        {
            case SkillTargetAlignment.Enemy:
                return caster.data.team != target.data.team;
            case SkillTargetAlignment.Ally:
                return caster.data.team == target.data.team;
            case SkillTargetAlignment.Self:
                return caster == target;
            case SkillTargetAlignment.Any:
                return true;
            default:
                return false;
        }
    }

    public bool IsValidSkillTarget(BattleUnit target)
    {
        // 현재 선택된 스킬(currentSkillSO)을 기준으로 검사
        return IsTargetValidForSkill(target, currentSkillSO, ActingUnit);
    }

    public List<BattleUnit> GetValidTargetsForCycle(SkillAsset skill, BattleUnit caster)
    {
        var list = new List<BattleUnit>();
        // var allUnits = FindObjectsOfType<BattleUnit>(); // [Optimization] Use registry
        var allUnits = _activeUnits;

        foreach (var u in allUnits)
        {
            // 1. 기본 유효성 (자신, 사망 여부 등) 체크
            // (IsValidSkillTarget은 currentSkillSO를 쓰므로, 여기선 인자로 받은 skill을 기준으로 검사해야 함)
            if (!IsTargetValidForSkill(u, skill, caster)) continue;

            list.Add(u);
        }
        return list;
    }

    public void ProcessMoveCommand(Tilemap map, Vector3Int cell)
    {
        if (state != BattleState.Moving) return;

        if (ActingUnit.CurrentMap == map && moveOptions.Contains(cell))
        {
            state = BattleState.Resolving;
            inputHandler.ClearAllPreviews();

            if (_isPostSkillMoveInProgress)
                StartCoroutine(Co_MoveAfterSkillThenConsume(ActingUnit, map, cell));
            else
                StartCoroutine(Co_MoveThenConsume(ActingUnit, map, cell, BattleAction.Move));

            moveOptions.Clear();
        }
    }

    void OnDisable()
    {
        if (provider != null) provider.OnMapsReady -= Init;
    }
    #endregion

    #region Initialization

    void Init()
    {
        RebindAllUnitsAndInitATB();
    }

    void SpawnPlayerUnits()
    {
        if (PlayerDataManager.Instance == null)
        {
            Debug.LogWarning("PlayerDataManager가 없습니다!");
            return;
        }

        var mapManager = BattleMapManager.Instance;
        if (mapManager == null) return;

        UnitData[] formation = PlayerDataManager.Instance.formation;
        Vector3Int centerOffset = Vector3Int.zero;

        for (int i = 0; i < formation.Length; i++)
        {
            UnitData data = formation[i];
            if (data != null)
            {
                Vector3Int cellPos = mapManager.GetFormationSpawnPoint(i) + centerOffset;
                GameObject go = Instantiate(data.battlePrefab);
                Vector3 worldPos = mapManager.PlayerFloor.GetCellCenterWorld(cellPos);
                go.transform.position = worldPos;

                BattleUnit unit = go.GetComponent<BattleUnit>();
                if (unit != null)
                {
                    unit.data = data;
                    unit.data.team = Team.Player;
                    var sr = go.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && data.UnitIcon != null)
                    {
                        sr.sprite = data.UnitIcon;
                    }
                    unit.ApplyData();

                    // [Persistence] 저장된 상태 로드 (ApplyData 이후에 호출해야 함)
                    if (PlayerDataManager.Instance != null)
                    {
                        PlayerDataManager.Instance.SyncToBattle(unit);
                    }
                }
            }
        }
    }

    IEnumerator Co_RebindBattleInputWhenMapsReady()
    {
        var provider = BattleMapManager.Instance as IBattleMapProvider;
        while (provider == null)
        {
            yield return null;
            provider = BattleMapManager.Instance as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>(true) as IBattleMapProvider;
        }
        while (provider.PlayerFloor == null || provider.EnemyFloor == null)
            yield return null;

        if (battleInput != null)
            battleInput.RebindProviders();
    }

    private void RebindAllUnitsAndInitATB()
    {
        // var battleUnit = FindObjectsOfType<BattleUnit>() // [Optimization] Use registry
        var battleUnit = _activeUnits
                        .Where(u => u.gameObject.activeInHierarchy && !u.IsDead)
                        .ToList();

        if (battleUnit.Count == 0) return;

        float minAGI = battleUnit.Min(unit => unit.EffectiveAGI);
        float maxAGI = battleUnit.Max(unit => unit.EffectiveAGI);

        foreach (var unit in battleUnit)
        {
            var map = (unit.data.team == Team.Player) ? provider.PlayerFloor : provider.EnemyFloor;
            var cell = map.WorldToCell(unit.transform.position);

            unit.Bind(map, cell);
            gridManager.SetOccupied(unit.data.team, unit.Cell, true);
            unit.InitializeATB(minAGI, maxAGI);
            unit.InitPassives(this);

            unit.OnDied -= HandleUnitDied;
            unit.OnDied += HandleUnitDied;
        }

        turnController.RegisterUnits(battleUnit);

        turnController.OnATBTicked -= ForwardATBTick;
        turnController.OnATBTicked += ForwardATBTick;
    }

    void ForwardATBTick(BattleUnit u)
    {
        OnATBChanged?.Invoke(u, u.ATB, u.MaxATB);
    }

    void HandleTurnReady(BattleUnit unit)
    {
        turnManager.StartTurn(unit);
    }

    void ResetATBAndTurnOrder()
    {
        turnController.ResetAllATB();
        turnManager.ForceClearActingUnit();
        state = BattleState.Idle;
        OnATBReset?.Invoke();
        EmitActionLabel(null, "");
    }

    #endregion

    void HandleWaveLoaded(BattleMapManager localProvider, Tilemap enemyFloor, Tilemap enemyOverlay)
    {
        if (enemyFloor != null)
        {
            var mapMgr = BattleMapManager.Instance as BattleMapManager ?? FindObjectOfType<BattleMapManager>(true);
            mapMgr.UseEnemyFloor(enemyFloor, enemyOverlay);
            provider = mapMgr;

            battleInput?.RebindProviders();
            gridManager.RebindProvider();
        }

        RebindAllUnitsAndInitATB();
        OnWaveStarted?.Invoke();
        ResetATBAndTurnOrder();
    }

    #region Turn Management
    public void OnUnitTurnStartedByManager(BattleUnit unit)
    {
        this.acting = unit;
        Debug.Log($"[BattleManager] 현재 행동 주체 동기화 완료: {unit.name}");
    }
    public void OnClickRest() => turnManager?.Rest();
    public void OnClickCalm() => turnManager?.Calm();
    #endregion

    #region Movement
    public void OnClickMove()
    {
        if (!IsPlayerTurn) return;
        if (!turnManager.CanPerformAction(BattleAction.Move)) return;

        if (acting == null)
        {
            Debug.LogWarning("현재 행동 중인 유닛이 없는데 이동 버튼이 눌림.");
            return;
        }

        // 2. 핸들러가 있는지 확인
        if (inputHandler == null)
        {
            Debug.LogError("InputHandler 참조가 비어있음!");
            return;
        }

        CloseSkillPanel();
        inputHandler.ClearAllPreviews();

        state = BattleState.Moving;
        moveOptions = gridManager.GetAdjacentWalkable(ActingUnit.data.team, ActingUnit.Cell).ToList();
        inputHandler.ShowMoveOptions(ActingUnit.CurrentMap, moveOptions);
    }

    IEnumerator Co_MoveThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell, BattleAction act)
    {
        if (unit == null || map == null) yield break;

        Vector3Int fromCell = unit.Cell;
        gridManager.SetOccupied(unit.data.team, fromCell, false);
        yield return unit.AnimateMoveTo(map, toCell);
        gridManager.SetOccupied(unit.data.team, unit.Cell, true);

        bool freeMove = fieldManager != null && fieldManager.IsBeastDomainFreeMove(unit, map, fromCell, toCell);
        if (freeMove)
        {
            if (IsPlayerTurn)
            {
                state = BattleState.ActionSelect;
                EmitActionLabel(unit, "");
            }
            yield break;
        }

        while (_reactionLocks > 0) yield return null;

        // TurnManager에게 소비 요청
        turnManager.OnActionConsumed(act);
    }
    #endregion

    #region Attack
    public void OnClickAttack()
    {
        if (!IsPlayerTurn) return;
        if (!turnManager.CanPerformAction(BattleAction.Attack)) return;

        inputHandler.ClearAllPreviews();
        OpenSkillPanel();
    }
    #endregion

    #region Action Consumption

    public IEnumerable<BattleUnit> GetLivingEnemiesOf(BattleUnit _battleunit)
    {
        if (_battleunit == null) yield break;
        // var currentUnit = FindObjectsOfType<BattleUnit>(); // [Optimization] Use registry
        var currentUnit = _activeUnits;

        foreach (var units in currentUnit)
        {
            if (units == null || units == _battleunit || units.data.team == _battleunit.data.team || units.IsDead || units.IsRetreated) continue;
            yield return units;
        }
    }

    public void CancelCurrentAction()
    {
        if (state == BattleState.TargetingKnockback)
        {
            inputHandler.CancelCellSelection();
            state = BattleState.Resolving;
            return;
        }

        if (state == BattleState.Targeting)
        {
            inputHandler.ClearAllPreviews();
            currentSkillSO = null;
            state = BattleState.ActionSelect;
            OnHint?.Invoke(string.Empty);
            OpenSkillPanel();
            return;
        }

        if (state == BattleState.Moving)
        {
            if (_isPostSkillMoveInProgress)
            {
                inputHandler.ClearAllPreviews();
                _isPostSkillMoveInProgress = false;
                turnManager.OnActionConsumed(BattleAction.Attack);
                return;
            }

            inputHandler.ClearAllPreviews();
            state = BattleState.ActionSelect;
            return;
        }

        if (isSelectingSkill)
        {
            CloseSkillPanel();
            state = BattleState.ActionSelect;
        }
    }
    #endregion

    #region Targeting

    // [수정] 핸들러 호출로 변경
    public bool SelectTargetByUnit(BattleUnit unit)
    {
        return inputHandler.TrySelectTarget(unit);
    }

    // [수정] 핸들러 호출로 변경
    public void CycleTarget(int dir)
    {
        if (!IsPlayerTurn || !IsTargeting) return;
        inputHandler.CycleTarget(dir);
    }

    // [수정] 핸들러로부터 타겟 받아오도록 변경
    public void ConfirmTarget()
    {
        if (!IsPlayerTurn || !IsTargeting) return;

        var target = inputHandler.GetSelectedTarget();
        if (target == null) return;

        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;

        inputHandler.ClearSkillPreview();

        if (currentSkillSO is AllyRetreatSwapSkill)
        {
            ConfirmSkillOnUnit(target);
            return;
        }

        StartCoroutine(skillProcessor.PerformStandardUnitSkillFlow(currentSkillSO, ActingUnit, target));
    }

    void ClearTargetSelection()
    {
        inputHandler.ClearAllPreviews();
        OnHint?.Invoke(string.Empty);
    }
    #endregion

    #region Death Handling
    void HandleUnitDied(BattleUnit dead)
    {
        gridManager.SetOccupied(dead.data.team, dead.Cell, false);

        if (dead == ActingUnit)
        {
            turnController.ResumeTime();
            turnManager.ForceClearActingUnit();
            state = BattleState.Idle;
        }

        dead.OnDied -= HandleUnitDied;
        EmitActionLabel(dead, "");

        Co_DieThenDestroy(dead).Forget();
    }

    async UniTaskVoid Co_DieThenDestroy(BattleUnit u)
    {
        if (u == null) { CheckBattleEnd(); return; }

        // 사망 애니메이션 대기 (BattleUnit.PlayDieAndWait는 UniTask)
        await u.PlayDieAndWait(1.0f);

        if (u != null && u.gameObject != null) Destroy(u.gameObject);
        CheckBattleEnd();
    }
    #endregion

    #region Battle End
    void HandleVictory()
    {
        Debug.Log("[Battle] 승리!");

        // [Persistence] 살아있는 아군 상태 저장
        if (PlayerDataManager.Instance != null)
        {
            // [Optimization] FindObjectsOfType 제거 → _activeUnits 레지스트리 사용
            foreach (var u in _activeUnits)
            {
                if (u != null && u.data.team == Team.Player)
                {
                    PlayerDataManager.Instance.SyncFromBattle(u);
                }
            }
        }

        // [Reward] 전투 승리 보상 생성 및 전달
        if (BattleRewardManager.Instance != null)
        {
            var rewards = BattleRewardManager.Instance.GenerateRewards();
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.SetPendingRewards(rewards);
                Debug.Log($"[Battle] Rewards generated: {rewards.Count} types.");
            }
        }

        SceneTransitionManager.Instance.ReturnToSavedPoint();
    }
    void CheckBattleEnd()
    {
        // var units = FindObjectsOfType<BattleUnit>(); // [Optimization] Use registry
        var units = _activeUnits;
        bool anyPlayer = units.Any(u => u.data.team == Team.Player && !u.IsDead);
        bool anyEnemy = units.Any(u => u.data.team == Team.Enemy && !u.IsDead);

        if (!anyEnemy)
        {
            if (waveManager.IsWaveTransitioning) return;
            waveManager.TryAdvanceToNextWave();
            return;
        }
        else if (!anyPlayer)
        {
            if (_battleEndedOnce) return;
            _battleEndedOnce = true;
            Debug.Log("[Battle] 패배...");

            // [Persistence] 패배 시에도 상태 저장이 필요한가?
            // 보통 패배하면 게임 오버거나, 상태가 유지된 채로 마을로 돌아감.
            // 여기서는 일단 저장함.
            if (PlayerDataManager.Instance != null)
            {
                // var units = FindObjectsOfType<BattleUnit>(); // 이미 위에서 정의됨
                // units 변수 재사용
                foreach (var u in units)
                {
                    if (u.data.team == Team.Player)
                    {
                        PlayerDataManager.Instance.SyncFromBattle(u);
                    }
                }
            }

            SceneTransitionManager.Instance.FadeToScene(SceneName.TitleScene);
        }
    }
    #endregion

    #region UI Helpers
    // 스킬 조준 시 범위 내 유닛들 UI 하이라이트 (InputHandler가 호출함)
    public void HighlightUnitsInArea(Tilemap map, List<Vector3Int> cells)
    {
        // 범위 내에 있는 유닛들을 불러옴
        var victims = gridManager.GetUnitsInArea(map, cells);

        // 패널에게 명단 넘겨서 하이라이트 작동
        StatusPanel?.HighlightUnits(victims);
    }
    public void ClearStatusPanelHighlights()
    {
        StatusPanel?.ClearHighlights();
    }
    // Handler가 유닛 정보 보기 위해 호출함
    public void ShowUnitInfo(BattleUnit unit)
    {
        if (unit == null) return;

        Debug.Log($"[BattleManager] Unit Info Clicked: {unit.name}");

        // [수정] UnitStatusPanelUI에는 SetUnit(상세보기) 기능이 없음.
        // 대신 해당 유닛을 리스트에서 강조(Highlight)하는 것으로 대체하거나 주석 처리.

        // StatusPanel?.SetUnit(unit); // <-- 삭제 대상 (없는 메서드)
        //StatusPanel?.HighlightUnits(new List<BattleUnit> { unit }); // 대안: 리스트에서 얘만 불 켜기

        // 타겟 마커 표시
        inputHandler.targetMarker?.Attach(unit);
    }

    #endregion
    bool IsAmbushHiddenTarget(BattleUnit u)
    {
        if (!u) return false;
        var usc = u.GetComponent<UnitStateController>();
        if (usc == null) return false;
        return usc.Has(UnitStateId.Ambush) || usc.HasBuff(UnitStateBuffId.SmokeHidden);
    }
    SelfAmbushSkill GetAmbushSkillFor(BattleUnit unit)
    {
        if (unit == null || unit.data == null || unit.data.skills == null) return null;
        foreach (var s in unit.data.skills)
        {
            if (s is SelfAmbushSkill ambush) return ambush;
        }
        return null;
    }

    public void TryApplyAmbushTurnStartHeal(BattleUnit unit)
    {
        // 내부 헬퍼를 이용해 스킬을 찾음
        var skill = GetAmbushSkillFor(unit);

        if (unit == null || skill == null) return;
        var usc = unit.GetComponent<UnitStateController>();
        if (usc == null || !usc.Has(UnitStateId.Ambush)) return;

        int route = unit.GetTrainingRouteIndex(skill);
        if (!skill.trainingHealOnTurnStart || skill.routeForHealOnTurnStart < 0 || route != skill.routeForHealOnTurnStart) return;

        int amount = skill.ComputeTurnStartHeal(unit);
        if (amount <= 0) return;

        unit.Heal(amount);
    }

    #region Enemy AI

    IEnumerator Co_EnemyResolveSkillOnUnit_NoMove(SkillDefinition def, BattleUnit caster, BattleUnit target)
    {
        state = BattleState.Resolving;

        bool impactDone = false;
        System.Action impact = null;
        impact = () =>
        {
            caster.OnAttackImpact -= impact;
            impactDone = true;
            ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster);
        };
        caster.OnAttackImpact += impact;

        yield return caster.AnimateAttack(target, null);

        if (!impactDone) ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster);

        FinishActionAfterSkill();
    }

    public IEnumerator Co_EnemyFireWebThenConsume(BattleUnit caster, EnemyCastState.PendingCast p)
    {
        state = BattleState.Resolving;
        caster.SetCasting(false);

        bool fired = false;
        bool arrived = false;

        ProjectileController projPrefab = p.projectilePrefab ?? caster.defaultProjectilePrefab ?? null;
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
            else OnArrive();
        }

        System.Action onFire = null;
        onFire = () => { caster.OnAttackImpact -= onFire; FireOnce(); };
        caster.OnAttackImpact += onFire;

        yield return caster.AnimateShootWeb();

        if (!fired) { caster.OnAttackImpact -= onFire; FireOnce(); }

        float timeout = 3f;
        while (!arrived && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }

        ClearSkillPreview();
        var ecs = caster.GetComponent<EnemyCastState>();
        ecs?.ClearPreviewAndFinalize(this);
        turnManager.OnActionConsumed(BattleAction.Attack);
    }

    #endregion

    public async UniTaskVoid OnClickEscape()
    {
        try
        {
            if (ActingUnit == null || ActingUnit.data.team != Team.Player) return;
            if (state == BattleState.Resolving) return;

            // [Optimization] FindObjectsOfType 제거 → _activeUnits 레지스트리 사용
            var aliveEnemies = _activeUnits.Where(u => u != null && u.data.team == Team.Enemy && !u.IsDead).ToList();
            float enemyAgiSum = Mathf.Max(0.0001f, aliveEnemies.Sum(u => u.EffectiveAGI));
            float successChance01 = Mathf.Clamp01(ActingUnit.EffectiveAGI / enemyAgiSum);
            int percent = Mathf.FloorToInt(successChance01 * 100f);

            string unitName = GetUnitLabel(ActingUnit);
            string safeName = unitName.Replace("<", "&lt;").Replace(">", "&gt;");
            string msg = $"<color=#C60004>{safeName}</color> 유닛을 전투에서 제외합니다. 진행할까요?\n(탈출 성공 확률: {percent}%)";

            bool ok = await PopupManager.Instance.ConfirmRetreatAsync(msg, successChance01);
            if (!ok) return;
            bool success = (Random.value < successChance01);

            if (success)
            {
                await PopupManager.Instance.ConfirmAsync("탈출에 성공했습니다.", "확인", "");
                RetreatCurrentUnit(ActingUnit);
            }
            else
            {
                await PopupManager.Instance.ConfirmAsync("탈출에 실패했습니다.", "확인", "");
                turnManager.EndPlayerTurn();
            }

            CancelCurrentAction();
            ClearAllPreviews();
            ClearTargetSelection();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    private string GetUnitLabel(BattleUnit u)
    {
        if (!string.IsNullOrEmpty(u.name)) return u.name;
        return u.name;
    }

    void RetreatCurrentUnit(BattleUnit _battleunit)
    {
        if (_battleunit == null) return;
        if (gridManager != null) gridManager.SetOccupied(_battleunit.data.team, _battleunit.Cell, false);
        _battleunit.Retreat();
        Destroy(_battleunit.gameObject);

        if (_battleunit == ActingUnit)
        {
            turnController.CompleteTurn(_battleunit);
            turnManager.ForceClearActingUnit();
            state = BattleState.Idle;
        }
        CheckBattleEnd();
    }


    public void OpenSkillPanel()
    {
        if (!IsPlayerTurn || ActingUnit == null) return;
        isSelectingSkill = true;

        var raw = ActingUnit?.data?.skills ?? System.Array.Empty<SkillAsset>();
        var view = new SkillAsset[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            var s = raw[i];
            if (s is ISkillForStateResolver resolver)
                view[i] = resolver.ResolveForCaster(ActingUnit) ?? s;
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
        var list = ActingUnit?.data?.skills;
        if (list == null || index < 0 || index >= list.Length) return;

        var picked = list[index];
        if (picked is ISkillForStateResolver resolver)
            picked = resolver.ResolveForCaster(ActingUnit) ?? picked;

        currentSkillSO = picked;
        EnterSkillTargeting(currentSkillSO);
    }
    private void EnterSkillTargeting(SkillAsset skill)
    {
        if (skill == null) return;

        int effectiveCost = skill.GetEffectiveCost(ActingUnit);
        SkillCostResource res = skill.GetCostResource(ActingUnit);

        if (!ActingUnit.HasResource(res, effectiveCost)) { Debug.Log($"[Skill] Resource({res}) insufficient"); return; }
        if (ActingUnit.IsSkillOnCooldown(skill)) { Debug.Log($"[Skill] Cooltime"); return; }

        if (skill is ISelfCastSkill self && self.SelfCastOnSelect)
        {
            if (skillProcessor.IsResolvingSelfCast) return;
            bool isFreeAction = false;
            int route = ActingUnit.GetTrainingRouteIndex(skill);

            if (skill is SelfStateSkill sss) { if (sss.trainingUseFreeAction && sss.routeForFreeAction >= 0 && route == sss.routeForFreeAction) isFreeAction = true; }
            else if (skill is SelfStateCleanseSkill scs) { if (scs.trainingUseFreeAction && scs.routeForFreeAction >= 0 && route == scs.routeForFreeAction) isFreeAction = true; }
            if (!isFreeAction && skill is HostilitySpikeSkill hss) { if (hss.trainingUseFreeAction && hss.routeForFreeAction >= 0 && route == hss.routeForFreeAction) isFreeAction = true; }
            if (!isFreeAction && skill is SelfBeastDomainSkill bds) { if (bds.trainingUseFreeAction && bds.routeForFreeAction >= 0 && route == bds.routeForFreeAction) isFreeAction = true; }

            StartCoroutine(skillProcessor.PerformSelfCastFlow(skill, ActingUnit, isFreeAction));
            return;
        }

        state = BattleState.Targeting;
        inputHandler.PrepareSkillTargeting(skill, ActingUnit);

        UpdateTargetingHint();
    }

    private void UpdateTargetingHint()
    {
        if (state == BattleState.Targeting && currentSkillSO != null)
        {
            if (currentSkillSO.targetMode == SkillTargetMode.Tile) OnHint?.Invoke("위치를 선택하세요");
            else OnHint?.Invoke("대상을 선택하세요");
        }
        else OnHint?.Invoke(string.Empty);
    }

    public void ConfirmSkillOnUnit(BattleUnit target)
    {
        if (state != BattleState.Targeting) return;
        state = BattleState.Resolving;
        inputHandler.ClearAllPreviews();

        StartCoroutine(RunSkillExecute(CurrentSkillSO, ActingUnit, target, null, default));
    }
    public void ConfirmSkillOnTile(Tilemap map, Vector3Int cell)
    {
        if (state != BattleState.Targeting) return;
        state = BattleState.Resolving;
        inputHandler.ClearAllPreviews();

        StartCoroutine(RunSkillExecute(CurrentSkillSO, ActingUnit, null, map, cell));
    }
    IEnumerator RunSkillExecute(SkillAsset skill, BattleUnit caster, BattleUnit target, Tilemap map, Vector3Int cell)
    {
        ClearSkillPreview();
        yield return skill.Execute(this, caster, target, map, cell);

        if (_skillConfirmLocked)
        {
            _skillConfirmLocked = false;
            if (state == BattleState.Resolving)
            {
                state = BattleState.Idle;
                CancelCurrentAction();
                if (IsPlayerTurn && turnManager.RemainingActions > 0) state = BattleState.ActionSelect;
            }
        }
    }

    public IEnumerator WaitForCellSelection(Tilemap map, List<Vector3Int> candidates, System.Action<Vector3Int?> onResult)
    {
        state = BattleState.TargetingKnockback;
        OnHint?.Invoke("밀어낼 위치를 선택하세요");

        bool done = false;
        Vector3Int? result = null;

        inputHandler.StartCellSelectionMode(map, candidates, (cell) => {
            result = cell;
            done = true;
        });

        while (!done) yield return null;

        OnHint?.Invoke(string.Empty);
        state = BattleState.Resolving;
        onResult(result);
    }

    public IEnumerator PerformStandardUnitSkillFlow(SkillAsset skill, BattleUnit caster, BattleUnit target)
    {
        yield return skillProcessor.PerformStandardUnitSkillFlow(skill, caster, target);
    }
    public IEnumerator PerformStandardTileSkillFlow(SkillAsset skill, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        yield return skillProcessor.PerformStandardTileSkillFlow(skill, map, cell, caster);
    }

    public Coroutine StartReactiveAttack(BattleUnit caster, BattleUnit target, SkillAsset skill, bool doGapClose)
    {
        if (caster == null || target == null || skill == null) return null;
        return StartCoroutine(skillProcessor.Co_ReactiveAttackFlow(skill, caster, target, doGapClose));
    }

    public void ResolveSkillAtCell(SkillDefinition def, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        var area = def.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        var victims = gridManager.GetUnitsInArea(map, area);
    }

    public int GetFinalSkillDamage(BattleUnit caster, BattleUnit target, SkillAsset source, float baseDamage)
    {
        return skillProcessor.GetFinalSkillDamage(caster, target, source, baseDamage);
    }

    public void ExecuteSkillDamage(BattleUnit caster, IEnumerable<BattleUnit> victims, SkillAsset source, Tilemap map, Vector3Int originCell)
    {
        skillProcessor.ExecuteSkillDamage(caster, victims, source, map, originCell);
    }

    public void SetPendingKnockback(ParametricDamageSkill skill, BattleUnit target, Vector3Int dest)
    {
        skillProcessor.SetPendingKnockback(skill, target, dest);
    }

    bool ShouldOfferPostSkillMove(BattleUnit unit, SkillAsset skill)
    {
        if (unit == null || skill == null) return false;
        if (!IsPlayerTurn) return false;
        if (skill is ParametricDamageSkill dmg)
        {
            if (!dmg.trainingUsePostMove) return false;
            int route = unit.GetTrainingRouteIndex(dmg);
            if (dmg.routeForPostMove < 0) return false;
            return route == dmg.routeForPostMove;
        }
        return false;
    }

    IEnumerator Co_PostSkillMoveThenConsume(BattleUnit unit)
    {
        if (unit == null || gridManager == null)
        {
            turnManager.OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        _isPostSkillMoveInProgress = true;
        state = BattleState.Moving;
        moveOptions = gridManager.GetAdjacentWalkable(unit.data.team, unit.Cell).ToList();

        if (moveOptions.Count == 0)
        {
            _isPostSkillMoveInProgress = false;
            turnManager.OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        ShowMovePreview(unit.CurrentMap, moveOptions);

        while (_isPostSkillMoveInProgress) yield return null;
    }
    IEnumerator Co_MoveAfterSkillThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell)
    {
        var fromCell = unit.Cell;
        gridManager.SetOccupied(unit.data.team, fromCell, false);

        yield return unit.AnimateMoveTo(map, toCell);

        gridManager.SetOccupied(unit.data.team, unit.Cell, true);
        _isPostSkillMoveInProgress = false;

        while (_reactionLocks > 0) yield return null;
        turnManager.OnActionConsumed(BattleAction.Attack);
    }

    public IEnumerator Co_HandleFearTurn(BattleUnit unit)
    {
        if (unit == null) yield break;
        var usc = unit.GetComponent<UnitStateController>();
        if (usc == null) yield break;
        var map = unit.CurrentMap;
        if (map == null || gridManager == null) yield break;

        var candidates = GetFearRetreatCandidates(unit);
        if (candidates.Count > 0)
        {
            var dest = candidates[Random.Range(0, candidates.Count)];
            var from = unit.Cell;
            gridManager.SetOccupied(unit.data.team, from, false);
            yield return unit.AnimateMoveTo(map, dest);
            gridManager.SetOccupied(unit.data.team, unit.Cell, true);
        }

        if (unit.data.team == Team.Player) turnManager.EndPlayerTurn();
        else turnManager.EndEnemyTurn(unit);
    }
    List<Vector3Int> GetFearRetreatCandidates(BattleUnit unit)
    {
        var result = new List<Vector3Int>();
        if (unit == null || gridManager == null) return result;
        var map = unit.CurrentMap;
        if (map == null) return result;

        var origin = unit.Cell;
        Vector3Int[] offsets;
        if (unit.data.team == Team.Player) offsets = new[] { new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0), };
        else offsets = new[] { new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), };

        foreach (var off in offsets)
        {
            var dest = origin + off;
            if (!map.HasTile(dest)) continue;
            if (gridManager.IsOccupied(Team.Player, dest) || gridManager.IsOccupied(Team.Enemy, dest)) continue;
            result.Add(dest);
        }
        return result;
    }

    public void UnlockSkillConfirm()
    {
        _skillConfirmLocked = false;
    }

    public void FinishActionAfterSkill()
    {
        var skill = currentSkillSO;
        var unit = ActingUnit; // 프로퍼티 사용

        ClearSkillPreview();
        CloseSkillPanel();
        UnlockSkillConfirm();

        if (unit != null && skill != null && ShouldOfferPostSkillMove(unit, skill))
        {
            currentSkill = default;
            currentSkillSO = null;
            UpdateTargetingHint();

            StartCoroutine(Co_PostSkillMoveThenConsume(unit));
            return;
        }

        turnManager.OnActionConsumed(BattleAction.Attack);

        currentSkill = default;
        currentSkillSO = null;
        UpdateTargetingHint();
    }

    // 핸들러 위임 (ClearTransient가 아니라 ClearMovePreview 호출)
    public void ShowMovePreview(Tilemap baseMap, IEnumerable<Vector3Int> cells) => inputHandler?.ShowMoveOptions(baseMap, cells);
    public void ShowSkillPreview(Tilemap baseMap, IEnumerable<Vector3Int> cells) => inputHandler?.ShowSkillPreview(baseMap, cells);

    public void ClearMovePreview() => inputHandler?.ClearMovePreview();

    // [수정] 핸들러 위임 (메서드 새로 만듦)
    public void HoldSkillPreview() => inputHandler?.HoldSkillPreview();
    public void ReleaseSkillPreview() => inputHandler?.ReleaseSkillPreview();

    public void ClearSkillPreview() => inputHandler?.ClearSkillPreview();

    public void ClearAllPreviews()
    {
        ClearMovePreview();
        ClearSkillPreview();
        StatusPanel?.ClearHighlights();
    }

    // 핸들러 하이라이터 사용
    public int CreateSkillPreviewToken() => inputHandler.skillHighlighter != null ? inputHandler.skillHighlighter.CreateGroup() : 0;
    public void SetSkillPreviewForToken(int token, Tilemap map, IEnumerable<Vector3Int> cells) => inputHandler.skillHighlighter?.SetGroupCells(token, map, cells);
    public void ClearSkillPreviewToken(int token) => inputHandler.skillHighlighter?.ClearGroup(token);

    public void EmitPassiveLabelAutoClear(BattleUnit u, string label, float seconds = 1.0f)
    {
        OnUnitPassiveLabel?.Invoke(u, label);
        Co_ClearPassiveLabelAfter(seconds).Forget();
    }
    async UniTaskVoid Co_ClearPassiveLabelAfter(float t)
    {
        // t초 후 패시브 라벨 자동 클리어
        await UniTask.Delay(System.TimeSpan.FromSeconds(t));
        OnUnitPassiveLabel?.Invoke(null, "");
    }

    public void ResetSkillSelectionState()
    {
        ClearSkillPreview();
        CloseSkillPanel();

        currentSkill = default;
        currentSkillSO = null;

        UpdateTargetingHint();
        UnlockSkillConfirm();

        if (IsPlayerTurn) state = BattleState.ActionSelect;
    }
}
