using System;
using UnityEngine;
using UnityEngine.UI;

public class SimpleQTEController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform barRect;      // 바 배경 (BarBG)
    [SerializeField] private RectTransform targetRect;   // 성공 영역 (TargetZone)
    [SerializeField] private RectTransform cursorRect;   // 움직이는 커서 (Cursor)

    [Header("Settings")]
    [SerializeField] private float cursorSpeed = 500f;   // 커서 이동 속도
    [SerializeField] private KeyCode triggerKey = KeyCode.Space; // 입력 키

    private Action<bool> _callback; // 결과(성공/실패)를 알려줄 콜백
    private bool _isRunning = false;
    private float _halfBarWidth;

    public void Init()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// QTE를 시작합니다.
    /// </summary>
    /// <param name="onResult">결과를 받을 델리게이트 (true: 성공, false: 실패)</param>
    public void StartQTE(Action<bool> onResult)
    {
        _callback = onResult;
        _isRunning = true;
        gameObject.SetActive(true);

        // 1. 바의 크기 계산 (Pivot 0.5 기준)
        _halfBarWidth = barRect.rect.width * 0.5f;

        // 2. 커서를 왼쪽 끝으로 초기화
        // 왼쪽 끝 위치 = -절반 너비
        cursorRect.anchoredPosition = new Vector2(-_halfBarWidth, 0f);
    }

    void Update()
    {
        if (!_isRunning) return;

        // 1. 커서 이동 (오른쪽으로)
        float moveStep = cursorSpeed * Time.deltaTime;
        cursorRect.anchoredPosition += new Vector2(moveStep, 0f);

        float currentX = cursorRect.anchoredPosition.x;

        // 2. 입력 감지
        if (Input.GetKeyDown(triggerKey) || Input.GetMouseButtonDown(0))
        {
            CheckHit(currentX);
            return;
        }

        // 3. 실패 조건: 커서가 바의 오른쪽 끝을 넘어감
        if (currentX > _halfBarWidth)
        {
            EndQTE(false); // 시간 초과 실패
        }
    }

    private void CheckHit(float cursorX)
    {
        // 타겟 영역의 범위 계산
        // 타겟이 바의 자식이므로 anchoredPosition.x가 로컬 좌표입니다.
        float targetX = targetRect.anchoredPosition.x;
        float targetHalfWidth = targetRect.rect.width * 0.5f;

        float minX = targetX - targetHalfWidth;
        float maxX = targetX + targetHalfWidth;

        // 커서가 범위 안에 있는지 확인
        if (cursorX >= minX && cursorX <= maxX)
        {
            Debug.Log("[QTE] 성공!");
            EndQTE(true);
        }
        else
        {
            Debug.Log("[QTE] 실패! (범위 벗어남)");
            EndQTE(false);
        }
    }

    private void EndQTE(bool isSuccess)
    {
        _isRunning = false;
        gameObject.SetActive(false); // UI 숨기기

        // 결과 전달
        _callback?.Invoke(isSuccess);
    }
}