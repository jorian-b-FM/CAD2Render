using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.io;
using C2R.Export;
using SimpleJSON;
using UnityEngine;
using static ConstDataValues;

namespace C2R
{
    public struct MeshData
    {
        public Transform Root;
        public string FileName;
        public string TreeLocation;
    }
    
    public class DataExporter : MonoBehaviour
    {
        public string exportFolderPath = "ExampleData/Default";
        private string _fullFolderPath;

        [InspectorButton(nameof(Export))]
        public bool export;

        [ContextMenu("Export to files")]
        async void Export()
        {
            // Use root folder (both in exe and in editor)
            if (!Path.IsPathRooted(exportFolderPath))
                _fullFolderPath = Path.Combine(Application.dataPath, "..", exportFolderPath);
            _fullFolderPath = Path.GetFullPath(_fullFolderPath);

            if (!Directory.Exists(_fullFolderPath))
                Directory.CreateDirectory(_fullFolderPath);
            
            var mainPath = Path.Combine(_fullFolderPath, "main.json");
            var randomizerPath = Path.Combine(_fullFolderPath, "randomizers.json");

            var randomizer = GetComponent<MainRandomizer>();

            var mainJson = ToJsonNodeWithJsonUtility(randomizer.Dataset);
            await File.WriteAllTextAsync(mainPath, mainJson.ToString(2));
            Debug.Log($"Data exported to '{mainPath}'");

            JSONNode randomizerJson = new JSONObject();
            var defaults = new JSONObject();
            foreach (Transform child in randomizer.transform)
            {
                randomizerJson[child.name] = await ToJsonNode(child);
            }

            var overrider = GetComponent<DataImporter>();

            if (overrider != null)
            {
                randomizerJson[defaultSettingsName] = defaults;
                foreach (var o in overrider.defaultDataObjects)
                {
                    var key = GetTypeName(o);
                    if (!defaults.HasKey(key))
                        defaults[key] = ToJsonNodeWithJsonUtility(o);
                    else
                        Debug.LogWarning($"Multiple of type {key} in the Defaults of {nameof(DataImporter)}. Only the first one will be used.");
                }
            }

            await File.WriteAllTextAsync(randomizerPath, randomizerJson.ToString(2));
            Debug.Log($"Data exported to '{randomizerPath}'");
        }

        private async Task<JSONNode> ToJsonNode(Transform o, MeshData meshData = default)
        {
            var childObject = new JSONObject
            {
                [poseName] = new JSONObject
                {
                    [nameof(o.localPosition)] = o.localPosition,
                    [nameof(o.localRotation)] = o.localRotation.eulerAngles,
                    [nameof(o.localScale)] = o.localScale,
                }
            };

            if (!o.gameObject.CompareTag("Untagged"))
                childObject[tagName] = o.gameObject.tag;

            var colliders = o.GetComponents<Collider>();

            if (colliders.Any())
            {
                var colliderArray = new JSONArray();
                foreach (var coll in colliders)
                {
                    // Collider is an engine type and JsonUtility does not work for those
                    // So we have to do it manually
                    var colliderObject = new JSONObject
                    {
                        [nameof(coll.isTrigger)] = coll.isTrigger,

                    };
                    switch (coll)
                    {
                        case MeshCollider meshCollider:
                            colliderObject[typeName] = nameof(MeshCollider);
                            break;
                        case BoxCollider boxCollider:
                            colliderObject[typeName] = nameof(BoxCollider);
                            colliderObject[nameof(boxCollider.center)] = boxCollider.center;
                            colliderObject[nameof(boxCollider.size)] = boxCollider.size;
                            break;
                        case SphereCollider sphereCollider:
                            colliderObject[typeName] = nameof(SphereCollider);
                            colliderObject[nameof(sphereCollider.radius)] = sphereCollider.radius;
                            colliderObject[nameof(sphereCollider.center)] = sphereCollider.center;
                            break;
                        case CapsuleCollider capsuleCollider:
                            colliderObject[typeName] = nameof(CapsuleCollider);
                            colliderObject[nameof(capsuleCollider.radius)] = capsuleCollider.radius;
                            colliderObject[nameof(capsuleCollider.center)] = capsuleCollider.center;
                            colliderObject[nameof(capsuleCollider.height)] = capsuleCollider.height;
                            colliderObject[nameof(capsuleCollider.direction)] = capsuleCollider.direction;
                            break;
                        default:
                            if (coll != null)
                                Debug.LogWarning($"No support (yet) for collider type: {coll.GetType()}");
                            break;
                    }
                    colliderArray.Add(colliderObject);
                }
                childObject[collidersName] = colliderArray;
            }

            // If we have any MeshFilters as children, they will be exported.
            // I prefer not having 500 mesh files so group them all
            bool hasData = !meshData.Equals(default(MeshData));
            if (!hasData && o.GetComponentsInChildren<MeshFilter>().Any())
            {
                meshData = new MeshData
                {
                    Root = o,
                    FileName = await ModelExporter.Export(_fullFolderPath, "Data", o),
                    TreeLocation = o.name
                };
            }
            else if (hasData)
                meshData.TreeLocation = GetCurrentPath(meshData, o.name);

            var meshFilter = o.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                childObject[meshName] = new JSONObject
                {
                    [assetName] = meshData.FileName,
                    [nameof(meshFilter.sharedMesh)] = meshData.TreeLocation
                };
            }
            
            var rb = o.GetComponent<Rigidbody>();
            if (rb != null)
            {
                childObject[rigidBodyName] = new JSONObject
                {
                    [nameof(rb.isKinematic)] = rb.isKinematic,
                    [nameof(rb.mass)] = rb.mass,
                    [nameof(rb.linearDamping)] = rb.linearDamping,
                    [nameof(rb.angularDamping)] = rb.angularDamping,
                    [nameof(rb.useGravity)] = rb.useGravity,

                    [nameof(rb.freezeRotation)] = rb.freezeRotation,
                    [nameof(rb.automaticCenterOfMass)] = rb.automaticCenterOfMass,
                    [nameof(rb.centerOfMass)] = rb.centerOfMass
                };
            }
            
            var randomizers = o.GetComponents<RandomizerInterface>();
            if (randomizers.Any())
            {
                var arr = new JSONArray();
                var tasks = randomizers.Select(ToJsonNode);
                foreach (var task in tasks)
                    arr.Add(await task);
                childObject[randomizerName] = arr;
            }

            var interfaces = o.GetComponents<MaterialRandomizerInterface>();
            if (interfaces.Any())
            {
                var arr = new JSONArray();
                var tasks = interfaces.Select(ToJsonNode);
                foreach (var task in tasks)
                    arr.Add(await task);
                childObject[materialRandomizerName] = arr;
            }

            if (o.childCount > 0)
            {
                var childrenData = new JSONObject();
                
                foreach (Transform child in o)
                    childrenData.Add(child.name, await ToJsonNode(child, meshData));

                childObject[childrenName] = childrenData;
            }

            return childObject;
        }

        private string GetCurrentPath(in MeshData data, string name)
        {
            return Path.Join(data.TreeLocation, name).Replace("\\", "/");
        }

        private async Task<JSONNode> ToJsonNode(Behaviour o)
        {
            var node = new JSONObject
            {
                [typeName] = GetTypeName(o),
                [dataName] = ToJsonNodeWithJsonUtility(o),
            };
            
            if (o is not IDatasetUser datasetUser)
                return node;
            
            var dataset = datasetUser.GetDataset();
            node[datasetName] = ToJsonNodeWithJsonUtility(dataset);
            
            var i = o.GetComponentIndex();
                
            switch (dataset)
            {
                case MaterialModelRandomizeData materialRandomizeData:
                    // // HDRP -> gltf seem to not be super well supported, so we'll disable it for now
                    // var materialsPath = $"{o.name}_materials{i}";
                    // if (await TryExportMaterials(materialRandomizeData.materialsPath, materialsPath))
                    //     node[datasetName][nameof(materialRandomizeData.materialsPath)] = materialsPath;
                    break;
                case LightRandomizeData lightRandomizeData:
                    var environmentsPath = TryExportCubemaps(lightRandomizeData.environmentsPath);
                    node[datasetName][nameof(lightRandomizeData.environmentsPath)] = environmentsPath;
                    break;
                case ObjectRandomizeData objectRandomizeData:
                    var modelsPath = $"{o.name}_objects{i}";
                    if (await TryExportPrefabs(objectRandomizeData.modelsPath, modelsPath))
                        node[datasetName][nameof(objectRandomizeData.modelsPath)] = modelsPath;
                    break;
            }
            return node;
        }

        private async Task<bool> TryExportMaterials(string path, string savePath)
        {
            var materials = ResourceManager.LoadAll<Material>(path);
            var di = new DirectoryInfo(Path.Combine(_fullFolderPath, savePath));
            if (!di.Exists)
                di.Create();
            
            foreach (var material in materials)
            {
                var o = new GameObject(material.name);
                // Need both a MeshFilter & MeshRenderer before gltf picks it up
                _ = o.AddComponent<MeshFilter>();
                var meshRenderer = o.AddComponent<MeshRenderer>();
                meshRenderer.material = material;
                await ModelExporter.Export(di, o.transform);
                Object.DestroyImmediate(o);
            }

            return true;
        }

        private async Task<bool> TryExportPrefabs(string path, string savePath)
        {
            var models = ResourceManager.LoadAll<GameObject>(path);
            var di = new DirectoryInfo(Path.Combine(_fullFolderPath, savePath));
            if (!di.Exists)
                di.Create();

            foreach (var model in models)
            {
                var json = await ToJsonNode(model.transform);
                await File.WriteAllTextAsync(Path.Combine(di.FullName, $"{model.name}.json"), json.ToString(2));
            }

            return true;
        }

        private string TryExportCubemaps(string path)
        {
            var environments = ResourceManager.LoadAll<Cubemap>(path);
            if (!environments.Any())
                return null;
            
            string environmentPath = "ExportedEnvironments";
            string targetFolder = Path.Combine(_fullFolderPath, environmentPath);
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            foreach (var environment in environments)
                TextureExporter.Export(targetFolder, environment);

            return environmentPath;
        }

        private JSONNode ToJsonNodeWithJsonUtility(UnityEngine.Object o)
        {
            var jsonNode = JSON.Parse(JsonUtility.ToJson(o, true));
            // Sanitize it, other unity objects will be serialized with instanceID. But that is not persistent
            if (jsonNode is JSONObject objectNode)
            {
                List<string> invalidKeys = new List<string>();
                foreach (var (key, valueNode) in objectNode)
                {
                    if (valueNode is JSONObject childNode && childNode.HasKey("instanceID"))
                        invalidKeys.Add(key);
                }

                foreach (var key in invalidKeys)
                    objectNode.Remove(key);
            }
            
            return jsonNode;
        }

        private string GetTypeName(object o)
        {
            // TODO: do we want assembly qualified name?
            return o.GetType().Name;
        }
    }
}