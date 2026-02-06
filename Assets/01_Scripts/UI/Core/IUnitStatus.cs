using System;
using System.Collections.Generic;
using UnityEngine;

public interface IUnitStatus
{
    string Name { get; }
    float HP { get; }
    float MaxHP { get; }
    float MP { get; }
    float MaxMP { get; }
    float Rage { get; }
    float MaxRage { get; }

    bool IsDead { get; }

    // Events
    event Action OnStatusChanged;
    event Action<bool> OnDead; // bool: isDead

    // State & Buffs
    IReadOnlyCollection<UnitStateId> GetStates();
    IEnumerable<UnitStateController.BuffView> GetBuffs();
    IEnumerable<StatusController.StatusView> GetStacks();
}
