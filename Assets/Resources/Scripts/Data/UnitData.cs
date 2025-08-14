using UnityEngine;

[CreateAssetMenu(menuName = "Battle/UnitData", fileName = "UnitData")]
public class UnitData : ScriptableObject
{
    public Team team = Team.Player;   // Player / Enemy
    public int MaxHP = 100;
    public int AttackDamage = 1;
    public int AttackRange = 2;
    public int AGI = 10;
}
