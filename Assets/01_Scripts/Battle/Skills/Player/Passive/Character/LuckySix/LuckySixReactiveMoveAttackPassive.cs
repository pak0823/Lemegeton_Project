using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Passives/LuckySix/Passive_2", fileName = "Passive_AttackOnEnemyMove")]
public class LuckySixReactiveMoveAttackPassive : PassiveAsset
{
    [Tooltip("이 슬롯의 스킬을 사용 (0 = 첫 번째 스킬 = '1번 스킬')")]
    public int skillSlotIndex = 0;

    [Tooltip("리액션 공격 시 gap close 점프를 사용할지 여부")]
    public bool useGapClose = true;

    private BattleUnit _owner;
    private BattleManager _battle;

    private readonly List<BattleUnit> _candidates = new();
    private bool _reactionScheduled;

    public override void OnAttach(BattleUnit _owner, BattleManager _battle)
    {
        this._owner = _owner;
        this._battle = _battle;
        _candidates.Clear();
        _reactionScheduled = false;

        BattleUnit.OnAnyMoved += HandleAnyMoved;
    }

    public override void OnDetach(BattleUnit _owner, BattleManager _battle)
    {
        BattleUnit.OnAnyMoved -= HandleAnyMoved;
        _candidates.Clear();
        _reactionScheduled = false;

        this._owner = null;
        this._battle = null;
    }

    private void HandleAnyMoved(BattleUnit _mover)
    {
        if (_owner == null || _battle == null) return;
        if (_mover == null || _mover.IsDead || _mover.IsRetreated) return;
        if (_owner.IsDead || _owner.IsRetreated) return;

        // 자기 자신 무시
        if (_mover == _owner) return;

        // 아군 이동은 무시 (요구사항: 적군 유닛이 이동했을 때)
        if (_mover.team == _owner.team) return;

        // "이 유닛의 차례가 아닐 때"만 발동
        if (_battle.ActingUnit == _owner) return;

        // 후보 목록에 추가
        if (!_candidates.Contains(_mover))
            _candidates.Add(_mover);

        // 한 번만 스케줄: 같은 프레임에 여러 번 이동해도 1회 처리
        if (!_reactionScheduled)
        {
            _reactionScheduled = true;

            if (_battle != null) _battle.RegisterReactionLock();

            _battle.StartCoroutine(Co_ReactiveAttack());
        }
    }

    private IEnumerator Co_ReactiveAttack()
    {
        // 같은 프레임에 여러 이동이 들어오면 모아서 처리하기 위해 한 프레임 대기
        yield return null;

        _reactionScheduled = false;

        // 조건을 만족하지 못해 중단될 경우 락 해제 필수
        if (_owner == null || _battle == null || _owner.IsDead || _owner.IsRetreated || _battle.ActingUnit == _owner)
        {
            _battle?.UnregisterReactionLock(); // [해제]
            yield break;
        }

        // 유효한 적만 남기기
        _candidates.RemoveAll(u => u == null || u.IsDead || u.IsRetreated || u.team == _owner.team);
        if (_candidates.Count == 0)
        {
            _battle.UnregisterReactionLock();
            yield break;
        }

        // 이동한 적이 둘 이상이면 그 중 하나를 무작위로 선택
        var target = _candidates[Random.Range(0, _candidates.Count)];
        _candidates.Clear();

        if (target == null || target.IsDead || target.IsRetreated)
        {
            _battle.UnregisterReactionLock();
            yield break;
        }

        var skill = GetReactiveSkill();
        if (skill == null)
        {
            _battle.UnregisterReactionLock();
            yield break;
        }

        SkillAsset skillToUse = skill;

        bool doGapClose = useGapClose && skill.ShouldGapCloseToTarget(_owner, target);

        _owner.AnnouncePassive(displayName);    // 패시브 발동 라벨 호출

        // 행동 토큰/턴에 영향 없는 리액션 공격 실행
        yield return _battle.StartReactiveAttack(_owner, target, skillToUse, doGapClose);

        // 공격이 다 끝난 뒤에 락 해제
        _battle.UnregisterReactionLock();
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
