using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class LoadedModelsFactory
{
    private static Dictionary<string, GameObject> _modelByPath = new Dictionary<string, GameObject>();
    private static Dictionary<string, Material[]> _materialsByPath = new Dictionary<string, Material[]>();

    private static GameObject _host;
    
    private static readonly string[] SupportedExtensions = new []
    {
        ".glb",
        ".gltf"
    };

    public static bool IsValidFilePath(string path)
    {
        if (!File.Exists(path))
            return false;
        var ext = Path.GetExtension(path).ToLower();
        return SupportedExtensions.Contains(ext);
    }
    
    /// Gets all materials from a path, regardless if the given path is a file or a directory
    public static async Task<Material[]> LoadMaterialsFromPath(string path)
    {
        // Check whether then given path is a file path, if so load that
        if (IsValidFilePath(path))
            return await LoadMaterialsFromFile(path);

        // If not, treat it as a folder, and load everything from there
        return await LoadMaterialsFromFolder(path);
    }
    
    /// Gets all materials of a model at a certain path
    public static async Task<Material[]> LoadMaterialsFromFile(string path)
    {
        // Load the model file
        _ = await LoadModelFromFile(path);
        // Now the dictionary should be filled in.
        return _materialsByPath.GetValueOrDefault(path, Array.Empty<Material>());
    }
    
    /// Gets all models in a directory
    public static async Task<Material[]> LoadMaterialsFromFolder(string folderPath)
    {
        // Start all file loading
        var taskList = new List<Task<GameObject>>();
        var files = Directory.GetFiles(folderPath);
        foreach (var file in files)
        {
            var task = LoadModelFromFile(file);
            taskList.Add(task);
        }

        // Wait for them all to finish
        foreach (var task in taskList)
            _ = await task;

        // Materials should now be filled in, create 1 list of them all
        return files
            .SelectMany(x => _materialsByPath.GetValueOrDefault(x, Array.Empty<Material>()))
            .ToArray();
    }

    /// Gets the models regardless whether the path is a file or directory
    public static async Task<IList<GameObject>> LoadModelsFromPath(string path)
    {
        // Check whether then given path is a file path, if so load that
        if (IsValidFilePath(path))
            return new[] {
                await LoadModelFromFile(path)
            };

        // If not, treat it as a folder, and load everything from there
        return await LoadModelsFromFolder(path);
    }
    
    /// Gets all models in a directory
    public static async Task<IList<GameObject>> LoadModelsFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return Array.Empty<GameObject>();
        
        // Start all file loading
        var taskList = new List<Task<GameObject>>();
        foreach (var file in Directory.GetFiles(folderPath))
        {
            var task = LoadModelFromFile(file);
            taskList.Add(task);
        }

        // Collect all loaded gameobjects
        var result = new List<GameObject>();
        foreach (var task in taskList)
        {
            var obj = await task;
            if (obj != null)
                result.Add(obj);
        }

        return result;
    }

    /// Loads a model from a file
    public static async Task<GameObject> LoadModelFromFile(string path)
    {
        if (_modelByPath.TryGetValue(path, out GameObject result))
            return result;

        GameObject go = null;
        var ext = Path.GetExtension(path).ToLower();
        switch (ext)
        {
            case ".glb":
            case ".gltf":
                var gltfAsset = await LoadGltfModel(path);
                go = gltfAsset.gameObject;
                var materials = new Material[gltfAsset.Importer.MaterialCount];
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = gltfAsset.Importer.GetMaterial(i);
                _materialsByPath[path] = materials;
                break;
            default:
                // TODO: we often load an entire directory, spamming this.
                // But I also want to know when a model is actually unsupported. 
                Logger.LogWarning($"Unsupported extension for loading a model: '{ext}' ['{path}']");
                _modelByPath[path] = null;
                _materialsByPath[path] = Array.Empty<Material>();
                return null;
        }

        if (go == null)
            Logger.LogError($"Failed to load '{path}'.");
        else
            result = go;
        
        _modelByPath[path] = result;
        return result;
    }

    private static async Task<GLTFast.GltfAsset> LoadGltfModel(string path)
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

        Debug.Log("GLTF file imported successfully.");
        return component;
    }
}
