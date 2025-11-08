using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    menuName = "Battle/Passives/Reactive/AttackAfterSelfMove",
    fileName = "Passive_AttackAfterSelfMove")]
public class ReactiveAfterMoveAttackPassive : PassiveAsset
{
    [Header("Skill")]
    [Tooltip("이 슬롯의 스킬을 사용 (0 = 1번 스킬)")]
    public int skillSlotIndex = 0;

    [Tooltip("리액션 공격 시 대상에게 점프(gap close)를 사용할지 여부")]
    public bool useGapClose = false;

    [Tooltip("true면 이 유닛의 '자기 턴에 한 이동'만 발동. false면 밀침/스킬 이동 등 모든 이동 포함.")]
    public bool onlyOnOwnTurn = false;

    private BattleUnit _owner;
    private BattleManager _battle;

    public override void OnAttach(BattleUnit owner, BattleManager battle)
    {
        _owner = owner;
        _battle = battle;

        if (_owner != null)
            _owner.OnMoved += HandleOwnerMoved;
    }

    public override void OnDetach(BattleUnit owner, BattleManager battle)
    {
        if (_owner != null)
            _owner.OnMoved -= HandleOwnerMoved;

        _owner = null;
        _battle = null;
    }

    private void HandleOwnerMoved(BattleUnit unit, Tilemap fromMap, Vector3Int fromCell, Vector3Int toCell)
    {
        if (_owner == null || _battle == null) return;
        if (unit != _owner) return;
        if (_owner.IsDead || _owner.IsRetreated) return;

        // 자기 턴에 한 이동만 반응할지 옵션
        if (onlyOnOwnTurn && _battle.ActingUnit != _owner)
            return;

        // 이동 직후 한 번만 처리하면 되니 바로 코루틴으로 넘긴다
        _battle.StartCoroutine(Co_AttackRandomEnemyAfterMove());
    }

    private IEnumerator Co_AttackRandomEnemyAfterMove()
    {
        // 한 프레임 양보해서 이동 연출/상태 정리 후 처리
        yield return null;
        //yield return new WaitForSeconds(0.15f); 텀이 너무 짧을 시 인위적으로 간격 늘릴 때 사용

        if (_owner == null || _battle == null) yield break;
        if (_owner.IsDead || _owner.IsRetreated) yield break;

        // 여전히 조건 유효한지 확인 (턴이 넘어가버렸다면 취소할 수도 있음 - 기획에 맞게 선택)
        // 여기서는 "이동 직후 즉시" 컨셉이라 턴 체크는 생략하거나 onlyOnOwnTurn에서 이미 걸렀다고 봄.

        // 살아있는 적 군을 수집
        var enemies = _battle
            .GetLivingEnemiesOf(_owner)
            .ToList();

        if (enemies.Count == 0)
            yield break;

        // 무작위 적 하나 선택
        var target = enemies[Random.Range(0, enemies.Count)];
        if (target == null || target.IsDead || target.IsRetreated)
            yield break;

        // 사용할 스킬 찾기 (1번 스킬 슬롯)
        var skill = GetReactiveSkill();
        if (skill == null)
            yield break;

        // MP / 쿨다운을 소비하게 할지 말지는 기획 선택.
        // 일단 일반 스킬과 동일하게 체크해두자:
        if (_owner.IsSkillOnCooldown(skill)) yield break;
        if (!_owner.HasMP(skill.mpCost)) yield break;

        bool doGapClose = useGapClose && skill.ShouldGapCloseToTarget(_owner, target);

        // 턴/행동 토큰에는 영향 주지 않는 리액션 공격 실행
        _battle.StartReactiveAttack(_owner, target, skill, doGapClose);
    }

    private SkillAsset GetReactiveSkill()
    {
        if (_owner == null || _owner.data == null) return null;

        var skills = _owner.data.skills;
        if (skills == null || skills.Length == 0) return null;

        int idx = Mathf.Clamp(skillSlotIndex, 0, skills.Length - 1);
        var s = skills[idx];

        // 상태에 따라 치환되는 스킬(StateConditionalMulti 등)을 쓰는 경우 대응
        if (s is ISkillForStateResolver resolver)
            s = resolver.ResolveForCaster(_owner) ?? s;

        return s;
    }
}
