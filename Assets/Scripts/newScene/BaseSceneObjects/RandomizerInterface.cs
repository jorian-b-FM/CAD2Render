﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public interface IDatasetUser
{
    ScriptableObject GetDataset();
    void SetDataset(ScriptableObject so);
    Type GetDataSetType();
}

public interface IDatasetUser<T> : IDatasetUser
    where T: ScriptableObject
{
    T Dataset { get; set; }

    ScriptableObject IDatasetUser.GetDataset()
    {
        return Dataset;
    }
    
    void IDatasetUser.SetDataset(ScriptableObject so)
    {
        Dataset = so as T;
    }

    Type IDatasetUser.GetDataSetType()
    {
        return typeof(T);
    }
}

public abstract class RandomizerInterface : MonoBehaviour
{
    public abstract void Randomize(ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null);

    [Obsolete("exports are now selected by using the ExportInstanceInfo tag")]
    public virtual List<GameObject> getExportObjects() { return new List<GameObject>(); }
    
    public bool addRandomizeButton = true;

    private Button _randomizeButton;
    private ScrollView _buttonList;

    protected virtual void OnDestroy()
    {
        if (_buttonList != null && _randomizeButton != null)
            _buttonList.Remove(_randomizeButton);
    }


    public abstract MainRandomizerData.RandomizerTypes randomizerType { get; }
    public bool updateCheck(uint currentUpdate, MainRandomizerData.RandomizerUpdateIntervals[] updateIntervals = null)
    {
        if (updateIntervals == null)
            return true;
        bool defaultTypeUpdate = true;//no default defined => randomize every update
        foreach (var pair in updateIntervals)
        {
            if(pair.randomizerType == randomizerType)
                return currentUpdate % Math.Max(pair.interval, 1) == 0;

            if (pair.randomizerType == MainRandomizerData.RandomizerTypes.Default)
                defaultTypeUpdate = currentUpdate % Math.Max(pair.interval, 1) == 0;
        }
        return defaultTypeUpdate;
    }

    protected void resetFrameAccumulation()
    {
        HDRenderPipeline renderPipeline = RenderPipelineManager.currentPipeline as HDRenderPipeline;
        if (renderPipeline != null)
        {
            renderPipeline.ResetPathTracing();
        }
    }

    protected void LinkGui()
    {
        string listName = "";
        switch (this.randomizerType)
        {

            case MainRandomizerData.RandomizerTypes.View:
                listName = "ViewRandomizerList";
                break;
            case MainRandomizerData.RandomizerTypes.Object:
                listName = "ObjectRandomizerList";
                break;
            case MainRandomizerData.RandomizerTypes.Light:
                listName = "LightRandomizerList";
                break;
            case MainRandomizerData.RandomizerTypes.Material:
                listName = "MaterialRandomizerList";
                break;

            case MainRandomizerData.RandomizerTypes.Default:
            default:
                listName = "ViewRandomizerList";//misc randomizers are placed in view cause its the less crouded list.
                break;
        }

        if (!addRandomizeButton)
            return;
        var GUI = GameObject.FindGameObjectWithTag("GUI");
        if (!GUI)
        {
            Debug.LogWarning("GUI not found while linking buttons");
            return;
        }
        var UIDoc = GUI.GetComponent<UIDocument>();
        if (!UIDoc)
        {
            Debug.LogWarning("UIDocument not found in the GUI while linking buttons");
            return;
        }
        _buttonList = UIDoc.rootVisualElement.Q<ScrollView>(listName);
        if (_buttonList ==  null)
        {
            Debug.LogWarning(listName + " not found in the GUI while linking buttons");
            return;
        }

        _randomizeButton = new Button();
        _randomizeButton.text = this.name;
        RandomNumberGenerator rng = new RandomNumberGenerator((int)System.DateTime.Now.Ticks);
        _randomizeButton.RegisterCallback<ClickEvent>(ev => Randomize(ref rng));

        _buttonList.Add(_randomizeButton);
    }

    public ScriptableObject GetDataset()
    {
        if (this is IDatasetUser datasetUser)
            return datasetUser.GetDataset();
        return null;
    }
    
    public static void CloneDataset<T>(ref T dataset) where T : UnityEngine.Object
    {
        var newDataset = Instantiate(dataset);
        var result = Regex.Match(dataset.name, @"\d+$", RegexOptions.RightToLeft);
        if (result.Length == 0)
            newDataset.name = dataset.name + "_1";
        else
            newDataset.name = dataset.name.Substring(0, dataset.name.Length - result.Value.Length) + (Int32.Parse(result.Value) + 1);

        string newDatasetFile = Path.GetDirectoryName(SceneManager.GetActiveScene().path) + "/" + newDataset.name + ".asset";
#if UNITY_EDITOR
        AssetDatabase.CreateAsset(newDataset, newDatasetFile);
        AssetDatabase.SaveAssets();
#endif
        dataset = newDataset;
    }
}
