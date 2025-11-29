using UnityEngine;

public enum ISBOSS { None, Boss }  //보스 구별

[CreateAssetMenu(menuName = "Battle/UnitData", fileName = "UnitData")]
public class UnitData : ScriptableObject
{
    [Header("Name")]
    public string DisplayName;
    
    [Header("Attributes")]
    public int PhysicalDamage = 0;  //근력
    public int MagicDamage = 0; //총명
    public int AGI = 0; //민첩
    public int BDY=0;   //신체
    public int MND=0;   //정신
    public int INS = 0; //통찰
    public int Hostility = 0; //적의

    [Header("Team Check")]
    public Team team = Team.Player;   // Player / Enemy

    [Header("Boss Check(Enemy Only)")]
    public ISBOSS isBoss = ISBOSS.None;

    [Header("Skills (per character)")]
    public SkillAsset[] skills; // 에디터에서 캐릭터별로 할당

    [Header("Passives (per character)")]
    public PassiveAsset[] passives; // 패시브 스킬들 (해금 여부는 런타임에서 결정)

    [Header("UI")]
    public Sprite UnitIcon; // ATB 아이콘용 스프라이트
}
