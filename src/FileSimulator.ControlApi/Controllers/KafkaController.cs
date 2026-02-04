using Microsoft.AspNetCore.Mvc;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Services;

namespace FileSimulator.ControlApi.Controllers;

/// <summary>
/// REST API controller for Kafka operations.
/// Provides endpoints for topic management, message production/consumption, and consumer groups.
/// </summary>
[ApiController]
[Route("api/kafka")]
public class KafkaController : ControllerBase
{
    private readonly IKafkaAdminService _adminService;
    private readonly IKafkaProducerService _producerService;
    private readonly IKafkaConsumerService _consumerService;
    private readonly ILogger<KafkaController> _logger;

    public KafkaController(
        IKafkaAdminService adminService,
        IKafkaProducerService producerService,
        IKafkaConsumerService consumerService,
        ILogger<KafkaController> logger)
    {
        _adminService = adminService;
        _producerService = producerService;
        _consumerService = consumerService;
        _logger = logger;
    }

    // ===== Topics =====

    /// <summary>
    /// List all topics (excluding internal topics).
    /// </summary>
    [HttpGet("topics")]
    public async Task<ActionResult<IReadOnlyList<TopicInfo>>> GetTopics(CancellationToken ct)
    {
        try
        {
            var topics = await _adminService.GetTopicsAsync(ct);
            return Ok(topics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get topics");
            return StatusCode(503, new { error = "Kafka unavailable", details = ex.Message });
        }
    }

    /// <summary>
    /// Get information about a specific topic.
    /// </summary>
    [HttpGet("topics/{name}")]
    public async Task<ActionResult<TopicInfo>> GetTopic(string name, CancellationToken ct)
    {
        try
        {
            var topic = await _adminService.GetTopicAsync(name, ct);
            if (topic == null)
                return NotFound(new { error = $"Topic '{name}' not found" });
            return Ok(topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get topic {Name}", name);
            return StatusCode(503, new { error = "Kafka unavailable", details = ex.Message });
        }
    }

    /// <summary>
    /// Create a new topic.
    /// </summary>
    [HttpPost("topics")]
    public async Task<ActionResult<TopicInfo>> CreateTopic(
        [FromBody] CreateTopicRequest request,
        CancellationToken ct)
    {
        try
        {
            await _adminService.CreateTopicAsync(request, ct);
            _logger.LogInformation("Created topic {Name} with {Partitions} partitions",
                request.Name, request.Partitions);

            var topic = await _adminService.GetTopicAsync(request.Name, ct);
            return CreatedAtAction(nameof(GetTopic), new { name = request.Name }, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create topic {Name}", request.Name);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a topic.
    /// </summary>
    [HttpDelete("topics/{name}")]
    public async Task<IActionResult> DeleteTopic(string name, CancellationToken ct)
    {
        try
        {
            await _adminService.DeleteTopicAsync(name, ct);
            _logger.LogInformation("Deleted topic {Name}", name);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete topic {Name}", name);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ===== Messages =====

    /// <summary>
    /// Get recent messages from a topic.
    /// </summary>
    [HttpGet("topics/{name}/messages")]
    public async Task<ActionResult<IReadOnlyList<KafkaMessage>>> GetMessages(
        string name,
        [FromQuery] int count = 50,
        CancellationToken ct = default)
    {
        try
        {
            // Validate count
            if (count < 1) count = 1;
            if (count > 1000) count = 1000;

            var messages = await _consumerService.GetRecentMessagesAsync(name, count, ct);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages from topic {Name}", name);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Produce a message to a topic.
    /// </summary>
    [HttpPost("topics/{name}/messages")]
    public async Task<ActionResult<ProduceMessageResult>> ProduceMessage(
        string name,
        [FromBody] ProduceMessageRequest request,
        CancellationToken ct)
    {
        try
        {
            // Ensure topic name matches route
            var actualRequest = request with { Topic = name };
            var result = await _producerService.ProduceAsync(actualRequest, ct);
            _logger.LogDebug("Produced message to topic {Name} at partition {Partition} offset {Offset}",
                name, result.Partition, result.Offset);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to produce message to topic {Name}", name);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ===== Consumer Groups =====

    /// <summary>
    /// List all consumer groups.
    /// </summary>
    [HttpGet("consumer-groups")]
    public async Task<ActionResult<IReadOnlyList<ConsumerGroupInfo>>> GetConsumerGroups(CancellationToken ct)
    {
        try
        {
            var groups = await _adminService.GetConsumerGroupsAsync(ct);
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get consumer groups");
            return StatusCode(503, new { error = "Kafka unavailable", details = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed information about a consumer group.
    /// </summary>
    [HttpGet("consumer-groups/{groupId}")]
    public async Task<ActionResult<ConsumerGroupDetail>> GetConsumerGroup(string groupId, CancellationToken ct)
    {
        try
        {
            var group = await _adminService.GetConsumerGroupDetailAsync(groupId, ct);
            if (group == null)
                return NotFound(new { error = $"Consumer group '{groupId}' not found" });
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get consumer group {GroupId}", groupId);
            return StatusCode(503, new { error = "Kafka unavailable", details = ex.Message });
        }
    }

    /// <summary>
    /// Reset consumer group offsets to earliest or latest.
    /// Group must be inactive (no active consumers).
    /// </summary>
    [HttpPost("consumer-groups/{groupId}/reset")]
    public async Task<IActionResult> ResetOffsets(
        string groupId,
        [FromBody] ResetOffsetsRequest request,
        CancellationToken ct)
    {
        try
        {
            // Ensure groupId matches route
            var actualRequest = request with { GroupId = groupId };
            await _adminService.ResetOffsetsAsync(actualRequest, ct);
            _logger.LogInformation("Reset offsets for consumer group {GroupId} on topic {Topic} to {ResetTo}",
                groupId, request.Topic, request.ResetTo);
            return Ok(new { message = $"Offsets reset for group '{groupId}'" });
        }
        catch (InvalidOperationException ex)
        {
            // Group not empty or not found
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset offsets for group {GroupId}", groupId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a consumer group.
    /// Group must be inactive (no active consumers).
    /// </summary>
    [HttpDelete("consumer-groups/{groupId}")]
    public async Task<IActionResult> DeleteConsumerGroup(string groupId, CancellationToken ct)
    {
        try
        {
            await _adminService.DeleteConsumerGroupAsync(groupId, ct);
            _logger.LogInformation("Deleted consumer group {GroupId}", groupId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete consumer group {GroupId}", groupId);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ===== Health =====

    /// <summary>
    /// Check Kafka broker connectivity.
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult> HealthCheck(CancellationToken ct)
    {
        try
        {
            var healthy = await _adminService.HealthCheckAsync(ct);
            if (healthy)
                return Ok(new { status = "healthy" });
            return StatusCode(503, new { status = "unhealthy" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka health check failed with exception");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }
}
