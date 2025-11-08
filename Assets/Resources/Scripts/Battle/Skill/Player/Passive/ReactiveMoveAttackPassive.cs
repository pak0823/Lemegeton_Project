using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Passives/Reactive/AttackOnEnemyMove", fileName = "Passive_AttackOnEnemyMove")]
public class ReactiveMoveAttackPassive : PassiveAsset
{
    [Tooltip("이 슬롯의 스킬을 사용 (0 = 첫 번째 스킬 = '1번 스킬')")]
    public int skillSlotIndex = 0;

    [Tooltip("리액션 공격 시 gap close 점프를 사용할지 여부")]
    public bool useGapClose = true;

    private BattleUnit _owner;
    private BattleManager _battle;

    private readonly List<BattleUnit> _candidates = new();
    private bool _reactionScheduled;

    public override void OnAttach(BattleUnit owner, BattleManager battle)
    {
        _owner = owner;
        _battle = battle;
        _candidates.Clear();
        _reactionScheduled = false;

        BattleUnit.OnAnyMoved += HandleAnyMoved;
    }

    public override void OnDetach(BattleUnit owner, BattleManager battle)
    {
        BattleUnit.OnAnyMoved -= HandleAnyMoved;
        _candidates.Clear();
        _reactionScheduled = false;

        _owner = null;
        _battle = null;
    }

    private void HandleAnyMoved(BattleUnit mover)
    {
        if (_owner == null || _battle == null) return;
        if (mover == null || mover.IsDead || mover.IsRetreated) return;
        if (_owner.IsDead || _owner.IsRetreated) return;

        // 자기 자신 무시
        if (mover == _owner) return;

        // 아군 이동은 무시 (요구사항: 적군 유닛이 이동했을 때)
        if (mover.team == _owner.team) return;

        // "이 유닛의 차례가 아닐 때"만 발동
        if (_battle.ActingUnit == _owner) return;

        // 후보 목록에 추가
        if (!_candidates.Contains(mover))
            _candidates.Add(mover);

        // 한 번만 스케줄: 같은 프레임에 여러 번 이동해도 1회 처리
        if (!_reactionScheduled)
        {
            _reactionScheduled = true;
            _battle.StartCoroutine(Co_ReactiveAttack());
        }
    }

    private IEnumerator Co_ReactiveAttack()
    {
        // 같은 프레임에 여러 이동이 들어오면 모아서 처리하기 위해 한 프레임 대기
        yield return null;

        _reactionScheduled = false;

        if (_owner == null || _battle == null) yield break;
        if (_owner.IsDead || _owner.IsRetreated) yield break;

        // 여전히 이 유닛의 차례라면(턴이 넘어왔으면) 반응하지 않음
        if (_battle.ActingUnit == _owner) yield break;

        // 유효한 적만 남기기
        _candidates.RemoveAll(u => u == null || u.IsDead || u.IsRetreated || u.team == _owner.team);
        if (_candidates.Count == 0) yield break;

        // 이동한 적이 둘 이상이면 그 중 하나를 무작위로 선택
        var target = _candidates[Random.Range(0, _candidates.Count)];
        _candidates.Clear();

        if (target == null || target.IsDead || target.IsRetreated) yield break;

        var skill = GetReactiveSkill();
        if (skill == null) yield break;

        // MP/쿨다운을 존중할지 여부는 기획에 따라 조절 가능
        if (_owner.IsSkillOnCooldown(skill)) yield break;
        if (!_owner.HasMP(skill.mpCost)) yield break;

        bool doGapClose = useGapClose && skill.ShouldGapCloseToTarget(_owner, target);

        // 행동 토큰/턴에 영향 없는 리액션 공격 실행
        _battle.StartReactiveAttack(_owner, target, skill, doGapClose);
    }

    private SkillAsset GetReactiveSkill()
    {
        if (_owner == null || _owner.data == null) return null;

        var arr = _owner.data.skills;
        if (arr == null || arr.Length == 0) return null;

        int idx = Mathf.Clamp(skillSlotIndex, 0, arr.Length - 1);
        var s = arr[idx];

        // 상태에 따라 치환되는 스킬(StateConditionalMulti 등) 대응
        if (s is ISkillForStateResolver resolver)
            s = resolver.ResolveForCaster(_owner) ?? s;

        return s;
    }
}
