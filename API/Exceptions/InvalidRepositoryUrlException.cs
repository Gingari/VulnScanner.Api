namespace Api.Exceptions;

public sealed class InvalidRepositoryUrlException : Exception
{
    public InvalidRepositoryUrlException()
        : base("Укажите HTTPS-ссылку на репозиторий разрешённого хостинга.")
    {
    }
}