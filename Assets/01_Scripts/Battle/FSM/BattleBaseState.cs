using Cysharp.Threading.Tasks;

public abstract class BattleBaseState
{
    protected BattleStateMachine stateMachine;
    protected BattleManager manager;

    public void Initialize(BattleStateMachine _sm, BattleManager _mgr)
    {
        stateMachine = _sm;
        manager = _mgr;
    }

    public virtual async UniTask Enter() { await UniTask.CompletedTask; }
    public virtual async UniTask Exit() { await UniTask.CompletedTask; }
    public virtual void Update() { }
}
