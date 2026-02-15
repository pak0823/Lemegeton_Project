using System;

using System.Collections;

using System.Collections.Generic;

using UnityEngine;



[CreateAssetMenu(menuName = "Data/Database/Training", fileName = "TrainingDB")]

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



    // 검색 최적화를 위한 딕셔너리 캐시

    // Key: (UnitData, SkillAsset), Value: Entry

    private Dictionary<(UnitData, SkillAsset), Entry> entryCache = new Dictionary<(UnitData, SkillAsset), Entry>();



    // LegacyID 검색 최적화를 위한 캐시

    // Key: (UnitData, LegacySkillId), Value: 이미 등록된 SkillAsset Key

    private Dictionary<(UnitData, SkillId), SkillAsset> legacyKeyCache = new Dictionary<(UnitData, SkillId), SkillAsset>();



    // 캐시 초기화 플래그

    private bool isInitialized = false;



    // 싱글턴 패턴(다른 데서 TrainingDB.Instance로 접근)

    static TrainingDB _instance;

    public static TrainingDB Instance => _instance;



    void OnEnable()

    {

        _instance = this;

    }



    // 리스트 내용을 딕셔너리로 구축

    private void RebuildCache()

    {

        entryCache.Clear();

        legacyKeyCache.Clear();



        foreach (var entry in entries)

        {

            if (entry == null || entry.unit == null || entry.skill == null) continue;



            // 메인 엔트리 캐싱

            if (!entryCache.ContainsKey((entry.unit, entry.skill)))

            {

                entryCache.Add((entry.unit, entry.skill), entry);

            }



            // Legacy ID 캐싱 (LegacyId가 있는 경우)

            if (entry.skill.legacyId != SkillId.None)

            {

                if (!legacyKeyCache.ContainsKey((entry.unit, entry.skill.legacyId)))

                {

                    legacyKeyCache.Add((entry.unit, entry.skill.legacyId), entry.skill);

                }

            }

        }

        isInitialized = true;

    }



    // 캐시를 이용한 빠른 검색

    Entry FindEntry(UnitData _unit, SkillAsset _skill)

    {

        if (_unit == null || _skill == null) return null;

        if (!isInitialized) RebuildCache();



        var key = GetTrainingKey(_unit, _skill);



        if (entryCache.TryGetValue((_unit, key), out Entry foundEntry))

        {

            return foundEntry;

        }

        return null;

    }



    public int GetRoute(UnitData unit, SkillAsset skill)

    {

        var entry = FindEntry(unit, skill);

        int route = entry != null ? entry.routeIndex : -1;



        return route;

    }



    public void SetRoute(UnitData _unit, SkillAsset _skill, int _routeIndex)

    {

        if (_unit == null || _skill == null) return;

        _routeIndex = Mathf.Clamp(_routeIndex, -1, 2);



        var key = GetTrainingKey(_unit, _skill);

        var entry = FindEntry(_unit, _skill);



        if (entry != null)

        {

            entry.routeIndex = _routeIndex;

        }

        else

        {

            // 없으면 새로 추가

            var newEntry = new Entry

            {

                unit = _unit,

                skill = key,

                routeIndex = _routeIndex

            };

            entries.Add(newEntry);



            // 캐시 갱신

            if (!entryCache.ContainsKey((_unit, key)))

            {

                entryCache.Add((_unit, key), newEntry);

            }

            if (key.legacyId != SkillId.None && !legacyKeyCache.ContainsKey((_unit, key.legacyId)))

            {

                legacyKeyCache.Add((_unit, key.legacyId), key);

            }

        }

    }



    // 캐시를 활용한 Key 검색

    SkillAsset GetTrainingKey(UnitData _unit, SkillAsset _skill)

    {

        if (_unit == null || _skill == null) return _skill;



        // legacyId가 설정되지 않은 스킬은 그대로 사용

        if (_skill.legacyId == SkillId.None)

            return _skill;



        // 캐시에서 같은 LegacyId를 가진 기존 키가 있는지 확인

        if (legacyKeyCache.TryGetValue((_unit, _skill.legacyId), out SkillAsset existingKey))

        {

            return existingKey;

        }



        // 없으면 이번 스킬이 키가 됨

        return _skill;

    }

    public int GetRouteByLegacy(UnitData _unit, SkillId _legacyId)

    {

        if (_unit == null) return -1;

        if (_legacyId == SkillId.None) return -1;

        if (!isInitialized) RebuildCache();



        // Legacy ID로 등록된 스킬 키를 찾음

        if (legacyKeyCache.TryGetValue((_unit, _legacyId), out SkillAsset keySkill))

        {

            // 그 키로 엔트리를 찾음

            if (entryCache.TryGetValue((_unit, keySkill), out Entry entry))

            {

                return entry.routeIndex;

            }

        }



        return -1;

    }



    // 해당 훈련이 해금되었는지 확인

    public bool IsUnlocked(UnitData unit, SkillAsset skill, int routeIndex)

    {

        var entry = FindEntry(unit, skill);

        if (entry == null) return false;



        return entry.unlockedRoutes.Contains(routeIndex);

    }



    // 훈련 해금

    public void UnlockRoute(UnitData unit, SkillAsset skill, int routeIndex)

    {

        var key = GetTrainingKey(unit, skill);

        var entry = FindEntry(unit, skill);



        if (entry == null)

        {

            entry = new Entry { unit = unit, skill = key, routeIndex = -1 };

            entries.Add(entry);



            // 캐시 갱신

            entryCache.Add((unit, key), entry);

            if (key.legacyId != SkillId.None && !legacyKeyCache.ContainsKey((unit, key.legacyId)))

            {

                legacyKeyCache.Add((unit, key.legacyId), key);

            }

        }



        if (!entry.unlockedRoutes.Contains(routeIndex))

        {

            entry.unlockedRoutes.Add(routeIndex);

        }

    }



    // 유닛 전체 초기화(리셋 버튼에서 사용)

    public void ClearSelectionsFor(UnitData _unit)

    {

        if (_unit == null) return;



        // 리스트에서 제거

        entries.RemoveAll(e => e != null && e.unit == _unit);



        // 캐시는 부분 삭제가 복잡하므로 그냥 재구축 (초기화나 리셋은 빈번하지 않으므로)

        RebuildCache();

    }

}

