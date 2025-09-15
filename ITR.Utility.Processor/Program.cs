using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    private static string inputDir = "";
    private static string processingDir = "";
    private static string outputDir = "";
    private static string errorDir = "";
    private static int retryCount = 3;
    private static Worker _worker;
    static async Task Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _worker = new Worker(config);

        inputDir = config["WatchSettings:InputDirectory"] ?? "";
        processingDir = config["WatchSettings:ProcessingDirectory"] ?? "";
        outputDir = config["WatchSettings:OutputDirectory"] ?? "";
        errorDir = config["WatchSettings:ErrorDirectory"] ?? "";
        retryCount = int.TryParse(config["WatchSettings:RetryCount"], out int r) ? r : 3;

        EnsureDirectory(inputDir);
        EnsureDirectory(processingDir);
        EnsureDirectory(outputDir);
        EnsureDirectory(errorDir);

        Console.WriteLine($"📂 Watching: {inputDir}");

        using var watcher = new FileSystemWatcher(inputDir);
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
        watcher.Filter = "*.*";
        watcher.Created += OnCreated;
        watcher.EnableRaisingEvents = true;

        Console.WriteLine("✅ Press Ctrl+C to exit.");
        await Task.Delay(-1);
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        // ✅ Get only the file name, not the whole path
        string fileName = Path.GetFileName(e.Name);

        // ✅ Always combine with Path.Combine
        string processingPath = Path.Combine(processingDir, fileName);
        string outputPath = Path.Combine(outputDir);
        string errorPath = Path.Combine(errorDir);

        // ✅ Normalize paths (good for logging and avoiding mixed slashes)
        processingPath = Path.GetFullPath(processingPath);
        outputPath = Path.GetFullPath(outputPath);
        errorPath = Path.GetFullPath(errorPath);


        try
        {
            WaitForFile(e.FullPath);

            if (!File.Exists(processingPath))
            {
                File.Copy(e.FullPath, processingPath);
                Console.WriteLine($"➡️ Copied to Processing: {processingPath}");
            }
            else
            {
                Console.WriteLine($"⚠️ Already in Processing, skipping: {processingPath}");
                return;
            }


            bool success = false;
            if (e.FullPath.EndsWith(".txt"))
            {
                for (int attempt = 1; attempt <= retryCount; attempt++)
                {
                    try
                    {
                        Console.WriteLine($"🔄 Processing attempt {attempt} for {fileName}");
                        Process(processingPath, outputPath);
                        success = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error in processing: {ex.Message}");
                        Task.Delay(2000).Wait();
                    }
                }
            }

            if (success)
            {
                if (File.Exists(e.FullPath)) File.Delete(e.FullPath);
                if (File.Exists(processingPath))
                    File.Move(processingPath, outputPath, overwrite: true);

                Console.WriteLine($"✅ File moved to Output: {outputPath}");
            }
            else
            {
                if (File.Exists(e.FullPath)) File.Delete(e.FullPath);
                if (File.Exists(processingPath))
                    File.Move(processingPath, errorPath, overwrite: true);

                Console.WriteLine($"❌ File moved to Errors: {errorPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Fatal error: {ex.Message}");
        }
    }

    private static void Process(string processingPath, string outputPath)
    {
        // ⚙️ Call Java utility instead of placeholder
        _worker.RunJavaUtility(
            filePath: processingPath,
            csiFilePath: processingPath + ".csi", // Example mapping
            outputPath: outputPath
        ).GetAwaiter().GetResult();
    }

    
    private static void WaitForFile(string path)
    {
        int retries = 10;
        while (retries > 0)
        {
            try
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                if (stream.Length > 0) break;
            }
            catch
            {
                Task.Delay(500).Wait();
            }
            retries--;
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
