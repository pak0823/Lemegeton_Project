using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ExplorationPersistenceManager : MonoBehaviour, IMapComponent
{
    private MapManager _manager;
    private List<GameObject> _activeAddressables = new List<GameObject>();

    public void Initialize(MapManager manager)
    {
        _manager = manager;
    }

    public void ClearActiveAddressables()
    {
        foreach (var obj in _activeAddressables)
        {
            if (obj != null) Addressables.ReleaseInstance(obj);
        }
        _activeAddressables.Clear();
    }

    public async Task RestoreSnapshot(ExplorationSnapshot snap, GameObject map, Transform container, Tilemap floorMap, List<Tilemap> wallMap)
    {
        if (snap == null) return;

        // 1. Gather existing Persistables
        var existing = new Dictionary<string, IExplorationPersistable>();
        foreach (var mb in map.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is IExplorationPersistable ip && !existing.ContainsKey(ip.PersistID))
                existing.Add(ip.PersistID, ip);
        }

        List<Task> loadingTasks = new List<Task>();

        foreach (var s in snap.objects)
        {
            // Case A: Existing Object (Update State)
            if (existing.TryGetValue(s.id, out var existIp))
            {
                if (s.kind == "Encounter" && s.b1)
                {
                    Debug.Log($"[Persistence] Disabling consumed existing Encounter: {s.id}");
                    // Explicitly disable the gameobject or load state as consumed
                    if (existIp is MonoBehaviour mb)
                    {
                         mb.gameObject.SetActive(false);
                    }
                    continue;
                }

                if (existIp is PushObject existPush)
                    existPush.SetTilemaps(new List<Tilemap> { floorMap }, wallMap);

                existIp.LoadState(s);
                continue;
            }

            // Case B: Spawn New Object (Async)
            if (s.kind == "Chest" || s.kind == "Trap" || s.kind == "Encounter")
            {
                if (s.kind == "Trap" && (s.b1 || !s.b2)) continue; // Trap destroyed/used
                if (s.kind == "Encounter" && s.b1) continue; // Encounter consumed

                var refObj = FindPrefabByName(map, s.prefabName);
                if (refObj == null) continue;

                loadingTasks.Add(RestoreSingleObjectAsync(refObj, s, container, floorMap, wallMap));
            }
        }

        await Task.WhenAll(loadingTasks);
        Debug.Log("[Persistence] Snapshot restoration complete.");
    }

    private async Task RestoreSingleObjectAsync(AssetReferenceGameObject refObj, ExplorationObjectState s, Transform container, Tilemap floorMap, List<Tilemap> wallMap)
    {
        var handle = refObj.InstantiateAsync(s.position, Quaternion.identity, container);
        await handle.Task;

        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            GameObject obj = handle.Result;
            _activeAddressables.Add(obj);

            var pid = obj.GetComponent<ExplorationPersistId>();
            if (!pid) pid = obj.AddComponent<ExplorationPersistId>();
            pid.OverrideIdForRestore(s.id);

            // Restore Name
            #if UNITY_EDITOR
            obj.name = refObj.editorAsset != null ? refObj.editorAsset.name : s.prefabName;
            #else
            obj.name = s.prefabName;
            #endif

            if (obj.TryGetComponent<PushObject>(out var push))
                push.SetTilemaps(new List<Tilemap> { floorMap }, wallMap);

            if (obj.TryGetComponent<MonoBehaviour>(out var mb) && mb is IExplorationPersistable ip)
                ip.LoadState(s);
        }
    }

    private AssetReferenceGameObject FindPrefabByName(GameObject map, string prefabName)
    {
        // Helper to check list
        AssetReferenceGameObject FindInList(List<AssetReferenceGameObject> list) {
            if (list == null) return null;
            foreach(var p in list) {
                if (CheckNameMatch(p, prefabName)) return p;
            }
            return null;
        }

        var objSpawner = map.GetComponentInChildren<MapObjectSpawner>(true);
        if (objSpawner != null)
        {
            var result = FindInList(objSpawner.trapRefs) ?? FindInList(objSpawner.chestRefs) ?? FindInList(objSpawner.patternRefs);
            if (result != null) return result;
        }

        var monsterSpawner = map.GetComponentInChildren<EncounterMonsterSpawner>(true);
        if (monsterSpawner != null)
        {
            var result = FindInList(monsterSpawner.monsterRefs);
            if (result != null) return result;
        }

        return null;
    }

    private bool CheckNameMatch(AssetReferenceGameObject refObj, string targetName)
    {
        if (refObj == null) return false;
        #if UNITY_EDITOR
        if (refObj.editorAsset != null && refObj.editorAsset.name == targetName) return true;
        #endif
        return refObj.RuntimeKey.ToString().Contains(targetName);
    }
}
