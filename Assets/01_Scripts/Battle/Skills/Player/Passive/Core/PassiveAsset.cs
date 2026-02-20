using UnityEngine;

public abstract class PassiveAsset : ScriptableObject
{
    [Header("Identity")]
    public string id;

    [Header("Display")]
    public string displayName;
    [TextArea] public string description;

    [Header("Unlock")]
    [Tooltip("true면 전투 시작 시 자동 활성.")]
    public bool unlockedByDefault = false;

    // 이미 해금되었는지 확인 — PlayerPrefs에서 PlayerDataManager 중앙 저장소로 이전
    public virtual bool IsUnlocked()
    {
        if (unlockedByDefault) return true;
        if (PlayerDataManager.Instance == null) return false;

        string key = string.IsNullOrEmpty(id) ? name : id;
        return PlayerDataManager.Instance.IsPassiveUnlocked(key);
    }

    // 해금 진행도 (0.0f ~ 1.0f)
    // 자식 클래스에서 오버라이드해서 "적 처치 수 / 100" 등을 리턴해야 함
    public virtual float GetProgress()
    {
        if (IsUnlocked()) return 1.0f;
        return 0.0f; // 기본값
    }

    // 해금 확정 (Awakened 버튼 눌렀을 때 실행) — PlayerPrefs → PlayerDataManager
    public virtual void Unlock()
    {
        if (PlayerDataManager.Instance == null)
        {
            Debug.LogWarning("[Passive] PlayerDataManager가 없어 해금 저장 실패.");
            return;
        }

        string key = string.IsNullOrEmpty(id) ? name : id;
        PlayerDataManager.Instance.UnlockPassive(key);
        Debug.Log($"[Passive] {displayName} 해금 완료!");
    }

    /// <summary>패시브를 소유한 유닛이 전투에 진입할 때 호출.</summary>
    public virtual void OnAttach(BattleUnit owner, BattleManager battle) { }

    /// <summary>패시브 비활성화/유닛 사망/퇴각 등으로 해제될 때 호출.</summary>
    public virtual void OnDetach(BattleUnit owner, BattleManager battle) { }
}
