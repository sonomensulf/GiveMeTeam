// Базовый класс: несёт HTTP-статус и машинный код ошибки
public abstract class ApiException(int statusCode, string code, string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

// 400: данные не прошли проверку
public class ValidationException(string code, string message)
    : ApiException(StatusCodes.Status400BadRequest, code, message);

// 409: данные корректны, но нарушено бизнес-правило
public class ConflictException(string code, string message)
    : ApiException(StatusCodes.Status409Conflict, code, message);

// 404: ресурс не найден
public class NotFoundException(string code, string message)
    : ApiException(StatusCodes.Status404NotFound, code, message);

// 403: недостаточно прав
public class ForbiddenException(string code, string message)
    : ApiException(StatusCodes.Status403Forbidden, code, message);