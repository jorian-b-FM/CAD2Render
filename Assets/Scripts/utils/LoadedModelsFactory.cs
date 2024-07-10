using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class LoadedModelsFactory
{
    private static Dictionary<string, GameObject> _modelByPath = new Dictionary<string, GameObject>();

    private static GameObject _host;

    public static async Task<GameObject> LoadModel(string path)
    {
        if (_modelByPath.TryGetValue(path, out GameObject result))
            return result;

        GameObject go = null;
        var ext = Path.GetExtension(path);
        switch (ext)
        {
            case ".glb":
            case ".gltf":
                go = await LoadGltfModel(path);
                break;
        }

        if (go == null)
            Logger.LogError($"Failed to load '{path}'.");
        else
            result = go;
        
        _modelByPath[path] = result;
        return result;
    }

    private static async Task<GameObject> LoadGltfModel(string path)
    {
        if (_host == null)
        {
            _host = new GameObject
            {
                name = $"[GENERATED] {nameof(LoadedModelsFactory)}"
            };
            _host.SetActive(false);
        }
        
        var go = new GameObject{
            name = $"[GENERATED] {Path.GetFileNameWithoutExtension(path)}"
        };
        go.transform.SetParent(_host.transform);
        var component = go.AddComponent<GLTFast.GltfAsset>();
        component.Url = path;

        await component.Load(component.FullUrl);
        // go = component;
        
        UnityEngine.Object.Destroy(component);

        Debug.Log("GLTF file imported successfully.");
        return go;
    }
}
