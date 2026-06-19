namespace PhotoBooth.Domain.Exceptions;

public class StorageException : PhotoBoothException
{
    public StorageException(string message) : base(message)
    {
    }

    public StorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
