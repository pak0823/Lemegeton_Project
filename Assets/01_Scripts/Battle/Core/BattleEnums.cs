public enum BattleState
{
    Idle,
    ActionSelect,
    Moving,
    Targeting,
    Resolving,
    TargetingKnockback,
    EndTurn,
    EnemyTurn,
    Victory,
    Defeat
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
