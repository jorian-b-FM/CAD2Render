﻿using System;
using System.Collections;
using UnityEngine;
using ResourceManager = Assets.Scripts.io.ResourceManager;


[AddComponentMenu("Cad2Render/MaterialRandomizers/ManufacturingLines")]
public class ManufacturingLinesHandler : MaterialRandomizerInterface, IDatasetUser<ManufacturingLinesData>
{
    [SerializeField] private ManufacturingLinesData dataset;

    public ManufacturingLinesData Dataset
    {
        get => dataset;
        set => dataset = value;
    }
    
    //private RandomNumberGenerator rng;
    private ComputeShader LineTextureGenerationShader;
    private RenderTexture LineZoneTexture;
    [InspectorButton("TriggerCloneClicked")]
    public bool clone;
    private void TriggerCloneClicked()
    {
        RandomizerInterface.CloneDataset(ref dataset);
    }

    public void Awake()
    {
        LineTextureGenerationShader = ResourceManager.loadShader("LineTextureGenerator");
    }

    public void OnDestroy()
    {
        if (LineZoneTexture)
            Destroy(LineZoneTexture);
    }

    public override void RandomizeSingleMaterial(MaterialTextures textures, ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null)
    {
        var ColorTexture = textures.set(MaterialTextures.MapTypes.colorMap,
            textures.GetCurrentLinkedTexture("_BaseColorMap", "baseColorTexture"),
            textures.GetCurrentLinkedColor("_Color", "baseColorFactor"));
        int texSizeX = ColorTexture.width;
        int texSizeY = ColorTexture.height;
        updateLineZoneTexture(texSizeX, texSizeY);


        int kernelHandle = LineTextureGenerationShader.FindKernel("CSMain");
        LineTextureGenerationShader.SetInt("randSeed", rng.IntRange(128, Int32.MaxValue));
        LineTextureGenerationShader.SetFloat("lineSpacing", dataset.lineSpacing);
        LineTextureGenerationShader.SetTexture(kernelHandle, "Result", ColorTexture);
        LineTextureGenerationShader.SetTexture(kernelHandle, "parameterTexture", LineZoneTexture);

        LineTextureGenerationShader.Dispatch(kernelHandle, texSizeX / 8, texSizeY / 8, 1);

        textures.linkTexture(MaterialTextures.MapTypes.colorMap);
    }

    private void updateLineZoneTexture(int resolutionX, int resolutionY)
    {

        if (LineZoneTexture == null || LineZoneTexture.width != resolutionX || LineZoneTexture.height != resolutionY)
        {
            if (LineZoneTexture != null)
                Destroy(LineZoneTexture);
            LineZoneTexture = new RenderTexture(resolutionX, resolutionY, 0);
            LineZoneTexture.Create();

            if (dataset.LineCreationZoneTexture == null)
            {
                //if no LineAndRustMask was provided create a default mask (no lines, rust everywhere)
                RenderTexture rt = RenderTexture.active;
                RenderTexture.active = LineZoneTexture;
                GL.Clear(true, true, new Color(1, 1, 0)); //red = line creation zones, green = color variation, blue = not used
                RenderTexture.active = rt;
            }
            else
                Graphics.Blit(dataset.LineCreationZoneTexture, LineZoneTexture);
        }
    }
}