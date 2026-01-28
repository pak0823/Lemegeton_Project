using UnityEngine;

public class WhoKilledMe : MonoBehaviour
{
    void OnDestroy()
    {
        // 이 오브젝트가 파괴될 때 로그를 남김
        Debug.LogError($"[범인 색출] 누군가가 {gameObject.name}을(를) 파괴(Destroy)했습니다!");

        // 누가 죽였는지 스택 추적 (콘솔에서 이 로그를 더블클릭 하세요)
        Debug.LogError(System.Environment.StackTrace);
    }
}