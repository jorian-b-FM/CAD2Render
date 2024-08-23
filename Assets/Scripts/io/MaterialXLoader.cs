using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using System.Xml;

public static class MaterialXLoader
{
    struct MaterialPropertyData
    {
        public string value;
        public string type;
        public string nodeName;
        public string colorSpace;
    }

    class MaterialData
    {
        public string elementName;
        public string type;
        public string name;
        public Dictionary<string, MaterialPropertyData> data = new ();
    }

    public static async Task<Material[]> Load(string path)
    {
        var materialNodes = new Dictionary<string, MaterialData>();
        MaterialData activeDefinition = null;

        string filePrefix = Path.GetDirectoryName(path);

        using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Async = true;


            using (XmlReader reader = XmlReader.Create(fs, settings))
            {
                while (await reader.ReadAsync())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "materialx":
                                    string subPath = reader.GetAttribute("fileprefix");
                                    filePrefix = Path.Join(filePrefix, subPath);
                                    break;
                                case "displacement":
                                case "tiledimage":
                                case "normalmap":
                                case "surfacematerial":
                                case "standard_surface":
                                    activeDefinition = new MaterialData {
                                        elementName = reader.Name,
                                        type = reader.GetAttribute("type"),
                                        name = reader.GetAttribute("name") 
                                    };
                                    materialNodes.Add(activeDefinition.name, activeDefinition);
                                    break;
                                case "input":
                                    var name = reader.GetAttribute("name");
                                    activeDefinition.data.Add(name, new MaterialPropertyData {
                                        value = reader.GetAttribute("value"),
                                        type = reader.GetAttribute("type"),
                                        nodeName = reader.GetAttribute("nodename"),
                                        colorSpace = reader.GetAttribute("colorspace")
                                    });
                                    break;
                            }
                            break;
                        case XmlNodeType.EndElement:
                            // Debug.Log($"End Element {reader.Name}");
                            break;
                        default:
                            // Debug.Log($"Other node {reader.NodeType} with value {reader.Value}");
                            break;
                    }
                }
            }
        }

        var result = new List<Material>();

        foreach (MaterialData definition in materialNodes.Values)
        {
            if (definition.type != "material")
                continue;
            
            // TODO switch material type based on shader            
            Material base_material = Resources.Load<Material>("Data/Simple");
            Material material = UnityEngine.Object.Instantiate(base_material);
            material.name = definition.name;

            foreach (var shader in definition.data.Values)
            {
                switch (shader.type)
                {
                    case "surfaceshader":
                        if (materialNodes.TryGetValue(shader.nodeName, out MaterialData surfaceNode))
                        {
                            foreach (var key in surfaceNode.data.Keys)
                            {
                                MaterialPropertyData node = surfaceNode.data[key];
                                switch (key)
                                {
                                    case "base_color":
                                        SetTexture(material, "_BaseColorMap", node, materialNodes, filePrefix);
                                            
                                        break;
                                    case "normal":
                                        SetTexture(material, "_NormalMap", node, materialNodes, filePrefix);
                                            
                                        break;
                                    case "specular_roughness":
                                        SetTexture(material, "_RoughnessMap", node, materialNodes, filePrefix);

                                        break;
                                    case "metalness":
                                        SetTexture(material, "_MetallicMap", node, materialNodes, filePrefix);

                                        break;
                                    default:
                                        Logger.LogWarning($"Unsupported input {key} in '{path}'.");
                                        break;
                                }
                            }
                            // Mask = Metallic (R), AO (G), Detail (B), Roughness (A)
                        }
                        break;
                    case "displacementshader":
                        if (materialNodes.TryGetValue(shader.nodeName, out var displacementNode))
                        {
                            foreach (var key in displacementNode.data.Keys)
                            {
                                MaterialPropertyData node = displacementNode.data[key];
                                switch (key)
                                {
                                    case "displacement":
                                        SetTexture(material, "_HeightMap", node, materialNodes, filePrefix);
                                        break;
                                    case "scale":
                                        material.SetFloat("_HeightMapAmplitude", ParseFloat(node.value));                                            
                                        break;
                                    default:
                                        Logger.LogWarning($"Unsupported input {key} in '{path}'.");
                                        break;
                                }
                            }
                        }
                        break;
                    default:
                        Logger.LogWarning($"Unsupported shader type {shader.type} in '{path}'.");
                        break;
                }
            }

            result.Add(material);
        }

        // material.SetTexture("", );
        Debug.Log("MLTX file imported successfully.");
        return result.ToArray();
    }

    private static void SetTexture(Material material, string name, MaterialPropertyData data, Dictionary<string, MaterialData> nodes, string filePrefix)
    {
        var texture = ParseTexture(data, nodes, filePrefix);
        material.SetTexture(name, texture);

        if (nodes.TryGetValue(data.nodeName, out var node))
        {
            if (node.data.TryGetValue("uvtiling", out MaterialPropertyData value))
                material.SetTextureScale(name, ParseVector(value.value));
        }
    }

    private static Vector4 ParseVector(string value)
    {
        Vector4 vec = new Vector4();
        var values = value.Split(",");
        for (int i = 0; i < values.Length; ++i)
        {
            vec[i] = float.Parse(values[i]);
        }
        return vec;
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value);
    }

    private static Texture ParseTexture(MaterialPropertyData data, Dictionary<string, MaterialData> nodes, string filePrefix)
    {
        // TODO If nodeName not present, check data.value (flat color)
        if (!nodes.TryGetValue(data.nodeName, out var node))
            return null;

        foreach (MaterialPropertyData subData in node.data.Values)
        {
            switch (subData.type)
            {
                case "filename":
                    bool linear = subData.colorSpace == null || !subData.colorSpace.Contains("srgb", StringComparison.InvariantCultureIgnoreCase);
                    var path = Path.Combine(filePrefix, subData.value);
                    byte[] fileData = File.ReadAllBytes(path);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear: linear);
                    if (!texture.LoadImage(fileData))
                        Logger.LogError($"Failed to load image at path '{path}'");
                    return texture;
                default:
                    // In the case of a normalmap node, the node can contain a reference to another node to get the actual texture
                    if (subData.type == node.type)
                    {
                        if (!string.IsNullOrEmpty(subData.nodeName))
                            return ParseTexture(subData, nodes, filePrefix);
                    }
                    break;
            }
        }
        return null;
    }
}