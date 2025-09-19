using System.Diagnostics;
using Npgsql;

namespace Yeek.Database;

public class ApplicationDbContext
{
    private NpgsqlDataSource? _dataSource;

    public NpgsqlDataSource DataSource
    {
        get
        {
            if (_dataSource == null)
                throw new InvalidOperationException("Connection not initialized");

            return _dataSource;
        }

        set
        {
            if (_dataSource != null)
                throw new InvalidOperationException("Connection already initialized");

            _dataSource = value;
        }
    }
}