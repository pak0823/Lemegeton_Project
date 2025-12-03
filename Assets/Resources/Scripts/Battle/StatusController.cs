// Assets/Scripts/Combat/StatusController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusId 
{ 
    None = 0 ,  // 없음
    Slow = 1 , // 민첩 감소
    ShootingStack = 2 , // 럭키식스 패시브 스택
    Bleed = 3 , // 출혈
    GuardStack = 4, // 방어
    MoveResist = 5, // 강제 이동 저항
    CounterStack = 6, // 대응
    WeakStack = 7 , // 나약
    Exhaust = 8, // 탈진
    Resist = 9  // 저항
}

public static class DebuffTuning
{
    // index = 스택 수 (0~6)
    public static readonly float[] Mult =
    {
        1.0f,  // 0스택: 100% 피해
        0.8f,  // 1스택
        0.6f,  // 2스택
        0.4f,  // 3스택
        0.3f,  // 4스택
        0.2f,  // 5스택
        0.1f   // 6스택: 10% 피해
    };
    public const int MaxStacks = 6;
    // 출혈 스택당 체력 비율(1% = 0.01f), 최대 6스택 동일 상한 사용
    public const float BleedPercentPerStack = 0.01f;

    public const float GuardPerStackMult = 0.9f; // 방어 중첩 1스택당 0.9배
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

        int maxStacks = GetMaxStacks(id);
        int clampedStacks = Mathf.Min(stacks, maxStacks);

        if (_map.TryGetValue(id, out var e))
        {
            e.stacks = clampedStacks;
            e.remainingTurns = durationTurns;
        }
        else
        {
            _map[id] = new StatusEntry(id, clampedStacks, durationTurns);
        }

        OnStatusChanged?.Invoke();
    }

    public void ApplyWithTurnContext(StatusId id, int stacks, int durationTurns)
    {
        int maxStacks = GetMaxStacks(id);

        if (_map.TryGetValue(id, out var e))
        {
            // 스택 증가 + 상태별 최대 중첩 캡
            e.stacks = Mathf.Min(e.stacks + stacks, maxStacks);

            // 지속시간은 '새로 부여된 상태' 기준으로 리셋
            e.remainingTurns = durationTurns;
        }
        else
        {
            // 새로 부여된 상태를 추가
            int clampedStacks = Mathf.Min(stacks, maxStacks);
            _map[id] = new StatusEntry(id, clampedStacks, durationTurns);
        }

        OnStatusChanged?.Invoke();
    }

    public void Clear(StatusId id)
    {
        if (_map.Remove(id)) OnStatusChanged?.Invoke();
    }

    public bool Has(StatusId id) => _map.ContainsKey(id);
    public int GetStacks(StatusId id) => _map.TryGetValue(id, out var e) ? e.stacks : 0;

    //상태별 최대 중첩 수를 반환한다.
    // GuardStack / WeakStack / Exhaust / Resist : 9중첩
    // 나머지 6중첩
    int GetMaxStacks(StatusId id)
    {
        switch (id)
        {
            case StatusId.GuardStack:
            case StatusId.WeakStack:
            case StatusId.Exhaust:
            case StatusId.Resist:
                return 9;

            default:
                return DebuffTuning.MaxStacks; // Slow, Bleed 등은 기존 상한 유지
        }
    }

    public float GetAgilityMultiplier() //민첩 배율 계산
    {
        int s = GetStacks(StatusId.Slow);
        s = Mathf.Clamp(s, 0, DebuffTuning.MaxStacks);
        return DebuffTuning.Mult[s];
    }

    // 편의용 민첩 배율 계산
    public float ApplyAgilityModifier(float baseAgility)
        => baseAgility * GetAgilityMultiplier();

    /// <summary>이 유닛의 턴 시작 시 지속시간 감소/정리.</summary>
    public void OnTurnStart()
    {
        // 출혈 틱: 스택 × 1% × MaxHP
        int bleedStacks = GetStacks(StatusId.Bleed);
        if (bleedStacks > 0 && _owner != null && !_owner.IsDead)
        {
            float p = DebuffTuning.BleedPercentPerStack * bleedStacks;
            int dmg = Mathf.Max(1, Mathf.CeilToInt(_owner.MaxHP * p)); // 최소 1
            _owner.TakeDamage(dmg); // 적대감 비발생 DoT로 둘 거면 별도 플래그가 있으면 활용, 없으면 그대로 사용
        }

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
