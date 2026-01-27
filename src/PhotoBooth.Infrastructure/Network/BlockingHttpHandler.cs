using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoBooth.Domain.Exceptions;

namespace PhotoBooth.Infrastructure.Network;

public class BlockingHttpHandler : DelegatingHandler
{
    private readonly ILogger<BlockingHttpHandler> _logger;
    private readonly bool _blockRequests;

    public BlockingHttpHandler(ILogger<BlockingHttpHandler> logger, IOptions<NetworkSecurityOptions> options)
    {
        _logger = logger;
        _blockRequests = options.Value.BlockOutboundRequests;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_blockRequests)
        {
            _logger.LogWarning("Blocked outbound HTTP request to {Uri}", request.RequestUri);
            throw new OutboundNetworkBlockedException(request.RequestUri!);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
