using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Training/Training DB", fileName = "TrainingDB")]
public class TrainingDB : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public UnitData unit;       // 어떤 유닛의
        public SkillAsset skill;    // 어떤 스킬에
        public int routeIndex;   // -1=미선택, 0~2 = 루트
    }

    [SerializeField] private List<Entry> entries = new();

    public int GetRoute(UnitData unit, SkillAsset skill)
    {
        if (unit == null || skill == null) return -1;

        var key = GetTrainingKey(unit, skill);

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.unit == unit && e.skill == key)
                return e.routeIndex;
        }
        return -1;
    }

    public void SetRoute(UnitData unit, SkillAsset skill, int routeIndex)
    {
        if (unit == null || skill == null) return;
        routeIndex = Mathf.Clamp(routeIndex, -1, 2);

        var key = GetTrainingKey(unit, skill);

        Debug.Log($"[TrainingDB.SetRoute] unit={unit.name}({unit.GetInstanceID()}) " +
              $"skill={skill.name}({skill.GetInstanceID()}) legacyId={skill.legacyId} " +
              $"-> key={key.name}({key.GetInstanceID()}) route={routeIndex}");

        // 이미 있으면 갱신
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.unit == unit && e.skill == key)
            {
                e.routeIndex = routeIndex;
                return;
            }
        }

        // 없으면 새로 추가
        entries.Add(new Entry
        {
            unit = unit,
            skill = key,
            routeIndex = routeIndex
        });
    }

    SkillAsset GetTrainingKey(UnitData unit, SkillAsset skill)
    {
        if (unit == null || skill == null) return skill;

        // legacyId가 설정되지 않은 스킬은 그대로 사용
        if (skill.legacyId == SkillId.None)
            return skill;

        // 이미 이 유닛에 대해 같은 legacyId로 저장된 항목이 있으면 그걸 키로 사용
        foreach (var e in entries)
        {
            if (e == null || e.unit != unit || e.skill == null) continue;
            if (e.skill.legacyId == skill.legacyId)
                return e.skill;
        }

        // 아직 없으면 이번 스킬 자체를 키로 사용
        return skill;
    }
    public int GetRouteByLegacy(UnitData unit, SkillId legacyId)
    {
        if (unit == null) return -1;
        if (legacyId == SkillId.None) return -1;

        foreach (var e in entries)
        {
            if (e == null || e.unit != unit) continue;
            if (e.skill == null) continue;
            if (e.skill.legacyId == legacyId)
                return e.routeIndex;
        }
        return -1;
    }

    // 유닛 전체 초기화(리셋 버튼에서 사용)
    public void ClearSelectionsFor(UnitData unit)
    {
        if (unit == null) return;
        entries.RemoveAll(e => e != null && e.unit == unit);
    }
}
