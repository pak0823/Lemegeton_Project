using UnityEngine;

[CreateAssetMenu(menuName = "Battle/UnitData", fileName = "UnitData")]
public class UnitData : ScriptableObject
{
    public Team team = Team.Player;   // Player / Enemy
    public int MaxHP = 0;    //최대 HP
    public int MaxMP = 0;     //최대 MP
    public int MaxRage = 0;     //최대 Rage(분노게이지)
    public int PhysicalDamage = 0;  //근력
    public int MagicDamage = 0; //총명
    public int AGI = 0; //민첩

    [Header("Skills (per character)")]
    public SkillAsset[] skills; // 에디터에서 캐릭터별로 할당

    [Header("UI")]
    public Sprite UnitIcon; // ATB 아이콘용 스프라이트
}
