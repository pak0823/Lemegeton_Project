using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CampTabController : MonoBehaviour
{
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

    public void UpdateTabVisuals(List<CampTab> tabs, CampTab activeTab)
    {
        foreach (var tab in tabs)
        {
            bool isActive = (tab.contentPage == activeTab.contentPage);

            // 스프라이트 교체
            if (tab.tabImage != null)
                tab.tabImage.sprite = isActive ? tab.selectedSprite : tab.normalSprite;

            // 좌표 수정 코루틴 실행
            StartCoroutine(ApplyTabPosition(tab, isActive));
        }
    }

    private IEnumerator ApplyTabPosition(CampTab tab, bool isActive)
    {
        yield return _waitForEndOfFrame;
        if (tab.tabImage != null)
        {
            RectTransform imageRT = tab.tabImage.rectTransform;
            // 레이아웃 그룹 계산 후 강제 좌표 주입
            imageRT.anchoredPosition = new Vector2(imageRT.anchoredPosition.x, isActive ? -30f : -22f);
        }
    }
}
