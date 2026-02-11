using System;
using System.Collections.Generic;
using UnityEngine;

public class ExplorationStatusManager : MonoBehaviour
{
    public static ExplorationStatusManager Instance;

    // 상태당 중첩 수 (Overweight는 bool처럼 쓰지만 int로 관리하여 확장성 확보)
    // Key: StatusID, Value: Stack Count
    private Dictionary<ExplorationStatusID, int> activeStatuses = new Dictionary<ExplorationStatusID, int>();

    // 상태가 추가/제거될 때 호출되는 이벤트 (ID, isAdded)
    public event Action<ExplorationStatusID, bool> OnStatusChanged;

    [Header("Debug")]
    [SerializeField] private List<ExplorationStatusID> debugActiveStatusList = new List<ExplorationStatusID>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 씬 전환 시 파괴되지 않도록 하려면 DontDestroyOnLoad 필요할 수 있으나,
            // ExplorationScene에 귀속된 매니저라면 씬과 함께 라이프사이클을 같이 하는 것이 맞음.
            // 현재 구조상 ExplorationScene에 위치한다고 가정함.
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 상태를 추가합니다. (중첩 가능)
    /// </summary>
    public void AddStatus(ExplorationStatusID id)
    {
        if (id == ExplorationStatusID.None) return;

        if (!activeStatuses.ContainsKey(id))
        {
            activeStatuses[id] = 1;
            debugActiveStatusList.Add(id);
            OnStatusChanged?.Invoke(id, true);
        }
        else
        {
            activeStatuses[id]++;
        }
        
        Debug.Log($"[ExplorationStatusManager] AddStatus: {id} (Count: {activeStatuses[id]})");
    }

    /// <summary>
    /// 상태를 제거합니다. (중첩 감소, 0이 되면 제거)
    /// </summary>
    public void RemoveStatus(ExplorationStatusID id)
    {
        if (activeStatuses.ContainsKey(id))
        {
            activeStatuses[id]--;
            if (activeStatuses[id] <= 0)
            {
                activeStatuses.Remove(id);
                debugActiveStatusList.Remove(id);
                OnStatusChanged?.Invoke(id, false);
                Debug.Log($"[ExplorationStatusManager] RemoveStatus: {id} Removed completely.");
            }
            else
            {
                Debug.Log($"[ExplorationStatusManager] RemoveStatus: {id} Decreased (Count: {activeStatuses[id]})");
            }
        }
    }

    /// <summary>
    /// 특정 상태를 보유하고 있는지 확인
    /// </summary>
    public bool HasStatus(ExplorationStatusID id) => activeStatuses.ContainsKey(id);

    /// <summary>
    /// 현재 적용된 상태들에 따른 활기 소모 배율을 계산합니다.
    /// </summary>
    public float GetVigorCostMultiplier()
    {
        float multiplier = 1.0f;

        // 딕셔너리를 순회하며 배율 계산
        foreach (var status in activeStatuses)
        {
            // 예시: 과중 상태일 경우 2배 (중첩되어도 배율은 상태 유무로만 판단할지, 스택 비례일지는 기획에 따름)
            // 현재는 상태 유무로만 판단 (단순 2배)
            if (status.Key == ExplorationStatusID.Overweight)
            {
                multiplier *= 2.0f; 
            }
            
            // 추후 다른 상태 추가 시 switch case 등으로 확장
            // case ExplorationStatusID.LightStep: multiplier *= 0.5f; break;
        }

        return multiplier;
    }

    /// <summary>
    /// 현재 적용된 상태들에 따른 이동 속도 배율을 계산합니다.
    /// </summary>
    public float GetMoveSpeedMultiplier()
    {
        float multiplier = 1.0f;

        foreach (var status in activeStatuses)
        {
            // 과중 상태일 경우 속도 50% 감소 (0.5배)
            if (status.Key == ExplorationStatusID.Overweight)
            {
                multiplier *= 0.5f;
            }
        }

        return multiplier;
    }
}
