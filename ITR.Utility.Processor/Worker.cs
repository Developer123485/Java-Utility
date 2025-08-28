using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WindowsInput;
using WindowsInput.Native;

public class Worker
{
    private readonly string _javaPath;
    private readonly string _jarPath;
    private readonly string _utilityDir;

    public Worker(IConfiguration config)
    {
        _javaPath = config["JavaSettings:JavaPath"] ?? "java";
        _jarPath = config["JavaSettings:JarPath"] ?? "";
        _utilityDir = config["JavaSettings:UtilityDir"] ?? "";
    }

    int guiWaitTime = 3000;
    int keyWaitTime = 500;

    public async Task RunJavaUtility(string filePath, string csiFilePath, string outputPath)
    {
        try
        {
            Console.WriteLine("Starting Java Utility...");

            var process = new Process();
            process.StartInfo.FileName = "java";
            process.StartInfo.Arguments = $"-jar \"{_jarPath}\"";
            process.StartInfo.WorkingDirectory = _utilityDir;
            process.StartInfo.UseShellExecute = false;
            process.Start();

            await Task.Delay(guiWaitTime); // Wait for GUI to load        

            var sim = new InputSimulator();
            sim.Keyboard.TextEntry(filePath);
            Thread.Sleep(keyWaitTime);

            PressKey(sim, VirtualKeyCode.TAB, 2);
            sim.Keyboard.TextEntry(csiFilePath.Replace(".txt", ""));
            Thread.Sleep(keyWaitTime);

            PressKey(sim, VirtualKeyCode.TAB, 2);
            sim.Keyboard.TextEntry(outputPath);
            Thread.Sleep(keyWaitTime);

            PressKey(sim, VirtualKeyCode.TAB, 5);
            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);

            Thread.Sleep(10000);

            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
            Thread.Sleep(keyWaitTime);
            sim.Keyboard.KeyPress(VirtualKeyCode.TAB);
            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
            Thread.Sleep(keyWaitTime);
            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
            sim.Keyboard.KeyPress(VirtualKeyCode.SPACE);

            Console.WriteLine("Java Utility Completed!");

            process.CloseMainWindow();  // sends WM_CLOSE to main window
            process.WaitForExit(guiWaitTime);  // wait up to 5s
            if (!process.HasExited)
            {
                process.Kill();         // force close if still alive
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running Java utility: {ex.Message}");
            throw; // rethrow so pipeline can handle retry
        }
    }

    private void PressKey(InputSimulator sim, VirtualKeyCode key, int times)
    {
        for (int i = 0; i < times; i++)
        {
            sim.Keyboard.KeyPress(key);
            Thread.Sleep(50);
        }
    }
}
