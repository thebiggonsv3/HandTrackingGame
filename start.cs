using Godot;
using System.Diagnostics;

public partial class start : Node2D
{
    Process pythonProcess;
    // Call this in _Ready() of a bootstrap node.
    public void StartPython()
    {
        pythonProcess = new Process();

        pythonProcess.StartInfo.FileName = "python";
        pythonProcess.StartInfo.Arguments = "res://player/GestureTranslate/gesttureRec.py";
        pythonProcess.StartInfo.UseShellExecute = false;

        pythonProcess.Start();
    }

    public override void _ExitTree()
    {
        try
        {
            pythonProcess.Kill();
        }
        catch { }
    }

}
