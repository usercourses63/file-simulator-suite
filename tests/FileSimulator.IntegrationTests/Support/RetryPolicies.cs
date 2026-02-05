using System.Net.Sockets;
using Polly;
using Polly.Retry;

namespace FileSimulator.IntegrationTests.Support;

/// <summary>
/// Polly retry policies for infrastructure tests.
/// </summary>
public static class RetryPolicies
{
    /// <summary>
    /// Retry policy for HTTP requests with exponential backoff.
    /// Retries on non-success status codes or HttpRequestException.
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts (default: 3)</param>
    /// <param name="baseDelaySeconds">Base delay in seconds (default: 2)</param>
    public static AsyncRetryPolicy<HttpResponseMessage> HttpRetryPolicy(
        int maxAttempts = 3,
        int baseDelaySeconds = 2)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                maxAttempts,
                attempt =>
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(baseDelaySeconds, attempt));
                    Console.WriteLine($"[Retry] HTTP request failed, retrying in {delay.TotalSeconds}s (attempt {attempt}/{maxAttempts})");
                    return delay;
                });
    }

    /// <summary>
    /// Retry policy for server readiness polling.
    /// Retries on false result for up to 60 seconds total.
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts (default: 30)</param>
    /// <param name="delaySeconds">Delay between attempts in seconds (default: 2)</param>
    public static AsyncRetryPolicy<bool> ServerReadinessPolicy(
        int maxAttempts = 30,
        int delaySeconds = 2)
    {
        return Policy
            .HandleResult<bool>(ready => !ready)
            .WaitAndRetryAsync(
                maxAttempts,
                attempt =>
                {
                    Console.WriteLine($"[Retry] Server not ready, retrying in {delaySeconds}s (attempt {attempt}/{maxAttempts})");
                    return TimeSpan.FromSeconds(delaySeconds);
                });
    }

    /// <summary>
    /// Retry policy for TCP connectivity checks.
    /// Retries on false result or SocketException.
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts (default: 5)</param>
    /// <param name="delaySeconds">Delay between attempts in seconds (default: 1)</param>
    public static AsyncRetryPolicy<bool> TcpConnectivityPolicy(
        int maxAttempts = 5,
        int delaySeconds = 1)
    {
        return Policy
            .HandleResult<bool>(connected => !connected)
            .Or<SocketException>()
            .WaitAndRetryAsync(
                maxAttempts,
                attempt =>
                {
                    Console.WriteLine($"[Retry] TCP connection failed, retrying in {delaySeconds}s (attempt {attempt}/{maxAttempts})");
                    return TimeSpan.FromSeconds(delaySeconds);
                });
    }

    /// <summary>
    /// Generic retry policy for async operations that may fail transiently.
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts</param>
    /// <param name="delaySeconds">Delay between attempts in seconds</param>
    public static AsyncRetryPolicy GenericRetryPolicy(
        int maxAttempts = 3,
        int delaySeconds = 2)
    {
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxAttempts,
                attempt =>
                {
                    var delay = TimeSpan.FromSeconds(delaySeconds * attempt);
                    Console.WriteLine($"[Retry] Operation failed, retrying in {delay.TotalSeconds}s (attempt {attempt}/{maxAttempts})");
                    return delay;
                });
    }
}
