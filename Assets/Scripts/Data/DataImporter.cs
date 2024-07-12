using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.io;
using C2R;
using SimpleJSON;
using UnityEngine;
using Object = UnityEngine.Object;
using static ConstDataValues;
using Task = System.Threading.Tasks.Task;

public static class ConstDataValues
{
    public const string defaultSettingsName = "__defaults__";
    public const string randomizerName = "Randomizers";
    public const string materialRandomizerName = "MaterialRandomizers";
    public const string poseName = "Pose";
    public const string tagName = "Tag";
    public const string datasetName = "Dataset";
    public const string typeName = "Type";
    public const string dataName = "Data";
    public const string collidersName = "Colliders";
    public const string meshName = "Mesh";
    public const string rigidBodyName = "RigidBody";
    public const string assetName = "Asset";
    public const string childrenName = "Children";
}

public class DataImporter : MonoBehaviour
{
    public bool loadFromFolder;
    public string folderPathToLoad = "ExampleData/Default";

    private string _fullFolderPath;

    public ScriptableObject[] defaultDataObjects;
    private Dictionary<Type, ScriptableObject> _defaultObjectByType;

    private List<Object> _createdResources;

    private GameObject _fakeResources;

    async void Awake()
    {
        if (!loadFromFolder) return;

        _createdResources = new List<Object>();

        _fakeResources = new GameObject("[GENERATED] Fake Resources");
        _fakeResources.SetActive(false);

        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(false);

        _defaultObjectByType = new Dictionary<Type, ScriptableObject>();
        foreach (var defaultDataObject in defaultDataObjects)
        {
            if (defaultDataObject == null)
                continue;

            var type = defaultDataObject.GetType();
            if (!_defaultObjectByType.TryAdd(type, defaultDataObject))
                Logger.LogWarning(
                    $"Multiple of type {type.Name} in the Defaults of {nameof(DataImporter)}. Only the first one will be used.");
        }
        
        // Use root folder (both in exe and in editor)
        if (!Path.IsPathRooted(folderPathToLoad))
            _fullFolderPath = Path.Combine(Application.dataPath, "..", folderPathToLoad);
        _fullFolderPath = Path.GetFullPath(_fullFolderPath);

        await LoadFromDirectory(_fullFolderPath);

        gameObject.SetActive(wasActive || gameObject.activeSelf);
    }

    private void OnDestroy()
    {
        foreach (var resource in _createdResources)
        {
            if (resource)
                Object.Destroy(resource);
        }
    }

    private async Task LoadFromDirectory(string folder)
    {
        if (!Directory.Exists(folder)) return;

        var randomizer = GetComponent<MainRandomizer>();

        var filePath = Path.Combine(folder, "main.json");
        TryLoadDataSet(filePath, randomizer);

        filePath = Path.Combine(folder, "randomizers.json");
        await TryLoadRandomizerSet(filePath, randomizer);
    }

    private static bool TryLoadDataSet(string filePath, MainRandomizer randomizer)
    {
        if (!File.Exists(filePath))
        {
            Logger.LogWarning($"Could not override dataset from '{filePath}': It does not exist");
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            // instantiate so we do not edit the asset
            var newDataset = CustomInstantiate(randomizer.Dataset);
            JsonUtility.FromJsonOverwrite(json, newDataset);
            randomizer.Dataset = newDataset;
            return true;
        }
        catch (Exception e)
        {
            Logger.LogError($"Could not override dataset from '{filePath}': {e}");
            return false;
        }
    }

    private async Task<bool> TryLoadRandomizerSet(string filePath, MainRandomizer randomizer)
    {
        if (!File.Exists(filePath))
        {
            Logger.LogWarning($"Could not override Randomizer set from '{filePath}': It does not exist");
            return false;
        }

        // Delete the existing randomizer set
        foreach (Transform child in randomizer.transform)
            Destroy(child.gameObject);

        var json = await File.ReadAllTextAsync(filePath);

        JSONNode node = JSON.Parse(json);

        // TODO: disable self to make any created script activate at once?
        return await TryCreateChildren(node, randomizer.transform);
    }

    private async Task<bool> TryCreateChildren(JSONNode node, Transform target)
    {
        if (node is not JSONObject)
        {
            Logger.LogError($"Expected Dictionary for '{randomizerName}' in '{target.name}'.");
            return false;
        }

        // direct children (other than __defaults__) are objects
        foreach (var child in node.Keys)
        {
            if (child == defaultSettingsName) // other than __defaults__
                continue;

            if (!await TryCreateChild(child, node[child], target))
                return false;
        }

        return true;
    }

    private async Task<GameObject> TryCreateChild(string childName, JSONNode node, Transform target)
    {
        GameObject childGO = null;
        Transform child = target.Find(childName);
        if (child != null)
            childGO = child.gameObject;
        else
        {
            var path = childName.Split("/");
            Transform parent = target;
            foreach (var part in path)
            {
                childGO = new GameObject(part);
                child = childGO.transform;
                child.SetParent(parent);
                parent = child;
            }
        }
        
        // Disable it until everything has been initialized (so awake does not get called instantly)
        childGO.SetActive(false);
        
        JSONNode valueNode;

        if (node.TryGetValue(tagName, out valueNode))
            childGO.tag = valueNode;
        else if (childName.StartsWith("keypoint", StringComparison.InvariantCultureIgnoreCase))
            childGO.tag = "Keypoint";

        if (node.TryGetValue(poseName, out valueNode))
            ReadPose(valueNode, child);
        
        if (node.TryGetValue(rigidBodyName, out valueNode))
            CreateRigidBody(valueNode, childGO);

        if (node.TryGetValue(meshName, out valueNode))
            await CreateMesh(valueNode, childGO);

        if (node.TryGetValue(childrenName, out valueNode))
            await TryCreateChildren(valueNode, child);

        if (node.TryGetValue(collidersName, out valueNode))
            SetupColliders(valueNode, childGO);
        
        if (node.TryGetValue(materialRandomizerName, out valueNode))
            await CreateMaterialRandomizers(valueNode, childGO);
        
        if (node.TryGetValue(randomizerName, out valueNode))
            await CreateRandomizers(valueNode, childGO);

        childGO.SetActive(true);
        return childGO;
    }

    private static void ReadPose(JSONNode node, Transform target)
    {
        JSONNode valueNode;
        
        if (node.TryGetValue(nameof(target.localPosition), out valueNode))
            target.localPosition = valueNode;
        else if (node.TryGetValue(nameof(target.position), out valueNode))
            target.position = valueNode;
        
        if (node.TryGetValue(nameof(target.localRotation), out valueNode))
            target.localRotation = Quaternion.Euler(valueNode);
        else if (node.TryGetValue(nameof(target.rotation), out valueNode))
            target.rotation = Quaternion.Euler(valueNode);
        
        if (node.TryGetValue(nameof(target.localScale), out valueNode))
            target.localScale = valueNode;
    }

    private static void CreateRigidBody(JSONNode node, GameObject go)
    {
        JSONNode valueNode;
        var rb = go.AddComponent<Rigidbody>();

        if (node.TryGetValue(nameof(rb.isKinematic), out valueNode))
            rb.isKinematic = valueNode;
        if (node.TryGetValue(nameof(rb.mass), out valueNode))
            rb.mass = valueNode;
        if (node.TryGetValue(nameof(rb.linearDamping), out valueNode))
            rb.linearDamping = valueNode;
        if (node.TryGetValue(nameof(rb.angularDamping), out valueNode))
            rb.angularDamping = valueNode;
        if (node.TryGetValue(nameof(rb.useGravity), out valueNode))
            rb.useGravity = valueNode;
        if (node.TryGetValue(nameof(rb.freezeRotation), out valueNode))
            rb.freezeRotation = valueNode;
        
        if (node.TryGetValue(nameof(rb.centerOfMass), out valueNode))
            rb.centerOfMass = valueNode;
        if (node.TryGetValue(nameof(rb.automaticCenterOfMass), out valueNode))
            rb.automaticCenterOfMass = valueNode;
    }
    
    private async Task CreateMesh(JSONNode node, GameObject go)
    {
        JSONNode valueNode;
        if (node.TryGetValue(assetName, out valueNode))
        {
            string meshPath = valueNode;
            if (!Path.IsPathRooted(meshPath))
                meshPath = Path.Combine(_fullFolderPath, meshPath);

            var model = await LoadedModelsFactory.LoadModel(meshPath);

            if (node.TryGetValue(nameof(MeshFilter.sharedMesh), out valueNode))
            {
                string submeshName = valueNode;
                var subMesh = model.transform.Find(submeshName);

                if (subMesh == null)
                    Logger.LogError($"Could not find mesh '{submeshName}' in '{meshPath}'");
                else
                    CustomInstantiate(subMesh, go.transform);
            }
            else // if no submesh was specified, just instantiate the whole thing
            {
                foreach (Transform child in model.transform)
                    CustomInstantiate(child, go.transform);
            }
        }
    }

    private static void SetupColliders(JSONNode collidersNode, GameObject target)
    {
        if (collidersNode is not JSONArray)
        {
            Logger.LogError($"Expected Array for '{collidersName}' in '{target.name}'");
            return;
        }

        // Loop over all children and parse them into creating MonoBehaviours
        foreach (var node in collidersNode.Children)
            SetupCollider(node, target);
    }

    private static void SetupCollider(JSONNode node, GameObject go)
    {
        if (!node.TryGetValue(typeName, out JSONNode typeNode))
        {
            Logger.LogError($"Collider for {go.name} has no type information. None will be created");
            return;
        }

        JSONNode valueNode;
        
        switch (typeNode.Value)
        {
            case nameof(MeshCollider):
                var meshCollider = go.AddComponent<MeshCollider>();

                var filter = go.GetComponentInChildren<MeshFilter>();
                meshCollider.sharedMesh = filter.sharedMesh;
                meshCollider.convex = true;
                break;
            
            case nameof(BoxCollider):
                var boxCollider = go.AddComponent<BoxCollider>();
                // Encompass the entire object by default
                var bounds = Utility.GetCombinedBounds(go);
                boxCollider.center = bounds.center;
                boxCollider.size = bounds.size;

                if (node.TryGetValue(nameof(boxCollider.center), out valueNode))
                    boxCollider.center = valueNode;
                if (node.TryGetValue(nameof(boxCollider.size), out valueNode))
                    boxCollider.size = valueNode;

                break;
            
            case nameof(SphereCollider):
                var sphereCollider = go.AddComponent<SphereCollider>();
                
                if (node.TryGetValue(nameof(sphereCollider.center), out valueNode))
                    sphereCollider.center = valueNode;
                if (node.TryGetValue(nameof(sphereCollider.radius), out valueNode))
                    sphereCollider.radius = valueNode;
                break;
            
            case nameof(CapsuleCollider):
                var capsuleCollider = go.AddComponent<CapsuleCollider>();
                
                if (node.TryGetValue(nameof(capsuleCollider.center), out valueNode))
                    capsuleCollider.center = valueNode;
                if (node.TryGetValue(nameof(capsuleCollider.radius), out valueNode))
                    capsuleCollider.radius = valueNode;
                if (node.TryGetValue(nameof(capsuleCollider.direction), out valueNode))
                    capsuleCollider.direction = valueNode;
                if (node.TryGetValue(nameof(capsuleCollider.height), out valueNode))
                    capsuleCollider.height = valueNode;
                break;
            
            default:
                Logger.LogError($"Unknown collider type: {typeNode} for object {go.name}");
                break;
        }

        // Set defaults for all colliders (if 1 was created)
        if (go.TryGetComponent(out Collider collider))
        {
            collider.isTrigger = node.GetValueOrDefault(nameof(Collider.isTrigger), false);
        }
    }

    private async Task CreateRandomizers(JSONNode randomizerNode, GameObject target)
    {
        if (randomizerNode is not JSONArray)
        {
            Logger.LogError($"Expected Array for '{randomizerName}' in '{target.name}'");
            return;
        }

        // Loop over all children and parse them into creating MonoBehaviours
        foreach (var node in randomizerNode.Children)
            await TryCreateBehaviour<RandomizerInterface>(node, target);
    }

    private async Task CreateMaterialRandomizers(JSONNode randomizersNode, GameObject target)
    {
        if (randomizersNode is not JSONArray)
        {
            Logger.LogError($"Expected Array for '{materialRandomizerName}' in '{target.name}'");
            return;
        }

        foreach (var linkNode in randomizersNode.Children)
            await TryCreateBehaviour<MaterialRandomizerInterface>(linkNode, target);
    }

    private async Task<T> TryCreateBehaviour<T>(JSONNode node, GameObject target)
        where T : MonoBehaviour
    {
        var behaviourType = node[typeName];
        var type = Type.GetType(behaviourType);
        if (type == null || !typeof(T).IsAssignableFrom(type))
        {
            Logger.LogError($"{behaviourType} is not a valid type. (Resolved to '{type?.FullName}')");
            return null;
        }

        // Add component & override the MonoBehaviour data if needed
        var behaviour = target.AddComponent(type) as T;
        if (node.TryGetValue(dataName, out JSONNode dataNode))
            JsonUtility.FromJsonOverwrite(dataNode.ToString(), behaviour);

        // We cannot override values inside of a nested Object through the above, so do that here
        await TryOverrideDataset(behaviour, node);
        return behaviour;
    }

    private async Task TryOverrideDataset(Object o, JSONNode node)
    {
        if (o is not IDatasetUser datasetUser)
            return;

        var datasetType = datasetUser.GetDataSetType();
        if (_defaultObjectByType.TryGetValue(datasetType, out ScriptableObject so))
        {
            // Instantiate it so we have a copy since we might override some values
            so = CustomInstantiate(so);
        }
        else
        {
            Logger.LogError($"No default defined for type {datasetType.Name}. A default object will be created but this might result in further errors.");
            so = ScriptableObject.CreateInstance(datasetType);
        }
        
        if (node.TryGetValue(datasetName, out JSONNode dataNode))
            JsonUtility.FromJsonOverwrite(dataNode.ToString(), so);
        datasetUser.SetDataset(so);
        
        // Custom behaviour per RandomizeData type. This is (usually) for loading in dynamic resources 
        switch (so)
        {
            case MaterialModelRandomizeData materialRandomizeData:
                if (!await TryLoadMaterials(materialRandomizeData.materialsPath))
                    Logger.LogWarning($"MaterialModelRandomizeData for {o.name} does not have any targets.");
                break;
            case ObjectRandomizeData objectRandomizeData:
                if (!await TryLoadPrefabs(objectRandomizeData.modelsPath))
                    Logger.LogWarning($"ObjectRandomizeHandler for {o.name} does not have any targets.");
                break;
            case LightRandomizeData lightRandomizeData:
                if (!TryLoadCubemaps(lightRandomizeData.environmentsPath))
                    Logger.LogWarning($"LightRandomizeData for {o.name} does not have any targets.");
                break;
        }
    }

    private async Task<bool> TryLoadMaterials(string path)
    {
        var materials = ResourceManager.LoadAll<Material>(path);
        // If it isn't a valid resource path (no resources found). Check if there are any jsons
        if (materials.Any())
            return true;
        
        string fullPath = Path.Combine(_fullFolderPath, path);
        
        IList<GameObject> models = await LoadedModelsFactory.Load(fullPath);
        if (!models.Any())
            return false;
        var list = new List<Material>();
        foreach (var model in models)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
                list.AddRange(renderer.sharedMaterials);
        }
        
        ResourceManager.RegisterSet(path, list.ToArray());
        return true;
    }
    
    private async Task<bool> TryLoadPrefabs(string path)
    {
        var objects = ResourceManager.LoadAll<GameObject>(path);
        // If it isn't a valid resource path (no resources found). Check if there are any jsons
        if (objects.Any())
            return true;
        
        string fullPath = Path.Combine(_fullFolderPath, path);

        if (!Directory.Exists(fullPath))
            return false;
        
        var objectJsons = Directory.GetFiles(fullPath, "*.json");

        objects = new GameObject[objectJsons.Length];

        for (var i = 0; i < objectJsons.Length; i++)
        {
            var jsonPath = objectJsons[i];
            var childName = Path.GetFileNameWithoutExtension(jsonPath);
            var json = await File.ReadAllTextAsync(jsonPath);
            var objectNode = JSON.Parse(json);

            objects[i] = await TryCreateChild(childName, objectNode, _fakeResources.transform);
        }

        // Note: use modelsPath and not fullPath here as we want to override the result of the dataset
        ResourceManager.RegisterSet(path, objects);
        return true;
    }

    private bool TryLoadCubemaps(string path)
    {
        Texture[] cubemaps = ResourceManager.LoadAll<Cubemap>(path);
        // If it isn't a valid resource path (no resources found). Check if there are any jsons
        if (cubemaps.Any())
            return true;
        
        string fullPath = Path.Combine(_fullFolderPath, path);

        var di = new DirectoryInfo(fullPath);

        if (!di.Exists)
            return false;

        var cubemapTextures = GetFiles(di, "*.exr", "*.png");

        cubemaps = new Texture[cubemapTextures.Count];

        for (var i = 0; i < cubemapTextures.Count; i++)
        {
            var cube = CubemapLoader.Load(cubemapTextures[i].FullName);
            _createdResources.Add(cube);
            cubemaps[i] = cube;
        }

        // Note: use environmentPath and not fullPath here as we want to override the result of the dataset
        ResourceManager.RegisterSet(path, cubemaps, typeof(Cubemap));
        return true;
    }

    private static T CustomInstantiate<T>(T objectToClone) where T : Object
    {
        var clone = Instantiate(objectToClone);
        clone.name = objectToClone.name;
        return clone;
    }
    
    private static T CustomInstantiate<T>(T objectToClone, Transform parent) where T : Object
    {
        var clone = Instantiate(objectToClone, parent, false);
        clone.name = objectToClone.name;
        return clone;
    }

    private static IList<FileInfo> GetFiles(DirectoryInfo di, params string[] exts)
    {
        List<FileInfo> list = new List<FileInfo>();
        foreach (string ext in exts)
            list.AddRange(di.GetFiles(ext));

        return list;
    }
}