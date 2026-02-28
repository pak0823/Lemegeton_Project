using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class MaterialSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text amountText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lackColor = Color.red; // 부족할 때 색상

    // 로드된 에셋을 추적하기 위한 핸들 (나중에 메모리 해제용)
    private AsyncOperationHandle<Sprite> _loadHandle;

    public void Setup(ItemData material, int currentCount, int requiredCount)
    {
        // 기존에 로드 중이거나 로드된 아이콘이 있다면 해제 (메모리 관리)
        if (_loadHandle.IsValid())
        {
            Addressables.Release(_loadHandle);
        }

        // 비동기 아이콘 로드 시작
        string fullKey = material.GetAtlasKey(); // "Atlas[Sprite]" 형태의 키 가져오기
        _loadHandle = Addressables.LoadAssetAsync<Sprite>(fullKey);

        _loadHandle.Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                iconImage.sprite = handle.Result;
            }
            else
            {
                Debug.LogError($"아이콘 로드 실패: {fullKey}");
                iconImage.sprite = null; // 실패 시 빈칸 처리
            }
        };

        // 텍스트 및 색상 설정
        amountText.text = $"{currentCount} / {requiredCount}";
        bool isEnough = currentCount >= requiredCount;
        amountText.color = isEnough ? normalColor : lackColor;
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 메모리 해제
        if (_loadHandle.IsValid())
        {
            Addressables.Release(_loadHandle);
        }
    }
}