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

    // 이미 해금되었는지 확인 (SaveData 등과 연동 필요, 지금은 임시로 PlayerPrefs 사용)
    public virtual bool IsUnlocked()
    {
        if (unlockedByDefault) return true;
        // "Passive_패시브이름" 키가 1이면 해금된 것으로 간주
        // id가 비어있으면 name을 사용하도록 폴백 로직 추가 가능하나, 지금은 id 위주로 감
        string key = string.IsNullOrEmpty(id) ? name : id;
        return PlayerPrefs.GetInt($"Passive_{key}", 0) == 1;
    }

    // 해금 진행도 (0.0f ~ 1.0f)
    // 자식 클래스에서 오버라이드해서 "적 처치 수 / 100" 등을 리턴해야 함
    public virtual float GetProgress()
    {
        if (IsUnlocked()) return 1.0f;
        return 0.0f; // 기본값
    }
    // 해금 확정 (Awakened 버튼 눌렀을 때 실행)
    public virtual void Unlock()
    {
        string key = string.IsNullOrEmpty(id) ? name : id;
        PlayerPrefs.SetInt($"Passive_{key}", 1);
        PlayerPrefs.Save();
        Debug.Log($"[Passive] {displayName} 해금 완료!");
    }

    /// <summary>패시브를 소유한 유닛이 전투에 진입할 때 호출.</summary>
    public virtual void OnAttach(BattleUnit owner, BattleManager battle) { }

    /// <summary>패시브 비활성화/유닛 사망/퇴각 등으로 해제될 때 호출.</summary>
    public virtual void OnDetach(BattleUnit owner, BattleManager battle) { }
}
