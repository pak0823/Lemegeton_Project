// Assets/Scripts/Combat/StatusController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusId { None = 0 ,Slow = 1 , ShootingStack = 2 }

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

    public StatusEntry(StatusId id, int stacks, int duration)
    {
        this.id = id;
        this.stacks = stacks;
        this.remainingTurns = duration;
    }
}

public class StatusController : MonoBehaviour
{
    readonly Dictionary<StatusId, StatusEntry> _map = new Dictionary<StatusId, StatusEntry>();
    public event Action OnStatusChanged;

    BattleUnit _owner;

    public struct StatusView
    {
        public StatusId id;
        public int stacks;
        public int remainingTurns;

        public StatusView(StatusId id, int stacks, int remaining)
        {
            this.id = id;
            this.stacks = stacks;
            this.remainingTurns = remaining;
        }
    }

    void OnEnable()
    {
        _owner = GetComponent<BattleUnit>();
        if (_owner != null) _owner.OnDied += OnOwnerDied;
    }

    void OnDisable()
    {
        if (_owner != null) _owner.OnDied -= OnOwnerDied;
    }
    void OnOwnerDied(BattleUnit dead)
    {
        ClearAllStatuses();           // 모든 버프/디버프 제거
    }

    public void ClearAllStatuses()
    {
        _map.Clear();                 // 내부 상태 사전 비우기
        OnStatusChanged?.Invoke();    // UI/ATB 등 갱신 트리거
    }

    public void SetStacks(StatusId id, int stacks, int durationTurns = 0)
    {
        if (stacks <= 0)
        {
            if (_map.Remove(id))
                OnStatusChanged?.Invoke();
            return;
        }

        if (_map.TryGetValue(id, out var e))
        {
            e.stacks = stacks;
            e.remainingTurns = durationTurns;
        }
        else
        {
            _map[id] = new StatusEntry(id, stacks, durationTurns);
        }

        OnStatusChanged?.Invoke();
    }

    public void ApplyWithTurnContext(StatusId id, int stacks, int durationTurns)
    {
        if (_map.TryGetValue(id, out var e))
        {
            // 스택 증가 + 최대 6중첩 캡
            e.stacks = Mathf.Min(e.stacks + stacks, DebuffTuning.SlowMaxStacks);

            // 지속시간은 '새로 부여된 둔화' 기준으로 리셋
            e.remainingTurns = durationTurns;
        }
        else
        {
            // 새로 부여된 상태를 추가
            _map[id] = new StatusEntry(id, Mathf.Min(stacks, DebuffTuning.SlowMaxStacks), durationTurns);
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
                e.remainingTurns--;
                if (e.remainingTurns <= 0) toRemove.Add(kv.Key);
                changed = true;
            }
        }
        foreach (var id in toRemove) _map.Remove(id);
        if (changed) OnStatusChanged?.Invoke();
    }

    /// <summary>UI 표시에 사용할 태그 문자열.</summary>

    public StatusView[] GetStatusViews()
    {
        var list = new List<StatusView>();
        foreach (var kv in _map)
        {
            var e = kv.Value;
            list.Add(new StatusView(kv.Key, e.stacks, e.remainingTurns));
        }
        return list.ToArray();
    }
}
