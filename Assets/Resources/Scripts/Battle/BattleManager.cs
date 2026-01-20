using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum BattleState { Idle, ActionSelect, Moving, Targeting, Resolving, TargetingKnockback, EndTurn }
public enum BattleAction { Move, Attack, Rest, Calm }

public class BattleManager : MonoBehaviour
{
    #region Variables
    public BattleGridManager grid;
    UnitStatusPanelUI _statusPanel;
    public LayerMask unitMask;

    [SerializeField] int baseActionsPerTurn = 1;

    IBattleMapProvider provider;

    bool initialized = false;
    public bool IsPlayerTurn => acting != null && acting.team == Team.Player;
    public bool IsTargeting => state == BattleState.Targeting;
    public bool IsKnockbackTargeting => state == BattleState.TargetingKnockback;
    Coroutine enemyRoutine;

    [Header("Modules")]
    [SerializeField] private BattleWaveManager waveManager;
    [SerializeField] public BattleSkillProcessor skillProcessor;
    [SerializeField] public BattleInputHandler inputHandler;
    public ATBTurnController turnController;

    // [프로퍼티 연결]
    public int CurrentWave => waveManager.CurrentWave;
    public int TotalWaves => waveManager.TotalWaves;

    public event System.Action<int, int, string> OnWaveChanged;
    public event System.Action OnWaveStarted;
    public event System.Action<int, int> OnWaveTransition;
    public event System.Action<BattleUnit, string> OnUnitPassiveLabel;
    private bool _battleEndedOnce = false;

    #region State & Variables
    public BattleState state { get; private set; } = BattleState.Idle;

    // [수정] 프로퍼티가 필드를 바라보도록 연결 (중요!)
    public SkillAsset CurrentSkillSO => currentSkillSO;

    private BattleUnit acting;
    private List<Vector3Int> moveOptions = new();
    private int remainingActions = 0;
    private HashSet<BattleAction> usedActions = new();

    // UI 및 이벤트
    public event System.Action<string> OnHint;
    public event System.Action<bool> OnSkillPanelToggled;
    public event System.Action<BattleUnit> OnUnitEndTurn;
    public event System.Action<BattleUnit> OnUnitTurnLabel;
    public event System.Action<BattleUnit, string> OnUnitActionLabel;
    #endregion

    public BattleUnit ActingUnit => acting;
    bool _skillConfirmLocked = false;

    public delegate void OnATBChangedDelegate(BattleUnit unit, float currentATB, float maxATB);
    public event OnATBChangedDelegate OnATBChanged;
    readonly System.Random rng = new System.Random();
    public static event System.Action<BattleUnit> OnAnyUnitTurnStarted;

    public event System.Action OnATBReset;

    public bool isSelectingSkill = false;
    public SkillDefinition currentSkill;
    public SkillAsset currentSkillSO;
    public event System.Action<SkillAsset[]> OnSkillPanelPopulateSO;

    public event System.Action<BattleUnit> OnOverworkTriggered;

    bool _isPostSkillMoveInProgress = false;
    private int _reactionLocks = 0;

    [Header("DBs")]
    [SerializeField] private StateStatModifierDB stateStatDb;

    [Header("Training")]
    public TrainingDB trainingDB;
    public TrainingDB Training => trainingDB;

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

    public void EmitActionLabel(BattleUnit u, string label) => OnUnitActionLabel?.Invoke(u, label);
    public void EmitPassiveLabel(BattleUnit u, string label) => OnUnitPassiveLabel?.Invoke(u, label);
    public void EmitTurnLabel(BattleUnit u) => OnUnitTurnLabel?.Invoke(u);


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
        public BattleUnit owner;
        public Tilemap map;
        public Vector3Int center;
        public int radius;
        public int remainingTurns;
        public int highlightToken;
    }
    List<BeastDomainZone> _beastZones = new List<BeastDomainZone>();

    [System.Serializable]
    public class StatusTileZone
    {
        public BattleUnit owner;
        public Tilemap map;
        public Vector3Int cell;
        public int remainingTurns;
        public TileBase originalTile;
        public StatusId effectStatusId;
        public int effectStack;
        public int effectDuration;
    }
    List<StatusTileZone> _statusTileZones = new List<StatusTileZone>();

    #region Unity Callbacks
    void Awake()
    {
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
        if (provider != null) provider.OnMapsReady += Init;
        else { Debug.LogWarning("[BattleManager] BattleMapManager not ready in Awake. Will retry in Start."); }
        if (Shared.BattleManager == null) Shared.BattleManager = this;

        if (!turnController) turnController = FindObjectOfType<ATBTurnController>();
        turnController.OnTurnReady += HandleTurnReady;

        if (waveManager == null) waveManager = GetComponentInChildren<BattleWaveManager>();
        waveManager.OnWaveLoaded += HandleWaveLoaded;
        waveManager.OnWaveInfoUpdated += (cur, tot, lbl) => OnWaveChanged?.Invoke(cur, tot, lbl);
        waveManager.OnWaveTransitionStarted += (next, tot) => OnWaveTransition?.Invoke(next, tot);
        waveManager.OnAllWavesCleared += HandleVictory;

        waveManager.Initialize();

        if (skillProcessor == null) skillProcessor = GetComponentInChildren<BattleSkillProcessor>();

        if (skillProcessor != null)
        {
            skillProcessor.Initialize(this);
        }
        else
        {
            Debug.LogError("[BattleManager] BattleSkillProcessor가 없습니다! 인스펙터나 자식 오브젝트를 확인하세요.");
        }

        if (inputHandler == null) inputHandler = GetComponentInChildren<BattleInputHandler>();
        if (inputHandler != null) inputHandler.Initialize(this);
    }

    void Start()
    {
        if (provider == null)
        {
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
            if (provider != null) provider.OnMapsReady += Init;
        }

        SpawnPlayerUnits();

        if (!initialized)
        {
            waveManager.StartFirstWave();
        }

        StartCoroutine(Co_RebindBattleInputWhenMapsReady());
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
        if (caster.team != target.team && SkillAsset.IsUntargetableByEnemy(target)) return false;

        // 3. 타겟 성향(Alignment) 체크
        switch (skill.targetAlignment)
        {
            case SkillTargetAlignment.Enemy:
                return caster.team != target.team;
            case SkillTargetAlignment.Ally:
                return caster.team == target.team;
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
        return IsTargetValidForSkill(target, currentSkillSO, acting);
    }

    public List<BattleUnit> GetValidTargetsForCycle(SkillAsset skill, BattleUnit caster)
    {
        var list = new List<BattleUnit>();
        var allUnits = FindObjectsOfType<BattleUnit>();

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

        if (acting.CurrentMap == map && moveOptions.Contains(cell))
        {
            state = BattleState.Resolving;
            inputHandler.ClearAllPreviews();

            if (_isPostSkillMoveInProgress)
                StartCoroutine(Co_MoveAfterSkillThenConsume(acting, map, cell));
            else
                StartCoroutine(Co_MoveThenConsume(acting, map, cell, BattleAction.Move));

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

        var mapManager = Shared.battleMapManager;
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
                    unit.team = Team.Player;
                    var sr = go.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && data.UnitIcon != null)
                    {
                        sr.sprite = data.UnitIcon;
                    }
                    unit.ApplyData();
                }
            }
        }
    }

    IEnumerator Co_RebindBattleInputWhenMapsReady()
    {
        var provider = Shared.battleMapManager as IBattleMapProvider;
        while (provider == null)
        {
            yield return null;
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>(true) as IBattleMapProvider;
        }
        while (provider.PlayerFloor == null || provider.EnemyFloor == null)
            yield return null;

        if (Shared.battleInput != null)
            Shared.battleInput.RebindProviders();
    }

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
        acting = unit;
        StartTurn(unit);
    }

    void ResetATBAndTurnOrder()
    {
        turnController.ResetAllATB();
        acting = null;
        state = BattleState.Idle;
        OnATBReset?.Invoke();
        EmitActionLabel(null, "");
    }

    #endregion

    private void TryReleaseGridOccupy(BattleUnit u)
    {
        if (u == null || grid == null) return;
        var map = grid.GetMap(u.team);
        if (map == null) return;
        Vector3Int cell = u.Cell;
        grid.SetOccupied(u.team, cell, false);
    }

    void HandleWaveLoaded(BattleMapManager localProvider, Tilemap enemyFloor, Tilemap enemyOverlay)
    {
        if (enemyFloor != null)
        {
            var mapMgr = Shared.battleMapManager as BattleMapManager ?? FindObjectOfType<BattleMapManager>(true);
            mapMgr.UseEnemyFloor(enemyFloor, enemyOverlay);
            provider = mapMgr;

            Shared.battleInput?.RebindProviders();
            Shared.battleGridManager?.RebindProvider();
        }

        RebindAllUnitsAndInitATB();
        OnWaveStarted?.Invoke();
        ResetATBAndTurnOrder();
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

        if (usc != null && usc.Has(UnitStateId.Sleep))
        {
            Debug.Log($"[Sleep] {_unit.name} 수면 상태이므로 턴을 넘깁니다.");
            usc.Remove(UnitStateId.Sleep);

            if (_unit.team == Team.Player)
                EndPlayerTurn();
            else
                EndEnemyTurn(_unit);
            return;
        }

        bool hadFear = (usc != null && usc.Has(UnitStateId.Fear));

        if (sc != null) sc.OnTurnStart();
        if (usc != null) usc?.OnTurnStart();

        var ambushSkill = GetAmbushSkillFor(_unit);
        if (ambushSkill != null)
        {
            TryApplyAmbushTurnStartHeal(_unit, ambushSkill);
        }

        if (hadFear)
        {
            Debug.Log($"[Fear] {_unit.name} 공포 상태 턴 시작 → 강제 후퇴 진행");
            state = BattleState.Resolving;
            remainingActions = 0;
            usedActions.Clear();

            StartCoroutine(Co_HandleFearTurn(_unit));
            return;
        }

        TickBeastDomainOnTurnStart(_unit);
        TickStatusTileZonesOnTurnStart(_unit);

        if (_unit.team == Team.Enemy)
        {
            var ecs = _unit.GetComponent<EnemyCastState>();
            if (ecs != null && ecs.TryTakeReady(out var pending))
            {
                StartCoroutine(Co_EnemyFireWebThenConsume(_unit, pending));
                return;
            }
        }

        if (_unit.team == Team.Player)
        {
            state = BattleState.ActionSelect;
        }
        else
        {
            state = BattleState.Resolving;
            StartCoroutine(EnemyTurnRoutine(_unit));
        }
    }

    public void OnClickRest()
    {
        if (acting == null || !IsPlayerTurn) return;
        if (remainingActions <= 0) return;

        ClearAllPreviews();
        ClearTargetSelection();

        float before = acting.HP;
        acting.HealPercent(0.10f);
        float after = acting.HP;

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
        float beforeRage = acting.Rage;

        int mpGain = Mathf.FloorToInt(maxMP * 0.10f);
        if (mpGain <= 0 && maxMP > 0f) mpGain = 1;

        if (acting.Rage <= 0f)
        {
            acting.GainMP(mpGain);
            OnActionConsumed(BattleAction.Calm);
            return;
        }

        if (mpGain > 0) acting.GainMP(mpGain);

        float rageCostTarget = maxRage * 0.10f;
        if (rageCostTarget <= 0f) rageCostTarget = beforeRage;

        float spend = Mathf.Min(beforeRage, rageCostTarget);
        if (spend > 0f) acting.AddRage(-spend);

        OnActionConsumed(BattleAction.Calm);
    }
    #endregion

    #region Movement
    public void OnClickMove()
    {
        if (acting == null || !IsPlayerTurn) return;
        if (usedActions.Contains(BattleAction.Move) || remainingActions <= 0) return;

        CloseSkillPanel();
        inputHandler.ClearAllPreviews();

        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(acting.team, acting.Cell).ToList();

        inputHandler.ShowMoveOptions(acting.CurrentMap, moveOptions);
    }

    IEnumerator Co_MoveThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell, BattleAction act)
    {
        if (unit == null || map == null) yield break;

        Vector3Int fromCell = unit.Cell;
        grid.SetOccupied(unit.team, fromCell, false);
        yield return unit.AnimateMoveTo(map, toCell);
        grid.SetOccupied(unit.team, unit.Cell, true);

        bool freeMove = IsBeastDomainFreeMove(unit, map, fromCell, toCell);

        if (freeMove)
        {
            if (unit.team == Team.Player)
            {
                state = BattleState.ActionSelect;
                EmitActionLabel(unit, "");
            }
            yield break;
        }

        while (_reactionLocks > 0) yield return null;

        OnActionConsumed(act);
    }
    #endregion

    #region Attack
    public void OnClickAttack()
    {
        if (!IsPlayerTurn || acting == null) return;
        if (usedActions.Contains(BattleAction.Attack) || remainingActions <= 0) return;

        inputHandler.ClearAllPreviews();
        OpenSkillPanel();
    }
    #endregion

    #region Action Consumption
    void OnActionConsumed(BattleAction act)
    {
        if (acting == null) return;

        usedActions.Add(act);
        remainingActions = Mathf.Max(0, remainingActions - 1);

        if (remainingActions > 0)
        {
            if (IsPlayerTurn)
            {
                state = BattleState.ActionSelect;
            }
            else
            {
                if (enemyRoutine != null) StopCoroutine(enemyRoutine);
                enemyRoutine = StartCoroutine(EnemyTurnRoutine(acting));
            }
        }
        else
        {
            if (IsPlayerTurn) EndPlayerTurn();
            else EndEnemyTurn(acting);
        }
    }

    void CheckStatusTileZoneEffect(BattleUnit unit)
    {
        if (unit == null || unit.IsDead) return;

        foreach (var zone in _statusTileZones)
        {
            if (zone.map == unit.CurrentMap && zone.cell == unit.Cell)
            {
                var sc = unit.GetComponent<StatusController>();
                if (sc != null)
                {
                    sc.ApplyWithTurnContext(zone.effectStatusId, zone.effectStack, zone.effectDuration);
                }
                break;
            }
        }
    }

    public IEnumerable<BattleUnit> GetLivingAlliesOf(BattleUnit unit)
    {
        if (unit == null) yield break;
        var all = FindObjectsOfType<BattleUnit>();
        foreach (var u in all)
        {
            if (u == null || u.IsDead || u.IsRetreated || u.team != unit.team) continue;
            yield return u;
        }
    }

    public IEnumerable<BattleUnit> GetLivingEnemiesOf(BattleUnit _battleunit)
    {
        if (_battleunit == null) yield break;
        var currentUnit = FindObjectsOfType<BattleUnit>();

        foreach (var units in currentUnit)
        {
            if (units == null || units == _battleunit || units.team == _battleunit.team || units.IsDead || units.IsRetreated) continue;
            yield return units;
        }
    }

    public BattleUnit GetUnitAt(Vector3Int cell)
    {
        if (grid == null) return null;
        var map = grid.GetMap(Team.Player);
        if (map == null) return null;

        Vector3 worldPos = map.GetCellCenterWorld(cell);
        Collider2D hit = Physics2D.OverlapCircle(worldPos, 0.2f, unitMask);

        if (hit != null) return hit.GetComponentInParent<BattleUnit>();
        return null;
    }

    private bool TryProcessOverwork()
    {
        if (acting == null) return false;

        var statusCtrl = acting.GetComponent<StatusController>();
        var usc = acting.GetComponent<UnitStateController>();
        if (statusCtrl == null) return false;

        int overworkStacks = statusCtrl.GetStacks(StatusId.Overwork);
        if (overworkStacks <= 0) return false;

        int nextStack = overworkStacks - 1;
        statusCtrl.SetStacks(StatusId.Overwork, nextStack);

        if (nextStack == 0)
        {
            bool skipSleep = false;
            if (acting.data != null && acting.data.skills != null)
            {
                foreach (var s in acting.data.skills)
                {
                    if (s is ParametricSupportSkill pss && pss.buffStatus == StatusId.Overwork)
                    {
                        int route = acting.GetTrainingRouteIndex(pss);
                        if (pss.trainingNoSleepOnOverworkEnd &&
                            pss.routeForNoSleepOnOverworkEnd >= 0 &&
                            route == pss.routeForNoSleepOnOverworkEnd)
                        {
                            skipSleep = true;
                            break;
                        }
                    }
                }
            }

            if (usc != null)
            {
                if (!skipSleep) usc.Apply(UnitStateId.Sleep);
                else Debug.Log($"[BattleManager] {acting.name} 과로 종료 -> 훈련 효과로 수면 면제!");
            }
            return false;
        }

        Debug.Log($"[BattleManager] {acting.name} 과로 발동! (남은 스택: {nextStack})");

        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        state = BattleState.ActionSelect;

        OnOverworkTriggered?.Invoke(acting);
        return true;
    }

    void EndPlayerTurn()
    {
        if (TryProcessOverwork()) return;
        CheckStatusTileZoneEffect(acting);
        ClearAllPreviews();
        ClearTargetSelection();

        OnUnitEndTurn?.Invoke(acting);
        turnController.CompleteTurn(acting);
        acting = null;
        state = BattleState.Idle;
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
                OnActionConsumed(BattleAction.Attack);
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

        StartCoroutine(skillProcessor.PerformStandardUnitSkillFlow(currentSkillSO, acting, target));
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
        grid.SetOccupied(dead.team, dead.Cell, false);

        if (dead == acting)
        {
            turnController.ResumeTime();
            acting = null;
            state = BattleState.Idle;
        }

        dead.OnDied -= HandleUnitDied;
        EmitActionLabel(dead, "");

        StartCoroutine(Co_DieThenDestroy(dead));
    }

    IEnumerator Co_DieThenDestroy(BattleUnit u)
    {
        if (u == null) { CheckBattleEnd(); yield break; }

        var routine = u.PlayDieAndWait(1.0f);
        if (routine != null)
        {
            while (u != null)
            {
                if (!routine.MoveNext()) break;
                yield return routine.Current;
                if (u == null || u.gameObject == null) break;
            }
        }

        if (u != null && u.gameObject != null) Destroy(u.gameObject);
        CheckBattleEnd();
    }
    #endregion

    #region Battle End
    void HandleVictory()
    {
        Debug.Log("[Battle] 승리!");
        if (Shared.PuzzleManager.IsPuzzleComplete)
            Shared.SceneTransitionManager.FadeToScene("EndScene");
        else
            Shared.SceneTransitionManager.ReturnToSavedPoint();
    }
    void CheckBattleEnd()
    {
        var units = FindObjectsOfType<BattleUnit>();
        bool anyPlayer = units.Any(u => u.team == Team.Player && !u.IsDead);
        bool anyEnemy = units.Any(u => u.team == Team.Enemy && !u.IsDead);

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

            if (Shared.PuzzleManager.IsPuzzleComplete)
                Shared.SceneTransitionManager.FadeToScene("EndScene");
            else
                Shared.SceneTransitionManager.ReturnToSavedPoint();
        }
    }
    #endregion

    #region UI Helpers
    // 스킬 조준 시 범위 내 유닛들 UI 하이라이트 (InputHandler가 호출함)
    public void HighlightUnitsInArea(Tilemap map, List<Vector3Int> cells)
    {
        // 범위 내에 있는 유닛들을 불러옴
        var victims = GetUnitsInArea(map, cells);

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

    void TryApplyAmbushTurnStartHeal(BattleUnit unit, SelfAmbushSkill skill)
    {
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
    IEnumerator EnemyTurnRoutine(BattleUnit enemy)
    {
        yield return new WaitForSeconds(0.5f);

        var players = Object.FindObjectsOfType<BattleUnit>()
            .Where(u => u != null && u.team == Team.Player && !u.IsDead)
            .Where(u => !SkillAsset.IsUntargetableByEnemy(u))
            .ToList();

        if (players.Count == 0) { EndEnemyTurn(enemy); yield break; }

        BattleUnit target = players[Random.Range(0, players.Count)];

        var ai = enemy.GetComponent<EnemyAI>();
        SkillAsset so = (ai != null) ? ai.ConsumePlannedSkillOrPick() : null;

        if (so != null) EmitActionLabel(enemy, so.displayName);

        if (so != null)
        {
            if (so.targetMode == SkillTargetMode.Unit)
            {
                yield return StartCoroutine(so.ResolveOnUnit(this, enemy, target));
                FinishActionAfterSkill();
                yield break;
            }
            else if (so.targetMode == SkillTargetMode.Tile)
            {
                yield break;
            }
        }
        EndEnemyTurn(enemy);
    }

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

    IEnumerator Co_EnemyFireWebThenConsume(BattleUnit caster, EnemyCastState.PendingCast p)
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
        OnActionConsumed(BattleAction.Attack);
    }

    void EndEnemyTurn(BattleUnit enemy)
    {
        if (enemy == null)
        {
            state = BattleState.Idle;
            return;
        }

        if (TryProcessOverwork()) return;
        enemyRoutine = null;
        CheckStatusTileZoneEffect(enemy);

        var ecs = enemy.GetComponent<EnemyCastState>();
        if (ecs == null || !ecs.IsCasting)
        {
            EmitActionLabel(enemy, "");
            var ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.PlanNextSkill();
        }

        enemy.ResetATB();
        OnUnitEndTurn?.Invoke(enemy);
        turnController.CompleteTurn(enemy);

        if (acting == enemy) acting = null;
        state = BattleState.Idle;
    }
    #endregion

    public async void OnClickEscape()
    {
        if (acting == null || acting.team != Team.Player) return;
        if (state == BattleState.Resolving) return;

        var aliveEnemies = FindObjectsOfType<BattleUnit>().Where(u => u.team == Team.Enemy && !u.IsDead).ToList();
        float enemyAgiSum = Mathf.Max(0.0001f, aliveEnemies.Sum(u => u.EffectiveAGI));
        float successChance01 = Mathf.Clamp01(acting.EffectiveAGI / enemyAgiSum);
        int percent = Mathf.FloorToInt(successChance01 * 100f);

        string unitName = GetUnitLabel(acting);
        string safeName = unitName.Replace("<", "&lt;").Replace(">", "&gt;");
        string msg = $"<color=#C60004>{safeName}</color> 유닛을 전투에서 제외합니다. 진행할까요?\n(탈출 성공 확률: {percent}%)";

        bool ok = await PopupManager.Instance.ConfirmRetreatAsync(msg, successChance01);
        if (!ok) return;
        bool success = (Random.value < successChance01);

        if (success)
        {
            await PopupManager.Instance.ConfirmAsync("탈출에 성공했습니다.", "확인", "");
            RetreatCurrentUnit(acting);
        }
        else
        {
            await PopupManager.Instance.ConfirmAsync("탈출에 실패했습니다.", "확인", "");
            EndPlayerTurn();
        }

        CancelCurrentAction();
        ClearAllPreviews();
        ClearTargetSelection();
    }

    private string GetUnitLabel(BattleUnit u)
    {
        if (!string.IsNullOrEmpty(u.name)) return u.name;
        return u.name;
    }

    void RetreatCurrentUnit(BattleUnit _battleunit)
    {
        if (_battleunit == null) return;
        TryReleaseGridOccupy(_battleunit);
        _battleunit.Retreat();
        Destroy(_battleunit.gameObject);

        if (_battleunit == acting)
        {
            turnController.CompleteTurn(_battleunit);
            acting = null;
            state = BattleState.Idle;
        }
        CheckBattleEnd();
    }


    public void OpenSkillPanel()
    {
        if (!IsPlayerTurn || acting == null) return;
        isSelectingSkill = true;

        var raw = acting?.data?.skills ?? System.Array.Empty<SkillAsset>();
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
        if (picked is ISkillForStateResolver resolver)
            picked = resolver.ResolveForCaster(acting) ?? picked;

        currentSkillSO = picked;
        EnterSkillTargeting(currentSkillSO);
    }
    private void EnterSkillTargeting(SkillAsset skill)
    {
        if (skill == null) return;

        int effectiveCost = skill.GetEffectiveCost(acting);
        if (!acting.HasMP(effectiveCost)) { Debug.Log($"[Skill] MP 부족"); return; }
        if (acting.IsSkillOnCooldown(skill)) { Debug.Log($"[Skill] 쿨다운"); return; }

        if (skill is ISelfCastSkill self && self.SelfCastOnSelect)
        {
            if (skillProcessor.IsResolvingSelfCast) return;
            bool isFreeAction = false;
            int route = acting.GetTrainingRouteIndex(skill);

            if (skill is SelfStateSkill sss) { if (sss.trainingUseFreeAction && sss.routeForFreeAction >= 0 && route == sss.routeForFreeAction) isFreeAction = true; }
            else if (skill is SelfStateCleanseSkill scs) { if (scs.trainingUseFreeAction && scs.routeForFreeAction >= 0 && route == scs.routeForFreeAction) isFreeAction = true; }
            if (!isFreeAction && skill is HostilitySpikeSkill hss) { if (hss.trainingUseFreeAction && hss.routeForFreeAction >= 0 && route == hss.routeForFreeAction) isFreeAction = true; }
            if (!isFreeAction && skill is SelfBeastDomainSkill bds) { if (bds.trainingUseFreeAction && bds.routeForFreeAction >= 0 && route == bds.routeForFreeAction) isFreeAction = true; }

            StartCoroutine(skillProcessor.PerformSelfCastFlow(skill, acting, isFreeAction));
            return;
        }

        state = BattleState.Targeting;
        inputHandler.PrepareSkillTargeting(skill, acting);

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

        StartCoroutine(RunSkillExecute(CurrentSkillSO, acting, target, null, default));
    }
    public void ConfirmSkillOnTile(Tilemap map, Vector3Int cell)
    {
        if (state != BattleState.Targeting) return;
        state = BattleState.Resolving;
        inputHandler.ClearAllPreviews();

        StartCoroutine(RunSkillExecute(CurrentSkillSO, acting, null, map, cell));
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
                if (IsPlayerTurn && remainingActions > 0) state = BattleState.ActionSelect;
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
        var victims = GetUnitsInArea(map, area);
    }

    bool IsCellOccupied(Tilemap map, Vector3Int cell)
    {
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
        var valid = new HashSet<Vector3Int>(cells.Where(c => map.HasTile(c)));
        foreach (var u in FindObjectsOfType<BattleUnit>())
        {
            if (u == null || u.CurrentMap != map) continue;
            if (valid.Contains(u.Cell)) yield return u;
        }
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
        if (unit == null || grid == null)
        {
            OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        _isPostSkillMoveInProgress = true;
        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(unit.team, unit.Cell).ToList();

        if (moveOptions.Count == 0)
        {
            _isPostSkillMoveInProgress = false;
            OnActionConsumed(BattleAction.Attack);
            yield break;
        }

        ShowMovePreview(unit.CurrentMap, moveOptions);

        while (_isPostSkillMoveInProgress) yield return null;
    }
    IEnumerator Co_MoveAfterSkillThenConsume(BattleUnit unit, Tilemap map, Vector3Int toCell)
    {
        var fromCell = unit.Cell;
        grid.SetOccupied(unit.team, fromCell, false);

        yield return unit.AnimateMoveTo(map, toCell);

        grid.SetOccupied(unit.team, unit.Cell, true);
        _isPostSkillMoveInProgress = false;

        while (_reactionLocks > 0) yield return null;
        OnActionConsumed(BattleAction.Attack);
    }

    public void SpawnBeastDomainZone(Tilemap map, BattleUnit owner, Vector3Int centerCell, int radius, int durationTurns)
    {
        if (!owner || !map) return;

        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var old = _beastZones[i];
            if (old.owner != owner) continue;
            _beastZones.RemoveAt(i);
        }

        var cells = new List<Vector3Int>();
        foreach (var c in AreaShapes.BeastDomainArea(centerCell, radius)) cells.Add(c);

        int token = 0;
        // [수정] 핸들러 하이라이터 사용
        if (inputHandler.beastDomainHighlighter != null)
        {
            token = inputHandler.beastDomainHighlighter.CreateGroup();
            inputHandler.beastDomainHighlighter.SetGroupCells(token, map, cells);
        }

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

        Debug.Log($"[BeastDomain] {owner.name} 생성 - token:{token}");
    }

    public void CreateStatusTileZone(BattleUnit owner, Tilemap map, Vector3Int cell, int zoneDuration, TileBase newTileBase, StatusId statusId, int stack = 1, int statusDuration = 3)
    {
        if (!map.HasTile(cell)) return;

        var existing = _statusTileZones.FirstOrDefault(z => z.map == map && z.cell == cell);
        if (existing != null)
        {
            existing.owner = owner;
            existing.remainingTurns = zoneDuration;
            existing.effectStatusId = statusId;
            existing.effectStack = stack;
            existing.effectDuration = statusDuration;
            if (newTileBase != null) map.SetTile(cell, newTileBase);
            return;
        }

        TileBase oldTile = map.GetTile(cell);
        if (newTileBase != null) map.SetTile(cell, newTileBase);

        var newZone = new StatusTileZone
        {
            owner = owner,
            map = map,
            cell = cell,
            remainingTurns = zoneDuration,
            originalTile = oldTile,
            effectStatusId = statusId,
            effectStack = stack,
            effectDuration = statusDuration
        };
        _statusTileZones.Add(newZone);
    }
    void TickBeastDomainOnTurnStart(BattleUnit unitWhoseTurnStarted)
    {
        if (unitWhoseTurnStarted == null) return;

        for (int i = _beastZones.Count - 1; i >= 0; i--)
        {
            var z = _beastZones[i];
            if (z.owner != unitWhoseTurnStarted) continue;

            TryApplyBeastDomainRageTraining(z.owner);
            z.remainingTurns--;

            if (z.remainingTurns <= 0)
            {
                // [수정] 핸들러 하이라이터 사용
                if (z.highlightToken != 0 && inputHandler.beastDomainHighlighter != null)
                    inputHandler.beastDomainHighlighter.ClearGroup(z.highlightToken);

                _beastZones.RemoveAt(i);
            }
        }
    }

    void TickStatusTileZonesOnTurnStart(BattleUnit unit)
    {
        if (unit == null) return;

        for (int i = _statusTileZones.Count - 1; i >= 0; i--)
        {
            var z = _statusTileZones[i];
            if (z.owner == unit)
            {
                z.remainingTurns--;
                if (z.remainingTurns <= 0)
                {
                    if (z.map != null) z.map.SetTile(z.cell, z.originalTile);
                    _statusTileZones.RemoveAt(i);
                }
            }
        }
    }
    void TryApplyBeastDomainRageTraining(BattleUnit owner)
    {
        if (owner == null || owner.data == null || owner.data.skills == null) return;
        SelfBeastDomainSkill domainSkill = null;
        foreach (var s in owner.data.skills)
        {
            domainSkill = s as SelfBeastDomainSkill;
            if (domainSkill != null) break;
        }
        if (domainSkill == null) return;

        int route = owner.GetTrainingRouteIndex(domainSkill);
        if (!domainSkill.trainingReduceRageOnTurnStart || domainSkill.routeForRageReduceOnTurnStart < 0 || route != domainSkill.routeForRageReduceOnTurnStart) return;

        float amount = owner.MagicDamage * domainSkill.rageReducePerClv;
        if (amount <= 0f) return;

        owner.AddRage(-amount);
    }

    public IEnumerator Co_HandleFearTurn(BattleUnit unit)
    {
        if (unit == null) yield break;
        var usc = unit.GetComponent<UnitStateController>();
        if (usc == null) yield break;
        var map = unit.CurrentMap;
        if (map == null || grid == null) yield break;

        var candidates = GetFearRetreatCandidates(unit);
        if (candidates.Count > 0)
        {
            var dest = candidates[Random.Range(0, candidates.Count)];
            var from = unit.Cell;
            grid.SetOccupied(unit.team, from, false);
            yield return unit.AnimateMoveTo(map, dest);
            grid.SetOccupied(unit.team, unit.Cell, true);
        }

        if (unit.team == Team.Player) EndPlayerTurn();
        else EndEnemyTurn(unit);
    }
    List<Vector3Int> GetFearRetreatCandidates(BattleUnit unit)
    {
        var result = new List<Vector3Int>();
        if (unit == null || grid == null) return result;
        var map = unit.CurrentMap;
        if (map == null) return result;

        var origin = unit.Cell;
        Vector3Int[] offsets;
        if (unit.team == Team.Player) offsets = new[] { new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0), };
        else offsets = new[] { new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), };

        foreach (var off in offsets)
        {
            var dest = origin + off;
            if (!map.HasTile(dest)) continue;
            if (grid.IsOccupied(Team.Player, dest) || grid.IsOccupied(Team.Enemy, dest)) continue;
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
            if (fromIn && toIn) return true;
        }
        return false;
    }

    bool IsEnemyOf(BattleUnit a, BattleUnit b)
    {
        return a != null && b != null && a.team != b.team;
    }

    public void UnlockSkillConfirm()
    {
        _skillConfirmLocked = false;
    }

    public void FinishActionAfterSkill()
    {
        var skill = currentSkillSO;
        var unit = acting;

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

        OnActionConsumed(BattleAction.Attack);

        currentSkill = default;
        currentSkillSO = null;
        UpdateTargetingHint();
    }

    // [수정] 핸들러 위임 (ClearTransient가 아니라 ClearMovePreview 호출)
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

    // [수정] 핸들러 하이라이터 사용
    public int CreateSkillPreviewToken() => inputHandler.skillHighlighter != null ? inputHandler.skillHighlighter.CreateGroup() : 0;
    public void SetSkillPreviewForToken(int token, Tilemap map, IEnumerable<Vector3Int> cells) => inputHandler.skillHighlighter?.SetGroupCells(token, map, cells);
    public void ClearSkillPreviewToken(int token) => inputHandler.skillHighlighter?.ClearGroup(token);

    public void EmitPassiveLabelAutoClear(BattleUnit u, string label, float seconds = 1.0f)
    {
        OnUnitPassiveLabel?.Invoke(u, label);
        StartCoroutine(Co_ClearPassiveLabelAfter(seconds));
    }
    IEnumerator Co_ClearPassiveLabelAfter(float t)
    {
        yield return new WaitForSeconds(t);
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