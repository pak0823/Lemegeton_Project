using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Battle/Skill/Player/Passive/NoEul/DoubleAttack")]
public class NoEulDoubleAttack : PassiveAsset
{
    [Tooltip("더블 어택 발동 확률")]
    public float chance = 0.5f;

    // 실제 구현 내용은 파일이 유실되어 기본 구조만 복구함.
    // 필요 시 추가 구현이 필요합니다.
    public override void OnAttach(BattleUnit owner, BattleManager battle)
    {
        base.OnAttach(owner, battle);
    }

    public override void OnDetach(BattleUnit owner, BattleManager battle)
    {
        base.OnDetach(owner, battle);
    }
}
