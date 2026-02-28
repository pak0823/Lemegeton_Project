using System;
using System.Collections.Generic;
using UnityEngine;


/*
 1 - 20: 스킬 중첩
 21 - 40: 피해 중첩
 50 - 55: 지속딜 중첩
 */
public enum StatusId 
{ 
    None = 0 ,  // 없음
    Shooting = 1, // 럭키식스 사격 중첩
    Action = 2, // 기간트 대응 중첩
    Fixing = 3, // 이동 저항 상태(현재는 스택 구현에 있는데 상태로 변경해야함)
    Overwork = 4, //라스트보르 과로 중첩
    Research = 5, //라스트보르 연구 중첩

    Defense = 21, // 방어 중첩
    Resistance = 22,  // 저항 중첩
    Weakness = 23, // 나약 중첩
    Exhaustion = 24, // 탈진 중첩
    Slow = 25 , // 민첩 감소 중첩
    Suppression = 26, //제압 중첩


    Bleeding = 50, // 출혈 중첩
    Poisoning = 51, // 중독 중첩
    Ignition = 52 // 발화 중첩
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
    public const float BleedPercentPerStack = 0.02f;    // 스택당 최대 체력의 1%
    public const float PoisonPercentPerStack = 0.03f;   // 스택당 최대 체력의 3%
    public const float IgnitionPercentPerStack = 0.03f; // 스택당 최대 체력의 3%

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

    // 상태별 저항력을 저장할 딕셔너리
    private Dictionary<StatusId, float> _resistances = new Dictionary<StatusId, float>();

    // 저항력 설정 함수 (패시브에서 호출)
    public void SetResistance(StatusId id, float value)
    {
        if (_resistances.ContainsKey(id))
            _resistances[id] = value;
        else
            _resistances.Add(id, value);
    }

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
            // 0이 들어오면 기존 턴 유지, 아니면 갱신
            if (durationTurns > 0) e.remainingTurns = durationTurns;
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
            e.remainingTurns = Mathf.Max(e.remainingTurns, durationTurns);
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
            case StatusId.Defense:
            case StatusId.Weakness:
            case StatusId.Exhaustion:
            case StatusId.Resistance:
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

    // 저항 값을 가져오는 헬퍼
    public float GetResistance(StatusId id)
    {
        // 딕셔너리에 설정된 값이 있으면 반환, 없으면 기본값 1.0f
        if (_resistances.TryGetValue(id, out float val))
            return val;

        return 1.0f;
    }

    /// <summary>이 유닛의 턴 시작 시 지속시간 감소/정리.</summary>
    public void OnTurnStart()
    {
        if (_owner == null || _owner.IsDead) return;

        // 출혈(Bleeding)
        // 공식: max{ S, MaxHP * 0.02 * S } * R
        int bleedStacks = GetStacks(StatusId.Bleeding);
        if (bleedStacks > 0)
        {
            float resistance = GetResistance(StatusId.Bleeding);
            float baseDmg = _owner.MaxHP * DebuffTuning.BleedPercentPerStack * bleedStacks;

            // S와 공식 중 큰 값 선택
            float rawDmg = Mathf.Max(bleedStacks, baseDmg);

            int finalDmg = Mathf.CeilToInt(rawDmg * resistance);
            if (finalDmg > 0) _owner.TakeDamage(finalDmg);
            // Debug.Log($"[Bleed] Stacks={bleedStacks}, Dmg={finalDmg}");
        }

        // 중독(Poisoning)
        // 공식: max{ S, (MaxHP - HP) * 0.03 * S } * R  (잃은 체력 비례)
        int poisonStacks = GetStacks(StatusId.Poisoning);
        if (poisonStacks > 0 && !_owner.IsDead)
        {
            float resistance = GetResistance(StatusId.Poisoning);
            float missingHP = _owner.MaxHP - _owner.HP;
            float baseDmg = missingHP * DebuffTuning.PoisonPercentPerStack * poisonStacks;

            // S와 공식 중 큰 값 선택
            float rawDmg = Mathf.Max(poisonStacks, baseDmg);

            int finalDmg = Mathf.CeilToInt(rawDmg * resistance);
            if (finalDmg > 0) _owner.TakeDamage(finalDmg);
            // Debug.Log($"[Poison] Stacks={poisonStacks}, MissingHP={missingHP}, Dmg={finalDmg}");
        }

        // 발화(Ignition)
        // 공식: max{ S, HP * 0.03 * S } * R (현재 체력 비례)
        int ignitionStacks = GetStacks(StatusId.Ignition);
        if (ignitionStacks > 0 && !_owner.IsDead)
        {
            float resistance = GetResistance(StatusId.Ignition);
            float currentHP = _owner.HP;
            float baseDmg = currentHP * DebuffTuning.IgnitionPercentPerStack * ignitionStacks;

            // S와 공식 중 큰 값 선택
            float rawDmg = Mathf.Max(ignitionStacks, baseDmg);

            int finalDmg = Mathf.CeilToInt(rawDmg * resistance);
            if (finalDmg > 0) _owner.TakeDamage(finalDmg);
            // Debug.Log($"[Ignition] Stacks={ignitionStacks}, CurHP={currentHP}, Dmg={finalDmg}");
        }

        bool changed = false;
        var toRemove = new List<StatusId>();
        foreach (var kv in _map)
        {
            var e = kv.Value;
            if (e.remainingTurns > 0)
            {
                e.remainingTurns--;
                // 턴 다 됨 -> 삭제 목록 추가
                if (e.remainingTurns <= 0) toRemove.Add(kv.Key);
                changed = true;
            }
        }
        foreach (var id in toRemove)
        {
            _map.Remove(id);
            // Debug.Log($"[Status] {_owner.name}'s {id} expired.");
        }
        if (changed || toRemove.Count > 0) OnStatusChanged?.Invoke();
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
