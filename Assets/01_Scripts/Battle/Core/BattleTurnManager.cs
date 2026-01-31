using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattleTurnManager : MonoBehaviour
{
    private BattleManager battleManager;
    private IGridProvider grid;
    private BattleInputHandler inputHandler;
    private BattleFieldManager fieldManager;

    [Header("Settings")]
    [SerializeField] private int baseActionsPerTurn = 1;

    // State
    public BattleUnit ActingUnit { get; private set; }
    public bool IsPlayerTurn => ActingUnit != null && ActingUnit.team == Team.Player;

    private int remainingActions = 0;
    public int RemainingActions => remainingActions;
    private HashSet<BattleAction> usedActions = new();
    private Coroutine enemyRoutine;

    // Events
    public event System.Action<BattleUnit> OnUnitTurnStarted; // BM.OnAnyUnitTurnStarted 연결용
    public event System.Action<BattleUnit> OnUnitEndTurn;
    public event System.Action<BattleUnit> OnOverworkTriggered;

    public void Initialize(BattleManager _battleManager,IGridProvider _grid, BattleInputHandler _input, BattleFieldManager _fieldManager)
    {
        battleManager = _battleManager;
        grid = _grid;
        inputHandler = _input;
        fieldManager = _fieldManager;
    }
    // 유닛 사망 시 강제로 턴 주체를 비우기 위해 호출
    public void ForceClearActingUnit()
    {
        ActingUnit = null;
    }

    // 전투 초기화/웨이브 전환 시 상태 리셋
    public void ResetTurnState()
    {
        ActingUnit = null;
        remainingActions = 0;
        usedActions.Clear();
        if (enemyRoutine != null) StopCoroutine(enemyRoutine);
        enemyRoutine = null;
    }

    #region Turn Lifecycle
    public void StartTurn(BattleUnit unit)
    {
        if (unit == null) return;
        ActingUnit = unit;
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();

        // 초기화
        battleManager.ClearAllPreviews();
        inputHandler.ClearAllPreviews(); // 타겟팅 초기화

        //BattleManager에게 현재 턴 유닛 전달
        battleManager.OnUnitTurnStartedByManager(unit);

        // 이벤트 발송
        OnUnitTurnStarted?.Invoke(unit);
        BattleManager.EmitGlobalTurnStart(unit); // Static Event Wrapper 호출
        battleManager.EmitActionLabel(null, "");
        battleManager.SetHint(string.Empty);
        battleManager.EmitTurnLabel(unit);

        Debug.Log($"[TurnManager] StartTurn -> {unit.name}");

        // 상태이상 체크 (수면)
        var usc = unit.GetComponent<UnitStateController>();
        if (usc != null && usc.Has(UnitStateId.Sleep))
        {
            Debug.Log($"[Sleep] {unit.name} 수면 상태 -> 턴 스킵");
            usc.Remove(UnitStateId.Sleep);
            EndTurnDirectly(unit);
            return;
        }

        // 상태이상 체크 (공포)
        bool hadFear = (usc != null && usc.Has(UnitStateId.Fear));

        // 턴 시작 효과 처리
        var sc = unit.GetComponent<StatusController>();
        if (sc != null) sc.OnTurnStart();
        if (usc != null) usc.OnTurnStart();

        // 매복 힐 등 특수 로직 (BM에 있던 로직 가져옴 - 필요시 Helper로 분리 가능)
        battleManager.TryApplyAmbushTurnStartHeal(unit);

        // 공포 처리
        if (hadFear)
        {
            Debug.Log($"[Fear] {unit.name} 공포 -> 강제 후퇴");
            battleManager.SetState(BattleState.Resolving);
            remainingActions = 0;
            usedActions.Clear();
            StartCoroutine(battleManager.Co_HandleFearTurn(unit)); // 이건 이동 로직이라 BM/Grid 의존성이 큼
            return;
        }

        // 필드 효과 (장판)
        fieldManager?.OnTurnStart(unit);

        // 적: 웹 캐스팅 체크
        if (unit.team == Team.Enemy)
        {
            var ecs = unit.GetComponent<EnemyCastState>();
            if (ecs != null && ecs.TryTakeReady(out var pending))
            {
                StartCoroutine(battleManager.Co_EnemyFireWebThenConsume(unit, pending));
                return;
            }
        }

        // 상태 전환
        if (unit.team == Team.Player)
        {
            battleManager.SetState(BattleState.ActionSelect);
        }
        else
        {
            battleManager.SetState(BattleState.Resolving);
            enemyRoutine = StartCoroutine(EnemyTurnRoutine(unit));
        }
    }

    private void EndTurnDirectly(BattleUnit unit)
    {
        if (unit.team == Team.Player) EndPlayerTurn();
        else EndEnemyTurn(unit);
    }

    public void EndPlayerTurn()
    {
        if (TryProcessOverwork()) return;

        fieldManager?.OnTurnEnd(ActingUnit);
        battleManager.ClearAllPreviews();

        OnUnitEndTurn?.Invoke(ActingUnit);

        // 턴 컨트롤러에게 종료 알림
        battleManager.turnController.CompleteTurn(ActingUnit);

        ActingUnit = null;
        battleManager.SetState(BattleState.Idle);
    }

    public void EndEnemyTurn(BattleUnit enemy)
    {
        if (enemy == null)
        {
            battleManager.SetState(BattleState.Idle);
            return;
        }

        if (TryProcessOverwork()) return;
        enemyRoutine = null;

        fieldManager?.OnTurnEnd(enemy);

        // 적 AI 계획 수립
        var ecs = enemy.GetComponent<EnemyCastState>();
        if (ecs == null || !ecs.IsCasting)
        {
            battleManager.EmitActionLabel(enemy, "");
            var ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.PlanNextSkill();
        }

        enemy.ResetATB(); // (ATBController가 해도 되지만 안전장치)
        OnUnitEndTurn?.Invoke(enemy);

        battleManager.turnController.CompleteTurn(enemy);

        if (ActingUnit == enemy) ActingUnit = null;
        battleManager.SetState(BattleState.Idle);
    }
    #endregion

    #region Action Logic
    // 행동 소모 및 턴 흐름 제어
    public void OnActionConsumed(BattleAction act)
    {
        if (ActingUnit == null) return;

        usedActions.Add(act);
        remainingActions = Mathf.Max(0, remainingActions - 1);

        if (remainingActions > 0)
        {
            if (IsPlayerTurn)
            {
                battleManager.SetState(BattleState.ActionSelect);
            }
            else
            {
                if (enemyRoutine != null) StopCoroutine(enemyRoutine);
                enemyRoutine = StartCoroutine(EnemyTurnRoutine(ActingUnit));
            }
        }
        else
        {
            if (IsPlayerTurn) EndPlayerTurn();
            else EndEnemyTurn(ActingUnit);
        }
    }

    // 과로 처리
    private bool TryProcessOverwork()
    {
        if (ActingUnit == null) return false;

        var statusCtrl = ActingUnit.GetComponent<StatusController>();
        var usc = ActingUnit.GetComponent<UnitStateController>();
        if (statusCtrl == null) return false;

        int overworkStacks = statusCtrl.GetStacks(StatusId.Overwork);
        if (overworkStacks <= 0) return false;

        int nextStack = overworkStacks - 1;
        statusCtrl.SetStacks(StatusId.Overwork, nextStack);

        if (nextStack == 0)
        {
            // 훈련 특성 체크 (과로 종료 시 수면 면제)
            bool skipSleep = CheckOverworkTraining(ActingUnit);

            if (usc != null)
            {
                if (!skipSleep) usc.Apply(UnitStateId.Sleep);
                else Debug.Log($"[BattleManager] {ActingUnit.name} 과로 종료 -> 훈련 효과로 수면 면제!");
            }
            return false;
        }

        Debug.Log($"[BattleManager] {ActingUnit.name} 과로 발동! (남은 스택: {nextStack})");

        // 행동력 리필 및 턴 연장
        remainingActions = baseActionsPerTurn;
        usedActions.Clear();
        battleManager.SetState(BattleState.ActionSelect);

        OnOverworkTriggered?.Invoke(ActingUnit);
        return true;
    }

    // 과로 훈련 체크 로직 (BM에 있던 거 간소화)
    private bool CheckOverworkTraining(BattleUnit unit)
    {
        if (unit.data == null || unit.data.skills == null) return false;
        foreach (var s in unit.data.skills)
        {
            if (s is ParametricSupportSkill pss && pss.buffStatus == StatusId.Overwork)
            {
                int route = unit.GetTrainingRouteIndex(pss);
                if (pss.trainingNoSleepOnOverworkEnd &&
                    pss.routeForNoSleepOnOverworkEnd >= 0 &&
                    route == pss.routeForNoSleepOnOverworkEnd)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // 외부(BM/Input)에서 호출하는 행동 검증
    public bool CanPerformAction(BattleAction action)
    {
        if (ActingUnit == null) return false;
        if (remainingActions <= 0) return false;
        if (usedActions.Contains(action)) return false;
        return true;
    }
    #endregion

    #region Basic Actions (Rest/Calm)
    public void Rest()
    {
        if (ActingUnit == null || !IsPlayerTurn) return;
        if (remainingActions <= 0) return;

        battleManager.ClearAllPreviews();

        float before = ActingUnit.HP;
        ActingUnit.HealPercent(0.10f);

        OnActionConsumed(BattleAction.Rest);
    }

    public void Calm()
    {
        if (ActingUnit == null || !IsPlayerTurn) return;
        if (remainingActions <= 0) return;

        battleManager.ClearAllPreviews();

        float maxMP = ActingUnit.MaxMP;
        float maxRage = ActingUnit.MaxRage;
        float beforeRage = ActingUnit.Rage;

        int mpGain = Mathf.FloorToInt(maxMP * 0.10f);
        if (mpGain <= 0 && maxMP > 0f) mpGain = 1;

        if (ActingUnit.Rage <= 0f)
        {
            ActingUnit.GainMP(mpGain);
            OnActionConsumed(BattleAction.Calm);
            return;
        }

        if (mpGain > 0) ActingUnit.GainMP(mpGain);

        float rageCostTarget = maxRage * 0.10f;
        if (rageCostTarget <= 0f) rageCostTarget = beforeRage;

        float spend = Mathf.Min(beforeRage, rageCostTarget);
        if (spend > 0f) ActingUnit.AddRage(-spend);

        OnActionConsumed(BattleAction.Calm);
    }
    #endregion

    #region Enemy AI
    IEnumerator EnemyTurnRoutine(BattleUnit enemy)
    {
        yield return new WaitForSeconds(0.5f);

        // 타겟 찾기 (Grid/Unit 조회는 BM을 통하거나 GridManager 직접 사용)
        var players = Object.FindObjectsOfType<BattleUnit>()
            .Where(u => u != null && u.team == Team.Player && !u.IsDead)
            .Where(u => !SkillAsset.IsUntargetableByEnemy(u))
            .ToList();

        if (players.Count == 0) { EndEnemyTurn(enemy); yield break; }

        BattleUnit target = players[Random.Range(0, players.Count)];

        var ai = enemy.GetComponent<EnemyAI>();
        SkillAsset so = (ai != null) ? ai.ConsumePlannedSkillOrPick() : null;

        if (so != null) battleManager.EmitActionLabel(enemy, so.displayName);

        if (so != null)
        {
            if (so.targetMode == SkillTargetMode.Unit)
            {
                // 스킬 실행은 BM/SkillProcessor에게 위임하고 대기
                yield return StartCoroutine(so.ResolveOnUnit(battleManager, enemy, target));

                // 스킬 후 처리 (BM을 통해 다시 여기 OnActionConsumed로 돌아옴)
                battleManager.FinishActionAfterSkill();
                yield break;
            }
            else if (so.targetMode == SkillTargetMode.Tile)
            {
                // 타일 타겟 AI 로직 (필요시 구현)
                yield break;
            }
        }
        EndEnemyTurn(enemy);
    }
    #endregion
}