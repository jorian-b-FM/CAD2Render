using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.io;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;


[AddComponentMenu("Cad2Render/Light Randomize Handler")]
public class LightRandomizeHandler : RandomizerInterface, IDatasetUser<LightRandomizeData>
{
    [SerializeField] private LightRandomizeData dataset;

    public LightRandomizeData Dataset
    {
        get => dataset;
        set => dataset = value;
    }
    
    [InspectorButton("TriggerCloneClicked")]
    public bool clone;
    private void TriggerCloneClicked()
    {
        RandomizerInterface.CloneDataset(ref dataset);
    }

    private Texture[] cubeMaps;
    private Texture[] projectorMaps;

    private List<Light> instantiatedLights;
    GameObject renderSettings = null;

    private int LightIndex = 0;
    
    public override MainRandomizerData.RandomizerTypes randomizerType => MainRandomizerData.RandomizerTypes.Light;

    public void Start()
    {
        this.LinkGui();

        cubeMaps = TryGetResources<Texture>(dataset.environmentsPath, typeof(Cubemap));
        projectorMaps = TryGetResources<Texture>(dataset.projectorTexturePath);

        if (cubeMaps.Length == 0 && dataset.environmentsPath != "")
        {
            Debug.LogWarning("No environment maps found in " + dataset.environmentsPath);
        }
        var temp = GameObject.FindGameObjectWithTag("EnvironmentSettings");
        if (temp != null)
        {
            renderSettings = (GameObject)temp.transform.Find("Rendering Settings")?.gameObject;
            //raytracingSettings = temp.transform.Find("Ray Tracing Settings")?.gameObject;
            //postProcesingSettings = temp.transform.Find("PostProcessing")?.gameObject;
        }

        instantiatedLights = new List<Light>();
    }

    protected override void OnDestroy()
    {
        DestroyLights();
        
        base.OnDestroy();
    }

    private static T[] TryGetResources<T>(string path, Type overrideType = null) where T: UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
            return Array.Empty<T>();

        if (overrideType == null)
            return ResourceManager.LoadAll<T>(path);
        return ResourceManager.LoadAll(path, overrideType).OfType<T>().ToArray();
    }
    
    public override void Randomize(ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null)
    {
        if (dataset.environmentVariatons)
            RandomizeEnvironment(ref rng);
        if(dataset.lightsourceVariatons)
            RandomizeExtraLights(ref rng);
        resetFrameAccumulation();
    }

    private void RandomizeExtraLights(ref RandomNumberGenerator rng)
    {
        DestroyLights();


        for (int i = 0; i < dataset.numLightsources; ++i)
        {
            float phi = rng.Range(dataset.minPhiLight, dataset.maxPhiLight);
            float theta = rng.Range(dataset.minThetaLight, dataset.maxThetaLight);
            float radius = GeometryUtils.convertMmToUnity(rng.Range(dataset.minRadiusLight, dataset.maxRadiusLight));

            Vector3 offset = SphericalCoordinates.SphericalToCartesian(phi * Mathf.PI / 180.0f, theta * Mathf.PI / 180.0f, radius);
            Vector3 spawnPosition = this.transform.position + offset;

            Light lightSource = Instantiate(dataset.lightSourcePrefab);
            lightSource.transform.position = spawnPosition;
            lightSource.transform.LookAt(this.transform);
            instantiatedLights.Add(lightSource);

            lightSource.intensity *= (float)Math.Pow(2, rng.Range(dataset.minIntensityModifier, dataset.maxIntensityModifier));

            if (dataset.applyProjectorVariations && projectorMaps.Length > 0)
            {
                float h, s, v;
                Color.RGBToHSV(lightSource.color, out h, out s, out v);
                lightSource.cookie = (Texture)projectorMaps[rng.IntRange(0, projectorMaps.Length)];
                lightSource.color = Color.HSVToRGB(rng.Next(), s, 1.0f);
            }
        }
    }

    private void DestroyLights()
    {
        if (instantiatedLights == null)
            return;
        
        foreach (Light lightsource in instantiatedLights)
        {
            Destroy(lightsource.gameObject);
        }

        instantiatedLights.Clear();
    }

    private void RandomizeEnvironment(ref RandomNumberGenerator rng)
    {
        HDRISky sky = null;
        if (renderSettings != null)
            renderSettings.GetComponent<Volume>()?.profile.TryGet<HDRISky>(out sky);
        if (sky == null)
        {
            Debug.LogWarning("No sky found in the light randomizer");
            return;
        }

        // change texture on cube
        if (cubeMaps.Length > 0)
        {
            Texture texture;
            if (!dataset.pickRandomEnvironment)
            {
                texture = cubeMaps[this.LightIndex];
                this.LightIndex = (this.LightIndex + 1) % cubeMaps.Length;
            }
            else
                texture = cubeMaps[rng.IntRange(0, cubeMaps.Length)];


            sky.hdriSky.Override(texture);
        }


        if (dataset.randomEnvironmentRotations)
        {
            sky.rotation.value = rng.Angle(dataset.minEnvironmentAngle, dataset.maxEnvironmentAngle);
        }

        if (dataset.randomExposuresEnvironment)
        {
            sky.exposure.value = rng.Range(dataset.minExposure, dataset.maxExposure);
        }
    }
}
