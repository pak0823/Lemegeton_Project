using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 턴 종료 시 최대 HP의 일정 비율만큼 회복하는 패시브.
/// 이 패시브를 가진 유닛만 턴 종료 회복을 받는다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Passives/End Turn Regen",
    fileName = "Passive_EndTurnRegen")]
public class EndTurnRegenPassive : PassiveAsset
{
    [Header("Regen")]
    [Tooltip("턴 종료 시 회복할 비율. 예: 0.1 = MaxHP의 10%")]
    [Range(0f, 1f)]
    public float healRatio = 0.10f;

    [Tooltip("계산된 회복량이 1 미만일 때 최소 1로 보정할지 여부")]
    public bool clampToAtLeast1 = true;

    // 동시에 이 패시브를 쓰는 모든 유닛들
    private readonly HashSet<BattleUnit> _owners = new();

    // 현재 전투의 BattleManager (이벤트 구독용)
    private BattleManager _battle;

    public override float GetProgress()
    {
        // 1. 이미 해금 상태라면 1.0 (100%) 반환
        // (부모의 IsUnlocked 로직: unlockedByDefault가 true거나 PlayerPrefs에 1로 저장됨)
        if (IsUnlocked()) return 1.0f;

        // 해금이 안 됐다면 테스트용으로 강제로 진행도 반환
        return 1.0f;
    }
    public override void OnAttach(BattleUnit owner, BattleManager battle)
    {
        base.OnAttach(owner, battle);
        if (owner == null || battle == null)
            return;

        // 처음 붙을 때 BattleManager 기억 + 이벤트 구독
        if (_battle == null)
        {
            _battle = battle;
            _battle.OnUnitEndTurn += HandleUnitEndTurn;
        }

        _owners.Add(owner);
    }

    public override void OnDetach(BattleUnit owner, BattleManager battle)
    {
        base.OnDetach(owner, battle);
        if (owner == null)
            return;

        _owners.Remove(owner);

        // 더 이상 이 패시브를 가진 유닛이 없으면 이벤트 구독 해제
        if (_battle != null && _owners.Count == 0)
        {
            _battle.OnUnitEndTurn -= HandleUnitEndTurn;
            _battle = null;
        }
    }

    void HandleUnitEndTurn(BattleUnit unit)
    {
        if (unit == null) return;
        if (_battle == null) return;

        // 이 패시브를 가진 유닛이 아니면 무시
        if (!_owners.Contains(unit)) return;
        if (unit.IsDead) return;

        float before = unit.HP;

        unit.HealPercent(healRatio);       // 기존 Heal 로직 사용
        unit.AnnouncePassive(displayName); // 패시브 라벨 표시

        Debug.Log($"{name} [Passive Heal] +{unit.HP - before} → {unit.HP}/{unit.MaxHP}");

    }
}
