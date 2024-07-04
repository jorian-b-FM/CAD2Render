#if UNITY_EDITOR
using System.IO;
using System.Linq;
using SimpleJSON;
using UnityEditor;
using UnityEngine;
using UnityGLTF;
using static ConstDataValues;

namespace C2R
{
    public class DataExporter : MonoBehaviour
    {
        public string exportFolderPath;


        [ContextMenu("Regenerate Files")]
        void RegenerateFiles()
        {
            var mainPath = Path.Combine(exportFolderPath, "main.json");
            var randomizerPath = Path.Combine(exportFolderPath, "randomizers.json");

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

        private JSONNode ToJsonNode(UnityEngine.Transform o)
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

            var collider = o.GetComponent<Collider>();
            switch (collider)
            {
                case MeshCollider meshCollider:
                    childObject[meshColliderName] = true;
                    break;
                case BoxCollider boxCollider:
                    childObject[boxColliderName] = new JSONObject
                    {
                        [nameof(boxCollider.center)] = boxCollider.center,
                        [nameof(boxCollider.size)] = boxCollider.size,
                        [nameof(boxCollider.isTrigger)] = boxCollider.isTrigger
                    };
                    break;
                default:
                    if (collider != null)
                        Debug.LogWarning($"No support (yet) for collider type: {collider.GetType()}");
                    break;
            }

            var meshFilter = o.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                var modelFile = ExportModel(meshFilter, exportFolderPath);

                childObject[meshName] = new JSONObject
                {
                    [assetName] = modelFile,
                    [nameof(meshFilter.sharedMesh)] = meshFilter.name
                };
            }


            var interfaces = o.GetComponents<RandomizerInterface>();
            if (interfaces.Any())
            {
                var arr = new JSONArray();
                childObject[randomizerName] = arr;
                foreach (var i in interfaces)
                {
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
                    }

                    arr.Add(iData);
                }
            }

            return childObject;
        }

        private static string ExportModel(MeshFilter mesh, string targetFolder, bool binary = true)
        {
            var ext = binary ? ".glb" : ".gltf";

            string sceneName = mesh.sharedMesh.name;
            
            var settings = GLTFSettings.GetOrCreateSettings();
            var exportOptions = new ExportContext(settings) { TexturePathRetriever = RetrieveTexturePath };
            var exporter = new GLTFSceneExporter(mesh.transform, exportOptions);
            
            var resultFile = GLTFSceneExporter.GetFileName(targetFolder, sceneName, ext);
            settings.SaveFolderPath = targetFolder;
            if (binary)
                exporter.SaveGLB(targetFolder, sceneName);
            else
                exporter.SaveGLTFandBin(targetFolder, sceneName);

            Debug.Log("Exported to " + resultFile);
            return resultFile;
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
            => JSON.Parse(JsonUtility.ToJson(o, true));

        private string GetTypeName(object o)
        {
            // TODO: do we want assembly qualified name?
            return o.GetType().Name;
        }

        private static string RetrieveTexturePath(UnityEngine.Texture texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            // texture is a subasset
            if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(Texture2D))
            {
                var ext = System.IO.Path.GetExtension(path);
                if (string.IsNullOrWhiteSpace(ext)) return texture.name + ".png";
                path = path.Replace(ext, "-" + texture.name + ext);
            }

            return path;
        }
    }
}
#endif