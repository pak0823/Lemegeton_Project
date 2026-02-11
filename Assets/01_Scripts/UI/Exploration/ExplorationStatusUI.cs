using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExplorationStatusUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExplorationStatusDataSO dataDB;
    [SerializeField] private Transform iconRoot; // 아이콘들이 배치될 부모 오브젝트 (Grid Layout Group 권장)
    [SerializeField] private GameObject iconPrefab; // 아이콘 프리팹 (Image 컴포넌트 포함)

    // 생성된 아이콘 관리 (Key: StatusID, Value: GameObject)
    private Dictionary<ExplorationStatusID, GameObject> _spawnedIcons = new Dictionary<ExplorationStatusID, GameObject>();

    void Start()
    {
        if (ExplorationStatusManager.Instance != null)
        {
            ExplorationStatusManager.Instance.OnStatusChanged += OnStatusChanged;
            
            // 초기 상태 동기화 (이미 적용된 상태가 있다면 표시)
            // 주의: Manager의 activeStatuses가 private이라 직접 순회 불가하지만,
            // 필요한 경우 Manager에 GetActiveStatuses()를 추가하거나, 
            // 여기서는 이벤트 기반으로만 처리함. (씬 로드 시 초기화 타이밍 이슈 주의)
        }
    }

    void OnDestroy()
    {
        if (ExplorationStatusManager.Instance != null)
        {
            ExplorationStatusManager.Instance.OnStatusChanged -= OnStatusChanged;
        }
    }

    private void OnStatusChanged(ExplorationStatusID id, bool isAdded)
    {
        if (isAdded)
        {
            // 이미 있으면 무시 (중첩 카운트 표시가 필요하면 여기서 텍스트 갱신 로직 추가)
            if (_spawnedIcons.ContainsKey(id)) return;

            SpawnIcon(id);
        }
        else
        {
            // 제거
            if (_spawnedIcons.ContainsKey(id))
            {
                Destroy(_spawnedIcons[id]);
                _spawnedIcons.Remove(id);
            }
        }
    }

    private void SpawnIcon(ExplorationStatusID id)
    {
        if (dataDB == null)
        {
            Debug.LogWarning("[ExplorationStatusUI] DataDB is missing!");
            return;
        }

        var data = dataDB.GetData(id);
        if (data == null)
        {
            // 데이터가 없으면 표시하지 않음 (투명 상태 등)
            return;
        }

        if (iconPrefab == null || iconRoot == null) return;

        GameObject newIcon = Instantiate(iconPrefab, iconRoot);
        newIcon.name = $"StatusIcon_{data.displayName}";

        // 이미지 설정
        Image img = newIcon.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = data.icon;
        }

        // (선택 사항) 툴팁 등 추가 컴포넌트 설정 가능
        
        _spawnedIcons.Add(id, newIcon);
    }
}
