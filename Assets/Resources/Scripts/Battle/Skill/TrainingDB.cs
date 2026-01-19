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
        public List<int> unlockedRoutes = new List<int>();  //해금된 인덱스를 저장하는 리스트
    }

    [SerializeField] private List<Entry> entries = new();

    // 싱글턴 패턴(다른 데서 TrainingDB.Instance로 접근)
    static TrainingDB _instance;
    public static TrainingDB Instance => _instance;

    void OnEnable()
    {
        _instance = this;
    }

    Entry FindEntry(UnitData _unit, SkillAsset _skill)
    {
        if (_unit == null || _skill == null) return null;

        var key = GetTrainingKey(_unit, _skill);

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.unit == _unit && e.skill == key)
                return e;
        }
        return null;
    }

    public int GetRoute(UnitData unit, SkillAsset skill)
    {
        var e = FindEntry(unit, skill);
        int route = e != null ? e.routeIndex : -1;

        return route;
    }

    public void SetRoute(UnitData _unit, SkillAsset _skill, int _routeIndex)
    {
        if (_unit == null || _skill == null) return;
        _routeIndex = Mathf.Clamp(_routeIndex, -1, 2);

        var key = GetTrainingKey(_unit, _skill);

        Debug.Log($"[TrainingDB.SetRoute] unit={_unit.name}({_unit.GetInstanceID()}) " +
              $"skill={_skill.name}({_skill.GetInstanceID()}) legacyId={_skill.legacyId} " +
              $"-> key={key.name}({key.GetInstanceID()}) route={_routeIndex}");

        // 이미 있으면 갱신
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.unit == _unit && e.skill == key)
            {
                e.routeIndex = _routeIndex;
                return;
            }
        }

        // 없으면 새로 추가
        entries.Add(new Entry
        {
            unit = _unit,
            skill = key,
            routeIndex = _routeIndex
        });
    }

    SkillAsset GetTrainingKey(UnitData _unit, SkillAsset _skill)
    {
        if (_unit == null || _skill == null) return _skill;

        // legacyId가 설정되지 않은 스킬은 그대로 사용
        if (_skill.legacyId == SkillId.None)
            return _skill;

        // 이미 이 유닛에 대해 같은 legacyId로 저장된 항목이 있으면 그걸 키로 사용
        foreach (var e in entries)
        {
            if (e == null || e.unit != _unit || e.skill == null) continue;
            if (e.skill.legacyId == _skill.legacyId)
                return e.skill;
        }

        // 아직 없으면 이번 스킬 자체를 키로 사용
        return _skill;
    }
    public int GetRouteByLegacy(UnitData _unit, SkillId _legacyId)
    {
        if (_unit == null) return -1;
        if (_legacyId == SkillId.None) return -1;

        foreach (var e in entries)
        {
            if (e == null || e.unit != _unit) continue;
            if (e.skill == null) continue;
            if (e.skill.legacyId == _legacyId)
                return e.routeIndex;
        }
        return -1;
    }

    // 해당 훈련이 해금되었는지 확인
    public bool IsUnlocked(UnitData unit, SkillAsset skill, int routeIndex)
    {
        var e = FindEntry(unit, skill);

        // 엔트리가 아예 없으면? -> 비용이 0인 기본 훈련만 해금된 것으로 간주할지, 아닐지 결정
        // 여기서는 안전하게 "DB에 없으면 잠김" 처리하되, CampSkillSlot에서 기본 처리를 보조함.
        if (e == null) return false;

        return e.unlockedRoutes.Contains(routeIndex);
    }

    // 훈련 해금
    public void UnlockRoute(UnitData unit, SkillAsset skill, int routeIndex)
    {
        var key = GetTrainingKey(unit, skill);
        var e = FindEntry(unit, skill);

        if (e == null)
        {
            // 없으면 새로 만들고 해금 목록에 추가
            e = new Entry { unit = unit, skill = key, routeIndex = -1 };
            entries.Add(e);
        }

        if (!e.unlockedRoutes.Contains(routeIndex))
        {
            e.unlockedRoutes.Add(routeIndex);
            // 저장 로직(JSON 등)이 있다면 여기서 호출
        }
    }

    // 유닛 전체 초기화(리셋 버튼에서 사용)
    public void ClearSelectionsFor(UnitData _unit)
    {
        if (_unit == null) return;
        entries.RemoveAll(e => e != null && e.unit == _unit);
    }
}
