using System;
using System.Collections;
using UnityEngine;

public class ExplorationModalPresenter : MonoBehaviour
{
    public static ExplorationModalPresenter Instance { get; private set; }

    [Header("UI Root / Prefabs")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private Transform uiCenterAnchor; // [New] 게임 화면 중앙 앵커 (팝업용)
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

        // Anchor가 있으면 Anchor 밑에, 없으면 uiRoot 밑에 생성
        Transform parent = uiCenterAnchor != null ? uiCenterAnchor : uiRoot;
        _bannerInstance = Instantiate(encounterBannerPrefab, parent);
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

    public void ShowRewardPopup(System.Collections.Generic.List<RewardData> rewards, Action onClosed)
    {
        // 중복 방지
        if (_rewardInstance != null) return;

        if (rewardPopupPrefab == null)
        {
            onClosed?.Invoke();
            return;
        }

        // Anchor가 있으면 Anchor 밑에, 없으면 uiRoot 밑에 생성
        Transform parent = uiCenterAnchor != null ? uiCenterAnchor : uiRoot;
        _rewardInstance = Instantiate(rewardPopupPrefab, parent);
        _rewardInstance.Open(rewards, () =>
        {
            _rewardInstance = null;
            onClosed?.Invoke();
        });
    }
}
