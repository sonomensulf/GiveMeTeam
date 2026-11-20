// Middleware стоит «вокруг» всех запросов и ловит исключения
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);   // выполняем остальную цепочку (контроллер и т.д.)
        }
        catch (BadHttpRequestException)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { code = "validation_error", message = "Некорректный формат запроса" });
        }
        catch (ApiException ex)  // наши «ожидаемые» ошибки
        {
            ctx.Response.StatusCode = ex.StatusCode;
            await ctx.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message });
        }
        catch (Exception ex)     // всё непредвиденное -> 500, детали не раскрываем
        {
            log.LogError(ex, "Необработанная ошибка");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { code = "internal_error", message = "Внутренняя ошибка сервера" });
        }
    }
}