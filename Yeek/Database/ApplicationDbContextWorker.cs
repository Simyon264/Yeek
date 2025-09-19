using Npgsql;

namespace Yeek.Database;

/// <summary>
/// Handles the main npgsql datasource
/// </summary>
public class ApplicationDbContextWorker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ApplicationDbContext _dbContext;

    public ApplicationDbContextWorker(IConfiguration configuration, ILoggerFactory loggerFactory, ApplicationDbContext dbContext)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _dbContext = dbContext;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = new NpgsqlDataSourceBuilder(_configuration.GetConnectionString("default"))
            .UseLoggerFactory(_loggerFactory);

        _dbContext.DataSource = builder.Build();

        var result = await Migrator.Migrate(_dbContext.DataSource, "Yeek.Database.Migrations", _loggerFactory.CreateLogger(typeof(Migrator)));

        if (!result)
            throw new InvalidOperationException("Failed to migrate");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _dbContext.DataSource.DisposeAsync();
    }
}