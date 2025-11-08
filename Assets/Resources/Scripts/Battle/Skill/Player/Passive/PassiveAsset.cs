using UnityEngine;

public abstract class PassiveAsset : ScriptableObject
{
    [Header("Display")]
    public string displayName;
    [TextArea] public string description;

    [Header("Unlock")]
    [Tooltip("true면 전투 시작 시 자동 활성. 추후 조건 해금 로직으로 교체 예정.")]
    public bool unlockedByDefault = true;

    /// <summary>패시브를 소유한 유닛이 전투에 진입할 때 호출.</summary>
    public virtual void OnAttach(BattleUnit owner, BattleManager battle) { }

    /// <summary>패시브 비활성화/유닛 사망/퇴각 등으로 해제될 때 호출.</summary>
    public virtual void OnDetach(BattleUnit owner, BattleManager battle) { }
}
