// Assets/Scripts/Combat/StatusController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusId { Slow = 1 }

public static class DebuffTuning
{
    // index = 둔화 스택 수 (0~6)
    public static readonly float[] SlowAgilityMult = { 1.0f, 0.9f, 0.8f, 0.7f, 0.5f, 0.3f, 0.1f };
    public const int SlowMaxStacks = 6;
}

[Serializable]
public class StatusEntry
{
    public StatusId id;
    public int stacks;
    public int remainingTurns;
    public bool skipNextStartTick;

    public StatusEntry(StatusId id, int stacks, int duration)
    {
        this.id = id;
        this.stacks = stacks;
        this.remainingTurns = duration;
    }
}

public class StatusController : MonoBehaviour
{
    readonly Dictionary<StatusId, StatusEntry> _map = new();
    public event Action OnStatusChanged;

    public void ApplyWithTurnContext(StatusId id, int stacks, int durationTurns, bool appliedDuringOwnersTurn)
    {
        bool skip = !appliedDuringOwnersTurn; // 적 턴 중 부여 → 다음 자신의 턴 시작 1회 스킵

        if (_map.TryGetValue(id, out var e))
        {
            // 스택 증가 + 최대 6중첩 캡
            e.stacks = Mathf.Min(e.stacks + stacks, DebuffTuning.SlowMaxStacks);

            // 지속시간은 '새로 부여된 둔화' 기준으로 리셋
            e.remainingTurns = durationTurns;

            // 스킵 플래그도 새 부여 기준으로 갱신
            e.skipNextStartTick = skip;
        }
        else
        {
            _map[id] = new StatusEntry(id, Mathf.Min(stacks, DebuffTuning.SlowMaxStacks), durationTurns)
            {
                skipNextStartTick = skip
            };
        }

        OnStatusChanged?.Invoke();
    }

    public void Clear(StatusId id)
    {
        if (_map.Remove(id)) OnStatusChanged?.Invoke();
    }

    public bool Has(StatusId id) => _map.ContainsKey(id);
    public int GetStacks(StatusId id) => _map.TryGetValue(id, out var e) ? e.stacks : 0;

    public float GetAgilityMultiplier() //민첩 배율 계산
    {
        int s = GetStacks(StatusId.Slow);
        s = Mathf.Clamp(s, 0, DebuffTuning.SlowMaxStacks);
        return DebuffTuning.SlowAgilityMult[s];
    }

    // 편의용 민첩 배율 계산
    public float ApplyAgilityModifier(float baseAgility)
        => baseAgility * GetAgilityMultiplier();

    /// <summary>이 유닛의 턴 시작 시 지속시간 감소/정리.</summary>
    public void OnTurnStart()
    {
        bool changed = false;
        var toRemove = new List<StatusId>();
        foreach (var kv in _map)
        {
            var e = kv.Value;
            if (e.remainingTurns > 0)
            {
                if (e.skipNextStartTick) { e.skipNextStartTick = false; /* 이번엔 스킵 */ }
                else
                {
                    e.remainingTurns--;
                    if (e.remainingTurns <= 0) toRemove.Add(kv.Key);
                }
                changed = true;
            }
        }
        foreach (var id in toRemove) _map.Remove(id);
        if (changed) OnStatusChanged?.Invoke();
    }

    /// <summary>UI 표시에 사용할 태그 문자열.</summary>
    public string[] GetStatusTags()
    {
        var tags = new List<string>();
        foreach (var kv in _map)
        {
            switch (kv.Key)
            {
                case StatusId.Slow: tags.Add($"Slow x{kv.Value.stacks}"); break;
                default: tags.Add(kv.Key.ToString()); break;
            }
        }
        return tags.ToArray();
    }
}
