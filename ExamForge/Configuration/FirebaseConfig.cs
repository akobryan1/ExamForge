namespace ExamForge.Configuration;

public class FirebaseConfig
{
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountKeyPath { get; set; } = "firebase-adminsdk.json";
    public string StorageBucket { get; set; } = string.Empty;
    public string HostingUrl { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;
}