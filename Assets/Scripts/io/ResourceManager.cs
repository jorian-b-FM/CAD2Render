﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.io
{

    internal static class ResourceManager
    {
        private static Dictionary<string, UnityEngine.Object[]> LoadedData = new Dictionary<string, UnityEngine.Object[]>();

        private static string makeHashCode(string path, Type type)
        {
            return type.ToString() + "?" +  path;// ?  should be an illegal character in a path name so should never make conflicts
        }

        public static T[] LoadAll<T>(string  path) where T: Object
        {
            UnityEngine.Object[] list;
            if (!LoadedData.TryGetValue(makeHashCode(path, typeof(T)), out list))
            {
                if (path != "")
                    list = Resources.LoadAll(path, typeof(T));
                else
                    list = Array.Empty<UnityEngine.Object>();
                LoadedData.Add(makeHashCode(path, typeof(T)), list);
            }
            return list.Cast<T>().ToArray();
        }

        /// Associates a list of object with a certain path for the given type.
        /// <remarks>Overrides the list if there is already an associated list</remarks>
        public static void RegisterSet<T>(string path, T[] list) where T: Object
        {
            var hash = makeHashCode(path, typeof(T));
            LoadedData[hash] = list;
        }

        private static Dictionary<string, ComputeShader> loadedShaders = new Dictionary<string, ComputeShader>();
        public static ComputeShader loadShader(string shaderName)
        {
            ComputeShader shader;

            if (!loadedShaders.TryGetValue(shaderName, out shader))
            {
                shader = (ComputeShader)Resources.Load("ComputeShaders/" + shaderName);
                loadedShaders.Add(shaderName, shader);
            }
            return shader;
        }
    }
}
