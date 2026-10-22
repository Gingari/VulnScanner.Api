namespace Api.Exceptions;

public sealed class ScanConflictException : Exception
{
    public ScanConflictException(string message) : base(message)
    {
    }
}