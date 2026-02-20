using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Skill/Player/Passive/LuckySix/ShootingInsight", fileName = "Passive_ShootingInsight")]
public class LuckySixShootingInsightPassive : PassiveAsset
{
    [Header("Stack Config")]
    public int triggerStacks = 5;     // 이 스택에 도달하면 통찰 강화 트리거
    public int resetStacks = 6;       // 이 스택에 도달하면 스택 리셋
    public bool onlyOnDamagePositive = true; // 실제 피해가 1 이상일 때만 카운트

    [Header("Insight Buff")]
    [Tooltip("5스택 도달 시 적용할 통찰 강화 버프 ID. StateStatModifierDB에서 insMultiplier를 설정하면 크리티컬/INS 관련 스탯에 반영됩니다.")]
    public UnitStateBuffId insightBuffId = UnitStateBuffId.InsightUp;

    // Runtime: 유닛별 스택 관리 (SO는 공유되므로 딕셔너리 필요)
    private readonly Dictionary<BattleUnit, int> _stacks = new();

    public override void OnAttach(BattleUnit _owner, BattleManager _battle)
    {
        if (_owner == null) return;

        _owner.OnDealtDamage += HandleDealtDamage;
        _stacks[_owner] = 0;
    }

    public override void OnDetach(BattleUnit _owner, BattleManager _battle)
    {
        if (_owner == null) return;

        _owner.OnDealtDamage -= HandleDealtDamage;
        _stacks.Remove(_owner);

        // 버프가 남아 있으면 제거
        var usc = _owner.GetComponent<UnitStateController>();
        if (usc != null && insightBuffId != UnitStateBuffId.None)
            usc.RemoveBuff(insightBuffId);
    }

    private void HandleDealtDamage(BattleUnit _dealer, BattleUnit _victim, int _damage, SkillAsset _source)
    {
        if (_dealer == null) return;
        if (onlyOnDamagePositive && _damage <= 0) return;

        if (!_stacks.TryGetValue(_dealer, out var cur))
            cur = 0;

        cur++;
        Debug.Log($"[Passive:ShootingInsight] {_dealer.name} stack -> {cur}");

        // 상태 아이콘 UI 갱신 (showTurns=false 이라 duration은 의미 없음)
        StatusController statusController = _dealer.GetComponent<StatusController>();
        if (statusController != null)
            statusController.SetStacks(StatusId.Shooting, cur, 0);

        // ─── 5스택 도달: 통찰 강화 버프 적용 ─────────────────────────────
        if (cur == triggerStacks)
        {
            Debug.Log($"[Passive:ShootingInsight] {_dealer.name} 통찰 강화 발동 — {insightBuffId} 버프 적용");
            _dealer.AnnouncePassive(displayName);    // 패시브 발동 라벨 호출

            // UnitStateController에 InsightUp 버프를 무기한 부여.
            // StateStatModifierDB에서 insightBuffId 항목의 insMultiplier를 설정하면
            // ComputeMultipliers()를 통해 INS(통찰) 스탯에 반영됩니다.
            var usc = _dealer.GetComponent<UnitStateController>();
            if (usc != null && insightBuffId != UnitStateBuffId.None)
                usc.ApplyBuff(insightBuffId);
        }

        // ─── 6스택 도달: 스택 리셋 + 버프 제거 ─────────────────────────────
        if (cur >= resetStacks)
        {
            Debug.Log($"[Passive:ShootingInsight] {_dealer.name} 스택 리셋 — 통찰 버프 해제");
            cur = 0;

            // 스택 아이콘 초기화
            if (statusController != null)
                statusController.SetStacks(StatusId.Shooting, 0);

            // InsightUp 버프 제거 (다음 5스택까지 비활성화)
            var usc = _dealer.GetComponent<UnitStateController>();
            if (usc != null && insightBuffId != UnitStateBuffId.None)
                usc.RemoveBuff(insightBuffId);
        }

        _stacks[_dealer] = cur;
    }
}
