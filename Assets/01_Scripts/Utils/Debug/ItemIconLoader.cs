using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets; // 필수
using UnityEngine.ResourceManagement.AsyncOperations;

public class ItemIconLoader : MonoBehaviour
{
    public Image targetImage;

    public void LoadIcon(ItemData data)
    {
        // 최종 키 생성: "ItemAtlas[item_bottle]" 
        string fullKey = data.GetAtlasKey();

        // Addressables를 통한 비동기 로드
        Addressables.LoadAssetAsync<Sprite>(fullKey).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                targetImage.sprite = handle.Result;
            }
            else
            {
                Debug.LogError($"아이콘 로드 실패: {fullKey}");
            }
        };
    }
}