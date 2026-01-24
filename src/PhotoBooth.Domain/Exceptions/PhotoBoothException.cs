namespace PhotoBooth.Domain.Exceptions;

public class PhotoBoothException : Exception
{
    public PhotoBoothException(string message) : base(message)
    {
    }

    public PhotoBoothException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class CameraNotAvailableException : PhotoBoothException
{
    public CameraNotAvailableException() : base("Camera is not available")
    {
    }

    public CameraNotAvailableException(string message) : base(message)
    {
    }

    public CameraNotAvailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class PhotoNotFoundException : PhotoBoothException
{
    public PhotoNotFoundException(string code) : base($"Photo with code '{code}' not found")
    {
        Code = code;
    }

    public PhotoNotFoundException(Guid id) : base($"Photo with id '{id}' not found")
    {
        PhotoId = id;
    }

    public string? Code { get; }
    public Guid? PhotoId { get; }
}
