namespace Nexus.Gateway.HealthProber;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public sealed record ServiceUnhealthyIntegrationEvent(
    string ServiceName,
    DateTime OccurredAt,
    string Reason);

public sealed class SagaRollbackPublisher : ISagaRollbackPublisher
{
    private readonly ILogger<SagaRollbackPublisher> _logger;

    public SagaRollbackPublisher(ILogger<SagaRollbackPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishServiceUnhealthyEventAsync(string serviceName, string reason, CancellationToken cancellationToken = default)
    {
        var evt = new ServiceUnhealthyIntegrationEvent(serviceName, DateTime.UtcNow, reason);
        _logger.LogWarning("[SAGA ROLLBACK TRIGGERED] Published ServiceUnhealthyIntegrationEvent for service '{Service}'. Reason: {Reason}", serviceName, reason);

        // Khi có MassTransit IBus, gọi _bus.Publish(evt, cancellationToken).
        // Tạm thời log sự kiện sẵn sàng cho Consumer của Commerce / Saga Orchestrator.
        return Task.CompletedTask;
    }
}
