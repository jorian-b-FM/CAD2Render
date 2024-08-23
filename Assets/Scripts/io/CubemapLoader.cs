using System.IO;
using UnityEngine;
using UnityEngine.Events;

public static class CubemapLoader
{
    
    public static Texture Load(string path)
    {
        byte[] fileData = File.ReadAllBytes(path);
        Texture2D equirectangularTexture = new Texture2D(1, 1);
        equirectangularTexture.LoadImage(fileData);
        
        // TODO this handles equirectangular. Do i need to add cross support?
        var prefab = Resources.Load<GameObject>("Data/SkyboxCaptureSetup");
        var setup = Object.Instantiate(prefab);
        var camera = setup.GetComponentInChildren<Camera>();
        var renderer = setup.GetComponentInChildren<MeshRenderer>();

        var material = new Material(renderer.sharedMaterial);
        material.mainTexture = equirectangularTexture;
        renderer.material = material;
        
        // TODO RenderTexture vs Cubemap
        var renderTexture = new RenderTexture(2048, 2048, 16);
        renderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        renderTexture.hideFlags = HideFlags.HideAndDontSave;
        
        camera.RenderToCubemap(renderTexture);
        
        Object.DestroyImmediate(setup);
        
        return renderTexture;
    }
}