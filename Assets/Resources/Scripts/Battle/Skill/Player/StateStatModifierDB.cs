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
        public float agiMultiplier = 1f;   // AGI

        [Header("Additive")]
        public int hpFlatAdd = 0;          // 최대 HP 가산이 필요하면
        public int mpFlatAdd = 0;

        [Header("Hostility")]
        public float hostilityStatMultiplier = 1f; // x1 = 변화 없음
        public int hostilityStatFlatAdd = 0;
    }

    [Serializable]
    public class BuffEntry
    {
        public UnitStateBuffId buff;

        [Header("Multipliers (x1 = no change)")]
        public float atkMultiplier = 1f;
        public float magMultiplier = 1f;
        public float defMultiplier = 1f;
        public float agiMultiplier = 1f;   // 연막 AGI 버프는 여기 1.7 설정

        [Header("Additive")]
        public int hpFlatAdd = 0;
        public int mpFlatAdd = 0;

        [Header("Hostility")]
        public float hostilityStatMultiplier = 1f;
        public int hostilityStatFlatAdd = 0;
    }

    public List<Entry> entries = new();
    public List<BuffEntry> buffEntries = new();

    public Entry Get(UnitStateId id)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].state == id) return entries[i];
        return null;
    }

    // 버프 조회
    public BuffEntry GetBuff(UnitStateBuffId id)
    {
        for (int i = 0; i < buffEntries.Count; i++)
            if (buffEntries[i] != null && buffEntries[i].buff == id) return buffEntries[i];
        return null;
    }
    public (float atk, float mag, float def, float agi, float hostilityMul, int hostilityAdd) ComputeMultipliers(UnitStateController usc)
    {
        float atk = 1f, mag = 1f, def = 1f, agi = 1f, hostMul = 1f;
        int hostAdd = 0;

        if (usc != null)
        {
            // 상태 누적
            foreach (var s in usc.GetAll())
            {
                var e = Get(s);
                if (e == null) continue;
                atk *= e.atkMultiplier;
                mag *= e.magMultiplier;
                def *= e.defMultiplier;
                agi *= e.agiMultiplier;
                hostMul *= e.hostilityStatMultiplier;
                hostAdd += e.hostilityStatFlatAdd;
            }

            // 버프 누적
            foreach (var b in usc.GetAllBuffs())
            {
                var be = GetBuff(b);
                if (be == null) continue;
                atk *= be.atkMultiplier;
                mag *= be.magMultiplier;
                def *= be.defMultiplier;
                agi *= be.agiMultiplier;              // ← 연막 AgiUp(1.7) 여기서 반영
                hostMul *= be.hostilityStatMultiplier;
                hostAdd += be.hostilityStatFlatAdd;
            }
        }

        return (atk, mag, def, agi, hostMul, hostAdd);
    }
}
