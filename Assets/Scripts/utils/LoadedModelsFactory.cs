using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class LoadedModelsFactory
{
    private static Dictionary<string, GameObject> _modelByPath = new Dictionary<string, GameObject>();

    private static GameObject _host;
    
    private static readonly string[] SupportedExtensions = new []
    {
        ".glb",
        ".gltf"
    };

    public static bool IsValidPath(string path)
    {
        if (!File.Exists(path))
            return false;
        var ext = Path.GetExtension(path).ToLower();
        return SupportedExtensions.Contains(ext);
    }

    public static async Task<IList<GameObject>> Load(string path)
    {
        if (IsValidPath(path))
            return new[] {
                await LoadModel(path)
            };

        return await LoadModels(path);
    }
    
    public static async Task<IList<GameObject>> LoadModels(string folderPath)
    {
        var taskList = new List<Task<GameObject>>();
        foreach (var file in Directory.GetFiles(folderPath))
        {
            var task = LoadModel(file);
            taskList.Add(task);
        }

        var result = new List<GameObject>();
        foreach (var task in taskList)
        {
            var obj = await task;
            if (obj != null)
                result.Add(obj);
        }

        return result;
    }
    
    public static async Task<GameObject> LoadModel(string path)
    {
        if (_modelByPath.TryGetValue(path, out GameObject result))
            return result;

        GameObject go = null;
        var ext = Path.GetExtension(path).ToLower();
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
