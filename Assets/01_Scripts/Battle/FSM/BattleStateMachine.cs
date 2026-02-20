using UnityEngine;
using Cysharp.Threading.Tasks;

public class BattleStateMachine : MonoBehaviour
{
    public BattleBaseState CurrentState { get; private set; }
    private BattleManager manager;

    public void Initialize(BattleManager _mgr)
    {
        manager = _mgr;
    }

    public async UniTaskVoid ChangeState(BattleBaseState newState)
    {
        try
        {
            if (CurrentState != null)
            {
                await CurrentState.Exit();
            }

            CurrentState = newState;

            if (CurrentState != null)
            {
                CurrentState.Initialize(this, manager);
                await CurrentState.Enter();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void Update()
    {
        CurrentState?.Update();
    }
}
