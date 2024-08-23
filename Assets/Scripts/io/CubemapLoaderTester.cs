using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace io
{
    public class CubemapLoaderTester : MonoBehaviour
    {
        public string path;
        [InspectorButton(nameof(Test))]
        public bool create;

        public Volume Volume;

        public UnityEvent<Texture> OnLoaded;

        private void Test()
        {
            var texture = CubemapLoader.Load(path);
            OnLoaded?.Invoke(texture);
        }

        public void LoadIntoHdrSky(Texture tex)
        {
            Volume.profile.TryGet<HDRISky>(out var sky);
            sky.hdriSky.value = tex;
        }
    }
}