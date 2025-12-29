using System.Collections;
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

    [Header("Advanced")]
    [Tooltip("패시브에서는 상태 기반 스킬 치환(ISkillForStateResolver)을 무시합니다. (권장: true)")]
    public bool ignoreStateResolver = true;

    private BattleUnit _owner;
    private BattleManager _battle;

    public override void OnAttach(BattleUnit _owner, BattleManager _battle)
    {
        this._owner = _owner;
        this._battle = _battle;

        if (this._owner != null)
            this._owner.OnMoved += HandleOwnerMoved;
    }

    public override void OnDetach(BattleUnit _owner, BattleManager _battle)
    {
        if (this._owner != null)
            this._owner.OnMoved -= HandleOwnerMoved;

        this._owner = null;
        this._battle = null;
    }

    private void HandleOwnerMoved(BattleUnit unit, Tilemap fromMap, Vector3Int fromCell, Vector3Int toCell)
    {
        if (_owner == null || _battle == null) return;
        if (unit != _owner) return;
        if (_owner.IsDead || _owner.IsRetreated) return;

        // 자기 턴 이동만 반응 옵션
        if (onlyOnOwnTurn && _battle.ActingUnit != _owner)
            return;

        // 이동 순간에 사용할 스킬을 "확정(capture)"한다.
        var capturedSkill = GetReactiveSkill_Captured();
        if (capturedSkill == null)
            return;

        _battle.RegisterReactionLock();

        _battle.StartCoroutine(Co_AttackRandomEnemyAfterMove(capturedSkill));
    }

    private IEnumerator Co_AttackRandomEnemyAfterMove(SkillAsset capturedSkill)
    {
        // 이동 연출/상태 정리 후 처리를 위해 1프레임 대기
        yield return null;

        if (_owner == null || _battle == null || _owner.IsDead || _owner.IsRetreated)
        {
            _battle?.UnregisterReactionLock(); // 락 해제
            yield break;
        }

        var enemies = _battle.GetLivingEnemiesOf(_owner).ToList();
        if (enemies.Count == 0)
        {
            _battle.UnregisterReactionLock(); // 락 해제
            yield break;
        }

        var target = enemies[Random.Range(0, enemies.Count)];
        if (target == null || target.IsDead || target.IsRetreated)
        {
            _battle.UnregisterReactionLock(); // 락 해제
            yield break;
        }

        var skill = capturedSkill;
        if (skill == null)
        {
            _battle.UnregisterReactionLock(); // 락 해제
            yield break;
        }

        // 쿨/MP 체크 (패시브 조건에 따라 다름)
        if (_owner.IsSkillOnCooldown(skill))
        {
            _battle.UnregisterReactionLock(); // 락 해제
            yield break;
        }

        int realCost = skill.GetEffectiveCost(_owner);
        SkillAsset skillToUse = skill;

        // 비용이 0보다 크다면 -> 강제로 0으로 만들기 위해 복제(Clone)
        if (realCost > 0)
        {
            skillToUse = Instantiate(skill);
            skillToUse.cost = 0;
            skillToUse.mpCost = 0;
        }
        else
        {
            // 비용이 이미 0이라면 -> 원본을 그대로 사용 (성능 이득)
            skillToUse = skill;
        }

        bool doGapClose = useGapClose && skill.ShouldGapCloseToTarget(_owner, target);

        _owner.AnnouncePassive(displayName);

        yield return _battle.StartReactiveAttack(_owner, target, skillToUse, doGapClose);

        // 모든 동작이 끝났으므로 락 해제
        _battle.UnregisterReactionLock();
    }

    private SkillAsset GetReactiveSkill_Captured()
    {
        if (_owner == null || _owner.data == null) return null;

        var skills = _owner.data.skills;
        if (skills == null || skills.Length == 0) return null;

        int idx = Mathf.Clamp(skillSlotIndex, 0, skills.Length - 1);
        var s = skills[idx];
        if (s == null) return null;

        // 1) 슬롯 스킬이 "라우터(StateConditionalSkillMulti)"면
        //    패시브에서는 라우터를 직접 시전하지 말고, defaultSkill(=의도한 기본 공격)만 고정해서 사용한다.
        if (s is StateConditionalSkillMulti multiRouter)
        {
            if (multiRouter.defaultSkill != null)
                return multiRouter.defaultSkill;

            // defaultSkill이 비어있으면: 여기서 라우터를 그대로 쓰면 지금 같은 문제가 재발할 수 있음.
            // 원인 추적을 쉽게 하려면 null로 막아버리는 게 안전하다.
            Debug.LogWarning(
                $"[Passive_AttackAfterSelfMove] Slot {idx} is StateConditionalSkillMulti but defaultSkill is null. " +
                $"Set defaultSkill to the intended basic attack skill asset.");
            return null;
        }

        // 2) 상태 치환(ISkillForStateResolver)은 옵션에 따라 수행
        if (!ignoreStateResolver && s is ISkillForStateResolver resolver)
            s = resolver.ResolveForCaster(_owner) ?? s;

        return s;
    }
}
