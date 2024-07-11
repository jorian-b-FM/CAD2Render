﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C2R.Export
{
    public static class ModelExporter
    {
        public static string ExportModel(string targetFolder, Transform o, bool binary = true)
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

            string sceneName = o.name;
            var ext = binary ? ".glb" : ".gltf";
            var resultFile = ToSafeFilename(targetFolder, sceneName, ext);
            var fileName = Path.GetFileName(resultFile);
            export.SaveToFileAndDispose(resultFile);
            return fileName;
        }
        
        private static string ToSafeFilename(string directory, string filename, string ext = null)
        {
            if (ext == null)
                ext = Path.GetExtension(filename);
            return GetFileName(directory, filename, ext);
        }
        
        private static string GetFileName(string directory, string fileNameThatMayHaveExtension, string requiredExtension)
        {
            var absolutePathThatMayHaveExtension = Path.Combine(directory, EnsureValidFileName(fileNameThatMayHaveExtension));

            if (!requiredExtension.StartsWith(".", StringComparison.Ordinal))
                requiredExtension = "." + requiredExtension;

            if (!Path.GetExtension(absolutePathThatMayHaveExtension).Equals(requiredExtension, StringComparison.OrdinalIgnoreCase))
                return absolutePathThatMayHaveExtension + requiredExtension;

            return absolutePathThatMayHaveExtension;
        }

        /// <summary>
        /// Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </remarks>
        private static string EnsureValidFileName(string filename)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidReStr = $@"[{invalidChars}]+";

            var reservedWords = new []
            {
                "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
                "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
                "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            var sanitisedNamePart = Regex.Replace(filename, invalidReStr, "_");
            foreach (var reservedWord in reservedWords)
            {
                var reservedWordPattern = $"^{reservedWord}\\.";
                sanitisedNamePart = Regex.Replace(sanitisedNamePart, reservedWordPattern, "_reservedWord_.", RegexOptions.IgnoreCase);
            }

            return sanitisedNamePart;
        }
    }
}