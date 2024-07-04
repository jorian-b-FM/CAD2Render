using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using C2R;
using SimpleJSON;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Loader;
using UnityGLTF.Plugins;
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
    public const string meshColliderName = "Collider";
    public const string boxColliderName = "Box";
    public const string meshName = "Mesh";
    public const string assetName = "Asset";
    public const string childrenName = "Children";
}

public class DataImporter : MonoBehaviour
{
    public bool loadFromFolder;
    public string folderPathToLoad;

    public ScriptableObject[] defaultDataObjects;
    private Dictionary<Type, ScriptableObject> _defaultObjectByType;

    void Awake()
    {
        if (!loadFromFolder) return;

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

        LoadFromDirectory(folderPathToLoad);
    }

    private async void LoadFromDirectory(string folder)
    {
        folder = Path.GetFullPath(folder);
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

        if (node.TryGetValue(meshName, out JSONNode meshNode))
            await CreateMesh(meshNode, childGO);

        if (node.TryGetValue(childrenName, out JSONNode childrenNode))
            await TryCreateChildren(childrenNode, child);

        if (node.TryGetValue(meshColliderName, out JSONNode meshColliderNode))
            SetupMeshCollider(meshColliderNode, childGO);

        if (node.TryGetValue(boxColliderName, out JSONNode boxColliderNode))
            SetupBoxCollider(boxColliderNode, childGO);
        
        if (node.TryGetValue(randomizerName, out JSONNode randomizerNode))
            CreateRandomizers(randomizerNode, childGO);

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

    private static async Task CreateMesh(JSONNode node, GameObject go)
    {
        Mesh mesh = null;

        JSONNode valueNode;
        if (node.TryGetValue(assetName, out valueNode))
        {
            string meshPath = valueNode;

            var component = go.AddComponent<GLTFComponent>();
            component.GLTFUri = meshPath;

            await component.Load();
            
            Destroy(component);

            Debug.Log("GLTF file imported successfully.");
        }
    }

    private static void SetupBoxCollider(JSONNode node, GameObject go)
    {
        JSONNode valueNode;

        var collider = go.AddComponent<BoxCollider>();
        var bounds = Utility.GetCombinedBounds(go);
        collider.center = bounds.center;
        collider.size = bounds.size;

        if (node.TryGetValue(nameof(collider.center), out valueNode))
            collider.center = valueNode;
        if (node.TryGetValue(nameof(collider.size), out valueNode))
            collider.size = valueNode;
        if (node.TryGetValue(nameof(collider.isTrigger), out valueNode))
            collider.isTrigger = valueNode;
    }

    private static void SetupMeshCollider(JSONNode node, GameObject go)
    {
        if (node is JSONBool boolNode && !boolNode.AsBool)
            return;

        var filters = go.GetComponentsInChildren<MeshFilter>();

        foreach (var filter in filters)
        {
            var collider = filter.gameObject.GetComponent<MeshCollider>();
            if (collider != null)
                continue;

            collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = true;
        }
    }

    private void CreateRandomizers(JSONNode randomizerNode, GameObject target)
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
            JsonUtility.FromJsonOverwrite(dataNode, behaviour);

        // We cannot override values inside of a nested Object through the above, so do that here
        return TryOverrideDataset(behaviour, node);
    }

    private bool TryOverrideDataset(Object o, JSONNode node)
    {
        if (o is not IDatasetUser datasetUser)
            return true;

        var datasetType = datasetUser.GetDataSetType();
        if (!_defaultObjectByType.TryGetValue(datasetType, out ScriptableObject so))
        {
            Logger.LogError(
                $"No default defined for type {datasetType.Name}. This means we cannot set the dataset & will likely result in further errors.");
            return false;
        }

        // Instantiate it so we have a copy since we might override some values
        so = Instantiate(so);
        if (node.TryGetValue(datasetName, out JSONNode dataNode))
            JsonUtility.FromJsonOverwrite(dataNode, so);
        datasetUser.SetDataset(so);
        return true;
    }
}