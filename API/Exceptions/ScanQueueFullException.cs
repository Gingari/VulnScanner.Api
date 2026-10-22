namespace Api.Exceptions;

public sealed class ScanQueueFullException : Exception
{
    public ScanQueueFullException()
        : base("Очередь сканирования заполнена. Повторите запрос позже.")
    {
    }
}