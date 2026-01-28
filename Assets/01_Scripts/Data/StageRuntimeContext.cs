// StageRuntimeContext.cs (DontDestroyOnLoad)
using UnityEngine;

public enum BattleContext { TrapEncounter, AfterPuzzle }

public class StageRuntimeContext : MonoBehaviour
{
    public static StageRuntimeContext Instance { get; private set; }

    public int CurrentStageNumber { get; private set; } = -1;
    public BattleContext CurrentBattleContext { get; private set; } = BattleContext.TrapEncounter;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetStageNumber(int n) => CurrentStageNumber = n;
    public void SetBattleContext(BattleContext ctx) => CurrentBattleContext = ctx;
}
