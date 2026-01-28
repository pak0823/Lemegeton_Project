// StackableStatusVisualDB.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Visuals/Stackable Status Visual DB", fileName = "StackableStatusVisualDB")]
public class StackableStatusVisualDB : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public StatusId id;        // 예: Slow
        public Sprite icon;        // 표시 아이콘
        public Color tint = Color.white;
        public int sortOrder = 0;  // 낮을수록 앞쪽
        public bool showStacks = true;
        public bool showTurns = true;
        public string displayName; // 툴팁/대체텍스트
    }

    [SerializeField] private List<Entry> entries = new();
    private Dictionary<StatusId, Entry> _map;

    void OnEnable() => Rebuild();
#if UNITY_EDITOR
    void OnValidate() => Rebuild();
#endif

    void Rebuild()
    {
        _map = new Dictionary<StatusId, Entry>(entries.Count);
        foreach (var e in entries) _map[e.id] = e;
    }

    public Entry Get(StatusId id)
    {
        if (_map == null) Rebuild();
        _map.TryGetValue(id, out var e);
        return e;
    }

    public int GetSortOrder(StatusId id) => Get(id)?.sortOrder ?? 0;
}
