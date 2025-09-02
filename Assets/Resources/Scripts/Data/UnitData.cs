using UnityEngine;

[CreateAssetMenu(menuName = "Battle/UnitData", fileName = "UnitData")]
public class UnitData : ScriptableObject
{
    public Team team = Team.Player;   // Player / Enemy
    public int MaxHP = 1000;
    public int MaxMP = 500;
    public int MaxRage = 0;
    public int AttackDamage = 0;
    public int AGI = 0;

    [Header("UI")]
    public Sprite UnitIcon; // ATB 아이콘용 스프라이트
}
