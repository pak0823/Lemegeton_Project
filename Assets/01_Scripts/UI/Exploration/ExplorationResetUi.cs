using UnityEngine;

public class ExplorationResetUi : MonoBehaviour
{
    public void OnNormalMapReset()
    {
        if (MapManager.Instance != null)
        {
            MapManager.Instance.ResetExplorationMap();
            Debug.Log("[TestUI]:탐험맵 초기화 완료");
        }
    }
}
