using Serilog.Events;

namespace Yeek.Security;

public static class HttpContextExtension
{
    public static LogEventLevel GetRequestLogLevel(HttpContext httpContext, double d, Exception? exception)
    {
        if (exception != null || httpContext.Response.StatusCode > 499)
            return LogEventLevel.Error;

        return LogEventLevel.Verbose;
    }
}