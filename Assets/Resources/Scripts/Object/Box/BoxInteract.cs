using UnityEngine;
using UnityEngine.UI;

public class BoxInteract : MonoBehaviour
{
    public Animator animator;
    public GameObject fKeyPrompt;   // F키 안내 UI (Text, Image 등)
    private bool isPlayerNear = false;
    private bool isOpened = false;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (fKeyPrompt != null)
            fKeyPrompt.SetActive(false); // 시작시 꺼두기
    }

    void Update()
    {
        // 안내 UI 표시 조건: 플레이어 근처 + 아직 안 열렸을 때
        if (fKeyPrompt != null)
            fKeyPrompt.SetActive(isPlayerNear && !isOpened);

        if (isPlayerNear && !isOpened && Input.GetKeyDown(KeyCode.F))
        {
            animator.SetTrigger("Box");
            isOpened = true;
            //Debug.Log("Box Open");
            ObjectGaugeManager.Instance.IncrementChest();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            //Debug.Log("isPlayerNear=true");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            //Debug.Log("isPlayerNear=false");
        }
    }
}
