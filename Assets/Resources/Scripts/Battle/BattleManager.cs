//상태 & 입력
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.GraphicsBuffer;

public enum BattleState { Idle, ActionSelect, Moving, Targeting, Resolving, EndTurn }
public enum BattleAction { Move, Attack }

public class BattleManager : MonoBehaviour
{
    public BattleGridManager grid;
    public TurnOrderManager turn;
    public LayerMask unitMask;

    BattleState state = BattleState.Idle;
    BattleUnit acting;
    List<Vector3Int> moveOptions = new();

    // === 추가턴 설계 ===
    [SerializeField] int baseActionsPerTurn = 1;     // 기본 행동 토큰(기본 1)
    int remainingActions = 0;                        // 남은 토큰
    readonly HashSet<BattleAction> usedActions = new(); // 이번 턴에 사용한 행동(중복 금지)

    public Highlighter highlighter;
    IBattleMapProvider provider;
    bool initialized = false; // 중복 Init 방지

    bool IsPlayerTurn => acting != null && acting.team == Team.Player;
    public bool IsTargeting => state == BattleState.Targeting;
    Coroutine enemyRoutine;   // 코루틴 핸들

    // === 타겟 선택(표시/순환) ===
    [Header("Targeting")]
    public TargetMarker targetMarker;              // 인스펙터에 배치한 TargetMarker 할당
    List<BattleUnit> targetCycle = new();          // 적 리스트(AGI desc)
    int targetIndex = -1;                          // 현재 인덱스
    BattleUnit selectedTarget;                     // 현재 선택된 대상

    //void GrantExtraActions(int n = 1)  // (추후 버프/효과에서 호출)
    //{
    //    remainingActions += Mathf.Max(0, n);
    //    //UpdateActionButtons();
    //}

    void Awake()
    {
        provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
        if (provider != null)
            provider.OnMapsReady += Init;
        else
        {
            Debug.LogWarning("[BattleManager] BattleMapManager not ready in Awake. Will retry in Start.");
        }
    }
    void Start()
    {
        // Start에서 한 번 더 획득/구독 시도
        if (provider == null)
        {
            provider = Shared.battleMapManager as IBattleMapProvider ?? FindObjectOfType<BattleMapManager>();
            if (provider != null)
                provider.OnMapsReady += Init;
        }

        // 이미 맵이 준비돼 있으면 바로 Init
        if (!initialized && provider != null && provider.PlayerFloor != null && provider.EnemyFloor != null)
            Init();
    }
    void OnDisable()
    {
        if (provider != null) provider.OnMapsReady -= Init;
    }

    void UpdateTurnIndicator()//임시 테스트용 턴 확인
    {
        if (acting == null) { Debug.Log("[Turn] (없음)"); return; }
        Debug.Log($"[Turn] {acting.team} - {acting.name}");
    }

    void Init()
    {
        var units = FindObjectsOfType<BattleUnit>();
        foreach (var u in units)
        {
            var map = (u.team == Team.Player) ? provider.PlayerFloor : provider.EnemyFloor;
            var cell = map.WorldToCell(u.transform.position);
            u.Bind(map, cell);
            grid.SetOccupied(u.team, u.Cell, true);

            // 사망 이벤트 구독
            u.OnDied += HandleUnitDied;
        }

        turn.BuildOrder(units);
        NextTurn();
    }
    void NextTurn()
    {
        acting = turn.Current;    
        if (acting == null) { CheckBattleEnd(); return; }

        // 턴 시작: 토큰/사용기록 초기화
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        highlighter?.Clear();
        ClearTargetSelection();   // 턴 전환 시 타겟 표시 정리
        //UpdateActionButtons();

        // 플레이어/적 분기
        if (acting.team == Team.Enemy)
        {
            state = BattleState.Resolving;        // 입력 잠깐 막기
            if (enemyRoutine != null) StopCoroutine(enemyRoutine);
            enemyRoutine = StartCoroutine(EnemyAutoAct());  // 항상 새로 시작
        }
        else
        {
            if (enemyRoutine != null) { StopCoroutine(enemyRoutine); enemyRoutine = null; }
            state = BattleState.ActionSelect;     // 플레이어만 버튼/입력 허용
        }

        UpdateTurnIndicator();//임시 테스트용
    }
    public void EndTurn()
    {
        highlighter?.Clear();
        turn.Advance();
        NextTurn();
    }

    public void OnClickMove()
    {
        if (!IsPlayerTurn) return;// 적 턴 금지
        if (usedActions.Contains(BattleAction.Move) || remainingActions <= 0) return; // 중복/토큰 없음
        state = BattleState.Moving;
        moveOptions = grid.GetAdjacentWalkable(acting.team, acting.Cell).ToList();
        highlighter?.ShowCells(acting.CurrentMap, moveOptions);

    }

    public void OnTileClicked(Tilemap clickedMap, Vector3Int cell)
    {
        if (!IsPlayerTurn) return;// 적 턴 금지
        if (state == BattleState.Moving)
        {
            if (clickedMap == acting.CurrentMap && moveOptions.Contains(cell))
            {
                // 이동은 코루틴에서만 처리: 점유 해제→애니→점유→턴 종료
                state = BattleState.Resolving; // 입력 잠금
                StartCoroutine(Co_MoveThenConsume(acting, clickedMap, cell, BattleAction.Move));
                //StartCoroutine(Co_MoveThenEndTurn(acting, cell));
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
        OnActionConsumed(act);   // 이동 1회 소비 → 남은 토큰 판단
    }


    public void OnClickAttack()
    {
        if (!IsPlayerTurn) return;// 적 턴 금지
        if (usedActions.Contains(BattleAction.Attack) || remainingActions <= 0) return; // 중복/토큰 없음
        state = BattleState.Targeting;
        var cells = GetAttackCells(provider.EnemyFloor, acting.Cell, acting.AttackRange);
        highlighter?.ShowCells(provider.EnemyFloor, cells);        //사거리 표시
        BuildTargetCycle();         // 적 리스트 구성(AGI 내림차순)
        if (targetCycle.Count > 0) SelectTarget(0); // 첫 대상 표시
    }

    IEnumerable<Vector3Int> GetAttackCells(Tilemap map, Vector3Int center, int range)
    {
        var list = new List<Vector3Int>();
        foreach (var pos in map.cellBounds.allPositionsWithin)
        {
            var c = (Vector3Int)pos;
            if (!map.HasTile(c)) continue;

            bool ok = grid.InRangeAcrossMaps(
                provider.PlayerFloor,    // 기준: 아군맵
                acting.CurrentMap, center,
                map, c,
                range);

            if (ok) list.Add(c);
        }
        return list;
    }

    public void OnUnitClicked(BattleUnit target)
    {
        if (!IsPlayerTurn) return;
        if (state != BattleState.Targeting) return;
        if (target.team == acting.team) return;

        // 교차 맵 사거리 체크
        var ok = grid.InRangeAcrossMaps(
            provider.PlayerFloor,          // 기준 맵(아군맵을 기준으로 사용)
            acting.CurrentMap, acting.Cell,
            target.CurrentMap, target.Cell,
            acting.AttackRange);

        if (!ok) return;
        ResolveAttack(target);
    }

    void TryAttackByTile(Tilemap map, Vector3Int cell)
    {
        // 공격은 적 타일맵에서만 허용 → provider 사용
        if (map != provider.EnemyFloor) return;

        // 교차 맵 사거리 체크
        var ok = grid.InRangeAcrossMaps(
            provider.PlayerFloor,
            acting.CurrentMap, acting.Cell,
            map, cell,
            acting.AttackRange);

        if (!ok) return;

        var world = map.GetCellCenterWorld(cell);
        var hit = Physics2D.OverlapCircle(world, 0.15f, unitMask);

        if (hit && hit.TryGetComponent(out BattleUnit target))
        {
            ResolveAttack(target);
        }
    }

    void ResolveAttack(BattleUnit target)
    {
        // 하이라이트/입력 잠금
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

    // === 행동 1회 소비 후 분기 ===
    void OnActionConsumed(BattleAction act)
    {
        usedActions.Add(act);
        remainingActions = Mathf.Max(0, remainingActions - 1);
        //UpdateActionButtons();

        if (remainingActions > 0)
        {
            // 같은 행동은 다시 못함
            if (acting.team == Team.Enemy)
            {
                // 적은 자동으로 다음 행동 시도
                if (enemyRoutine != null) StopCoroutine(enemyRoutine);
                enemyRoutine = StartCoroutine(EnemyAutoAct());
            }
            else
            {
                state = BattleState.ActionSelect;
            }
        }
        else
        {
            EndTurn();
        }
    }

    // === 타겟 사이클 구축/선택/확정 ===
    void BuildTargetCycle()
    {
        targetCycle = FindObjectsOfType<BattleUnit>()
            .Where(u => u.team == Team.Enemy && !u.IsDead)
            .OrderByDescending(u => u.AGI)  // 빠른 → 느린
            .ToList();
        targetIndex = -1;
        selectedTarget = null;
    }
    void SelectTarget(int index)
    {
        if (targetCycle.Count == 0) { ClearTargetSelection(); return; }
        int n = targetCycle.Count;
        targetIndex = ((index % n) + n) % n;                // 안전한 모듈로
        selectedTarget = targetCycle[targetIndex];
        // 표시 업데이트
        if (targetMarker != null) targetMarker.Attach(selectedTarget);
    }
    public void CycleTarget(int dir)
    {
        if (!IsPlayerTurn || !IsTargeting) return;
        if (targetCycle.Count == 0) return;
        SelectTarget(targetIndex + dir); // dir=+1(→), -1(←)
    }
    public void ConfirmTarget()
    {
        if (!IsPlayerTurn || !IsTargeting) return;
        if (selectedTarget == null) return;

        bool inRange = grid.InRangeAcrossMaps(
            provider.PlayerFloor,
            acting.CurrentMap, acting.Cell,
            selectedTarget.CurrentMap, selectedTarget.Cell,
            acting.AttackRange);

        if (!inRange) { Debug.Log("사거리 밖 대상"); return; }
        ResolveAttack(selectedTarget);
    }
    void ClearTargetSelection()
    {
        selectedTarget = null;
        targetIndex = -1;
        if (targetMarker != null) targetMarker.Hide();
    }

    public void CancelCurrentAction()
    {
        if (state == BattleState.Moving || state == BattleState.Targeting)
        {
            state = BattleState.ActionSelect;
            highlighter?.Clear();
            ClearTargetSelection();    // 취소 시 숨김
        }
    }

    void HandleUnitDied(BattleUnit dead)
    {
        // 점유 해제
        grid.SetOccupied(dead.team, dead.Cell, false);

        // 턴 큐에서 제거
        turn.Remove(dead);

        // 만약 현재 턴 유닛이 죽은 경우 → 즉시 다음으로
        if (dead == acting)
        {
            NextTurn();
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

    void CheckBattleEnd()
    {
        var units = FindObjectsOfType<BattleUnit>();
        bool anyPlayer = units.Any(u => u.team == Team.Player && !u.IsDead);
        bool anyEnemy = units.Any(u => u.team == Team.Enemy && !u.IsDead);

        if (!anyEnemy)
        {
            Debug.Log("[Battle] 승리!");
            // TODO: 결과 처리/씬 전환 등
        }
        else if (!anyPlayer)
        {
            Debug.Log("[Battle] 패배...");
            // TODO: 결과 처리/씬 전환 등
        }

    }

    IEnumerator EnemyAutoAct()  // 적 AI 행동
    {
        yield return new WaitForSeconds(1f); // 살짝 텀(연출/안전)

        // 1) 가장 가까운 플레이어 타겟 선정(교차 맵 거리 기준)
        var players = FindObjectsOfType<BattleUnit>()
        .Where(u => u.team == Team.Player && !u.IsDead)
        .ToList();
        if (players.Count == 0) 
        {
            EndTurn(); 
            yield break; 
        }

        BattleUnit target = players
            .OrderBy(u => grid.CrossMapDistance(
                provider.PlayerFloor, acting.CurrentMap, acting.Cell,
                u.CurrentMap, u.Cell))
            .First();

        // 2) 사거리면 공격
        bool inRange = grid.InRangeAcrossMaps(
            provider.PlayerFloor,
            acting.CurrentMap, acting.Cell,
            target.CurrentMap, target.Cell,
            acting.AttackRange);

        // 1순위: 공격 (아직 사용하지 않았고 사거리 이내)
        if (inRange && !usedActions.Contains(BattleAction.Attack) && remainingActions > 0)
        {
            ResolveAttack(target);   // ← 추가 핸들러 등록/EndTurn 중복 호출 금지
            yield break;

        }

        // 2순위: 이동 (아직 사용하지 않았고 후보가 있을 때)
        var candidates = grid.GetAdjacentWalkable(acting.team, acting.Cell).ToList();
        if (!usedActions.Contains(BattleAction.Move) && remainingActions > 0 && candidates.Count > 0)
        {
            var best = candidates
                .OrderBy(c => grid.CrossMapDistance(
                    provider.PlayerFloor,
                    acting.CurrentMap, c,
                    target.CurrentMap, target.Cell))
                .First();

            // 이동 전 점유 해제 → 애니 → 이동 후 점유
            grid.SetOccupied(acting.team, acting.Cell, false);
            yield return acting.AnimateMoveTo(acting.CurrentMap, best); // 내부에서 MoveTo 갱신
            grid.SetOccupied(acting.team, acting.Cell, true);

            OnActionConsumed(BattleAction.Move); // 이동 소비 → 남은 토큰 판단
            yield break;
        }
        else
        {
            // 할 수 있는 행동이 없으면 즉시 턴 종료
            remainingActions = 0;
            EndTurn();
        }
    }

    // (선택) 버튼 토글: 이미 쓴 행동 비활성화
    //void UpdateActionButtons()
    //{
    //    //버튼 레퍼런스를 쓰고 있다면 여기서 interactable 토글
    //    moveBtn.interactable = !usedActions.Contains(BattleAction.Move) && remainingActions > 0 && acting?.team == Team.Player;
    //    attackBtn.interactable = !usedActions.Contains(BattleAction.Attack) && remainingActions > 0 && acting?.team == Team.Player;
    //}
}

