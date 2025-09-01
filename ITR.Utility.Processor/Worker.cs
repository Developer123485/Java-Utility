using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Native;

public class Worker
{
    private readonly string _javaPath;
    private readonly string _jarPath;
    private readonly string _utilityDir;
    private readonly int _processTimeoutMs = 10 * 60 * 1000; // e.g. 10 min

    public Worker(IConfiguration config)
    {
        _javaPath = config["JavaSettings:JavaPath"] ?? "java";
        _jarPath = Path.GetFullPath(config["JavaSettings:JarPath"] ?? "");
        _utilityDir = Path.GetFullPath(config["JavaSettings:UtilityDir"] ?? Directory.GetCurrentDirectory());
    }

    public async Task RunJavaUtility(string filePath, string csiFilePath, string outputPath)
    {
        // Make paths absolute to avoid surprises
        filePath = Path.GetFullPath(filePath);
        csiFilePath = Path.GetFullPath(csiFilePath);
        outputPath = Path.GetFullPath(outputPath);

        if (!File.Exists(_jarPath)) throw new FileNotFoundException("JAR not found", _jarPath);
        if (!File.Exists(filePath)) throw new FileNotFoundException("Input file not found", filePath);
        if (!Directory.Exists(_utilityDir)) throw new DirectoryNotFoundException($"UtilityDir not found: {_utilityDir}");
        if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

        // Build CLI args — adjust ordering if JAR expects different order
        string args = $"-jar \"{_jarPath}\" \"{filePath}\" \"{csiFilePath}\" \"{outputPath}\"";

        var psi = new ProcessStartInfo(_javaPath, args)
        {
            WorkingDirectory = _utilityDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process() { StartInfo = psi };

        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (stdOut) stdOut.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (stdErr) stdErr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exited = await Task.Run(() => process.WaitForExit(_processTimeoutMs));
        if (!exited)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"Java process did not exit within {_processTimeoutMs / 1000} sec.");
        }

        // Optionally inspect output for success keywords if exit code is unreliable
        string outText = stdOut.ToString();
        string errText = stdErr.ToString();

        Console.WriteLine($"[java stdout]\n{outText}");
        if (!string.IsNullOrWhiteSpace(errText)) Console.WriteLine($"[java stderr]\n{errText}");

        if (process.ExitCode != 0)
        {
            throw new Exception($"Java utility exited with code {process.ExitCode}. Stderr: {errText}");
        }

        // If JAR returns exit code 0 but you still want to validate that an output file was created, add checks here.
    }
}

//using System;
//using System.Diagnostics;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Configuration;
//using WindowsInput;
//using WindowsInput.Native;

//public class Worker
//{
//    private readonly string _javaPath;
//    private readonly string _jarPath;
//    private readonly string _utilityDir;

//    public Worker(IConfiguration config)
//    {
//        _javaPath = config["JavaSettings:JavaPath"] ?? "java";
//        _jarPath = config["JavaSettings:JarPath"] ?? "";
//        _utilityDir = config["JavaSettings:UtilityDir"] ?? "";
//    }

//    int guiWaitTime = 5000;
//    int keyWaitTime = 500;

//    public async Task RunJavaUtility(string filePath, string csiFilePath, string outputPath)
//    {
//        try
//        {
//            Console.WriteLine("Starting Java Utility...");

//            var process = new Process();
//            process.StartInfo.FileName = "java";
//            process.StartInfo.Arguments = $"-jar \"{_jarPath}\"";
//            process.StartInfo.WorkingDirectory = _utilityDir;
//            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
//            process.StartInfo.CreateNoWindow = false;
//            process.Start();

//            await Task.Delay(guiWaitTime); // Wait for GUI to load        

//            var sim = new InputSimulator();
//            sim.Keyboard.TextEntry(filePath);
//            Thread.Sleep(keyWaitTime);

//            PressKey(sim, VirtualKeyCode.TAB, 2);
//            sim.Keyboard.TextEntry(csiFilePath.Replace(".txt", ""));
//            Thread.Sleep(keyWaitTime);

//            PressKey(sim, VirtualKeyCode.TAB, 2);
//            sim.Keyboard.TextEntry(outputPath);
//            Thread.Sleep(keyWaitTime);

//            PressKey(sim, VirtualKeyCode.TAB, 5);
//            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);

//            Thread.Sleep(10000);

//            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
//            Thread.Sleep(keyWaitTime);
//            sim.Keyboard.KeyPress(VirtualKeyCode.TAB);
//            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
//            Thread.Sleep(keyWaitTime);
//            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
//            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);

//            Console.WriteLine("Java Utility Completed!");

//            process.CloseMainWindow();  // sends WM_CLOSE to main window
//            process.WaitForExit(guiWaitTime);  // wait up to 5s
//            if (!process.HasExited)
//            {
//                process.Kill();         // force close if still alive
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"Error running Java utility: {ex.Message}");
//            throw; // rethrow so pipeline can handle retry
//        }
//    }

//    private void PressKey(InputSimulator sim, VirtualKeyCode key, int times)
//    {
//        for (int i = 0; i < times; i++)
//        {
//            sim.Keyboard.KeyPress(key);
//            Thread.Sleep(50);
//        }
//    }
//}

