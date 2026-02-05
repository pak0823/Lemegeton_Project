using UnityEngine;
using System;

// SRP: Handles stats, regeneration, and modification logic.
public class UnitStats : MonoBehaviour
{
    public UnitData Data { get; private set; }
    
    // Runtime Stats
    public float HP { get; private set; }
    public float MP { get; private set; }
    public float Rage { get; private set; }

    // Events
    public event Action OnStatsChanged;

    public void SetHP(float value) { HP = value; OnStatsChanged?.Invoke(); }
    public void SetMP(float value) { MP = value; OnStatsChanged?.Invoke(); }
    public void SetRage(float value) { Rage = value; OnStatsChanged?.Invoke(); }

    public void Initialize(UnitData data)
    {
        Data = data;
        // Initialization logic is currently handled by BattleUnit.ApplyData
        // We just serve as container for now.
    }

    public float CalculateMaxHP()
    {
        // Placeholder for complex calculation
        return Data.baseBDY * 3 + Data.baseSTR; 
    }

    public void ModifyHP(int amount)
    {
        HP = Mathf.Clamp(HP + amount, 0, CalculateMaxHP());
        OnStatsChanged?.Invoke();
    }
}
