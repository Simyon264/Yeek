using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using TickerQ.Utilities.Base;
using Yeek.Database;
using Yeek.Security.Model;

namespace Yeek.FileHosting.JavaScript;

public class ScriptService(ApplicationDbContext dbContext, IServiceScopeFactory serviceScopeFactory)
{
    public static readonly ConcurrentDictionary<Guid, Job> Jobs = new();

    /// <summary>
    /// Starts a script Job, returning its ID.
    /// </summary>
    /// <returns>The ID for the just started job.</returns>
    public Guid StartJob(string script, Guid user, bool apply)
    {
        var guid = Guid.CreateVersion7();
        var channel = Channel.CreateUnbounded<string>();
        channel.Writer.TryWrite("[OK] Waiting for job to be picked up...");
        Jobs.TryAdd(guid, new Job(channel, script, user, apply));
        return guid;
    }


    [TickerFunction(functionName: "ProcessJsJobs", "*/1 * * * *")]
    public async Task ProcessJobs(CancellationToken token)
    {
        if (Jobs.IsEmpty)
            return;

        // https://github.com/nuskey8/luau-dotnet (tried, currently has compilation issues)
        // https://www.moonsharp.org/ (tried, no good async interop support)

        var finished = new List<Guid>();

        try
        {
            foreach (var (jobId, job) in Jobs)
            {
                var constraints = new V8RuntimeConstraints
                {
                    MaxOldSpaceSize = 2 * 1024 * 1024,
                    MaxNewSpaceSize = 1 * 1024 * 1024,
                    MaxArrayBufferAllocation = 1 * 1024 * 1024,
                };

                using var runtime = new V8Runtime("ScriptRunner", constraints);
                runtime.MaxHeapSize = 2 * 1024 * 1024;

                V8ScriptEngine? engine = null;
                CancellationTokenRegistration? reg = null;

                try
                {
                    engine = runtime.CreateScriptEngine(
                        V8ScriptEngineFlags.EnableTaskPromiseConversion |
                        V8ScriptEngineFlags.EnableDateTimeConversion);
                    engine.MaxRuntimeHeapSize = 2 * 1024 * 1024;
                    engine.MaxRuntimeStackUsage = 512 * 1024;
                    engine.DefaultAccess = ScriptAccess.ReadOnly;

                    var logger = new
                    {
                        info = new Func<string, Task>(async msg => { await job.Channel.Writer.WriteAsync($"[INFO] {msg}"); }),
                        error = new Func<string, Task>(async msg => { await job.Channel.Writer.WriteAsync($"[ERROR] {msg}"); })
                    };

                    var context = new ScriptContext(dbContext, serviceScopeFactory, engine);

                    engine.AddHostObject("log", logger);
                    engine.AddHostObject("context", context);

                    var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                    reg = timeoutCts.Token.Register(() =>
                    {
                        try
                        {
                            engine?.Interrupt();
                        }
                        catch
                        {
                            // ignored
                        }
                    });

                    try
                    {
                        engine.Execute(job.Code);

                        if (engine.Script.run is not null)
                        {
                            try
                            {
                                var task = JavaScriptExtensions.ToTask(engine.Script.run());
                                await task;
                                await job.Channel.Writer.WriteAsync("[OK] Script completed successfully.", token);
                                await context.ProcessChanges(job.Channel, job.ApplyChanges, job.UserId, job.Code);
                            }
                            catch (ScriptEngineException ex)
                            {
                                await job.Channel.Writer.WriteAsync($"[ERROR] {ex.Message}", token);
                            }
                        }
                        else
                        {
                            await job.Channel.Writer.WriteAsync("[ERROR] No async function 'run' found.", token);
                        }
                    }
                    catch (ScriptInterruptedException)
                    {
                        await job.Channel.Writer.WriteAsync("[WARN] Script execution timed out.", token);
                    }
                    catch (Exception ex)
                    {
                        if (ex is IScriptEngineException awa)
                        {
                            await job.Channel.Writer.WriteAsync($"[ERROR] {ex.Message}", token);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                catch (Exception)
                {
                    await job.Channel.Writer.WriteAsync("[ERROR] Script execution failed.", token);
                    throw;
                }
                finally
                {
                    reg?.Dispose();
                    engine?.Dispose();

                    await job.Channel.Writer.WriteAsync("[OK] Script execution finished.", token);
                    job.Channel.Writer.TryComplete();
                    finished.Add(jobId);
                }
            }
        }
        finally
        {
            foreach (var guid in finished)
            {
                Jobs.TryRemove(guid, out _);
            }
        }
    }
}

public class Job(Channel<string> channel, string code, Guid userId, bool applyChanges)
{
    public Channel<string> Channel { get; set; } = channel;
    public string Code { get; set; } = code;
    public Guid UserId { get; set; } = userId;
    public bool ApplyChanges { get; set; } = applyChanges;
}