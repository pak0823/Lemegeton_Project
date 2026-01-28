public enum BattleState
{
    Idle,
    ActionSelect,
    Moving,
    Targeting,
    Resolving,
    TargetingKnockback,
    EndTurn
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