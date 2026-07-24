using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using API;
using FM.Core;
using FM.Core.Net;
using FM.Unity.Logging;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

public class RestApiManager : MonoBehaviour
{
    #region Inspector
    
    [SerializeField]
    private DataImporter _dataImporter;

    [SerializeField]
    private MainRandomizer _mainRandomizer;
    
    #endregion
    
    public void StartServer()
    {
        _restAPI.DefaultPort = _settingsManager.Settings.Port;
        _restAPI.StartWebServer();
    }
    
    void Start()
    {
        _settingsManager = new RestApiSettingsManager();
        
        FM.Core.Logging.Logging.LoggingSystems.Add(new UnityConsoleLoggingSystem());
        FM.Core.Logging.Logging.Enabled = true;
        
        InitializeHandlers();

        StartServer();
    }

    void OnDestroy()
    {
        _restAPI?.StopWebServer();

        foreach (var folder in _createdFolders)
            Directory.Delete(folder, true);
    }

    private void InitializeHandlers()
    {
        _restAPI.AddHandler(HttpMethod.POST, "Setup", HandleZipProject)
            .AddErrorRoute<InvalidDataException>(HttpStatusCode.BadRequest);
        _restAPI.AddDataHandler(HttpMethod.GET, CommonContentTypes.ImagePNG, "Preview", GetPreviewImage)
            .AddErrorRoute<InvalidOperationException>(HttpStatusCode.BadRequest);
        
        _restAPI.AddHandler(HttpMethod.POST, "StartRecording", StartRecording);
        _restAPI.AddJsonHandler(HttpMethod.GET, "RecordingProgress", GetRecordingProgress);
        _restAPI.AddHandler(HttpMethod.POST, "StopRecording", StopRecording);
        
        var downloadHandler = _restAPI.AddDataHandler(HttpMethod.GET, "DownloadOutput", DownloadOutputToStream)
            .AddErrorRoute<InvalidOperationException>(HttpStatusCode.BadRequest);
        downloadHandler.AddResponsePostProcessor(async (_, response) =>
        {
            var folder = _mainRandomizer.GetOutputPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folderName = Path.GetFileName(folder);
            response.AddHeader("Content-Disposition", $"attachment; filename=\"{folderName}.zip\"");
        });
        

        _restAPI.AddHandler(new SwaggerHandler(
            _restAPI,
            "docs",
            "https://unpkg.com/swagger-ui-dist/swagger-ui.css",
            "https://unpkg.com/swagger-ui-dist/swagger-ui-bundle.js",
            "Cad2Render API",
            "1.0.0",
            ""));
    }
    
    private void StartRecording() => _mainRandomizer.StartRecording();

    private RecordingProgressState GetRecordingProgress()
    {
        var active = _mainRandomizer.GetRecordingProgress(out int captured, out var total);
        return new RecordingProgressState
        {
            Active = active,
            ImagesCaptured = captured,
            TotalImagesToCapture = total
        };
    }

    private void StopRecording() => _mainRandomizer.StopRecording();
    
    private Task HandleZipProject(Stream zipStream)
    {
        var extractPath = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString());

        Directory.CreateDirectory(extractPath);

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(extractPath);
        
        _createdFolders.Add(extractPath);
        
        var projectRoot = FindProjectRoot(extractPath);

        var jsonFiles = Directory.GetFiles(projectRoot, "*.json", SearchOption.TopDirectoryOnly);

        if (jsonFiles.Length != 1)
        {
            throw new InvalidDataException(
                $"Expected exactly one JSON file in project root, found {jsonFiles.Length}");
        }

        var jsonFile = jsonFiles[0];

        _dataImporter.LoadFromFile(jsonFile, autoCreateOutputDirectory: true);

        return Task.CompletedTask;
    }

    private async Task DownloadOutputToStream(Stream outputStream)
    {
        var outputPath = _mainRandomizer.GetOutputPath();
        if (string.IsNullOrEmpty(outputPath))
            throw new InvalidOperationException("No project loaded.");
        
        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var filePath in Directory.EnumerateFiles(
                     outputPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            var entry = archive.CreateEntry(
                Path.GetRelativePath(outputPath, filePath),
                CompressionLevel.Fastest);

            await using var entryStream = entry.Open();
            await using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            await fileStream.CopyToAsync(entryStream);
        }
    }

    private async Task<byte[]> GetPreviewImage()
    {
        if (_mainRandomizer.IsCapturing)
            throw new InvalidOperationException("Cannot take a preview when you are recording.");
        return await _mainRandomizer.GetPreviewImage();
    }

    private static string FindProjectRoot(string path)
    {
        while (true)
        {
            var directories = Directory.GetDirectories(path);
            var files = Directory.GetFiles(path);

            // Only descend if this folder contains exactly one folder and nothing else
            if (directories.Length == 1 && files.Length == 0)
            {
                path = directories[0];
                continue;
            }

            return path;
        }
    }

    private readonly RestAPI _restAPI = new RestAPI();

    private List<string> _createdFolders = new();
    private RestApiSettingsManager _settingsManager;

    private struct RecordingProgressState
    {
        public bool Active;
        public int ImagesCaptured;
        public int TotalImagesToCapture;
    }
}
