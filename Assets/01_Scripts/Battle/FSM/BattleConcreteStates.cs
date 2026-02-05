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
        
        manager.state = BattleState.Idle; // Legacy sync
        
        // Transition to next state (e.g. Turn loop or just Idle)
        // Ideally, BattleManager should drive the state machine based on events
    }
}

// 2. Player Turn State
public class BattlePlayerTurnState : BattleBaseState
{
    public override async UniTask Enter()
    {
         manager.state = BattleState.ActionSelect; // Legacy sync
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
        manager.state = BattleState.EnemyTurn; // Legacy sync
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
