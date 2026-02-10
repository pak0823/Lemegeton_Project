using UnityEngine;




public class Tester : MonoBehaviour

{

    private void Update()

    {

        if (Input.GetKeyDown(KeyCode.Space))

            Test_AddRandomItem();

       
    }
    private void LateUpdate()
    {
        MovePlayer();
    }

private void MovePlayer()
{
  if(Input.GetKey(KeyCode.A))   
  {
    transform.position += Vector3.left * Time.deltaTime;
  }
  else if(Input.GetKey(KeyCode.D))
  {
    transform.position += Vector3.right * Time.deltaTime;
  }
  else if(Input.GetKey(KeyCode.W))
  {
    transform.position += Vector3.up * Time.deltaTime;
  }
  else if(Input.GetKey(KeyCode.S))
  {
    transform.position += Vector3.down * Time.deltaTime;
  }
}

    public void Test_AddRandomItem()

    {

        string testID;

        int testAmount = 1;

        int pick = Random.Range(0, 6);



        switch (pick)

        {

            case 0:

                testID = "101";

                break;

            case 1:

                testID = "102";

                break;

            case 2:

                testID = "103";

                break;

            case 3:

                testID = "104";

                break;

            case 4:

                testID = "105";

                break;

            case 5:

                testID = "106";

                break;

            default:

                testID = "101";

                break;

        }



        



        InventoryManager.Instance.AddItem(testID, testAmount);

        Debug.Log($"[Test] {testID} 아이템 1개 획득");



        // 아이템 획득 후 즉시 저장

        PlayerDataManager.Instance.SaveGame();

    }

}

