using UnityEngine;
using UnityEngine.UI;

public class RegionButtonManager : MonoBehaviour
{
    public Transform buttonParent; // ��ư�� ��ġ�� �θ� ������Ʈ
    public GameObject buttonPrefab; // RegionButton ������
    public RegionDisplay regionDisplay; // ǥ���� UI ������Ʈ

    void Start()
    {
        LoadRegionButtons();
        Debug.Log("����");
    }

    void LoadRegionButtons()
    {
        RegionData[] allRegions = Resources.LoadAll<RegionData>("Regions");

        if (allRegions == null)
            Debug.Log("allRegions is null!");

        foreach (RegionData region in allRegions)
        {
            RegionData localRegion = region; // ���� ���� ĸó

            GameObject btnObj = Instantiate(buttonPrefab, buttonParent);
            //btnObj.GetComponentInChildren<Text>().text = region.regionName;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => regionDisplay.ShowRegion(region));
        }
    }
}
