using Microsoft.AspNetCore.SignalR;

namespace ExamForge.SignalRServer.Hubs;

public class SessionHub : Hub
{
    public async Task JoinSession(string sessionId, string studentId, string studentName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Group($"monitor_{sessionId}").SendAsync("StudentJoined", new
        {
            StudentId = studentId,
            StudentName = studentName,
            ConnectionId = Context.ConnectionId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task SendHeartbeat(string sessionId, string studentId, object payload)
    {
        await Clients.Group($"monitor_{sessionId}").SendAsync("StudentHeartbeat", new
        {
            StudentId = studentId,
            Payload = payload,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task ReportEvent(string sessionId, string studentId, string studentName, string eventType, string details)
    {
        var severity = eventType switch
        {
            "multiple_login" => "Critical",
            "tab_switch" => "Warning",
            "disconnect" => "Warning",
            _ => "Info"
        };

        await Clients.Group($"monitor_{sessionId}").SendAsync("IntegrityEvent", new
        {
            StudentId = studentId,
            StudentName = studentName,
            EventType = eventType,
            Severity = severity,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task MonitorSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"monitor_{sessionId}");
    }

    public async Task SendCommand(string sessionId, string studentId, string command, object data)
    {
        // Broadcast to the session; clients can filter by studentId
        await Clients.Group(sessionId).SendAsync("ReceiveCommand", new
        {
            StudentId = studentId,
            Command = command,
            Data = data,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastMessage(string sessionId, string message)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveMessage", new
        {
            Message = message,
            From = "Teacher",
            Timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Optionally notify monitors of disconnects
        await base.OnDisconnectedAsync(exception);
    }
}