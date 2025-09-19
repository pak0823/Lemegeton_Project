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

    [Header("Skill Runtime")]
    public bool isSelectingSkill = false;          // 스킬 선택 패널이 열렸는지
    public SkillDefinition currentSkill;           // 현재 선택된 스킬(선택 전이면 id 미정)
    public Vector3Int selectedCell;                // 타일 스킬용 내부 커서
    public SkillAsset currentSkillSO;                   // 현재 선택된 SO 스킬
    public event System.Action<SkillAsset[]> OnSkillPanelPopulateSO; // SO 목록 UI용

    // UI와 통신용 이벤트
    public event System.Action<bool> OnSkillPanelToggled;  // true=열기/false=닫기

    [Header("Projectile/VFX")]
    public GameObject projectilePrefab;     // 투사체

    //점프 애니메이션 속도 및 높이 값
    [SerializeField] float jumpDuration = 0.08f;     // 시간 기반
    [SerializeField] float jumpArc = 0.15f;

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
    }

    void Start()
    {
        if (provider == null)
        {
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
            if (provider != null) provider.OnMapsReady += Init;
        }

        if (!initialized && provider != null && provider.PlayerFloor != null && provider.EnemyFloor != null)
        {
            Init();
        }
            
    }

    void OnDisable()
    {
        if (provider != null) provider.OnMapsReady -= Init;
    }
    #endregion

    #region Initialization

    void Init() //초기 세팅
    {
        var units = FindObjectsOfType<BattleUnit>().ToList();
        if (units.Count == 0) return;

        float minAGI = units.Min(u => u.EffectiveAGI);
        float maxAGI = units.Max(u => u.EffectiveAGI);

        foreach (var u in units)
        {
            var map = (u.team == Team.Player) ? provider.PlayerFloor : provider.EnemyFloor;
            var cell = map.WorldToCell(u.transform.position);

            u.Bind(map, cell);
            grid.SetOccupied(u.team, u.Cell, true);
            u.InitializeATB(minAGI, maxAGI);

            u.OnDied += HandleUnitDied;
        }

        // AGI 스냅샷 저장
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
                if (logAGIOnStatusChange)
                {
                    var unitCapture = u; // 클로저 캡처 주의: 루프 변수 복사
                    sc.OnStatusChanged += () => LogUnitAGI(unitCapture, "StatusChanged");
                }
            }
        }

        initialized = true;
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

    [Header("Debug/Logs")]
    [SerializeField] bool logAGIOnTurnStart = true;
    [SerializeField] bool logAGIOnStatusChange = false;

    void LogUnitAGI(BattleUnit u, string reason)
    {
        if (u == null) return;
        var sc = u.GetComponent<StatusController>();
        int stacks = (sc != null) ? sc.GetStacks(StatusId.Slow) : 0;            // GetStacks
        float mult = (sc != null) ? sc.GetAgilityMultiplier() : 1f;             // GetAgilityMultiplier
        Debug.Log($"[AGI:{reason}] {u.team}/{u.name}  base={u.AGI:0.##}  slowStacks={stacks}  mult={mult:0.##}  effective={u.EffectiveAGI:0.##}");
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

        var sc = unit.GetComponent<StatusController>();
        if (sc != null) sc.OnTurnStart();
        if (logAGIOnTurnStart) LogUnitAGI(unit, "TurnStart");

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

        //UpdateTurnIndicator();
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

    void ResolveAttack(BattleUnit target)
    {
        if (acting == null) return;

        state = BattleState.Resolving;
        ClearAllPreviews();
        Debug.Log("공격 시작 → 임팩트에서 데미지 1회 적용");
        StartCoroutine(Co_AttackThenConsume(acting, target, BattleAction.Attack));
    }

    IEnumerator Co_AttackThenConsume(BattleUnit attacker, BattleUnit target, BattleAction act)
    {
        bool impactDone = false;
        System.Action impact = null; 
        impact = () =>
        {
            attacker.OnAttackImpact -= impact;
            impactDone = true;
            if (target != null && !target.IsDead)
            {
                target.PlayHit();
                target.TakeDamage(attacker.PhysicalDamage);
            }
        };
        attacker.OnAttackImpact += impact;
        yield return attacker.AnimateAttack(target);

        if (!impactDone && target != null && !target.IsDead)
        {
            Debug.LogWarning("[Attack] 임팩트 이벤트 없음 → 폴백으로 데미지 적용");
            target.PlayHit();
            target.TakeDamage(attacker.PhysicalDamage);
        }

        OnActionConsumed(act); // 공격 1회 소비 → 남은 토큰 판단
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
            if (!isSelectingSkill) OpenSkillPanel();
            return;
        }
        if (state == BattleState.Moving)
        {
            ClearMovePreview();
            ClearTargetSelection();
            state = BattleState.ActionSelect;
            return;
        }
        // 그밖의 상황에서 패널이 열려 있다면(= 취소 2회째) 패널 닫기
        if (isSelectingSkill)
        {
            ClearAllPreviews();
            CloseSkillPanel();
            state = BattleState.ActionSelect;
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
        Debug.Log($"[Die] {dead.name}");

        // 사망 연출 대기 후 제거
        StartCoroutine(Co_DieThenDestroy(dead));
        CheckBattleEnd(); // 전투 종료 판정
    }

    IEnumerator Co_DieThenDestroy(BattleUnit u)
    {
        yield return u.PlayDieAndWait(1.0f); // 필요시 시간 조정 or 이벤트로 대체
        Destroy(u.gameObject);// 오브젝트 제거
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
            Debug.Log("[Battle] 승리!");
            Shared.SceneTransitionManager.ReturnToSavedPoint();
        }
        else if (!anyPlayer) 
        { 
            Debug.Log("[Battle] 패배...");
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

        // TODO(도발 연동 자리): 도발 대상 우선 선택 훅
        // var tauntTarget = FindTauntTarget(players);
        // BattleUnit target = tauntTarget ?? players[Random.Range(0, players.Count)];

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

        System.Action onFire = null;
        onFire = () =>
        {
            // 한 번만
            caster.OnAttackImpact -= onFire;
            if (fired) return;
            fired = true;

            System.Action onArrive = () =>
            {
                if (p.trapPrefab != null && p.map != null)
                {
                    // 같은 타일내에 이미 거미줄이 있으면 제거
                    WebTrapController.RemoveAt(p.map, p.cell);

                    // 새 거미줄 생성
                    var trap = Instantiate(p.trapPrefab, targetW, Quaternion.identity);
                    trap.Init(p.map, p.cell, p.owner);
                }
                arrived = true;
            };

            if (projPrefab != null)
            {
                var go = Instantiate(projPrefab, startW, Quaternion.identity);
                var pc = go.GetComponent<ProjectileController>();
                if (pc != null) pc.Init(startW, targetW, onArrive, p.projectileSpeed);
                else onArrive(); // 컴포넌트 누락 폴백
            }
            else
            {
                // 프리팹이 없으면 바로 생성(연출 생략)
                onArrive();
            }
        };
        caster.OnAttackImpact += onFire;   // 임팩트에서 발사하도록 훅 연결

        // 발사 모션 시작
        yield return caster.AnimateShootWeb();  // 애니 끝까지 대기 (발사는 이미 중간에 실행됨)

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
    public void OnClickEscape()
    {
        // 플레이어 턴에서만 허용, 해 resolving 중(피해 계산 등)에는 금지
        if (acting == null || acting.team != Team.Player) return;
        if (state == BattleState.Resolving) return;

        // 진행 중이던 선택/표시 정리
        CancelCurrentAction();      // Moving/Targeting 상태면 ActionSelect로 되돌림
        ClearAllPreviews();
        ClearTargetSelection();

        Debug.Log("[Battle] 도망가기 → 탐험으로 복귀");
        Shared.SceneTransitionManager.ReturnToSavedPoint(); // 저장된 씬/좌표로 페이드 복귀
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
    }
    public void CloseSkillPanel()
    {
        isSelectingSkill = false;
        OnSkillPanelToggled?.Invoke(false);
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

        // Unit 타겟형이면: AGI 내림차순 사이클 구성 후 첫 타겟으로 마커 표시
        if (skill.targetMode == SkillTargetMode.Unit)
        {
            BuildTargetCycle();           // Enemy만, AGI desc
            if (targetCycle.Count > 0)
                SelectTarget(0);          // 첫 타겟(=가장 빠른 AGI)으로 마커/미리보기
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
                //selectedCell = map.HasTile(hover) ? hover : new Vector3Int(-2, -2, 0);  // 마우스가 타일 위면 그 타일, 아니면 기본(-2,-2)
                //PreviewSkillAreaOnTile(map, selectedCell); // 즉시 프리뷰
                if (map.HasTile(hover))
                {
                    selectedCell = hover;
                    PreviewSkillAreaOnTile(map, selectedCell);
                }
            }
        }
    }

    // 현재 선택된 스킬의 범위를 "유닛 기준"으로 미리보기
    public void PreviewSkillAreaOnUnit(BattleUnit unit)
    {
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) { ClearAllPreviews(); return; }
        if (unit == null || unit.team == Team.Player) { ClearAllPreviews(); return; } // 아군(플레이어)에는 미리보기 표시하지 않음

        var origin = unit.Cell;                            // 유닛의 현재 셀
        var cells = currentSkillSO.GetAreaCells(origin, SkillLibrary.IsOddColumn(origin));
        var map = unit.CurrentMap;

        // 범위 내 유닛 수집 → 패널 하이라이트
        skillHighlighter.ShowCells(map, cells);
        var victims = GetUnitsInArea(map, cells);
        StatusPanel?.HighlightUnits(victims);
    }

    // 현재 선택된 스킬의 범위를 "타일 기준"으로 미리보기
    public void PreviewSkillAreaOnTile(Tilemap map, Vector3Int originCell)
    {
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Tile) { ClearAllPreviews(); return; }
        if (map == null) { ClearAllPreviews(); return; }

        var cells = currentSkillSO.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        skillHighlighter.ShowCells(map, cells);

        // 범위 내 유닛 수집 → 패널 하이라이트
        var victims = GetUnitsInArea(map, cells);
        StatusPanel?.HighlightUnits(victims);
    }

    public void ConfirmSkillOnUnit(BattleUnit target)
    {
        if (!IsPlayerTurn || acting == null || target == null) return;
        if (currentSkillSO == null || currentSkillSO.targetMode != SkillTargetMode.Unit) return;

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

        // 1) 타겟 앞 점프(연출)
        if (TryGetFrontCellOfTarget(caster, target, out var frontCell))
        {
            Vector3 frontW = provider.EnemyFloor.GetCellCenterWorld(frontCell);
            yield return caster.AnimateJumpToWorld(frontW, jumpDuration, null, jumpArc);
        }

        // 2) 공격 모션 중 임팩트 타이밍에 해결
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

        // 4) (임시) 시전 후 이동 정책: legacyId 경로 유지
        if (caster != null && !caster.IsDead)
        {
            if (TryComputePostMoveDestinationLegacy(skill.legacyId, caster, out var destCell))
                yield return Co_MoveUnitSmooth(caster, caster.CurrentMap, destCell, postMoveDuration);
        }

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

        // 시전 후 이동
        if (caster != null && !caster.IsDead)
        {
            if (TryComputePostMoveDestinationLegacy(skill.legacyId, caster, out var destCell))
                yield return Co_MoveUnitSmooth(caster, caster.CurrentMap, destCell, postMoveDuration);
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
        foreach (var unit in FindObjectsOfType<BattleUnit>())
        {
            if (unit == null || unit.IsDead) continue;
            if (unit.CurrentMap == map && unit.Cell == cell) return true;
        }
        return false;
    }
    bool IsWalkableCell(Tilemap map, Vector3Int cell)
    {
        // 맵에 타일이 있어야 하고, 유닛이 점유하고 있지 않아야 한다
        if (!map.HasTile(cell)) return false;
        if (IsCellOccupied(map, cell)) return false;
        return true;
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

    // 이동 보간 시간(인스펙터에서 조절 가능)
    [SerializeField] float postMoveDuration = 0.1f;
    
    // 목적지 계산: 스킬2=앞(NW), 스킬1=뒤(SE), 최대 2칸, 워커블만
    bool TryComputePostMoveDestinationLegacy(SkillId id, BattleUnit caster, out Vector3Int dest)
    {
        dest = caster.Cell;

        if (id != SkillId.Skill1 && id != SkillId.Skill2) return false;

        var stepAx = (id == SkillId.Skill2) ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
        var map = caster.CurrentMap;
        var curAx = SkillLibrary.ToAxial(caster.Cell);
        var last = caster.Cell;

        for (int i = 0; i < 2; i++)
        {
            curAx = new Vector2Int(curAx.x + stepAx.x, curAx.y + stepAx.y);
            var next = SkillLibrary.ToOffset(curAx);
            if (!IsWalkableCell(map, next)) break;
            last = next;
        }

        if (last == caster.Cell) return false;
        dest = last;
        return true;
    }

    IEnumerator Co_MoveUnitSmooth(BattleUnit unit, Tilemap map, Vector3Int toCell, float duration)
    {
        var fromCell = unit.Cell;
        var startPos = unit.transform.position;
        var endPos = map.GetCellCenterWorld(toCell);
        endPos.z = startPos.z;

        // 점유 해제(이동 시작)
        grid.SetOccupied(unit.team, fromCell, false);

        // --- 애니메이션 준비 ---
        var anim = unit.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.ResetTrigger("Idle");
            anim.SetBool("IsMoving", true);
            Vector2 dir = (endPos - startPos);
            // 루트모션을 쓰지 않는 구성(Transform 직접 보간) 기준
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            unit.transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(t));
            yield return null;
        }

        // 좌표/맵 필드 갱신
        unit.MoveTo(map, toCell);

        // 점유 설정(이동 완료)
        grid.SetOccupied(unit.team, unit.Cell, true);

        if (anim != null)
        {
            anim.SetBool("IsMoving", false);
        }
    }


    public void ExecuteSkillDamage(BattleUnit caster, IEnumerable<BattleUnit> victims, SkillAsset source, Tilemap map, Vector3Int originCell)
    {
        if (caster == null || source == null) return;

        // (임시) 아군/적 팀 구분
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
                v.TakeDamage(damage);
                Debug.Log("damage:"+ damage);
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
    }


    // 타겟팅 취소/종료 시 미리보기 지우기
    public void ShowMovePreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
    => moveHighlighter?.ShowCells(baseMap, cells);
    public void ShowSkillPreview(Tilemap baseMap, IEnumerable<Vector3Int> cells)
        => skillHighlighter?.ShowCells(baseMap, cells);

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


    bool TryGetFrontCellOfTarget(BattleUnit caster, BattleUnit target, out Vector3Int frontCell) //근접 공격 시 타겟 앞 타일로 이동
    {
        frontCell = target.Cell;

        // 타겟 기준으로 '시전자 방향'을 찾는다.
        int dirIdx = SkillLibrary.NearestDirectionIndex(target.Cell, caster.Cell);
        var stepAx = SkillLibrary.DirIndexToAxial(dirIdx);

        var tAx = SkillLibrary.ToAxial(target.Cell);
        var frontAx = new Vector2Int(tAx.x + stepAx.x, tAx.y + stepAx.y);
        var candidate = SkillLibrary.ToOffset(frontAx);

        var map = target.CurrentMap;

        // 실제로 이동하는 건 아니고 '연출용 좌표'로만 쓸 거라 HasTile 정도만 체크
        if (map != null && map.HasTile(candidate))
        {
            frontCell = candidate;
            return true;
        }
        return false;
    }

    IEnumerator Co_GapCloseThenResolveOnTarget(SkillDefinition def, BattleUnit caster, BattleUnit target)
    {
        state = BattleState.Resolving;

        Vector3 originalW = caster.transform.position;

        // 1) 타겟 앞 타일(적 맵 좌표)의 월드 지점으로 '연출용 점프'
        if (TryGetFrontCellOfTarget(caster, target, out var frontCell))
        {
            Vector3 frontW = provider.EnemyFloor.GetCellCenterWorld(frontCell);
            yield return caster.AnimateJumpToWorld(frontW, jumpDuration, null, jumpArc);
        }

        // 2) 공격 모션 + 임팩트 타이밍에 범위피해(대상 유닛 셀을 '원점'으로)
        bool impactDone = false;
        System.Action impact = null;
        impact = () =>
        {
            caster.OnAttackImpact -= impact;
            impactDone = true;
            ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster); // 기존 범위·피해 루틴 재사용
        };
        caster.OnAttackImpact += impact;

        yield return caster.AnimateAttack(target);  // 기존 근접 모션 재사용

        if (!impactDone) // 폴백(애니 이벤트 누락 시)
            ResolveSkillAtCell(def, target.CurrentMap, target.Cell, caster);

        // 3) 원위치 '순간 복귀'
        caster.transform.position = originalW;

        FinishActionAfterSkill(); // 패널 닫기/토큰 소모/턴 진행
    }

    IEnumerator Co_ProjectileSkillThenFinish(SkillDefinition def, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        state = BattleState.Resolving;

        bool castEnded = false;   // 캐스터 모션 종료(AnimEvent_AttackEnd)
        bool projEnded = false;   // 투사체 도착/폭발/피해 적용까지 완료

        // 1) 캐스터 모션 종료 이벤트 훅
        System.Action onCastEnd = null;
        onCastEnd = () => { caster.OnAttackEnded -= onCastEnd; castEnded = true; };
        caster.OnAttackEnded += onCastEnd;

        // 2) 발사 타이밍(AnimEvent_AttackImpact) 훅: 투사체 생성만!
        System.Action onFire = null;
        onFire = () =>
        {
            caster.OnAttackImpact -= onFire;
            // 투사체 생성 및 Init
            if (projectilePrefab != null)
            {
                var startW = caster.transform.position;
                var targetW = map.GetCellCenterWorld(cell);
                var go = Instantiate(projectilePrefab, startW, Quaternion.identity);
                var pc = go.GetComponent<ProjectileController>();
                if (pc != null)
                {
                    // 일정 속도로 이동시키기 (초당 3유닛)
                    pc.Init(startW, targetW, () => {
                        ResolveSkillAtCell(def, map, cell, caster); // 폭발 직후에 범위피해 적용
                    }, speedUnitsPerSec: 3f);
                }
                else
                {
                    // 컴포넌트 누락 대비 폴백
                    StartCoroutine(FallbackProjectile(startW, targetW, 0.35f, () =>
                    {
                        ResolveSkillAtCell(def, map, cell, caster);
                        projEnded = true;
                    }));
                }
            }
            else
            {
                // 프리팹 없을 땐 즉시 적용(테스트용)
                ResolveSkillAtCell(def, map, cell, caster);
                projEnded = true;
            }
        };

        caster.OnAttackImpact += onFire;

        // 3) 제자리 원거리 모션 재생
        yield return caster.AnimateRanged(); // 내부에서 AttackEnd를 기다림

        // 4) 안전장치: 혹시 모션이 먼저 끝나도 투사체가 끝날 때까지 대기
        float timeout = 3f; // 과도한 지연 방지
        while (!(castEnded && projEnded) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        FinishActionAfterSkill();
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
