public enum BattleState
{
    Idle,
    ActionSelect,
    Moving,
    Targeting,
    Resolving,
    TargetingKnockback,
    EndTurn,
    EnemyTurn // Added for FSM support
}

public enum BattleAction
{
    Move,
    Attack,
    Rest,
    Calm
}

public enum Team
{
    Player,
    Enemy,
    Neutral
}