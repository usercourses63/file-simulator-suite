namespace FileSimulator.TestConsole.Models;

public class KafkaTestResult
{
    public string TestName { get; set; } = "";
    public bool Success { get; set; }
    public long DurationMs { get; set; }
    public string Details { get; set; } = "";
    public string? Error { get; set; }
}
