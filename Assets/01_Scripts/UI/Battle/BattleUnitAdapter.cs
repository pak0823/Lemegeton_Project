using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleUnitAdapter : IUnitStatus
{
    private readonly BattleUnit _unit;
    private readonly UnitStateController _stateController;
    private readonly StatusController _statusController;

    public string Name => _unit.name;
    public float HP => _unit.HP;
    public float MaxHP => _unit.MaxHP;
    public float MP => _unit.MP;
    public float MaxMP => _unit.MaxMP;
    public float Rage => _unit.Rage;
    public float MaxRage => _unit.MaxRage;
    public bool IsDead => _unit.HP <= 0; // Simplified check

    public event Action OnStatusChanged;
    public event Action<bool> OnDead;

    public BattleUnitAdapter(BattleUnit unit)
    {
        _unit = unit;
        _stateController = unit.GetComponent<UnitStateController>();
        _statusController = unit.GetComponent<StatusController>();

        // Subscribe to BattleUnit events
        if (_statusController != null)
            _statusController.OnStatusChanged += HandleStatusChanged;

        if (_stateController != null)
        {
            _stateController.OnStatesChanged += HandleStatusChanged;
            _stateController.OnBuffsChanged += HandleStatusChanged;
        }

        _unit.OnDied += HandleDied;
        
        // HP/MP/Rage changes might not have a direct event in BattleUnit other than OnDamaged, 
        // relying on UnitStatusItemUI's Update loop for bars usually, but for event-based UI we might need more triggers.
        // However, the original UnitStatusItemUI used Update() for bars, and events for Chips.
        // We will maintain that pattern or adapt.
    }

    public void Dispose()
    {
        if (_statusController != null)
            _statusController.OnStatusChanged -= HandleStatusChanged;

        if (_stateController != null)
        {
            _stateController.OnStatesChanged -= HandleStatusChanged;
            _stateController.OnBuffsChanged -= HandleStatusChanged;
        }

        _unit.OnDied -= HandleDied;
    }

    private void HandleStatusChanged()
    {
        OnStatusChanged?.Invoke();
    }

    private void HandleDied(BattleUnit unit)
    {
        OnDead?.Invoke(true);
    }

    public IReadOnlyCollection<UnitStateId> GetStates()
    {
        return _stateController != null ? _stateController.GetAll() : Array.Empty<UnitStateId>();
    }

    public IEnumerable<UnitStateController.BuffView> GetBuffs()
    {
        if (_stateController == null) return Array.Empty<UnitStateController.BuffView>();

        var allBuffs = _stateController.GetAllBuffs();
        var list = new List<UnitStateController.BuffView>();
        foreach (var b in allBuffs)
        {
            int remain = _stateController.GetRemainingBuffTurns(b);
            list.Add(new UnitStateController.BuffView(b, remain));
        }
        return list;
    }

    public IEnumerable<StatusController.StatusView> GetStacks()
    {
        return _statusController != null ? _statusController.GetStatusViews() : Array.Empty<StatusController.StatusView>();
    }
}
