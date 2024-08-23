using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[AddComponentMenu("Cad2Render/MaterialRandomizers/Main Material Randomizer", 1)]
public class MaterialRandomizeHandler : RandomizerInterface, IDatasetUser<MaterialRandomizeData>
{
    private List<MaterialTextures> materialTextureTable = new List<MaterialTextures>();
    private List<GameObject> subjectInstances;
    [SerializeField] private MaterialRandomizeData dataset;

    public MaterialRandomizeData Dataset
    {
        get => dataset;
        set => dataset = value;
    }
    [InspectorButton("TriggerCloneClicked")]
    public bool clone;
    private MaterialRandomizerInterface[] linkedMaterialRandomizers;

    public override MainRandomizerData.RandomizerTypes randomizerType => MainRandomizerData.RandomizerTypes.Material;

    private void TriggerCloneClicked()
    {
        RandomizerInterface.CloneDataset(ref dataset);
    }
    public void Awake()
    {
        LinkGui();
        linkedMaterialRandomizers = GetLinkedInterfaces();
    }
    
    public void initialize(ref List<GameObject> instantiatedModels)
    {
        if (instantiatedModels != null)
            subjectInstances = instantiatedModels;
        else
        {
            subjectInstances = new List<GameObject>();
            subjectInstances.Add(this.gameObject);
        }
    }

    public MaterialTextures getTextures(int index)
    {
        if (index < materialTextureTable.Count)
            return materialTextureTable[index];
        else
            return null;
    }

    public override void Randomize(ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null)
    {
        if (subjectInstances == null)
            initialize(ref subjectInstances);
        int index = 0;

        foreach (GameObject instance in subjectInstances)
        {
            //Run all RandomizeSingleInstance functions that are part of the material randomizer inside the generator
            if (instance != this.gameObject)//skip if the instance is the same gameobject as the gameObject inside the generator (this is execute in the next loop)
                foreach (MaterialRandomizerInterface randomizer in linkedMaterialRandomizers)
                    if(randomizer.isActiveAndEnabled)
                        randomizer.RandomizeSingleInstance(instance, ref rng, bopSceneIterator);

            //Run all RandomizeSingleInstance functions that are directly linked to the instance
            foreach (MaterialRandomizerInterface randomizer in instance.GetComponentsInChildren<MaterialRandomizerInterface>().OrderByDescending(o => o.GetPriority()))
                if (randomizer.isActiveAndEnabled)
                    randomizer.RandomizeSingleInstance(randomizer.gameObject, ref rng, bopSceneIterator);

            foreach (Renderer rend in instance.GetComponentsInChildren<Renderer>())
            {
                for (int materialIndex = 0; materialIndex < rend.materials.Length; ++materialIndex)
                {
                    //Reuse the MaterialTextures objects to limit the amount of textures that need to be created and destroyed
                    if (index < materialTextureTable.Count)
                        materialTextureTable[index].UpdateLinkedRenderer(rend, materialIndex);
                    else
                        materialTextureTable.Add(new MaterialTextures(dataset.generatedTextureResolution, rend, materialIndex));

                    //Run all RandomizeSingleMaterial functions that are part of the material randomizer inside the generator
                    if (instance != this.gameObject)//skip if the instance is the same gameobject as the gameObject inside the generator (this is execute in the next loop)
                        foreach (MaterialRandomizerInterface randomizer in linkedMaterialRandomizers)
                            if (randomizer.isActiveAndEnabled)
                                randomizer.RandomizeSingleMaterial(materialTextureTable[index], ref rng, bopSceneIterator);

                    //Run all RandomizeSingleMaterial functions that are directly linked to the instance (starting from each renderer component find all MaterialRandomizerInterface linked to the renderer or one of its (grand)parents)
                    foreach (MaterialRandomizerInterface randomizer in rend.gameObject.GetComponentsInParent<MaterialRandomizerInterface>().OrderByDescending(o => o.GetPriority()))
                        if (randomizer.isActiveAndEnabled)
                            randomizer.RandomizeSingleMaterial(materialTextureTable[index], ref rng, bopSceneIterator);

                    //Submit the changes done by the randomizers to the GPU
                    materialTextureTable[index].linkpropertyBlock();
                    ++index;
                }
            }
        }
        resetFrameAccumulation();
    }
    
    public MaterialRandomizerInterface[] GetLinkedInterfaces()
    {
        return GetComponentsInChildren<MaterialRandomizerInterface>()
            .OrderByDescending(o => o.GetPriority())
            .ToArray();
    }
    
    [System.Obsolete]
    public override List<GameObject> getExportObjects()
    {
        if (subjectInstances != null)
            return subjectInstances;
        else
            return new List<GameObject>();
    }

}