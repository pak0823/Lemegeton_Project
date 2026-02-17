public enum DebuffType
{
    Slow,
    Poison,
    Stun,
    Burn,
}
public enum Direction 
{ 
    None, 
    West, 
    East, 
    NW, 
    NE, 
    SW, 
    SE 
}
public enum SceneName
{
    None = 0,
    TitleScene,
    ExplorationScene,
    BattleScene,
}
// SceneNameExtensions class added for convenient string conversion if needed later. But Enum.ToString() is sufficient for now.
