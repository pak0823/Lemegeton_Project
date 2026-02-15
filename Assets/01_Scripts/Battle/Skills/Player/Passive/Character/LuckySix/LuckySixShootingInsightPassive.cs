using System.Collections.Generic;

using UnityEngine;



[CreateAssetMenu(menuName = "Battle/Skill/Player/Passive/LuckySix/ShootingInsight", fileName = "Passive_ShootingInsight")]

public class LuckySixShootingInsightPassive : PassiveAsset

{

    [Header("Stack Config")]

    public int triggerStacks = 5;     // 이 스택에 도달하면 통찰 강화 트리거

    public int resetStacks = 6;       // 이 스택에 도달하면 스택 리셋

    public bool onlyOnDamagePositive = true; // 실제 피해가 1 이상일 때만 카운트



    // 나중에 통찰(크리티컬 관련) 스탯으로 쓸 자리

    [Header("Insight Placeholder")]

    public float insightValue = 6.0f; // TODO: 추후 크리 확률/배율과 연결



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

        // 추후 통찰 버프/상태를 쓴다면 여기서 제거

    }



    private void HandleDealtDamage(BattleUnit _dealer, BattleUnit _victim, int _damage, SkillAsset _source)

    {

        if (_dealer == null) return;

        if (onlyOnDamagePositive && _damage <= 0) return;



        if (!_stacks.TryGetValue(_dealer, out var cur))

            cur = 0;



        cur++;

        Debug.Log($"[Passive:ShootingInsight] {_dealer.name} stack -> {cur}");



        StatusController statusController = _dealer.GetComponent<StatusController>();

        if (statusController != null)

            statusController.SetStacks(StatusId.Shooting, cur, 0); // showTurns=false 이라 duration은 의미 없음



        // 5스택 도달: 통찰 강화 트리거 (현재는 로그/플레이스홀더만)

        if (cur == triggerStacks)

        {

            Debug.Log($"[Passive:ShootingInsight] {_dealer.name} 통찰 강화 발동 (x{insightValue}) [TODO: 실제 스탯 연동]");



            _dealer.AnnouncePassive(displayName);    // 패시브 발동 라벨 호출

            // TODO: UnitStateBuffId.Insight 같은 버프를 적용해서

            //       StateStatModifierDB에서 치명 관련 수치를 올리도록 연동 가능.

        }



        // 6스택 도달: 리셋

        if (cur >= resetStacks)

        {

            Debug.Log($"[Passive:ShootingInsight] {_dealer.name} 스택 리셋");

            cur = 0;

            if (statusController != null)

                statusController.SetStacks(StatusId.Shooting, 0); // 아이콘 제거

            // TODO: 여기서 사격 중첩/통찰 버프 상태 제거

        }



        _stacks[_dealer] = cur;

    }

}

