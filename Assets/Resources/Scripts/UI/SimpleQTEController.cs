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

    [Header("Settings")]
    [SerializeField] private float minDuration = 1.0f; // 최소 도달 시간
    [SerializeField] private float maxDuration = 2.0f; // 최대 도달 시간

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

        // 1. 랜덤 세팅 (위치 & 속도)
        RandomizeSettings();

        // 2. Ready 텍스트 출력 (니 요청: 텍스트 출력 -> 사라짐)
        if (readyText != null)
        {
            readyText.gameObject.SetActive(true);
            readyText.text = "준비!";
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
            EndQTE(QTEResult.Fail);
        }
    }

    private void CheckHit(float cursorX)
    {
        // 1. Target 범위 계산
        float targetX = targetRect.anchoredPosition.x;
        float targetHalf = targetRect.rect.width * 0.5f;
        float tMin = targetX - targetHalf;
        float tMax = targetX + targetHalf;

        // 타겟 안에 없으면 짤없이 실패
        if (cursorX < tMin || cursorX > tMax)
        {
            Debug.Log("[QTE] 빗나감 (Fail)");
            EndQTE(QTEResult.Fail);
            return;
        }

        // 2. Perfect 범위 계산
        // Perfect가 Target의 자식이라고 가정하고 월드 좌표계 혹은 상대 좌표 계산
        // 단순하게 구현하기 위해 로컬 좌표를 더함 (Target Pos + Perfect Local Pos)
        float perfectX = targetX + perfectRect.anchoredPosition.x;
        float perfectHalf = perfectRect.rect.width * 0.5f;
        float pMin = perfectX - perfectHalf;
        float pMax = perfectX + perfectHalf;

        if (cursorX >= pMin && cursorX <= pMax)
        {
            Debug.Log("[QTE] 대성공! (Perfect)");
            EndQTE(QTEResult.Perfect);
        }
        else
        {
            Debug.Log("[QTE] 일반 성공 (Success)");
            EndQTE(QTEResult.Success);
        }
    }

    private void EndQTE(QTEResult result)
    {
        _canInput = false;
        gameObject.SetActive(false);
        _callback?.Invoke(result);
    }
}