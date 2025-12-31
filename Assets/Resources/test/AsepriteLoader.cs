using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsepriteLoader : MonoBehaviour
{
    [Header("Input Files")]
    public TextAsset jsonFile;      // JSON 파일 (Array 포맷)
    public Texture2D spriteSheet;   // 스프라이트 시트 이미지 (PNG)

    [Header("Settings")]
    public SpriteRenderer targetRenderer; // 애니메이션을 보여줄 렌더러
    public float pixelsPerUnit = 100f;    // PPU 설정

    private List<Sprite> _sprites = new List<Sprite>(); // 생성된 스프라이트 리스트
    private Coroutine _animRoutine;

    void Start()
    {
        if (jsonFile != null && spriteSheet != null && targetRenderer != null)
        {
            ParseAndSlice();
            PlayAnimation();
        }
        else
        {
            Debug.LogError("JSON 파일, 텍스처, 혹은 타겟 렌더러가 할당되지 않았습니다.");
        }
    }

    void ParseAndSlice()
    {
        // 1. JSON 파싱
        AsepriteArrayRoot data = JsonUtility.FromJson<AsepriteArrayRoot>(jsonFile.text);

        if (data == null || data.frames == null) return;

        // 2. 텍스처 전체 높이 (Y좌표 변환용)
        int texHeight = spriteSheet.height;

        _sprites.Clear();

        // 3. 프레임 순회하며 스프라이트 생성
        for (int i = 0; i < data.frames.Length; i++)
        {
            var frameData = data.frames[i];
            var frame = frameData.frame;

            // [중요] 좌표계 변환 (Top-Left -> Bottom-Left)
            // 유니티 Rect의 Y는 바닥 기준이므로, 전체 높이에서 Y와 높이를 빼야 함
            float unityY = texHeight - frame.y - frame.h;

            // Rect 생성 (x, y, w, h)
            Rect rect = new Rect(frame.x, unityY, frame.w, frame.h);

            // 스프라이트 생성 (Pivot은 중앙인 0.5, 0.5로 설정)
            Sprite newSprite = Sprite.Create(spriteSheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
            newSprite.name = frameData.filename;

            _sprites.Add(newSprite);
        }

        Debug.Log($"스프라이트 {_sprites.Count}장 생성 완료.");
    }

    void PlayAnimation()
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(Co_PlayAnim());
    }

    IEnumerator Co_PlayAnim()
    {
        // 원본 데이터를 다시 파싱하거나, 생성할 때 duration을 리스트에 같이 저장하는 구조체 등을 쓰면 더 좋음.
        // 여기서는 간단히 다시 JSON을 참조하여 duration을 가져옵니다.
        AsepriteArrayRoot data = JsonUtility.FromJson<AsepriteArrayRoot>(jsonFile.text);

        int index = 0;

        while (true)
        {
            if (_sprites.Count == 0) yield break;

            // 1. 현재 프레임 스프라이트 교체
            targetRenderer.sprite = _sprites[index];

            // 2. 대기 시간 계산 (JSON은 ms 단위이므로 1000으로 나눠 초 단위로 변환)
            int durationMs = data.frames[index].duration;
            float waitSeconds = durationMs / 1000f;

            yield return new WaitForSeconds(waitSeconds);

            // 3. 다음 인덱스 (무한 루프)
            index = (index + 1) % _sprites.Count;
        }
    }
}