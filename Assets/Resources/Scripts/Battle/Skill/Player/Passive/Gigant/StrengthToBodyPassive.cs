using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 유닛의 근력에 비례해서 신체(BDY)를 올려주는 패시브.
/// 예: ratio = 0.2 이고 PhysicalDamage = 50 이면, BDY +10.
/// BDY가 HP 공식에 반영되어 있다면 MaxHP도 자동으로 증가.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Passives/Strength To Body", fileName = "Passive_StrengthToBody")]
public class StrengthToBodyPassive : PassiveAsset
{
    [Header("Config")]
    [Tooltip("근력 → 신체 전환 계수. 예: 0.2 면 PhysicalDamage × 0.2 만큼 BDY 증가")]
    [Range(0f, 10f)]
    public float ratio = 0.2f;

    // SO 하나를 여러 유닛이 공유하므로, 유닛별로 적용한 BDY 보너스를 기억해야 함
    private readonly Dictionary<BattleUnit, int> _bonusBdyByUnit = new();

    public override void OnAttach(BattleUnit owner, BattleManager battle)
    {
        if (owner == null) return;

        // 현재 근력(상태/버프까지 반영된 값)을 기준으로 계산
        int curPhysical = owner.PhysicalDamage;
        int bonusBDY = Mathf.RoundToInt(curPhysical * ratio);

        if (bonusBDY <= 0)
            return;

        // 이미 붙어있던 경우 중복 적용 방지 (이론상 SetPassiveEnabled가 막아주긴 함)
        if (_bonusBdyByUnit.ContainsKey(owner))
            return;

        _bonusBdyByUnit[owner] = bonusBDY;

        // BDY 증가 전 HP 기준 기록
        int oldMaxHP = owner.MaxHP;
        int oldHP = owner.HP;

        // BattleUnit 쪽에 BDY 보너스를 더해주는 helper를 따로 두고 호출
        owner.AddBodyBonusFromPassive(bonusBDY);

        // BDY 증가 후 새로운 MaxHP 계산됨
        int newMaxHP = owner.MaxHP;

        // 증가한 만큼 현재 HP도 올려줌
        int deltaHP = newMaxHP - oldMaxHP;
        if (deltaHP > 0)
        {
            owner.Heal(deltaHP); // 내부적으로 MaxHP 넘지 않음
        }

        // 패시브 발동 라벨 (ShootingInsightPassive에서 쓰던 것과 동일 패턴)
        owner.AnnouncePassive(displayName);
    }

    public override void OnDetach(BattleUnit owner, BattleManager battle)
    {
        if (owner == null) return;

        if (_bonusBdyByUnit.TryGetValue(owner, out int bonusBDY))
        {
            // 붙일 때 더했던 만큼 빼서 원상복구
            owner.AddBodyBonusFromPassive(-bonusBDY);
            _bonusBdyByUnit.Remove(owner);
        }
    }
}
