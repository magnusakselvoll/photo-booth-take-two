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
