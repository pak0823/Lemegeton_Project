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
    bool IsPlayerTurn => acting != null && acting.team == Team.Player;
    public bool IsTargeting => state == BattleState.Targeting;
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
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
        if (provider != null) provider.OnMapsReady += Init;
        else { Debug.LogWarning("[BattleManager] BattleMapManager not ready in Awake. Will retry in Start."); }
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

        // ATB 최대 유닛 찾기
        if (!atbPaused)
        {
            var readyUnit = FindObjectsOfType<BattleUnit>().FirstOrDefault(u => u.IsTurnReady && !u.IsDead);
            if (readyUnit != null)
            {
                acting = readyUnit;
                atbPaused = true;
                StartTurn(acting);
            }
        }
    }


    #region Turn Management
    //void NextTurn()
    //{
    //    acting = turn.Current;
    //    if (acting == null)
    //    {
    //        CheckBattleEnd();
    //        return;
    //    }

    //    // 턴 시작: 토큰/사용기록 초기화
    //    remainingActions = baseActionsPerTurn;
    //    usedActions.Clear();
    //    highlighter?.Clear();
    //    ClearTargetSelection(); // 턴 전환 시 타겟 표시 정리
    //    manualEndRequested = false; // 턴 시작마다 리셋

    //    // 플레이어/적 분기
    //    if (acting.team == Team.Enemy)
    //    {
    //        state = BattleState.Resolving; // 입력 잠깐 막기
    //        if (enemyRoutine != null) StopCoroutine(enemyRoutine);
    //        enemyRoutine = StartCoroutine(EnemyAutoAct()); // 항상 새로 시작
    //    }
    //    else
    //    {
    //        if (enemyRoutine != null) { StopCoroutine(enemyRoutine); enemyRoutine = null; }
    //        state = BattleState.ActionSelect; // 플레이어만 버튼/입력 허용
    //    }

    //    UpdateTurnIndicator();//임시 테스트용
    //}
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

    //public void EndTurn()
    //{
    //    highlighter?.Clear();

    //    // 수동 종료 + 이번 턴에 아무 행동도 안 했으면 1 회복
    //    if (acting != null && acting.team == Team.Player && manualEndRequested && usedActions.Count == 0)
    //    {
    //        acting.Heal(1);
    //        Debug.Log("[EndTurn] 행동 없이 수동 종료 → HP +1 회복");
    //    }

    //    manualEndRequested = false; // 사용했으면 바로 리셋
    //    turn.Advance();
    //    NextTurn();
    //}

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

        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(acting.team, acting.Cell).ToList();
        highlighter?.ShowCells(acting.CurrentMap, moveOptions);
    }

    public void OnTileClicked(Tilemap clickedMap, Vector3Int cell)
    {
        if (!IsPlayerTurn) return;

        if (state == BattleState.Moving)
        {
            if (clickedMap == acting.CurrentMap && moveOptions.Contains(cell))
            {
                state = BattleState.Resolving; // 입력 잠금
                StartCoroutine(Co_MoveThenConsume(acting, clickedMap, cell, BattleAction.Move));
                highlighter?.Clear();
                moveOptions.Clear();
                return;
            }
        }
        else if (state == BattleState.Targeting)
        {
            TryAttackByTile(clickedMap, cell);
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
        if (!IsPlayerTurn) return; // 적 턴 금지
        if (usedActions.Contains(BattleAction.Attack) || remainingActions <= 0) return; // 중복/토큰 없음

        state = BattleState.Targeting;
        var cells = GetAttackCells(provider.EnemyFloor, acting.Cell, acting.AttackRange);
        highlighter?.ShowCells(provider.EnemyFloor, cells); //사거리 표시
        BuildTargetCycle(); // 적 리스트 구성(AGI 내림차순)
        if (targetCycle.Count > 0) SelectTarget(0); // 첫 대상 표시
    }

    IEnumerable<Vector3Int> GetAttackCells(Tilemap map, Vector3Int center, int range)
    {
        var list = new List<Vector3Int>();
        foreach (var pos in map.cellBounds.allPositionsWithin)
        {
            var c = (Vector3Int)pos;
            if (!map.HasTile(c)) continue;
            bool ok = grid.InRangeAcrossMaps(provider.PlayerFloor, acting.CurrentMap, center, map, c, range);
            if (ok) list.Add(c);
        }
        return list;
    }

    public void OnUnitClicked(BattleUnit target)
    {
        if (!IsPlayerTurn) return;
        if (state != BattleState.Targeting) return;
        if (target.team == acting.team) return;

        var ok = grid.InRangeAcrossMaps(provider.PlayerFloor, acting.CurrentMap, acting.Cell, target.CurrentMap, target.Cell, acting.AttackRange);
        if (!ok) return;

        ResolveAttack(target);
    }

    void TryAttackByTile(Tilemap map, Vector3Int cell)
    {
        if (map != provider.EnemyFloor) return;
        var ok = grid.InRangeAcrossMaps(provider.PlayerFloor, acting.CurrentMap, acting.Cell, map, cell, acting.AttackRange);
        if (!ok) return;

        var world = map.GetCellCenterWorld(cell);
        var hit = Physics2D.OverlapCircle(world, 0.15f, unitMask);
        if (hit && hit.TryGetComponent(out BattleUnit target))
            ResolveAttack(target);
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
        acting.ATB = 0f;       // 현 유닛만 초기화
        acting = null;
        atbPaused = false;
        state = BattleState.Idle;
    }

    public void CancelCurrentAction()
    {
        if (state == BattleState.Moving || state == BattleState.Targeting)
        {
            state = BattleState.ActionSelect;
            highlighter?.Clear();
            ClearTargetSelection(); // 취소 시 숨김
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

    void SelectTarget(int index)
    {
        if (targetCycle.Count == 0) { ClearTargetSelection(); return; }
        int n = targetCycle.Count;
        targetIndex = ((index % n) + n) % n; // 안전한 모듈로
        selectedTarget = targetCycle[targetIndex];
        targetMarker?.Attach(selectedTarget);
    }

    public void CycleTarget(int dir)
    {
        if (!IsPlayerTurn || !IsTargeting || targetCycle.Count == 0) return;
        SelectTarget(targetIndex + dir); // dir=+1(→), -1(←)
    }

    public void ConfirmTarget()
    {
        if (!IsPlayerTurn || !IsTargeting || selectedTarget == null) return;
        bool inRange = grid.InRangeAcrossMaps(provider.PlayerFloor, acting.CurrentMap, acting.Cell, selectedTarget.CurrentMap, selectedTarget.Cell, acting.AttackRange);
        if (!inRange) { Debug.Log("사거리 밖 대상"); return; }
        ResolveAttack(selectedTarget);
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
        //turn.Remove(dead);

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

        if (!anyEnemy) { Debug.Log("[Battle] 승리!"); }
        else if (!anyPlayer) { Debug.Log("[Battle] 패배..."); }
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

        bool inRange = grid.InRangeAcrossMaps(provider.PlayerFloor, enemy.CurrentMap, enemy.Cell,
                                               target.CurrentMap, target.Cell, enemy.AttackRange);

        if (inRange)
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

        enemy.ATB = 0f;
        if (acting == enemy) acting = null;

        atbPaused = false;     // 전체 ATB 재개
        state = BattleState.Idle;
    }
    #endregion
}
