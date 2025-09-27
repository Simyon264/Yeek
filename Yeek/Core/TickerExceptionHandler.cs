using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces;

namespace Yeek.Core;

public class TickerExceptionHandler : ITickerExceptionHandler
{
    private readonly ILogger<TickerExceptionHandler> _logger;

    public TickerExceptionHandler(ILogger<TickerExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleExceptionAsync(Exception exception, Guid tickerId, TickerType tickerType)
    {
        _logger.LogError("Job failed! {TickerId} - {Exception}", tickerId, exception.ToString());
    }

    public async Task HandleCanceledExceptionAsync(Exception exception, Guid tickerId, TickerType tickerType)
    {
        _logger.LogError("Job failed! {TickerId} - {Exception}", tickerId, exception.ToString());
    }
}