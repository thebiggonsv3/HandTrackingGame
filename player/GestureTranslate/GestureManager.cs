using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;

public partial class GestureManager : Node
{
    // Use only for debugging
    private const bool VerboseLogs = false;
    // How long to hold the input action for jump and move gestures, in seconds.
    private const float JumpHoldSeconds = 0.15f;
    private const float MoveHoldSeconds = 0.12f;

    // Queue to hold gestures received from the gesture client, ensuring thread safety.
    private readonly Queue<string> _pendingGestures = new Queue<string>();
    private readonly object _gestureQueueLock = new object();
    // Event to signal when the Python tracker is ready to accept connections.
    private readonly ManualResetEventSlim _pythonTrackerReady = new ManualResetEventSlim(false);

    private gestureClient _client;
    private Player _player;
    private Process _pythonProcess;

    // Track the last state of F1, F2, and F3 keys to detect key presses for testing gestures.
    private bool _f1PressedLast;
    private bool _f2PressedLast;
    private bool _f3PressedLast;

    // When the node is ready, set up the gesture client and start the Python hand tracker process.
    public override void _Ready()
    {
        SetProcess(true);
        SetPhysicsProcess(true);

        _player = GetNode<Player>("../Player");

        // Start the tracker process first, then connect the client once the Python side is ready.
        KillOldHandTrackerProcesses();
        StartHandTrackerProcess();

        _client = new gestureClient();
        _client.OnGesture += HandleGesture;

        if (!_pythonTrackerReady.Wait(5000))
            GD.PrintErr("GestureManager: timed out waiting for Python tracker to listen on port 5000");

        GD.PrintErr($"GestureManager: client connected={_client.Connect()}");
    }

    // Kills the gesture app if still running when game closes
    public override void _ExitTree()
    {
        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            try
            {
                _pythonProcess.Kill(true);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"GestureManager: failed to kill Python tracker on exit: {ex.Message}");
            }
        }

        KillOldHandTrackerProcesses();
    }

    // Remove older tracker instances so only one hand-tracking process stays active.
    private void KillOldHandTrackerProcesses()
    {
        try
        {
            // Query for processes with command line containing "HandTracker.py" to identify old instances.
            var query = new SelectQuery("Win32_Process", "CommandLine LIKE '%HandTracker.py%'");
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject process in searcher.Get())
            {
                // Check if the process has a valid ProcessId
                if (process["ProcessId"] is int pid)
                {
                    // Skip killing the current Python process if it's still running.
                    if (_pythonProcess != null && pid == _pythonProcess.Id)
                        continue;

                    // Attempt to kill the old process and log the result.
                    try
                    {
                        var oldProcess = Process.GetProcessById(pid);
                        oldProcess.Kill(true);
                        GD.PrintErr($"GestureManager: killed old HandTracker process PID={pid}");
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"GestureManager: failed to kill old HandTracker PID={pid}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"GestureManager: error scanning for old HandTracker processes: {ex.Message}");
        }
    }

    public override void _Process(double delta)
    {
        ProcessQueuedGestures();

        // Keyboard shortcuts keep manual testing simple without changing real gameplay input.
        bool f1 = Input.IsKeyPressed(Key.A);
        bool f2 = Input.IsKeyPressed(Key.D);
        bool f3 = Input.IsKeyPressed(Key.Space);

        if (f1 && !_f1PressedLast)
            HandleGestureMainThread("move_left");

        if (f2 && !_f2PressedLast)
            HandleGestureMainThread("move_right");

        if (f3 && !_f3PressedLast)
            HandleGestureMainThread("jump");

        _f1PressedLast = f1;
        _f2PressedLast = f2;
        _f3PressedLast = f3;
    }

    private (string Executable, string Arguments) ResolvePythonCommand()
    {
        string[] candidates = new[]
        {
            System.Environment.GetEnvironmentVariable("PYTHON312"),
            System.Environment.GetEnvironmentVariable("PYTHON"),
            System.Environment.GetEnvironmentVariable("PYTHON3"),
            "python3.12",
            "python3.12.exe",
            "py",
            "python3",
            "python"
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (IsPython312Available(candidate))
                return candidate.Equals("py", StringComparison.OrdinalIgnoreCase)
                    ? (candidate, "-3.12 -u")
                    : (candidate, "-u");
        }

        return ("python3.12", "-u");
    }

    private bool IsPython312Available(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = command.Equals("py", StringComparison.OrdinalIgnoreCase) ? "-3.12 --version" : "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(2000);
            return process.ExitCode == 0 && output.IndexOf("3.12", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private void StartHandTrackerProcess()
    {
        try
        {
            // Start the Python hand tracker process using the specified script and Python executable.
            var scriptPath = ProjectSettings.GlobalizePath("res://player/HandTracking/HandTracker.py");
            var pythonCommand = ResolvePythonCommand();
            var psi = new ProcessStartInfo
            {
                FileName = pythonCommand.Executable,
                Arguments = $"{pythonCommand.Arguments} \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(scriptPath),
            };

            _pythonProcess = Process.Start(psi);
            if (_pythonProcess != null)
            {
                _pythonProcess.OutputDataReceived += (sender, e) =>
                {
                    // e.Data is null when the process ends, so we check for that before processing.
                    if (string.IsNullOrEmpty(e.Data))
                        return;

                    if (!_pythonTrackerReady.IsSet && e.Data.Contains("HandTracker: listening on 127.0.0.1:5000"))
                        _pythonTrackerReady.Set();
                };

                _pythonProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        GD.PrintErr($"Python stderr: {e.Data}");
                };

                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();
                GD.PrintErr($"GestureManager: started Python tracker process, PID={_pythonProcess.Id} script={scriptPath}");
            }
            else
            {
                GD.PrintErr("GestureManager: failed to start Python tracker process (returned null)");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"GestureManager: failed to start hand tracker: {ex.Message}");
        }
    }

    // Handles messages received from the gesture server
    private void HandleGesture(string action)
    {
        if (string.IsNullOrEmpty(action))
            return;

        lock (_gestureQueueLock)
        {
            _pendingGestures.Enqueue(action);
        }
    }

    // Processes queued gestures
    private void ProcessQueuedGestures()
    {
        while (true)
        {
            string action;
            lock (_gestureQueueLock)
            {
                if (_pendingGestures.Count == 0)
                    break;

                action = _pendingGestures.Dequeue();
            }

            HandleGestureMainThread(action);
        }
    }

    // Handles gestures on the main thread to ensure thread safety with Godot's API.
    private void HandleGestureMainThread(string action)
    {
        if (_player == null)
        {
            GD.PrintErr("GestureManager: player reference is null in HandleGestureMainThread");
            return;
        }

        // Register the gesture action with the player systems and trigger the corresponding input actions.
        bool registered = false;
        switch (action)
        {
            case "jump":
                _player.Movement.RequestGestureJump();
                PulseAction("jump", JumpHoldSeconds);
                registered = true;
                break;

            case "move_left":
            case "left":
                _player.Movement.RequestGestureDirection(1f);
                PulseAction("move_left", MoveHoldSeconds);
                registered = true;
                break;

            case "left_up":
            case "move_left_up":
                _player.Movement.RequestGestureDirection(1f);
                _player.Movement.RequestGestureJump();
                registered = true;
                break;
            case "top_left":
                _player.Movement.RequestGestureDirection(1f);
                _player.Movement.RequestGestureJump();
                registered = true;
                break;

            case "move_right":
            case "right":
                _player.Movement.RequestGestureDirection(-1f);
                PulseAction("move_right", MoveHoldSeconds);
                registered = true;
                break;

            case "right_up":
            case "move_right_up":
                _player.Movement.RequestGestureDirection(-1f);
                _player.Movement.RequestGestureJump();
                registered = true;
                break;
            case "top_right":
                _player.Movement.RequestGestureDirection(-1f);
                _player.Movement.RequestGestureJump();
                registered = true;
                break;

            case "attack":
                if (_player.AnimatedSprite.Animation != "attack" || !_player.AnimatedSprite.IsPlaying())
                {
                    _player.AnimatedSprite.Play("attack");
                    registered = true;
                }
                break;

            default:
                if (VerboseLogs)
                    GD.Print($"GestureManager: unknown action '{action}'");
                break;
        }

        if (registered)
            GD.PrintErr($"GestureManager: gesture registered '{action}'");
    }

    // Triggers a pulse action for the specified duration.
    private async void PulseAction(string action, float holdSeconds)
    {
        Input.ActionPress(action);
        await ToSignal(GetTree().CreateTimer(holdSeconds), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease(action);
    }
}