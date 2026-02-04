using System;
using System.Collections;
using UnityEngine;

public class ExplorationModalPresenter : MonoBehaviour
{
    public static ExplorationModalPresenter Instance { get; private set; }

    [Header("UI Root / Prefabs")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private EncounterBannerUI encounterBannerPrefab;
    [SerializeField] private RewardPopupUI rewardPopupPrefab;

    private EncounterBannerUI _bannerInstance;
    private RewardPopupUI _rewardInstance;
    private Coroutine _bannerCo;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (uiRoot == null) uiRoot = transform; // 기본값
    }

    public void ShowEncounterBanner(string message, float seconds, Action onFinished)
    {
        HideEncounterBanner();

        if (encounterBannerPrefab == null)
        {
            onFinished?.Invoke();
            return;
        }

        _bannerInstance = Instantiate(encounterBannerPrefab, uiRoot);
        _bannerInstance.SetMessage(message);

        if (_bannerCo != null) StopCoroutine(_bannerCo);
        _bannerCo = StartCoroutine(Co_Banner(seconds, onFinished));
    }

    private IEnumerator Co_Banner(float seconds, Action onFinished)
    {
        float end = Time.unscaledTime + Mathf.Max(0f, seconds);
        while (Time.unscaledTime < end) yield return null;

        HideEncounterBanner();
        _bannerCo = null;

        onFinished?.Invoke();
    }

    public void HideEncounterBanner()
    {
        if (_bannerCo != null)
        {
            StopCoroutine(_bannerCo);
            _bannerCo = null;
        }

        if (_bannerInstance != null)
        {
            Destroy(_bannerInstance.gameObject);
            _bannerInstance = null;
        }
    }

    public void ShowRewardPopup(Action onClosed)
    {
        // 중복 방지
        if (_rewardInstance != null) return;

        if (rewardPopupPrefab == null)
        {
            onClosed?.Invoke();
            return;
        }

        _rewardInstance = Instantiate(rewardPopupPrefab, uiRoot);
        _rewardInstance.Open(() =>
        {
            _rewardInstance = null;
            onClosed?.Invoke();
        });
    }
}
