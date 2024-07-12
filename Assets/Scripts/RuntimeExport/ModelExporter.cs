using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast.Export;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C2R.Export
{
    public static class ModelExporter
    {
        /// <returns>The filename of the exported model</returns>
        public static async Task<string> Export(DirectoryInfo info, Transform o)
        {
            return await ExportToGltf(info.FullName, "", o.name, o);
        }
        
        /// <returns>The filename of the exported model</returns>
        public static async Task<string> Export(string directory, Transform o)
        {
            return await ExportToGltf(directory, "", o.name, o);
        }
        
        /// <returns>The path of the exported model relative to root.</returns>
        public static async Task<string> Export(string root, string directory, Transform o)
        {
            return await ExportToGltf(root, directory, o.name, o);
        }

        private static async Task<string> ExportToGltf(string root, string directory, string fileName, Transform o, bool binary = false)
        {
            var export = new GLTFast.Export.GameObjectExport(new ExportSettings
            {
                FileConflictResolution = FileConflictResolution.Overwrite
            });
            // Instantiate it & reset transform
            var clone = Object.Instantiate(o, null, false);
            clone.name = o.name;
            clone.gameObject.SetActive(true);
            export.AddScene(new[]
            {
                clone.gameObject
            }, clone.worldToLocalMatrix, o.name);
            Object.DestroyImmediate(clone.gameObject);
            
            var ext = binary ? ".glb" : ".gltf";
            var targetFolder = Path.Combine(root, directory);
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);
            
            var resultFile = PathHelper.ToSafeFilename(targetFolder, fileName, ext);
            fileName = Path.GetFileName(resultFile);
            await export.SaveToFileAndDispose(resultFile);
            
            return Path.Combine(directory, fileName).Replace("\\", "/");
        }
    }
}