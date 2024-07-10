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
    public const string poseName = "Pose";
    public const string datasetName = "Dataset";
    public const string typeName = "Type";
    public const string dataName = "Data";
    public const string linksName = "Links";
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

    private GameObject _fakeResources;

    async void Awake()
    {
        if (!loadFromFolder) return;

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

    private async Task LoadFromDirectory(string folder)
    {
        if (!Directory.Exists(folder)) return;

        var randomizer = GetComponent<MainRandomizer>();

        // instantiate so we do not edit the asset
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
            var newDataset = Instantiate(randomizer.Dataset);
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
        var childGO = new GameObject(childName);
        Transform child = childGO.transform;
        child.SetParent(target);
        // Disable it until everything has been initialized (so awake does not get called instantly)
        childGO.SetActive(false);

        if (node.TryGetValue(poseName, out JSONNode poseNode))
            ReadPose(poseNode, child);
        
        if (node.TryGetValue(rigidBodyName, out JSONNode rigidBodyNode))
            CreateRigidBody(rigidBodyNode, childGO);

        if (node.TryGetValue(meshName, out JSONNode meshNode))
            await CreateMesh(meshNode, childGO);

        if (node.TryGetValue(childrenName, out JSONNode childrenNode))
            await TryCreateChildren(childrenNode, child);

        if (node.TryGetValue(collidersName, out JSONNode colliderNode))
            SetupColliders(colliderNode, childGO);
        
        if (node.TryGetValue(randomizerName, out JSONNode randomizerNode))
            await CreateRandomizers(randomizerNode, childGO);

        childGO.SetActive(true);
        return childGO;
    }

    private static void ReadPose(JSONNode node, Transform target)
    {
        JSONNode valueNode;
        target.position = node[nameof(target.position)];
        target.rotation = node[nameof(target.rotation)];
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
        if (node.TryGetValue(nameof(rb.automaticCenterOfMass), out valueNode))
            rb.automaticCenterOfMass = valueNode;
        if (node.TryGetValue(nameof(rb.centerOfMass), out valueNode))
            rb.centerOfMass = valueNode;
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
                    Instantiate(subMesh, go.transform, false);
            }
            else // if no submesh was specified, just instantiate the whole thing
            {
                Instantiate(model, go.transform, false);
            }
            
            Debug.Log("GLTF file imported successfully.");
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
        {
            if (!TryCreateBehaviour<RandomizerInterface>(node, target, out var behaviour))
                continue;

            // Custom behaviour per handler type
            switch (behaviour)
            {
                case ObjectRandomizeHandler objectRandomizer:
                    string path = objectRandomizer.Dataset.modelsPath;
                    var objects = ResourceManager.LoadAll<GameObject>(path);
                    // If it isn't a valid resource path (no resources found). Check if there are any jsons
                    if (!objects.Any())
                    {
                        string fullPath = Path.Combine(_fullFolderPath, path);

                        if (Directory.Exists(fullPath))
                        {
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

                            // Note: use path and not fullPath here as we want to override the result of the dataset
                            ResourceManager.RegisterSet(path, objects);
                        }
                        else
                            Logger.LogWarning($"ObjectRandomizeHandler for {target.name} does not have any target objects.");
                    }
                    break;
                case MaterialRandomizeHandler materialRandomizer:
                    if (node.TryGetValue(linksName, out var linksNode))
                    {
                        if (linksNode is not JSONArray)
                        {
                            Logger.LogError($"Expected Array for '{linksName}' in '{target.name}'");
                            continue;
                        }

                        foreach (var linkNode in linksNode.Children)
                            TryCreateBehaviour<MaterialRandomizerInterface>(linkNode, target, out _);
                    }

                    break;
            }
        }
    }

    private bool TryCreateBehaviour<T>(JSONNode node, GameObject target, out T behaviour)
        where T : MonoBehaviour
    {
        var behaviourType = node[typeName];
        var type = Type.GetType(behaviourType);
        if (type == null || !typeof(T).IsAssignableFrom(type))
        {
            Logger.LogError($"{behaviourType} is not a valid type. (Resolved to '{type?.FullName}')");
            behaviour = null;
            return false;
        }

        // Add component & override the MonoBehaviour data if needed
        behaviour = target.AddComponent(type) as T;
        if (node.TryGetValue(dataName, out JSONNode dataNode))
            JsonUtility.FromJsonOverwrite(dataNode.ToString(), behaviour);

        // We cannot override values inside of a nested Object through the above, so do that here
        return TryOverrideDataset(behaviour, node);
    }

    private bool TryOverrideDataset(Object o, JSONNode node)
    {
        if (o is not IDatasetUser datasetUser)
            return true;

        var datasetType = datasetUser.GetDataSetType();
        if (_defaultObjectByType.TryGetValue(datasetType, out ScriptableObject so))
        {
            // Instantiate it so we have a copy since we might override some values
            so = Instantiate(so);
        }
        else
        {
            Logger.LogError($"No default defined for type {datasetType.Name}. A default object will be created but this might result in further errors.");
            so = ScriptableObject.CreateInstance(datasetType);
        }
        
        if (node.TryGetValue(datasetName, out JSONNode dataNode))
            JsonUtility.FromJsonOverwrite(dataNode.ToString(), so);
        datasetUser.SetDataset(so);
        return true;
    }
}