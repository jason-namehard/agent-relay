using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentRelay.Core;

public static class RelayJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        Converters = { new UtcTimestampConverter(), new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new JsonException($"Expected {typeof(T).Name}.");
}

public sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().ToUniversalTime();
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
}

public sealed class RelayException(string code, string message, int exitCode = 2) : Exception(message)
{
    public string Code { get; } = code;
    public int ExitCode { get; } = exitCode;
}

public enum TaskState
{
    Created, AwaitingConfirmation, AwaitingManual, Dispatching, Dispatched,
    Acknowledged, Running, Succeeded, Failed, TimedOut, DeliveryUnknown, Cancelled
}

public sealed record AgentRecord(string AgentId, string Host, string ConfigPath, string? CliPath,
    string? HostVersion, string[] InstructionPaths, string ValidationStatus, DateTimeOffset? VerifiedAt);
public sealed record AdapterRecord(string AdapterId, string Executable, string Version, string VerificationStatus,
    DateTimeOffset VerifiedAt, string Evidence);
public sealed record OriginSession(string Host, string SessionId, string ConfigProfile, string Cwd);
public sealed record DispatchRequest(string From, string To, string Content, string Mode, string AdapterId,
    string IdempotencyKey, DateTimeOffset? DeadlineAt = null, OriginSession? OriginSession = null);
public sealed record RelaySettings(int ArtifactTtlDays = 3, int TaskTimeoutMinutes = 30,
    int DeliveryTimeoutSeconds = 30, int MaxDeliveryAttempts = 3);

public sealed class TaskRecord
{
    public required string TaskId { get; set; }
    public required string RequesterId { get; set; }
    public required string RecipientId { get; set; }
    public required string AttemptId { get; set; }
    public required string AdapterId { get; set; }
    public required string Mode { get; set; }
    public required string RequestPath { get; set; }
    public required string ResultPath { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset DeadlineAt { get; set; }
    public TaskState State { get; set; }
    public int Revision { get; set; }
    public int ExecutionAttempt { get; set; } = 1;
    public string ResumeStatus { get; set; } = "manual_required";
    public string DeadlineDelivery { get; set; } = "on_next_command";
    public OriginSession? OriginSession { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? SideEffectsPossible { get; set; }
}

public sealed record TaskView(TaskRecord Task, bool DeadlineExceeded, string NextAction, string Prompt);
public sealed record ArtifactRecord(string ArtifactId, string TaskId, string AttemptId, string Kind,
    string Path, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string State);
public sealed record AuditEvent(long Sequence, string EventId, string TaskId, string AttemptId,
    string Event, string SenderId, DateTimeOffset RecordedAt, string Detail);
public sealed record InboxItem(long NotificationId, string TaskId, string Event, string Message,
    DateTimeOffset CreatedAt, bool IsRead);
public sealed record OutboxItem(string TaskId, string AttemptId, string State, int Submissions,
    DateTimeOffset? LeaseUntil, string? Receipt);
public sealed record DeliveryResult(string Status, string? Receipt, string? Error, bool Retryable = false);
public sealed record CleanupResult(int Eligible, int Deleted, string[] Paths);

public interface IDeliveryAdapter
{
    string Id { get; }
    Task<DeliveryResult> DeliverAsync(TaskRecord task, string home, TimeSpan timeout, CancellationToken cancellationToken);
}
