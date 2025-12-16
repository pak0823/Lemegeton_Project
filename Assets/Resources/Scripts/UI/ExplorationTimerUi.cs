using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Project.UI;

public class ExplorationTimerUi : MonoBehaviour
{
    public Text timerText;
    public Text endMessageText;
    public Image timerImage;

    private List<int> fibonacciMinutes = new List<int> { 5, 3, 2, 1 };
    private int currentStage = 0;

    private float timeLeft = 0f;
    private bool isRunning = false;
    private bool isFinished = false;

    private int _pauseTokens = 0;
    public bool IsTemporarilyPaused => _pauseTokens > 0;

    [Header("Timer Warning")]
    [SerializeField] private float warningThresholdSeconds = 60f;     // 60초 이하 경고
    [SerializeField] private Color warningColor = new Color(0.86f, 0.1f, 0.1f); // 붉은색
    private Color _defaultTimerColor; // 원래 글자색을 기억

    // (임시) 인지 게이지 표기. 주석대로 공통 HUD로 이전 가능.
    public Text objectGaugeText;
    public Text perceiveGaugeText;

    // 외부에서 호출할 API 추가
    public void Pause() { _pauseTokens++; }
    public void Resume() { _pauseTokens = Mathf.Max(0, _pauseTokens - 1); }
    public void PauseForRealtime(float seconds)
    {
        StartCoroutine(Co_PauseForRealtime(seconds));
    }

    // 런타임 메모리 (씬 간 유지용)
    static class Memory
    {
        public static bool has;
        public static int stage;
        public static float timeLeft;
        public static bool finished;
    }
    void Awake()
    {
        Shared.ExplorationTimerUi = this;
    }

    // 외부에서 호출할 저장 API
    public void SaveRuntime()
    {
        Memory.has = true;
        Memory.stage = currentStage;
        Memory.timeLeft = Mathf.Max(0f, timeLeft);
        Memory.finished = isFinished;
    }
    public static void ClearSavedRuntime()
    {
        // 씬 간 유지되는 정적 메모리 완전 초기화
        Memory.has = false;
        Memory.stage = 0;
        Memory.timeLeft = 0f;
        Memory.finished = false;
    }

    private System.Collections.IEnumerator Co_PauseForRealtime(float seconds)
    {
        Pause();
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, seconds));
        Resume();
    }

    // 내부에서 호출할 복원 API
    void TryRestore()
    {
        if (!Memory.has) return;

        currentStage = Mathf.Clamp(Memory.stage, 0, fibonacciMinutes.Count - 1);
        timeLeft = Mathf.Max(0f, Memory.timeLeft);
        isFinished = Memory.finished;
        isRunning = !isFinished && timeLeft > 0f;

        // 1회 사용 후 클리어
        Memory.has = false;

        if (endMessageText) endMessageText.gameObject.SetActive(false);

        // UI 갱신
        if (isRunning) UpdateTimerUI();
    }


    private void OnEnable()
    {
        // 원래 타이머 색 기억
        if (timerText) _defaultTimerColor = timerText.color;

        // 저장본이 있으면 복원, 없으면 초기화
        if (Memory.has)
        {
            TryRestore();
        }
        else
        {
            currentStage = 0;
            isFinished = false;
            StartNextStage();
        }
    }

    private void OnDisable()
    {
        // 깔끔한 정리를 위해 비활성화 시 실행 중지
        isRunning = false;
    }

    public void OnUiShown()
    {
        // UI가 “처음 켜질 때”만 초기화/복원
        if (Memory.has)
            TryRestore();
        else
        {
            currentStage = 0;
            isFinished = false;
            StartNextStage();
        }
    }
    public void OnUiHidden()
    {
        // 꺼질 때 안전 정리
        isRunning = false;
        if (timerText) timerText.gameObject.SetActive(false);
        if (objectGaugeText) objectGaugeText.gameObject.SetActive(false);
        if (perceiveGaugeText) perceiveGaugeText.gameObject.SetActive(false);
        if (timerImage) timerImage.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 타이머/게이지 오브젝트 꺼져 있었다면 다시 켠다
        if (timerText && !timerText.gameObject.activeSelf) timerText.gameObject.SetActive(true);
        if (timerImage && !timerImage.gameObject.activeSelf) timerImage.gameObject.SetActive(true);

        if (!isRunning || isFinished)
        {
            if (timerText && timerText.gameObject.activeSelf)
            {
                timerText.gameObject.SetActive(false);
                if (timerImage) timerImage.gameObject.SetActive(false);
                if (objectGaugeText) objectGaugeText.gameObject.SetActive(false);  // 임시용
                if (perceiveGaugeText) perceiveGaugeText.gameObject.SetActive(false);  // 임시용
            }            
            return;
        }

        if (!IsTemporarilyPaused)
            timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            currentStage++;

            if (currentStage < fibonacciMinutes.Count)
            {
                StartNextStage();
            }
            else
            {
                StartCoroutine(ShowEndMessage());
                isRunning = false;
                isFinished = true;
            }
        }

        UpdateTimerUI();
    }

    private void StartNextStage()
    {
        timeLeft = fibonacciMinutes[currentStage] * 60f;
        isRunning = true;
        isFinished = false;
        if (endMessageText) endMessageText.gameObject.SetActive(false);
        UpdateTimerUI();
    }

    void UpdateTimerUI()
    {

        if (timeLeft > warningThresholdSeconds)
        {
            int minutes = Mathf.FloorToInt(timeLeft / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            timerText.text = $"{minutes:D2}:{seconds:D2}";

            // 기본색 복원
            if (timerText) timerText.color = _defaultTimerColor;
        }
        else
        {
            int seconds = Mathf.FloorToInt(timeLeft);
            int milliseconds = Mathf.FloorToInt((timeLeft - seconds) * 1000);
            timerText.text = $"{seconds:D2}:{milliseconds:D3}";

            // 경고색 적용
            if (timerText) timerText.color = warningColor;
        }
    }

    IEnumerator ShowEndMessage()
    {
        endMessageText.gameObject.SetActive(true);
        endMessageText.text = "나를 주시하던 무언가의 기척이 사라졌습니다.";

        Color color = endMessageText.color;
        color.a = 0f;
        endMessageText.color = color;

        float duration = 0.5f;
        float elapsed = 0f;

        //Fade In
        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            color.a = alpha;
            endMessageText.color = color;
            yield return null;
        }

        // 유지 시간
        yield return new WaitForSeconds(3f);

        //Fade Out
        elapsed = 0f;
        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration); 
            color.a = alpha;
            endMessageText.color = color;
            yield return null;
        }

        endMessageText.gameObject.SetActive(false);
    }
}
