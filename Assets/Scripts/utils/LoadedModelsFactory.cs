using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityGLTF;

public static class LoadedModelsFactory
{
    private static Dictionary<string, Mesh[]> _modelByPath = new Dictionary<string, Mesh[]>();

    private static GameObject _host;

    public static async Task<Mesh[]> LoadModel(string path)
    {
        if (_modelByPath.TryGetValue(path, out Mesh[] results))
            return results;

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
            results = go.GetComponentsInChildren<MeshFilter>()
                .Select(x => x.sharedMesh)
                .ToArray();
        
        _modelByPath[path] = results;
        return results;
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
        
        var go = new GameObject();
        go.transform.SetParent(_host.transform);
        var component = go.AddComponent<GLTFComponent>();
        component.GLTFUri = path;

        await component.Load();
            
        UnityEngine.Object.Destroy(component);

        Debug.Log("GLTF file imported successfully.");
        return go;
    }
}
