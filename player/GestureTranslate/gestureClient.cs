using Godot;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

public class gestureClient
{
    // Connects to app to listen for gestures
    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _thread;

    public Action<string> OnGesture;

    // Tries to connect to the gesture server
    public bool Connect()
    {
        const int maxAttempts = 20;
        // If successful, starts a thread to listen for messages from the server
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                _client = new TcpClient { NoDelay = true };
                _client.Connect("127.0.0.1", 5000);
                _stream = _client.GetStream();

                _thread = new Thread(Listen) { IsBackground = true };
                _thread.Start();
                GD.PrintErr("gestureClient: connected and listening");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"gestureClient: connect attempt {attempt + 1}/{maxAttempts} failed: {ex.Message}");
                Thread.Sleep(200);
            }
        }

        GD.PrintErr("gestureClient: could not connect to 127.0.0.1:5000 after retries");
        return false;
    }

    // Listens for messages from the gesture server
    private void Listen()
    {
        // Reads messages from server
        using var reader = new StreamReader(_stream, Encoding.UTF8);
        while (true)
        {
            // Tries to read lines from server (infinite loop until connection is closed)
            try
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                HandleMessage(line);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"gestureClient: listen error: {ex.Message}");
                break;
            }
        }
    }

    // Handles messages received from the gesture server
    private void HandleMessage(string msg)
    {
        // Attempts to decode the JSON message and invoke player actions if it's a valid gesture message
        try
        {
            var json = JsonSerializer.Deserialize<GestureMessage>(msg);
            if (json?.type == "gesture")
                OnGesture?.Invoke(json.action);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"gestureClient: failed to parse message: {ex.Message}");
        }
    }
}

// Returns the type from JSON message and action to do, using set to allow for deserialization from JSON
public class GestureMessage
{
    public string type { get; set; }
    public string action { get; set; }
}
