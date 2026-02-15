using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Battle/Skill/Player/Passive/NoEul/WeakOnLowestHp")]
public class NoEulWeakOnLowestHpPassive : PassiveAsset
{
    [SerializeField] private int weakDurationTurns = 3;
    [SerializeField] private StatusId weakStatusId = StatusId.Weakness;
    [SerializeField] private int weakStacks = 1;



    private BattleUnit owner;

    private BattleManager battle;



    public override void OnAttach(BattleUnit _owner, BattleManager _battlemanager)

    {

        owner = _owner;

        battle = _battlemanager;



        if (owner != null)

            owner.OnDealtDamage += HandleDealtDamage;

    }



    public override void OnDetach(BattleUnit _owner, BattleManager _battlemanager)

    {

        if (owner != null)

            owner.OnDealtDamage -= HandleDealtDamage;



        owner = null;

        battle = null;

    }



    private void HandleDealtDamage(BattleUnit attacker, BattleUnit target, int damage, SkillAsset skill)

    {

        // 다른 유닛의 공격은 무시

        if (attacker != owner) return;

        if (battle == null) return;

        if (target == null || target.IsDead) return;

        if (target.data.team == owner.data.team) return; // 아군은 제외



        // 이 공격을 맞기 전 대상 HP 복원

        float targetHpBefore = target.HP + Mathf.Max(0, damage);



        // 현재 생존 중인 적들 중에서 HP가 가장 낮은 애들 찾기

        var candidates = new List<BattleUnit>();

        float minHp = float.MaxValue;



        foreach (var enemy in battle.GetLivingEnemiesOf(owner))

        {

            if (enemy == null) continue;



            float hp = (enemy == target) ? targetHpBefore : enemy.HP;   // 공격받은 대상은 "맞기 전 HP"로 비교



            if (hp < minHp - 0.01f)

            {

                minHp = hp;

                candidates.Clear();

                candidates.Add(enemy);

            }

            else if (Mathf.Abs(hp - minHp) <= 0.01f)

            {

                candidates.Add(enemy);

            }

        }



        if (candidates.Count == 0) return;



        // 최소 HP 적들 중 랜덤 하나 선택

        var chosen = candidates[Random.Range(0, candidates.Count)];



        // 이번에 때린 대상이 그 랜덤 선택된 "가장 낮은 체력 적"이 아닐 경우 발동 X

        if (chosen != target) return;



        var sc = target.GetComponent<StatusController>();

        if (sc == null) return;



        sc.ApplyWithTurnContext(weakStatusId, weakStacks, weakDurationTurns);

        Debug.Log($"[Passive:WeakOnLowestHp] {owner.name} → {target.name}에게 나약 {weakStacks}중첩({weakDurationTurns}턴) 부여");

    }


}
