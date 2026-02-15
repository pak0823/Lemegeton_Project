using System;

using System.Collections.Generic;

using UnityEngine;



[CreateAssetMenu(menuName = "Data/Database/StatusDescription", fileName = "StatusDescriptionDB")]

public class StatusDescriptionDB : ScriptableObject

{

    [Serializable]

    public class Entry

    {

        [Header("ID 선택 (둘 중 하나만 사용)")]

        public StatusId statusId = StatusId.None;

        public UnitStateBuffId buffId = UnitStateBuffId.None;



        [Header("표시용 텍스트")]

        public string displayName;          // 예: "출혈", "마력 강화"

        [TextArea]

        public string shortDescription;     // 예: "매 턴 체력을 잃습니다."

    }



    public List<Entry> entries = new List<Entry>();



    static StatusDescriptionDB _instance;

    public static StatusDescriptionDB Instance => _instance;



    Dictionary<StatusId, Entry> _statusMap;

    Dictionary<UnitStateBuffId, Entry> _buffMap;



    void OnEnable()

    {

        _instance = this;

        Rebuild();

    }



    void OnValidate()

    {

        if (_instance == null) _instance = this;

        Rebuild();

    }



    void Rebuild()

    {

        _statusMap = new Dictionary<StatusId, Entry>();

        _buffMap = new Dictionary<UnitStateBuffId, Entry>();



        foreach (var e in entries)

        {

            if (e.statusId != StatusId.None)

            {

                _statusMap[e.statusId] = e;

            }

            if (e.buffId != UnitStateBuffId.None)

            {

                _buffMap[e.buffId] = e;

            }

        }

    }



    public Entry Get(StatusId id)

    {

        if (_statusMap == null) Rebuild();

        _statusMap.TryGetValue(id, out var e);

        return e;

    }



    public Entry Get(UnitStateBuffId id)

    {

        if (_buffMap == null) Rebuild();

        _buffMap.TryGetValue(id, out var e);

        return e;

    }



    public string GetDisplayName(StatusId id)

    {

        if (id == StatusId.None) return "";

        var e = Get(id);

        return !string.IsNullOrEmpty(e?.displayName) ? e.displayName : id.ToString();

    }



    public string GetDisplayName(UnitStateBuffId id)

    {

        if (id == UnitStateBuffId.None) return "";

        var e = Get(id);

        return !string.IsNullOrEmpty(e?.displayName) ? e.displayName : id.ToString();

    }



    public string GetShortDescription(StatusId id)

    {

        var e = Get(id);

        return e?.shortDescription ?? "";

    }



    public string GetShortDescription(UnitStateBuffId id)

    {

        var e = Get(id);

        return e?.shortDescription ?? "";

    }

}

