namespace Nexus.Gateway.Common;

/// <summary>
/// Expected invalid HTTP input. Only use for request validation owned by the gateway;
/// upstream gRPC and unexpected failures retain their existing exception mapping.
/// </summary>
public sealed class GatewayInputException : Exception
{
    public GatewayInputException(string message, string field = "General", string errorCode = "VAL_INVALID_INPUT")
        : base(message)
    {
        Field = field;
        ErrorCode = errorCode;
    }

    public string Field { get; }
    public string ErrorCode { get; }
}
