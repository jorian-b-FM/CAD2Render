using System.Collections;
using UnityEngine;


[AddComponentMenu("Cad2Render/MaterialRandomizers/Texture Resampler")]
public class TextureResamplerHandler : MaterialRandomizerInterface, IDatasetUser<TextureResamplerData>
{
    [SerializeField] private TextureResamplerData dataset;

    public TextureResamplerData Dataset
    {
        get => dataset;
        set => dataset = value;
    }
    
    //private RandomNumberGenerator rng;
    private TextureResampler texResampler;
    [InspectorButton("TriggerCloneClicked")]
    public bool clone;
    private void TriggerCloneClicked()
    {
        RandomizerInterface.CloneDataset(ref dataset);
    }

    public void Awake()
    {
        texResampler = new TextureResampler(dataset);
    }

    public override void RandomizeSingleMaterial(MaterialTextures textures, ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null)
    {
        bool first = true;
        foreach (MaterialTextures.MapTypes type in dataset.resampleTextures)
        {
            if (first)
                texResampler.ResampleTexture(textures, textures.GetCurrentLinkedTexture(textures.getTextureName(type)), type, ref rng);
            else
                texResampler.applyPreviousResample(textures, type);
            first = false;
            textures.linkTexture(type);
        }
    }
}