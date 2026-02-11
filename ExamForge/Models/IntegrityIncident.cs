using System;

namespace ExamForge.Models
{
    public class IntegrityIncident
    {
        public string Id { get; set; } = "";
        public string ExamId { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string IncidentType { get; set; } = "";
        public string Severity { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Details { get; set; } = "";
        public string Status { get; set; } = "Pending";
    }
}