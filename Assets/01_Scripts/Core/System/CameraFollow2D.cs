// CameraFollow2D.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                 // 플레이어 Transform
    public Vector2 offset = new Vector2(0f, 1.5f);

    [Header("Follow")]
    [Tooltip("따라가는 부드러움(작을수록 빠르게 따라감)")]
    public float smoothTime = 0.15f;
    public float maxSpeed = 100f;
    Vector3 _vel;                            // SmoothDamp용

    [Header("Bounds (선택)")]
    [Tooltip("맵 경계(CompositeCollider2D/BoxCollider2D 등). 비워두면 무제한.")]
    public Collider2D worldBounds;

    [Header("픽셀 스냅 (선택)")]
    [Tooltip("픽셀 단위로 스냅(타일/픽셀 아트에 유용)")]
    public bool pixelSnap = false;
    public float pixelsPerUnit = 16f;

    Camera _cam;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 첫 프레임에 바로 스냅(부드러운 이동 없이)
        SnapToTarget();
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    void OnSceneLoaded(Scene s, LoadSceneMode m) => SnapToTarget();

    void LateUpdate()
    {
        if (!target) return;

        // 목표 위치 계산
        Vector3 desired = target.position + (Vector3)offset;
        desired.z = transform.position.z; // 2D 카메라 Z는 유지

        // 부드럽게 따라가기
        Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime, maxSpeed, Time.deltaTime);

        //// 경계 클램프(선택)
        //if (worldBounds && _cam.orthographic)
        //    next = ClampInsideBounds(next);

        //// 픽셀 스냅(선택)
        //if (pixelSnap && pixelsPerUnit > 0f)
        //    next = PixelSnap(next);

        transform.position = next;
    }

    Vector3 ClampInsideBounds(Vector3 pos)
    {
        var b = worldBounds.bounds;

        // 카메라 절반 크기(직교)
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;

        // 맵이 카메라보다 작으면 가운데 고정
        if (minX > maxX) { float cx = b.center.x; minX = maxX = cx; }
        if (minY > maxY) { float cy = b.center.y; minY = maxY = cy; }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        return pos;
    }

    Vector3 PixelSnap(Vector3 pos)
    {
        float unitsPerPixel = 1f / pixelsPerUnit;
        pos.x = Mathf.Round(pos.x / unitsPerPixel) * unitsPerPixel;
        pos.y = Mathf.Round(pos.y / unitsPerPixel) * unitsPerPixel;
        return pos;
    }

    // 첫 배치 시 부드러운 이동 없이 즉시 타깃에 붙이기
    public void SnapToTarget()
    {
        if (!target) return;
        Vector3 p = target.position + (Vector3)offset;
        p.z = transform.position.z;
        if (worldBounds && _cam.orthographic) p = ClampInsideBounds(p);
        if (pixelSnap && pixelsPerUnit > 0f) p = PixelSnap(p);
        transform.position = p;
        _vel = Vector3.zero;
    }

    // 런타임 중 타깃 교체할 때 호출하면 즉시 스냅
    public void SetTarget(Transform t, bool snap = true)
    {
        target = t;
        if (snap) SnapToTarget();
    }
}
