using Dapper;
using Npgsql;

namespace Yeek.Database;

public static class Migrator
{
    private static TaskCompletionSource _completionSource = new TaskCompletionSource();
    public static Task MigrationComplete = _completionSource.Task;

    public static async Task<bool> Migrate(NpgsqlDataSource context, string prefix, ILogger logger)
    {
        logger.LogDebug("Migrating with prefix {Prefix}", prefix);
        await using var connection = await context.OpenConnectionAsync();
        var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync("""
                                CREATE TABLE IF NOT EXISTS SchemaVersions (
                                    SchemaVersionID INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                    ScriptName TEXT NOT NULL,
                                    Applied TIMESTAMP NOT NULL
                                );
                                """, transaction: transaction);

        var appliedScripts = connection.Query<string>(
            "SELECT ScriptName FROM SchemaVersions",
            transaction: transaction);

        var scriptsToApply = MigrationFileScriptList(prefix).ExceptBy(appliedScripts, s => s.name).OrderBy(x => x.name);

        var success = true;
        foreach (var (name, script) in scriptsToApply)
        {
            logger.LogInformation("Applying migration {Transaction}!", name);
            await transaction.SaveAsync(name);

            try
            {
                var code = script.Up(connection);

                await connection.ExecuteAsync(code, transaction: transaction);

                await connection.ExecuteAsync(
                    "INSERT INTO SchemaVersions (ScriptName, Applied) VALUES (@Script, NOW());",
                    new { Script = name },
                    transaction);

                await transaction.ReleaseAsync(name);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception during migration {Transaction}, rolling back...!", name);
                await transaction.RollbackAsync(name);
                success = false;
                break;
            }
        }

        logger.LogInformation("Committing migrations");
        await transaction.CommitAsync();
        _completionSource.SetResult();
        return success;
    }

    private static IEnumerable<(string name, IMigrationScript)> MigrationFileScriptList(string prefix)
    {
        var assembly = typeof(Migrator).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".sql") || !resourceName.StartsWith(prefix))
                continue;

            var index = resourceName.LastIndexOf('.', resourceName.Length - 5, resourceName.Length - 4);
            index += 1;

            var name = resourceName[(index + "Script".Length)..^4];

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName)!);
            var scriptContents = reader.ReadToEnd();
            yield return (name, new FileMigrationScript(scriptContents));
        }
    }

    public interface IMigrationScript
    {
        string Up(NpgsqlConnection connection);
    }

    private sealed class FileMigrationScript : IMigrationScript
    {
        private readonly string _code;

        public FileMigrationScript(string code) => _code = code;

        public string Up(NpgsqlConnection connection) => _code;
    }
}