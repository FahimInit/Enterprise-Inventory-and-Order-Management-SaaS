namespace Inventory.Domain.Common;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName}.NotFound", $"{entityName} with identifier '{key}' was not found.")
    {
    }

    public NotFoundException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}

public sealed class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException(string message = "You do not have permission to perform this operation.")
        : base("Security.Forbidden", message)
    {
    }

    public ForbiddenAccessException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}
