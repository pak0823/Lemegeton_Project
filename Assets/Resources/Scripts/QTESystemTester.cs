using UnityEngine;
// New Input System 사용 시 필요하다면 추가, 
// GameControls 클래스가 있으니 굳이 네임스페이스 없어도 됨.

public class QTESystemTester : MonoBehaviour
{
    private GameControls _controls;

    private void Awake()
    {
        _controls = new GameControls();
    }

    private void OnEnable()
    {
        // Debug 액션맵 활성화 (T키 설정해둔 곳)
        _controls.Debug.Enable();
    }

    private void OnDisable()
    {
        _controls.Debug.Disable();
    }

    private void Update()
    {
        // T키를 누르면 가짜 이벤트 발생
        if (_controls.Debug.Test.WasPerformedThisFrame())
        {
            Debug.Log("[Tester] 테스트용 QTE 이벤트를 요청합니다.");
            
            // 싱글톤 매니저 호출
            ExplorationQTEManager.Instance.StartExplorationEvent(
                onSuccess: () => {
                    Debug.Log(">> [테스트 결과] 보상 획득! (성공 로직 동작함)");
                },
                onFail: () => {
                    Debug.Log(">> [테스트 결과] 함정 발동! (실패 로직 동작함)");
                }
            );
        }
    }
}