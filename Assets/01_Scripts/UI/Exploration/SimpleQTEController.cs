using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class SimpleQTEController : BaseQTEController
{
    [Header("UI References")]
    [SerializeField] private RectTransform barRect; //QTE Bar
    [SerializeField] private RectTransform targetRect;  //성공 범위
    [SerializeField] private RectTransform perfectRect; //대성공 범위
    [SerializeField] private RectTransform cursorRect;  //이동하는 커서
    [SerializeField] private Text readyText; // 준비 텍스트
    [SerializeField] private Text feedbackText;  // 판정 결과 텍스트 (Perfect / Good / Fail)

    [Header("Settings")]
    [SerializeField] private float minDuration = 1.0f; // 최소 도달 시간
    [SerializeField] private float maxDuration = 2.0f; // 최대 도달 시간

    // 판정 후 UI가 유지되는 시간
    [Range(0.1f, 3.0f)]
    [SerializeField] private float resultKeepTime = 1.0f;

    private GameControls _controls; // 자동 생성된 클래스
    private Action<QTEResult> _callback;
    private bool _canInput = false; // 입력 가능 상태인지 체크
    private float _halfBarWidth;
    private float _currentSpeed; // 이번 판 속도

    private void Awake() => _controls = new GameControls();
    private void OnEnable() => _controls.QTE.Enable();
    private void OnDisable() => _controls.QTE.Disable();

    public override void Init()
    {
        gameObject.SetActive(false);
    }

    // 부모의 추상 메서드 구현 (override 필수)
    public override void StartQTE(Action<QTEResult> onResult)
    {
        _callback = onResult;
        gameObject.SetActive(true);
        StartCoroutine(Co_PlayQTESequence());
    }
    private IEnumerator Co_PlayQTESequence()
    {
        // 0. 초기화
        _canInput = false;
        _halfBarWidth = barRect.rect.width * 0.5f;

        // 바, 타겟, 커서는 일단 숨김 (Ready 텍스트만 보여주기 위해)
        SetUIElementsActive(false);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false); // 결과 텍스트 숨김

        // 1. 랜덤 세팅 (위치 & 속도)
        RandomizeSettings();

        // 2. Ready 텍스트 출력 (니 요청: 텍스트 출력 -> 사라짐)
        if (readyText != null)
        {
            readyText.gameObject.SetActive(true);
            readyText.text = "Ready!";
            yield return new WaitForSeconds(1.0f); // 1초간 텍스트 보여줌
            readyText.gameObject.SetActive(false);
        }

        // 3. QTE UI 등장
        SetUIElementsActive(true);

        // 4. 1초 대기 (나오고 1초 후에 시작)
        yield return new WaitForSeconds(1.0f);

        // 5. 시작 (커서 이동 및 입력 허용)
        _canInput = true;
    }
    private void SetUIElementsActive(bool isActive)
    {
        barRect.gameObject.SetActive(isActive);
        // targetRect와 cursorRect는 barRect의 자식이라면 자동으로 꺼지겠지만,
        // 혹시 모르니 명시적으로 처리하거나 barRect만 꺼도 됨.
        // 여기선 깔끔하게 barRect만 제어한다고 가정.
    }
    private void RandomizeSettings()
    {
        // 속도 랜덤 (거리 / 시간 = 속도)
        float totalDist = barRect.rect.width; // 전체 거리 (왼쪽 끝 -> 오른쪽 끝)
        float randomDuration = Random.Range(minDuration, maxDuration);
        _currentSpeed = totalDist / randomDuration;

        // 타겟 위치 랜덤
        // 타겟이 바 밖으로 삐져나가지 않게 범위 제한 필요
        float barWidth = barRect.rect.width;
        float targetWidth = targetRect.rect.width;

        // 배치 가능한 X 범위: (-바/2 + 타겟/2) ~ (바/2 - 타겟/2)
        float safePadding = 10f; // 여유분
        float minX = -(barWidth * 0.5f) + (targetWidth * 0.5f) + safePadding;
        float maxX = (barWidth * 0.5f) - (targetWidth * 0.5f) - safePadding;

        float randomX = Random.Range(minX, maxX);
        targetRect.anchoredPosition = new Vector2(randomX, 0f);

        // 커서 초기화 (왼쪽 끝)
        cursorRect.anchoredPosition = new Vector2(-(barWidth * 0.5f), 0f);
    }

    void Update()
    {
        // 입력 가능 상태가 아니면 업데이트 안 함
        if (!_canInput) return;

        // 1. 커서 이동 (랜덤 속도 적용)
        float moveStep = _currentSpeed * Time.deltaTime;
        cursorRect.anchoredPosition += new Vector2(moveStep, 0f);
        float currentX = cursorRect.anchoredPosition.x;

        // 2. 입력 감지
        if (_controls.QTE.Trigger.WasPerformedThisFrame())
        {
            CheckHit(currentX);
            return;
        }

        // 3. 실패 조건 (끝까지 가면)
        if (currentX > _halfBarWidth)
        {
            HandleResult(QTEResult.Fail);
        }
    }

    private void CheckHit(float cursorX)
    {
        float targetX = targetRect.anchoredPosition.x;
        float targetHalf = targetRect.rect.width * 0.5f;
        float tMin = targetX - targetHalf;
        float tMax = targetX + targetHalf;

        if (cursorX < tMin || cursorX > tMax)
        {
            // 실패 처리 (빗나감)
            HandleResult(QTEResult.Fail);
            return;
        }

        float perfectX = targetX + perfectRect.anchoredPosition.x;
        float perfectHalf = perfectRect.rect.width * 0.5f;
        float pMin = perfectX - perfectHalf;
        float pMax = perfectX + perfectHalf;

        if (cursorX >= pMin && cursorX <= pMax)
        {
            HandleResult(QTEResult.Perfect);
        }
        else
        {
            HandleResult(QTEResult.Success);
        }
    }

    // [변경] 결과를 처리하고 딜레이 코루틴을 시작하는 함수
    private void HandleResult(QTEResult result)
    {
        _canInput = false; // 입력 차단

        // 결과 텍스트 표시
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(true);
            switch (result)
            {
                case QTEResult.Perfect:
                    feedbackText.text = "Perfect!!";
                    feedbackText.color = Color.yellow; // 혹은 원하는 색상
                    break;
                case QTEResult.Success:
                    feedbackText.text = "Good!";
                    feedbackText.color = Color.green;
                    break;
                case QTEResult.Fail:
                    feedbackText.text = "Fail...";
                    feedbackText.color = Color.red;
                    break;
            }
        }

        // 지연 종료 시작
        StartCoroutine(Co_EndSequence(result));
    }

    // [추가] 딜레이 후 UI를 끄고 매니저에게 결과를 알림
    private IEnumerator Co_EndSequence(QTEResult result)
    {
        // 설정된 시간만큼 대기 (이 시간 동안 바와 텍스트가 유지됨)
        yield return new WaitForSeconds(resultKeepTime);

        // UI 끄기
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        gameObject.SetActive(false);

        // 매니저에게 결과 전달 (이 시점에 매니저의 while 루프가 풀림)
        _callback?.Invoke(result);
    }

    private void EndQTE(QTEResult result)
    {
        _canInput = false;
        gameObject.SetActive(false);
        _callback?.Invoke(result);
    }
}