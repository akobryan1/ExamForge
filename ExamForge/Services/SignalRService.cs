using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ExamForge.Services;

/// <summary>
/// SignalR client service for real-time communication with exam server
/// </summary>
public class SignalRService : IDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    // Events for incoming messages
    public event Action<object>? OnStudentJoined;
    public event Action<object>? OnStudentHeartbeat;
    public event Action<object>? OnIntegrityEvent;
    public event Action<string, string, string, string>? OnReportEvent; // sessionId, studentId, eventType, details

    public SignalRService(string hubUrl)
    {
        _hubUrl = hubUrl;
    }

    /// <summary>
    /// Initialize connection and register event handlers
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_connection != null)
        {
            Debug.WriteLine("⚠️ Already connected or connecting");
            return;
        }

        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Register incoming message handlers
            _connection.On<object>("StudentJoined", data =>
            {
                Debug.WriteLine($"📥 StudentJoined: {data}");
                OnStudentJoined?.Invoke(data);
            });

            _connection.On<object>("StudentHeartbeat", data =>
            {
                Debug.WriteLine($"💓 StudentHeartbeat: {data}");
                OnStudentHeartbeat?.Invoke(data);
            });

            _connection.On<string, string, string, string>("ReportEvent", async (sessionId, studentId, studentName, payloadJson) =>
            {
                Debug.WriteLine($"🔔 ReportEvent received: {sessionId} {studentId} {payloadJson}");
                // Forward to local handler so UI can log or persist
                OnIntegrityEvent?.Invoke(new { sessionId, studentId, studentName, payloadJson });
            });

            _connection.On<object>("IntegrityEvent", data =>
            {
                Debug.WriteLine($"⚠️ IntegrityEvent: {data}");
                OnIntegrityEvent?.Invoke(data);
            });

            // New: server can request client to report an event that should be logged to Firestore
            _connection.On<string, string, string, string>("RequestReportEvent", (sessionId, studentId, eventType, details) =>
            {
                Debug.WriteLine($"🔔 RequestReportEvent: {eventType} for {studentId} in {sessionId}");
                OnReportEvent?.Invoke(sessionId, studentId, eventType, details);
            });

            _connection.Closed += async (error) =>
            {
                Debug.WriteLine($"❌ Connection closed: {error?.Message}");
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await ConnectAsync();
            };

            await _connection.StartAsync();
            Debug.WriteLine($"✅ Connected to SignalR hub: {_hubUrl}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to connect: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Teacher starts monitoring a specific exam session
    /// </summary>
    public async Task MonitorSessionAsync(string sessionId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to SignalR hub");
        }

        await _connection.InvokeAsync("MonitorSession", sessionId);
        Debug.WriteLine($"👁️ Monitoring session: {sessionId}");
    }

    /// <summary>
    /// Teacher sends command to specific student
    /// </summary>
    public async Task SendCommandAsync(string sessionId, string studentId, string command, object data)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to SignalR hub");
        }

        await _connection.InvokeAsync("SendCommand", sessionId, studentId, command, data);
        Debug.WriteLine($"📤 Sent command '{command}' to student {studentId}");
    }

    /// <summary>
    /// Teacher broadcasts message to all students in session
    /// </summary>
    public async Task BroadcastMessageAsync(string sessionId, string message)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to SignalR hub");
        }

        await _connection.InvokeAsync("BroadcastMessage", sessionId, message);
        Debug.WriteLine($"📢 Broadcast message to session {sessionId}");
    }

    /// <summary>
    /// Disconnect from SignalR hub
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
            Debug.WriteLine("🔌 Disconnected from SignalR hub");
        }
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}