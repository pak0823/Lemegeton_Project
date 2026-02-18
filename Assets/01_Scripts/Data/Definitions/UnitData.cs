using UnityEngine;



public enum ISBOSS { None, Boss }  //보스 구별



[CreateAssetMenu(menuName = "Battle/Unit/UnitDefinition", fileName = "UnitData")]

public class UnitData : ScriptableObject

{

    [Header("Unique ID")]

    public int unitID;



    [Header("Name")]

    public string DisplayName;

    

    [Header("Attributes")]

    public int baseSTR = 0;  //근력

    public int baseCLV = 0; //총명

    public int baseAGI = 0; //민첩

    public int baseBDY=0;   //신체

    public int baseMND=0;   //정신

    public int baseINS = 0; //통찰

    public int baseHostility = 0; //적의



    [Header("Team Check")]

    public Team team = Team.Player;   // Player / Enemy



    [Header("Boss Check(Enemy Only)")]

    public ISBOSS isBoss = ISBOSS.None;



    [Header("Animation")]

    [Tooltip("이 유닛이 사용하는 각 스킬(legacyId)에 대해, 유닛 고유의 애니메이션 트리거를 매핑합니다.")]

    public SkillAnimBinding[] skillAnimBindings;



    [Header("Skills (per character)")]

    public SkillAsset[] skills; // 에디터에서 캐릭터별로 할당



    [Header("Passives (per character)")]

    public PassiveAsset[] passives; // 패시브 스킬들 (해금 여부는 런타임에서 결정)



    [Header("Bond & Traits")]

    [Range(0, 12)]

    public int currentBond = 0; // 현재 유대 수치 (Max 12)

    // public TraitAsset activeTrait; // Deprecated: All unlocked traits are active now

    // 성격 리스트 (순서대로 2, 6, 12에 해금)

    public TraitAsset[] traits;



    [Header("UI")]

    public Sprite UnitIcon; // ATB 아이콘용 스프라이트

    public Sprite UnitStandImage; // 진형 배치용 스프라이트

    public GameObject battlePrefab; //전투 전용 프리팹



    [System.Serializable]

    public struct SkillAnimBinding

    {

        public SkillId skillId;     // SkillAsset.legacyId 와 동일한 값

        public string triggerName;  // 이 유닛이 그 스킬을 쓸 때 사용할 애니메이션 트리거

    }


    public (float maxHP, float maxMP, float maxRage) CalcMaxStats()
    {
        float maxHP = Mathf.Max(1, (baseBDY * 3f) + baseSTR);
        float maxMP = Mathf.Max(0, (baseMND * 3f) + baseCLV);
        float maxRage = Mathf.Max(0, baseSTR + baseCLV + baseAGI + baseBDY + baseMND + baseINS);
        return (maxHP, maxMP, maxRage);
    }
}

