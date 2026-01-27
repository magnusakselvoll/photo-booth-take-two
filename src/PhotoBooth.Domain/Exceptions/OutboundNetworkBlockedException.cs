namespace PhotoBooth.Domain.Exceptions;

public class OutboundNetworkBlockedException : PhotoBoothException
{
    public OutboundNetworkBlockedException(Uri requestUri)
        : base($"Outbound network access is blocked. Attempted request to: {requestUri}")
    {
        RequestUri = requestUri;
    }

    public Uri RequestUri { get; }
}
