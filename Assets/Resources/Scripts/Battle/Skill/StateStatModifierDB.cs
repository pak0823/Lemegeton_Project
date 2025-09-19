using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/States/State Stat Modifiers DB", fileName = "StateStatModifierDB")]
public class StateStatModifierDB : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public UnitStateId state;
        [Header("Multipliers (x1 = no change)")]
        public float atkMultiplier = 1f;   // PhysicalDamage
        public float magMultiplier = 1f;   // MagicDamage
        public float defMultiplier = 1f;   // Defense (있다면)
        public float spdMultiplier = 1f;   // Speed/Initiative (있다면)

        [Header("Additive")]
        public int hpFlatAdd = 0;          // 최대 HP 가산이 필요하면
        public int mpFlatAdd = 0;

        [Header("Hostility")]
        public float hostilityStatMultiplier = 1f; // x1 = 변화 없음
        public int hostilityStatFlatAdd = 0;
    }

    public List<Entry> entries = new();

    public Entry Get(UnitStateId id)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].state == id) return entries[i];
        return null;
    }
}
