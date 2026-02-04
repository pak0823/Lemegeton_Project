using UnityEngine;

public class Tester : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            Test_AddRandomItem();
    }

    public void Test_AddRandomItem()
    {
        string testID;
        int testAmount = 1;
        int pick = Random.Range(0, 2);

        switch (pick)
        {
            case 0:
                testID = "101";
                break;
            case 1:
                testID = "102";
                break;
            default:
                testID = "101";
                break;
        }

        

        InventoryManager.Instance.AddItem(testID, testAmount);
        Debug.Log($"[Test] {testID} æ∆¿Ã≈€ 1∞≥ »πµÊ");

        // æ∆¿Ã≈€ »πµÊ »ƒ ¡ÔΩ√ ¿˙¿Â
        PlayerDataManager.Instance.SaveGame();
    }
}
