using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class HideRandomizeHandler : RandomizerInterface
{
    public override MainRandomizerData.RandomizerTypes randomizerType => MainRandomizerData.RandomizerTypes.Object;

    public void Start()
    {
        //this.LinkGui("ObjectRandomizerList");
    }
    public float hideChance = 0.5f;

    public override void Randomize(ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null)
    {
        this.gameObject.SetActive(rng.Next() > hideChance);
        resetFrameAccumulation();
    }
}
