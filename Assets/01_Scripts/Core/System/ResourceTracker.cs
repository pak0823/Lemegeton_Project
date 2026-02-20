using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

/// <summary>
/// Addressables 핸들을 추적·일괄 해제하는 공통 유틸리티.
///
/// 사용 패턴:
///   var tracker = new ResourceTracker();
///   var sprite = await tracker.LoadAsync&lt;Sprite&gt;("key");
///   ...
///   tracker.ReleaseAll();   // 소유 객체에서 OnDestroy시 호출
/// </summary>
public class ResourceTracker
{
    // 추적 중인 핸들 목록
    private readonly List<AsyncOperationHandle> _handles = new();

    // ─── 스프라이트 전용 단순 로드 (UI에서 가장 많이 쓰는 패턴) ──────────────────

    /// <summary>
    /// Addressables에서 에셋을 비동기 로드하고 핸들을 내부에서 추적합니다.
    /// 로드 실패 시 null 반환.
    /// </summary>
    public async Cysharp.Threading.Tasks.UniTask<T> LoadAsync<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(key)) return null;

        var handle = Addressables.LoadAssetAsync<T>(key);
        _handles.Add(handle);

        await handle.ToUniTask();

        if (handle.Status == AsyncOperationStatus.Succeeded)
            return handle.Result;

        Debug.LogWarning($"[ResourceTracker] 로드 실패: {key}");
        return null;
    }

    /// <summary>
    /// Addressables에서 여러 에셋을 비동기 로드하고 핸들을 내부에서 추적합니다.
    /// </summary>
    public async Cysharp.Threading.Tasks.UniTask<IList<T>> LoadAssetsAsync<T>(string key, System.Action<T> callback = null) where T : class
    {
        if (string.IsNullOrEmpty(key)) return null;

        var handle = Addressables.LoadAssetsAsync<T>(key, callback);
        _handles.Add(handle);

        await handle.ToUniTask();

        if (handle.Status == AsyncOperationStatus.Succeeded)
            return handle.Result;

        Debug.LogWarning($"[ResourceTracker] 멀티 로드 실패: {key}");
        return null;
    }

    /// <summary>
    /// 추적 중인 모든 핸들을 해제합니다. OnDestroy 또는 패널 닫힐 때 호출.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var h in _handles)
        {
            if (h.IsValid())
                Addressables.Release(h);
        }
        _handles.Clear();
    }

    /// <summary>현재 추적 중인 핸들 수</summary>
    public int Count => _handles.Count;
}
