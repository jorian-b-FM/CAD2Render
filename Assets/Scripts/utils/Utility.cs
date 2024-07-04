using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C2R
{
    public static class Utility
    {
        public static string GetAssetPath(UnityEngine.Object asset)
        {
            // TODO: Check if sufficient
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(asset);
#else
            return asset.name;
#endif
        }
        
        public static Bounds Combine(IList<Bounds> bounds)
        {
            if (bounds.Count <= 1) return bounds.FirstOrDefault();

            var b = bounds[0];
            for (int i = 1; i < bounds.Count; ++i)
                b.Encapsulate(bounds[i]);

            return b;
        }

        public static Bounds GetCombinedBounds(GameObject go)
        {
            var bounds = go.GetComponentsInChildren<MeshFilter>()
                .Select(x => x.sharedMesh.bounds)
                .ToArray();
            return Combine(bounds);
        }
    }
}