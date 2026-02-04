public enum DamageSchool { Physical, Magical, Composite }
public enum AttackAttr { None, Pierce, Strike, Slash }
public enum SkillTargetMode { Unit, Tile }
public enum SkillTargetAlignment { Enemy, Ally, Any, Self }
public enum SkillCostResource { MP, Rage }
public enum SkillAnimKind { None, Melee, Ranged, SelfCast, Special }
public enum SkillId
{
    None = -1,
    Skill1 = 0,
    Skill2 = 1,
    Skill3 = 2,
    Skill4 = 3,
    Skill5 = 4
}
public enum TargetPriorityMode
{
    None,
    RandomSurvivor,
    HighestHostility,
    PreferredStatusThenHighestHostility,  // 예: Slow 우선 → 그 안에서 적대감 최고
}
public enum AreaPreset
{
    Single, //단일 대상
    Ring, //원형(중앙 포함 7칸)
    LineDiagU3, //세로(3칸)
    LineHorizontal, //(가로 3칸)
    LineDiagU7 //(1시 7시 방향 대각선 7칸)
}