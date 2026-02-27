using Cysharp.Threading.Tasks;
using UnityEngine;

// 1. Initialization State
public class BattleInitState : BattleBaseState
{
    public override async UniTask Enter()
    {
        // Rebind and Start Wave like in legacy Start/Init
        // manager.RebindAllUnitsAndInitATB(); // Access helper if public, or move logic here
        // For now, assume we call manager's public init methods

        // Wait for maps or fade in
        await UniTask.Delay(500);

        // manager.state = BattleState.Idle; // Moved to SetStateToFSM

        // Transition to next state (e.g. Turn loop or just Idle)
        // Ideally, BattleManager should drive the state machine based on events
    }
}

// 2. Player Turn State
public class BattlePlayerTurnState : BattleBaseState
{
    public override async UniTask Enter()
    {
         // manager.state = BattleState.ActionSelect; // Moved to SetStateToFSM
         // Enable UI, Listen for Input
         manager.SetHint("당신의 턴입니다.");
         await UniTask.CompletedTask;
    }

    public override async UniTask Exit()
    {
        // Disable UI or cleanup
        await UniTask.CompletedTask;
    }
}

// 3. Enemy Turn State
public class BattleEnemyTurnState : BattleBaseState
{
    public override async UniTask Enter()
    {
        // manager.state = BattleState.EnemyTurn; // Moved to SetStateToFSM
        // Trigger AI Logic
        // In legacy, this was implicit via BattleTurnManager calling methods
        await UniTask.CompletedTask;
    }
}

// 4. Win State
public class BattleWinState : BattleBaseState
{
    public override async UniTask Enter()
    {
        Debug.Log("[FSM] Victory!");
        // manager.HandleVictory();
        await UniTask.CompletedTask;
    }
}

// 5. Lose State
public class BattleLoseState : BattleBaseState
{
    public override async UniTask Enter()
    {
        Debug.Log("[FSM] Defeat...");
        // manager.HandleDefeat();
        await UniTask.CompletedTask;
    }
}

// 6. Moving State (임시/이동 대기)
public class BattleMovingState : BattleBaseState
{
    public override async UniTask Enter()
    {
        // Debug.Log("[FSM] Moving...");
        await UniTask.CompletedTask;
    }
}

// 7. Targeting State (스킬 타겟팅 대기)
public class BattleTargetingState : BattleBaseState
{
    public override async UniTask Enter()
    {
        // Debug.Log("[FSM] Targeting...");
        await UniTask.CompletedTask;
    }
}

// 8. Targeting Knockback State (밀치기 위치 지정)
public class BattleTargetingKnockbackState : BattleBaseState
{
    public override async UniTask Enter()
    {
        // Debug.Log("[FSM] TargetingKnockback...");
        await UniTask.CompletedTask;
    }
}

// 9. Resolving State (연출/결과 처리 대기)
public class BattleResolvingState : BattleBaseState
{
    public override async UniTask Enter()
    {
        // Debug.Log("[FSM] Resolving...");
        await UniTask.CompletedTask;
    }
}
