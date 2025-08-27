using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.GraphicsBuffer;

public enum BattleState { Idle, ActionSelect, Moving, Targeting, Resolving, EndTurn }
public enum BattleAction { Move, Attack }

public class BattleManager : MonoBehaviour
{
    #region Variables
    public BattleGridManager grid;
    public TurnOrderManager turn;
    public LayerMask unitMask;

    BattleState state = BattleState.Idle;
    bool atbPaused = false; // 턴 중 ATB 충전 멈춤
    BattleUnit acting;
    List<Vector3Int> moveOptions = new();

    // === 추가턴 설계 ===
    [SerializeField] int baseActionsPerTurn = 1; // 기본 행동 토큰(기본 1)
    int remainingActions = 0; // 남은 토큰
    readonly HashSet<BattleAction> usedActions = new(); // 이번 턴에 사용한 행동(중복 금지)

    public Highlighter highlighter;
    IBattleMapProvider provider;
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

    // === 수동 종료 감지용 ===
    bool manualEndRequested = false;

    // ATB UI 업데이트용 이벤트
    public delegate void OnATBChangedDelegate(BattleUnit unit, float currentATB, float maxATB);
    public event OnATBChangedDelegate OnATBChanged;
    readonly System.Random rng = new System.Random();// 소난수 발생기

    [Header("Skill Runtime")]
    public bool isSelectingSkill = false;          // 스킬 선택 패널이 열렸는지
    public SkillDefinition currentSkill;           // 현재 선택된 스킬(선택 전이면 id 미정)
    public Vector3Int selectedCell;                // 타일 스킬용 내부 커서

    // UI와 통신용 이벤트
    public event System.Action<bool> OnSkillPanelToggled;  // true=열기/false=닫기
    public event System.Action<SkillDefinition[]> OnSkillPanelPopulate;   // 버튼 라벨 세팅용

    [Header("Projectile/VFX")]
    public GameObject projectilePrefab;     // 투사체
    [SerializeField] float projectileDuration = 0.35f;
    [SerializeField] float explosionDuration = 0.25f;
    [SerializeField] float projectileArcHeight = 0.6f;

    //점프 애니메이션 속도 및 높이 값
    [SerializeField] float jumpDuration = 0.08f;     // 시간 기반
    [SerializeField] float jumpArc = 0.15f;
    #endregion

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

    void Init()
    {
        var units = FindObjectsOfType<BattleUnit>().ToList();
        if (units.Count == 0) return;

        float minAGI = units.Min(u => u.AGI);
        float maxAGI = units.Max(u => u.AGI);

        foreach (var u in units)
        {
            var map = (u.team == Team.Player) ? provider.PlayerFloor : provider.EnemyFloor;
            var cell = map.WorldToCell(u.transform.position);

            u.Bind(map, cell);
            grid.SetOccupied(u.team, u.Cell, true);
            u.InitializeATB(minAGI, maxAGI);

            u.OnDied += HandleUnitDied;
        }

        //turn.BuildOrder(units);
        initialized = true;
    }

    #endregion

    void Update()
    {
        if (!initialized) return;

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
                    .ThenByDescending(u => u.AGI)          // 2) AGI 높은 순
                    .ThenBy(u => rng.NextDouble())         // 3) 아주 작은 난수
                    .First();

                acting = selected;
                atbPaused = true;
                StartTurn(acting);
            }
        }
    }


    #region Turn Management
    void StartTurn(BattleUnit unit)
    {
        if (unit == null) return;

        acting = unit;
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        highlighter?.Clear();
        ClearTargetSelection();
        manualEndRequested = false;

        // 모든 ATB 정지
        atbPaused = true;

        if (unit.team == Team.Player)
        {
            state = BattleState.ActionSelect; // 플레이어 입력 허용
            Debug.Log($"[PlayerTurn] {unit.name} 턴 시작 → ATB 정지");
        }
        else
        {
            state = BattleState.Resolving; // 입력 잠금
            Debug.Log($"[EnemyTurn] {unit.name} 턴 시작 → ATB 정지");
            StartCoroutine(EnemyTurnRoutine(unit));
        }

        UpdateTurnIndicator();
    }

    public void OnClickEndTurn()
    {
        if (acting == null || acting.team != Team.Player) return;
        manualEndRequested = true;   // 회복 판정용 플래그만 남김
        EndPlayerTurn();             // 종료 로직은 한 군데로 집약
    }

    void UpdateTurnIndicator() //임시 테스트용 턴 확인
    {
        if (acting == null) { Debug.Log("[Turn] (없음)"); return; }
        Debug.Log($"[Turn] {acting.team} - {acting.name}");
    }
    #endregion

    #region Movement
    public void OnClickMove()
    {
        if (acting == null || !IsPlayerTurn) return; // 적 턴 금지
        if (usedActions.Contains(BattleAction.Move) || remainingActions <= 0) return; // 중복/토큰 없음

        // 스킬 패널/선택 상태 정리
        CloseSkillPanel();       // 패널 열려있으면 닫기
        highlighter?.Clear();    // 남아있을지 모를 프리뷰 정리
        ClearTargetSelection();  // 타겟 마커 숨김

        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(acting.team, acting.Cell).ToList();
        highlighter?.ShowCells(acting.CurrentMap, moveOptions);
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
                highlighter?.Clear();
                moveOptions.Clear();
                return;
            }
        }
        else if (state == BattleState.Targeting)
        {
            if (currentSkill.GetAreaCells != null
                && currentSkill.targetMode == SkillTargetMode.Tile
                && clickedMap == provider.EnemyFloor)
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

        highlighter?.Clear();
        ClearTargetSelection();
        OpenSkillPanel();
    }

    public void OnUnitClicked(BattleUnit target)
    {
        if (!IsPlayerTurn) return;
        if (!IsTargeting) return;
        if (currentSkill.GetAreaCells == null) return;
        if (currentSkill.targetMode != SkillTargetMode.Unit) return;
        if (target == null || target.team == acting.team) return;

        ConfirmSkillOnUnit(target);
    }

    void ResolveAttack(BattleUnit target)
    {
        if (acting == null) return;

        state = BattleState.Resolving;
        highlighter?.Clear();
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
                target.TakeDamage(attacker.AttackDamage);
            }
        };
        attacker.OnAttackImpact += impact;
        yield return attacker.AnimateAttack(target);

        if (!impactDone && target != null && !target.IsDead)
        {
            Debug.LogWarning("[Attack] 임팩트 이벤트 없음 → 폴백으로 데미지 적용");
            target.PlayHit();
            target.TakeDamage(attacker.AttackDamage);
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
        highlighter?.Clear();
        ClearTargetSelection();

        if (manualEndRequested && usedActions.Count == 0)
        {
            acting.Heal(1);
            Debug.Log("[EndPlayerTurn] 행동 없이 수동 종료 → HP +1 회복");
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
        if (state == BattleState.Targeting && currentSkill.GetAreaCells != null)
        {
            ClearPreview(); // 하이라이트 제거
            ClearTargetSelection(); // TargetMarker 제거
            currentSkill = default;  // 스킬만 초기화
            state = BattleState.ActionSelect;
            if (!isSelectingSkill) OpenSkillPanel();
            return;
        }
        if (state == BattleState.Moving)
        {
            ClearPreview();
            ClearTargetSelection();
            state = BattleState.ActionSelect;
            return;
        }
        // 그밖의 상황에서 패널이 열려 있다면(= 취소 2회째) 패널 닫기
        if (isSelectingSkill)
        {
            ClearPreview();
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
            .OrderByDescending(u => u.AGI)
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
        if (currentSkill.GetAreaCells != null && currentSkill.targetMode == SkillTargetMode.Unit)
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
        if (!IsPlayerTurn || !IsTargeting || selectedTarget == null) return;// 스킬 미선택이면 무시
        if (currentSkill.GetAreaCells == null) return;  // Unit형 스킬일 때만 C로 확정
        if (currentSkill.targetMode != SkillTargetMode.Unit) return;
        if (selectedTarget == null) return;

        ConfirmSkillOnUnit(selectedTarget);
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
            //Shared.SceneTransitionManager.ReturnToSavedPoint();
        }
        else if (!anyPlayer) 
        { 
            Debug.Log("[Battle] 패배...");
            //Shared.SceneTransitionManager.ReturnToSavedPoint();
        }
    }
    #endregion

    #region Enemy AI
    IEnumerator EnemyTurnRoutine(BattleUnit enemy)
    {
        yield return new WaitForSeconds(0.5f); // 살짝 텀

        // 행동 순서 예시: 공격 우선, 이동 가능 시 이동
        var players = FindObjectsOfType<BattleUnit>()
                        .Where(u => u.team == Team.Player && !u.IsDead)
                        .ToList();
        if (players.Count == 0) { EndEnemyTurn(enemy); yield break; }

        BattleUnit target = players
            .OrderBy(u => grid.CrossMapDistance(provider.PlayerFloor, enemy.CurrentMap, enemy.Cell, u.CurrentMap, u.Cell))
            .First();

        int RandPartten = Random.Range(0, 11);
        Debug.Log("RandPartten:" + RandPartten);
        if (RandPartten < 6)
        {
            ResolveAttack(target); // 공격
            yield return new WaitUntil(() => state != BattleState.Resolving);
        }
        else
        {
            // 이동
            var candidates = grid.GetAdjacentWalkable(enemy.team, enemy.Cell).ToList();
            if (candidates.Count > 0)
            {
                var best = candidates
                    .OrderBy(c => grid.CrossMapDistance(
                    provider.PlayerFloor, // 기준 맵
                    enemy.CurrentMap, // fromMap
                    enemy.Cell, // fromCell (현재 위치)
                    target.CurrentMap, // toMap
                    c)).First(); // toCell (이동 후보)
                yield return enemy.AnimateMoveTo(enemy.CurrentMap, best);
                enemy.MoveTo(enemy.CurrentMap, best);
            }
        }

        EndEnemyTurn(enemy);
    }

    void EndEnemyTurn(BattleUnit enemy)
    {
        enemyRoutine = null;

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
        highlighter?.Clear();
        ClearTargetSelection();

        Debug.Log("[Battle] 도망가기 → 탐험으로 복귀");
        Shared.SceneTransitionManager.ReturnToSavedPoint(); // 저장된 씬/좌표로 페이드 복귀
    }


    public void OpenSkillPanel()
    {
        isSelectingSkill = true;

        // 스킬 목록 제공(여기선 1~5까지; 4개만 쓰면 배열 길이만 줄이면 됨)
        var defs = new[]
        {
            SkillLibrary.Get(SkillId.Skill1),
            SkillLibrary.Get(SkillId.Skill2),
            SkillLibrary.Get(SkillId.Skill3),
            SkillLibrary.Get(SkillId.Skill4),
            SkillLibrary.Get(SkillId.Skill5)
        };

        OnSkillPanelPopulate?.Invoke(defs);
        OnSkillPanelToggled?.Invoke(true);
    }
    public void CloseSkillPanel()
    {
        isSelectingSkill = false;
        OnSkillPanelToggled?.Invoke(false);
    }

    public void SelectSkill(int index)
    {
        // index: 0~3
        var id = (SkillId)index;
        var def = SkillLibrary.Get(id);
        currentSkill = def;
        EnterSkillTargeting(def);
    }
    private void EnterSkillTargeting(SkillDefinition def)
    {
        // 스킬 타겟팅 모드로 진입
        state = BattleState.Targeting;
        highlighter?.Clear();
        ClearTargetSelection(); // 기존 선택/마커 초기화
        Debug.Log($"[Skill] Select: {def.name} (mode: {def.targetMode}) → Targeting");

        // Unit 타겟형이면: AGI 내림차순 사이클 구성 후 첫 타겟으로 마커 표시
        if (def.targetMode == SkillTargetMode.Unit)
        {
            BuildTargetCycle();           // Enemy만, AGI desc
            if (targetCycle.Count > 0)
                SelectTarget(0);          // 첫 타겟(=가장 빠른 AGI)으로 마커/미리보기
        }
        else // Tile 타겟형: 내부 타일 커서를 1회 세팅하고 프리뷰 유지
        {
            var map = provider?.EnemyFloor;
            if (map != null)
            {
                var cam = Camera.main;
                Vector3 world = (cam != null)
                    ? cam.ScreenToWorldPoint(Input.mousePosition)
                    : Vector3.zero;
                world.z = 0f;
                
                var hover = map.WorldToCell(world);
                                // 마우스가 타일 위면 그 타일, 아니면 기본(-2,-2)
                var start = map.HasTile(hover) ? hover : new Vector3Int(-2, -2, 0);
                
                selectedCell = start;                   // 내부 커서 고정
                PreviewSkillAreaOnTile(map, selectedCell); // 즉시 프리뷰
            }
        }
    }

    // 포인티드-탑 헥사: x(컬럼) 홀짝 판단 (Step 3에서 쓸 예정, 지금도 사용 가능)
    bool IsOddColumn(Vector3Int cell) => Mathf.Abs(cell.x) % 2 == 1;

    // 현재 선택된 스킬의 범위를 "유닛 기준"으로 미리보기
    public void PreviewSkillAreaOnUnit(BattleUnit unit)
    {
        if (currentSkill.GetAreaCells == null) return;
        if (unit == null) { highlighter.Clear(); return; }

        // 아군(플레이어)에는 미리보기 표시하지 않음
        if (unit.team == Team.Player) { highlighter.Clear(); return; }

        var origin = unit.Cell;                            // 유닛의 현재 셀
        bool odd = SkillLibrary.IsOddColumn(origin);       // 컬럼 홀짝
        var cells = currentSkill.GetAreaCells(origin, odd);

        // 해당 유닛이 서 있는 맵(타일맵)을 기준으로 하이라이트
        var map = unit.CurrentMap;
        highlighter.ShowCells(map, cells);
    }

    // 현재 선택된 스킬의 범위를 "타일 기준"으로 미리보기
    public void PreviewSkillAreaOnTile(Tilemap map, Vector3Int originCell)
    {
        if (currentSkill.GetAreaCells == null) return;

        bool odd = SkillLibrary.IsOddColumn(originCell);
        var cells = currentSkill.GetAreaCells(originCell, odd);
        highlighter.ShowCells(map, cells);
    }

    public void ConfirmSkillOnUnit(BattleUnit target)
    {
        if (currentSkill.GetAreaCells == null || target == null) return;
        if (!IsPlayerTurn || acting == null) return;

        // 미리보기 정리
        ClearPreview();

        StartCoroutine(Co_GapCloseThenResolveOnTarget(currentSkill, acting, target));
    }
    public void ConfirmSkillOnTile(Tilemap map, Vector3Int originCell)
    {
        if (currentSkill.GetAreaCells == null || map == null) return;
        if (!IsPlayerTurn || acting == null) return;

        ClearPreview();
        StartCoroutine(Co_ProjectileSkillThenFinish(currentSkill, map, originCell, acting));
    }

    // 스킬 범위를 계산해, 같은 맵에 있는 유닛들 중 해당 셀에 위치한 유닛에게 피해 적용
    public void ResolveSkillAtCell(SkillDefinition def, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        // 1) 범위 셀들 계산 (axial 변환은 SkillLibrary 내부에서 처리됨)
        var area = def.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));

        // 2) 피격 대상 수집 (같은 맵 + 셀 일치)
        var victims = GetUnitsInArea(map, area);

        // 3) 피해 적용 (임시: 적 유닛만 타격, 피해량은 캐스터의 일반 공격력 사용)
        ExecuteSkillDamage(caster, victims, def);
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

    IEnumerable<BattleUnit> GetUnitsInArea(Tilemap map, IEnumerable<Vector3Int> cells)
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

    // 스킬 해결 → (필요 시) 이동 애니메 → 종료
    IEnumerator Co_ResolveSkillThenFinish(SkillDefinition def, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        // 상태 잠금
        state = BattleState.Resolving;

        // 1) 범위/피해 적용 (기존 ResolveSkillAtCell의 로직과 동일)
        var area = def.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        var victims = GetUnitsInArea(map, area);
        ExecuteSkillDamage(caster, victims, def);

        // 2) 스킬1/2 후 이동 목적지 계산
        if (caster != null && !caster.IsDead)
        {
            if (TryComputePostMoveDestination(def, caster, out var destCell))
            {
                // 3) 보간 이동
                yield return Co_MoveUnitSmooth(caster, caster.CurrentMap, destCell, postMoveDuration);
            }
        }

        // 4) 종료 처리(토큰 소모/턴 진행/패널 닫기 등)
        FinishActionAfterSkill();
    }
    // 목적지 계산: 스킬2=앞(NW), 스킬1=뒤(SE), 최대 2칸, 워커블만
    bool TryComputePostMoveDestination(SkillDefinition def, BattleUnit caster, out Vector3Int dest)
    {
        dest = caster.Cell;

        if (def.id != SkillId.Skill1 && def.id != SkillId.Skill2) return false;

        // 절대 대각 방향(축좌표): 앞=NW(0,-1), 뒤=SE(0,+1)
        var stepAx = (def.id == SkillId.Skill2) ? new Vector2Int(0, -1) : new Vector2Int(0, 1);

        var map = caster.CurrentMap;
        var curAx = SkillLibrary.ToAxial(caster.Cell);
        var last = caster.Cell;

        for (int i = 0; i < 2; i++) // 최대 2칸
        {
            curAx = new Vector2Int(curAx.x + stepAx.x, curAx.y + stepAx.y);
            var next = SkillLibrary.ToOffset(curAx);

            if (!IsWalkableCell(map, next)) break; // 타일 없거나 점유 중이면 중단
            last = next;
        }

        if (last != caster.Cell) { dest = last; return true; }
        return false;
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


    void ExecuteSkillDamage(BattleUnit caster, IEnumerable<BattleUnit> victims, SkillDefinition def)
    {
        if (caster == null) return;

        // (임시) 아군/적 팀 구분
        foreach (var v in victims)
        {
            if (v == null) continue;
            if (IsEnemyOf(caster, v))        // 팀 판별 유틸은 프로젝트에 맞게 교체
            {
                // 임시 피해량: 캐스터의 기본 공격력(없다면 1)
                int damage = Mathf.Max(1, caster.AttackDamage);
                v.TakeDamage(damage);
                // (선택) 맞은 유닛 하이라이트/히트 VFX 등 추가 가능
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
        ClearPreview();
        // 스킬 실행 완료 → 패널 닫기 + 스킬 선택 해제
        CloseSkillPanel();   // 이벤트까지 함께 발행됨
        currentSkill = default;
        // 스킬은 '공격'으로 간주하여 행동 토큰 소모 로직 재사용
        OnActionConsumed(BattleAction.Attack);
    }


    // 타겟팅 취소/종료 시 미리보기 지우기
    public void ClearPreview()
    {
        if (highlighter != null) highlighter.Clear();
    }


    bool TryGetFrontCellOfTarget(BattleUnit caster, BattleUnit target, out Vector3Int frontCell) //근접 공격 시 타겟 앞 타일로 이동
    {
        frontCell = target.Cell;

        // 타겟 기준으로 '시전자 방향'을 찾는다.
        int dirIdx = SkillLibrary.NearestDirectionIndex(target.Cell, caster.Cell);
        var stepAx = SkillLibrary.DirIndexToAxial(dirIdx);

        var tAx = SkillLibrary.ToAxial(target.Cell);
        var frontAx = new Vector2Int(tAx.x + stepAx.x, tAx.y + stepAx.y);
        var candidate = SkillLibrary.ToOffset(frontAx);

        // 실제로 이동하는 건 아니고 '연출용 좌표'로만 쓸 거라 HasTile 정도만 체크
        if (provider != null && provider.EnemyFloor != null && provider.EnemyFloor.HasTile(candidate))
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
    IEnumerator FallbackProjectile(Vector3 start, Vector3 end, float time, System.Action done)
    {
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime / Mathf.Max(0.01f, time); yield return null; }
        done?.Invoke();
    }
}
