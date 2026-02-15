using System;

using System.Collections.Generic;

using UnityEngine;



[CreateAssetMenu(menuName = "Data/Database/Visual/UnitStateBuff", fileName = "UnitStateBuffVisualDB")]

public class UnitStateBuffVisualDB : ScriptableObject

{

    [Serializable]

    public class Entry

    {

        public UnitStateBuffId id;

        public Sprite icon;

        public Color tint = Color.white;

        public int sortOrder = 0;      // 낮을수록 앞쪽

        public string displayName;     // 툴팁 등에 사용할 이름

        public bool showTurns = true;  // 턴 표시 여부

    }



    [SerializeField] private List<Entry> entries = new();

    private Dictionary<UnitStateBuffId, Entry> _cache;



    void OnEnable() => Rebuild();

#if UNITY_EDITOR

    void OnValidate() => Rebuild();

#endif



    void Rebuild()

    {

        _cache = new Dictionary<UnitStateBuffId, Entry>(entries.Count);

        foreach (var e in entries)

            _cache[e.id] = e;

    }



    public Entry GetEntry(UnitStateBuffId id)

    {

        if (_cache == null) Rebuild();

        _cache.TryGetValue(id, out var e);

        return e;

    }



    public Sprite GetIcon(UnitStateBuffId id) => GetEntry(id)?.icon;

    public Color GetColor(UnitStateBuffId id) => GetEntry(id)?.tint ?? Color.white;

    public int GetSortOrder(UnitStateBuffId id) => GetEntry(id)?.sortOrder ?? 0;

    public bool GetShowTurns(UnitStateBuffId id) => GetEntry(id)?.showTurns ?? true;

}

