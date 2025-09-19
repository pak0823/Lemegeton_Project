using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Visuals/Unit State Visual DB", fileName = "UnitStateVisualDB")]
public class UnitStateVisualDB : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public UnitStateId id;
        public Sprite icon;
        public Color tint = Color.white;
        public int sortOrder = 0;     // 낮을수록 앞에 배치
        public string displayName;    // (선택) UI 텍스트에 쓸 이름
    }

    [SerializeField] private List<Entry> entries = new();
    private Dictionary<UnitStateId, Entry> _cache;

    void OnEnable() => Rebuild();
#if UNITY_EDITOR
    void OnValidate() => Rebuild();
#endif

    void Rebuild()
    {
        _cache = new Dictionary<UnitStateId, Entry>(entries.Count);
        foreach (var e in entries) _cache[e.id] = e;
    }

    public Entry GetEntry(UnitStateId id)
    {
        if (_cache == null) Rebuild();
        _cache.TryGetValue(id, out var e);
        return e;
    }

    public Sprite GetIcon(UnitStateId id) => GetEntry(id)?.icon;
    public Color GetColor(UnitStateId id) => GetEntry(id)?.tint ?? Color.white;

    public int GetSortOrder(UnitStateId id)
    {
        var e = GetEntry(id);
        return e != null ? e.sortOrder : 0;
    }
}
