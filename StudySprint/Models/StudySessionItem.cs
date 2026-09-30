using System.Text.Json.Serialization;

namespace StudySprint.Models;

/// <summary>
/// Represents one study_sessions row returned by Supabase.
/// </summary>
public sealed class StudySessionItem
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("subject_id")]
    public Guid? SubjectId { get; set; }

    [JsonPropertyName("goal")]
    public string Goal { get; set; } = string.Empty;

    [JsonPropertyName("session_date")]
    public DateTime SessionDate { get; set; }

    [JsonPropertyName("duration_minutes")]
    public int DurationMinutes { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
