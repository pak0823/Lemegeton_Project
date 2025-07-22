using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class BarrierController : MonoBehaviour
{
    [Header("Barrier Components")]
    [Tooltip("Barrier의 Collider2D (닫힐 때 활성화)")]
    Collider2D barrierCollider;
    [Tooltip("Barrier 앞에 위치한 트리거 Collider2D (Player 진입 감지)")]
    Collider2D exitTrigger;

    [Header("Interaction")]
    [Tooltip("UI로 'Press F to Exit' 표시용 이벤트")]
    public UnityEvent onShowPrompt;
    public UnityEvent onHidePrompt;

    private bool isOpen = false;
    private bool playerInRange = false;

    void Start()
    {
        // 장벽용 콜라이더와 진입 감지용 트리거를 구분해서 찾기
        var allCols = GetComponentsInChildren<Collider2D>();
        foreach (var col in allCols)
        {
            if (col.isTrigger) exitTrigger = col;
            else barrierCollider = col;
        }

        if (exitTrigger == null) Debug.LogError("ExitTrigger 콜라이더가 없습니다!");
        if (barrierCollider == null) Debug.LogError("BarrierCollider 콜라이더가 없습니다!");

        ObjectGaugeManager.Instance.onThresholdReached.AddListener(Open);
        Close();
    }

    void Update()
    {
        if (isOpen && playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            // 다음 맵 로드
            //LoadNextMap();
            Debug.Log("다음맵으로 이동함");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("닿고 있는 오브젝트:" + other);
        if (!isOpen) return;
        if (other.CompareTag("Player"))
        {
            Debug.Log("ExitTrigger에 PLAYER 진입 감지! → UI 띄워야 함");
            playerInRange = true;
            onShowPrompt?.Invoke();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isOpen) return;
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            onHidePrompt?.Invoke();
        }
    }

    // Barrier를 여는 처리
    public void Open()
    {
        isOpen = true;
        barrierCollider.enabled = false;
        Debug.Log("Barrier Opened");
        // 애니메이션 재생 (열림)
        //Animator animator = GetComponent<Animator>();
        //if (animator != null) animator.SetTrigger("Open");
    }

    // Barrier를 닫는 처리
    public void Close()
    {
        isOpen = false;
        barrierCollider.enabled = true;
        // 애니메이션 재생 (닫힘)
        //Animator animator = GetComponent<Animator>();
        //if (animator != null) animator.SetTrigger("Close");
    }

    // 다음 맵으로 이동
    private void LoadNextMap()
    {
        // TODO: 실제 씬 이름이나 MapManager 호출로 변경
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
