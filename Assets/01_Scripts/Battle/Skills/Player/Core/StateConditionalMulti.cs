using System;

using System.Collections;

using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using UnityEngine.Tilemaps;



/// <summary>

/// 캐스터 상태에 따라 스킬을 치환하는 멀티 규칙 라우터.

/// - rules 배열을 위에서부터 평가해 첫 매칭 규칙의 skill로 치환

/// - chosen skill이 ParametricDamageSkill이면 areaPreset 덮어쓰기 옵션 제공

/// - 이 라우터 SO 자체는 직접 시전되지 않는다(치환 전용)

/// </summary>

[CreateAssetMenu(menuName = "Battle/Skill/Template/StateConditionalMulti", fileName = "StateConditionalSkillMulti")]

public class StateConditionalSkillMulti : SkillAsset, ISkillForStateResolver

{

    [Serializable]

    public class Rule

    {

        [Tooltip("이 규칙의 식별용 이름(옵션)")]

        public string name;



        [Header("조건(AND/OR/NOT)")]

        public List<UnitStateId> requireAll = new();   // 모두 가지고 있어야 통과

        public List<UnitStateId> requireAny = new();   // 하나라도 있으면 통과

        public List<UnitStateId> forbidAny = new();   // 하나라도 있으면 탈락



        [Header("매칭 시 사용할 스킬")]

        public SkillAsset skill;



        [Header("옵션: ParametricDamageSkill 범위 프리셋 덮어쓰기")]

        public bool overrideAreaPreset = false;

        public AreaPreset areaPresetWhenMatched = AreaPreset.Single;



        [Header("옵션: 표시 정보 덮어쓰기")]

        public bool overrideDisplayName = false;

        public string displayNameWhenMatched;

        public bool overrideIcon = false;

        public Sprite iconWhenMatched;

    }



    [Header("규칙(위에서부터 우선 적용)")]

    public List<Rule> rules = new();



    [Header("기본 스킬(어느 규칙도 매칭되지 않을 때)")]

    public SkillAsset defaultSkill;



    public override IEnumerator Execute(BattleManager _battlemanager, BattleUnit _caster, BattleUnit _targetUnit, Tilemap _targetMap, Vector3Int _targetCell)

    {

        // 현재 상태에 맞는 스킬을 찾아낸다

        SkillAsset realSkill = ResolveForCaster(_caster);



        // 만약 매칭되는 게 없거나, 자기가 자기 자신을 리턴하면(무한루프 방지) 종료

        if (realSkill == null || realSkill == this)

        {

            Debug.LogWarning($"[StateConditionalMulti] {name}: 실행할 스킬을 찾지 못함.");

            _battlemanager.CancelCurrentAction();

            yield break;

        }



        // 찾은 진짜 스킬에게 실행을 위임한다

        // 매니저는 이게 라우터였는지 알 필요 없이, 결과적으로 진짜 스킬이 나간다.

        yield return realSkill.Execute(_battlemanager, _caster, _targetUnit, _targetMap, _targetCell);

    }



    /// <summary>캐스터 상태를 보고 실제 시전할 SkillAsset을 반환</summary>

    public SkillAsset ResolveForCaster(BattleUnit caster)

    {

        // 현재 상태 집합을 해시셋으로

        var usc = caster ? caster.GetComponent<UnitStateController>() : null;

        var states = usc != null ? usc.GetAll() : Array.Empty<UnitStateId>();

        var set = new HashSet<UnitStateId>(states);



        // 규칙 순서대로 평가

        foreach (var r in rules)

        {

            if (r == null) continue;



            // forbidAny: 하나라도 포함되면 탈락

            if (r.forbidAny != null && r.forbidAny.Any(set.Contains))

                continue;



            // requireAll: 모두 포함해야 통과

            if (r.requireAll != null && r.requireAll.Any() && !r.requireAll.All(set.Contains))

                continue;



            // requireAny: 비어있지 않다면 최소 하나 포함해야 통과

            if (r.requireAny != null && r.requireAny.Any() && !r.requireAny.Any(set.Contains))

                continue;



            // ---- 규칙 매칭됨 ----

            var chosen = r.skill ? r.skill : defaultSkill;

            if (chosen == null) return this; // 비상시: 자기 자신 반환



            // 필요 시 런타임 클론을 만들어 안전하게 덮어쓰기

            SkillAsset product = chosen;



            // ParametricDamageSkill 범위 프리셋 덮어쓰기

            if (r.overrideAreaPreset && chosen is ParametricDamageSkill pdm)

            {

                var inst = ScriptableObject.Instantiate(pdm); // 런타임 클론

                inst.hideFlags = HideFlags.HideAndDontSave;

                inst.areaPreset = r.areaPresetWhenMatched;

                product = inst;

            }



            // 표시 정보(이름/아이콘) 덮어쓰기

            if (r.overrideDisplayName || r.overrideIcon)

            {

                // 표시정보만 바꾸고 싶어도 원본 SO를 건드리지 않으려고 클론

                var inst = ScriptableObject.Instantiate(product);

                inst.hideFlags = HideFlags.HideAndDontSave;

                if (r.overrideDisplayName && !string.IsNullOrEmpty(r.displayNameWhenMatched))

                    inst.displayName = r.displayNameWhenMatched;

                if (r.overrideIcon && r.iconWhenMatched != null)

                    inst.descriptionImage = r.iconWhenMatched;

                product = inst;

            }



            return product;

        }



        // 어떤 규칙도 매칭되지 않으면 기본 스킬

        return defaultSkill != null ? defaultSkill : this;

    }



    // 라우터 자체는 직접 시전되지 않음(치환 전용 no-op)

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddRow) { yield break; }

    public override IEnumerator ResolveOnUnit(BattleManager _battlemanager, BattleUnit caster, BattleUnit target) { yield break; }

    public override IEnumerator ResolveOnTile(BattleManager _battlemanager, Tilemap map, Vector3Int originCell, BattleUnit caster) { yield break; }

}

