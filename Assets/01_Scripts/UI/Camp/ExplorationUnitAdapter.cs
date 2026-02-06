using System;
using System.Collections.Generic;
using UnityEngine;

public class ExplorationUnitAdapter : IUnitStatus
{
    private readonly UnitData _data;

    public string Name => _data != null ? _data.DisplayName : "Unknown";
    
    // 탐험 모드에서는 현재 상태가 저장되지 않는다면 Max 값을 보여주거나
    // 별도의 런타임 데이터가 있다면 그걸 사용해야 함.
    // 현재는 Preview 느낌으로 Max 값을 표시.
    public float HP => MaxHP; 
    public float MaxHP => CalculateMaxHP();
    public float MP => MaxMP;
    public float MaxMP => CalculateMaxMP();
    public float Rage => 0; // 탐험 중에는 분노 0 가정
    public float MaxRage => CalculateMaxRage();
    public bool IsDead => false;

    public event Action OnStatusChanged;
#pragma warning disable 67
    public event Action<bool> OnDead;
#pragma warning restore 67

    public ExplorationUnitAdapter(UnitData data)
    {
        _data = data;
    }

    private float CalculateMaxHP()
    {
        if (_data == null) return 1;
        // BattleUnit 공식: BDY * 3 + STR + Buffs
        return Mathf.Max(1, (_data.baseBDY * 3f) + _data.baseSTR);
    }

    private float CalculateMaxMP()
    {
        if (_data == null) return 0;
        // BattleUnit 공식: MND * 3 + CLV
        return Mathf.Max(0, (_data.baseMND * 3f) + _data.baseCLV);
    }

    private float CalculateMaxRage()
    {
        if (_data == null) return 0;
        return Mathf.Max(0, _data.baseSTR + _data.baseCLV + _data.baseAGI + _data.baseBDY + _data.baseMND + _data.baseINS);
    }

    public IReadOnlyCollection<UnitStateId> GetStates()
    {
        return Array.Empty<UnitStateId>();
    }

    public IEnumerable<UnitStateController.BuffView> GetBuffs()
    {
        return Array.Empty<UnitStateController.BuffView>();
    }

    public IEnumerable<StatusController.StatusView> GetStacks()
    {
        return Array.Empty<StatusController.StatusView>();
    }

    public void NotifyChanged()
    {
        OnStatusChanged?.Invoke();
    }
}
