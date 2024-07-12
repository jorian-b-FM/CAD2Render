using System;
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
        
        public static Object[] LoadAll(string path, Type type)
        {
            UnityEngine.Object[] list;
            if (!LoadedData.TryGetValue(makeHashCode(path, type), out list))
            {
                if (path != "")
                    list = Resources.LoadAll(path, type);
                else
                    list = Array.Empty<Object>();
                LoadedData.Add(makeHashCode(path, type), list);
            }
            return list;
        }

        public static T[] LoadAll<T>(string path) where T : Object
            => LoadAll(path, typeof(T)).Cast<T>().ToArray();

        /// Associates a list of object with a certain path for the given type.
        /// <remarks>Overrides the list if there is already an associated list</remarks>
        public static void RegisterSet<T>(string path, T[] list) where T: Object
        {
            var hash = makeHashCode(path, typeof(T));
            LoadedData[hash] = list;
        }
        
        public static void RegisterSet(string path, Object[] list, Type type)
        {
            var hash = makeHashCode(path, type);
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
