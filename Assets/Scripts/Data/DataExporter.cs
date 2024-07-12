using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        void Export()
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
            File.WriteAllText(mainPath, mainJson.ToString(2));

            JSONNode randomizerJson = new JSONObject();
            var defaults = new JSONObject();
            foreach (Transform child in randomizer.transform)
            {
                randomizerJson[child.name] = ToJsonNode(child);
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

            File.WriteAllText(randomizerPath, randomizerJson.ToString(2));
        }

        private JSONNode ToJsonNode(UnityEngine.Transform o, MeshData meshData = default)
        {
            var childObject = new JSONObject
            {
                [poseName] = new JSONObject
                {
                    [nameof(o.position)] = o.position,
                    [nameof(o.rotation)] = o.rotation,
                    [nameof(o.localScale)] = o.localScale,
                }
            };

            var colliders = o.GetComponents<Collider>();

            if (colliders.Any())
            {
                var colliderArray = new JSONArray();
                foreach (var collider in colliders)
                {
                    // Collider is an engine type and JsonUtility does not work for those
                    // So we have to do it manually
                    var colliderObject = new JSONObject
                    {
                        [nameof(collider.isTrigger)] = collider.isTrigger,

                    };
                    switch (collider)
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
                            if (collider != null)
                                Debug.LogWarning($"No support (yet) for collider type: {collider.GetType()}");
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
                    FileName = ModelExporter.Export(_fullFolderPath, o),
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
            
            var interfaces = o.GetComponents<RandomizerInterface>();
            if (interfaces.Any())
            {
                var arr = new JSONArray();
                childObject[randomizerName] = arr;
                for (int index = 0; index < interfaces.Length; index++)
                {
                    var i = interfaces[index];
                    var iData = ToJsonNode(i);
                    switch (i)
                    {
                        case MaterialRandomizeHandler materialRandomizer:
                            var linksArr = new JSONArray();
                            iData[linksName] = linksArr;
                            var links = materialRandomizer.GetLinkedInterfaces();
                            foreach (var link in links)
                                linksArr.Add(ToJsonNode(link));
                            break;
                        case LightRandomizeHandler lightRandomizer:
                            var environments = ResourceManager.LoadAll<Cubemap>(lightRandomizer.Dataset.environmentsPath);
                            if (environments.Any())
                            {
                                string environmentPath = "ExportedEnvironments";
                                string targetFolder = Path.Combine(_fullFolderPath, environmentPath);
                                if (!Directory.Exists(targetFolder))
                                    Directory.CreateDirectory(targetFolder);

                                foreach (var environment in environments)
                                {
                                    var exportedFile = TextureExporter.Export(targetFolder, environment);
                                }
                                
                                iData[datasetName][nameof(lightRandomizer.Dataset.environmentsPath)] = environmentPath;
                            }
                            break;
                        case ObjectRandomizeHandler objectRandomizer:
                            var models = ResourceManager.LoadAll<GameObject>(objectRandomizer.Dataset.modelsPath);
                            var fakePath = $"{o.name}_objects{index}";
                            var di = new DirectoryInfo(Path.Combine(_fullFolderPath, fakePath));
                            if (!di.Exists)
                                di.Create();

                            foreach (var model in models)
                            {
                                var json = ToJsonNode(model.transform, meshData);
                                File.WriteAllText(Path.Combine(di.FullName, $"{model.name}.json"), json.ToString(2));
                            }

                            iData[datasetName][nameof(objectRandomizer.Dataset.modelsPath)] = fakePath;
                            break;
                    }

                    arr.Add(iData);
                }
            }

            if (o.childCount > 0)
            {
                var childrenData = new JSONObject();
                
                foreach (Transform child in o)
                    childrenData.Add(child.name, ToJsonNode(child, meshData));

                childObject[childrenName] = childrenData;
            }

            return childObject;
        }

        private string GetCurrentPath(in MeshData data, string name)
        {
            return Path.Join(data.TreeLocation, name).Replace("\\", "/");
        }

        private JSONNode ToJsonNode(UnityEngine.Behaviour o)
        {
            var node = new JSONObject
            {
                [typeName] = GetTypeName(o),
                [dataName] = ToJsonNodeWithJsonUtility(o),
            };
            if (o is IDatasetUser datasetUser)
                node[datasetName] = ToJsonNodeWithJsonUtility(datasetUser.GetDataset());
            return node;
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