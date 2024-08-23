using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchSceneHandler : RandomizerInterface, IDatasetUser<SwitchSceneData>
{
    [SerializeField] private SwitchSceneData dataset;

    public SwitchSceneData Dataset
    {
        get => dataset;
        set => dataset = value;
    }
    
    [InspectorButton("TriggerCloneClicked")]
    public bool clone;
    
    public override MainRandomizerData.RandomizerTypes randomizerType => MainRandomizerData.RandomizerTypes.View;

    private void TriggerCloneClicked()
    {
        RandomizerInterface.CloneDataset(ref dataset);
    }

    public override void Randomize(ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null)
    {
        SceneManager.LoadSceneAsync(dataset.scenePath);//make sure it is not a child of the main randomizer
    }
    
    private void Start()
    {
        if (dataset != null && dataset.scenePath != "")
            this.LinkGui();
    }
}
