using UnityEngine;
using UnityEngine.UI;

public class RegionButtonManager : MonoBehaviour
{
    public Transform buttonParent; // 버튼을 배치할 부모 오브젝트
    public GameObject buttonPrefab; // RegionButton 프리팹
    public RegionDisplay regionDisplay; // 표시할 UI 컴포넌트

    void Start()
    {
        LoadRegionButtons();
        Debug.Log("시작");
    }

    void LoadRegionButtons()
    {
        RegionData[] allRegions = Resources.LoadAll<RegionData>("Regions");

        if (allRegions == null)
            Debug.Log("allRegions is null!");

        foreach (RegionData region in allRegions)
        {
            RegionData localRegion = region; // 람다 안전 캡처

            GameObject btnObj = Instantiate(buttonPrefab, buttonParent);
            //btnObj.GetComponentInChildren<Text>().text = region.regionName;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => regionDisplay.ShowRegion(region));
        }
    }
}
