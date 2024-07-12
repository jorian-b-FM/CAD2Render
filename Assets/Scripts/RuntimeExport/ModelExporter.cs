using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C2R.Export
{
    public static class ModelExporter
    {
        public static string Export(string targetFolder, Transform o)
        {
            return ExportToGltf(targetFolder, o.name, o);
        }

        private static string ExportToGltf(string targetFolder, string fileName, Transform o, bool binary = false)
        {
            var export = new GLTFast.Export.GameObjectExport();
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
            var resultFile = PathHelper.ToSafeFilename(targetFolder, fileName, ext);
            fileName = Path.GetFileName(resultFile);
            export.SaveToFileAndDispose(resultFile);
            
            return fileName;
        }
    }
}