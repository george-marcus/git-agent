using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitAgent.Models;
using GitAgent.Providers;

namespace GitAgent.Services;

public interface IJsonRpcServer
{
    Task StartAsync(int port, CancellationToken cancellationToken);
}

public class JsonRpcServer : IJsonRpcServer
{
    private readonly IProviderFactory _providerFactory;
    private readonly IGitInspector _gitInspector;
    private readonly ISafetyValidator _safetyValidator;
    private readonly IConflictResolver _conflictResolver;

    public JsonRpcServer(IProviderFactory providerFactory, IGitInspector gitInspector, ISafetyValidator safetyValidator, IConflictResolver conflictResolver)
    {
        _providerFactory = providerFactory;
        _gitInspector = gitInspector;
        _safetyValidator = safetyValidator;
        _conflictResolver = conflictResolver;
    }

    public async Task StartAsync(int port, CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        Console.WriteLine($"JSON-RPC server listening on port {port}");
        Console.WriteLine("Press Ctrl+C to stop");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line == null) break;

                    var response = await ProcessRequestAsync(line);
                    await writer.WriteLineAsync(response);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = CreateErrorResponse(null, -32603, ex.Message);
                await writer.WriteLineAsync(errorResponse);
            }
        }
    }

    private async Task<string> ProcessRequestAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson, JsonRpcJsonContext.Default.JsonRpcRequest);
            if (request == null)
            {
                return CreateErrorResponse(null, -32700, "Parse error");
            }

            object? result = request.Method switch
            {
                "git-agent/run" => await HandleRunAsync(request.Params),
                "git-agent/conflicts" => await HandleConflictsAsync(request.Params),
                "git-agent/suggest" => await HandleSuggestAsync(request.Params),
                "git-agent/status" => await HandleStatusAsync(),
                "git-agent/providers" => HandleProviders(),
                _ => throw new InvalidOperationException($"Unknown method: {request.Method}")
            };

            return CreateSuccessResponse(request.Id, result);
        }
        catch (JsonException)
        {
            return CreateErrorResponse(null, -32700, "Parse error");
        }
        catch (InvalidOperationException ex)
        {
            return CreateErrorResponse(null, -32601, ex.Message);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(null, -32603, ex.Message);
        }
    }

    private async Task<RunResult> HandleRunAsync(JsonElement? paramsElement)
    {
        var instruction = paramsElement?.GetProperty("instruction").GetString()
            ?? throw new InvalidOperationException("instruction is required");

        var providerName = paramsElement?.TryGetProperty("provider", out var p) == true
            ? p.GetString()
            : null;

        var provider = string.IsNullOrEmpty(providerName)
            ? await _providerFactory.CreateProviderAsync()
            : await _providerFactory.CreateProviderAsync(providerName);

        var context = await _gitInspector.BuildRepoContextAsync();
        var commands = await provider.GenerateGitCommands(instruction, context);
        var validated = _safetyValidator.FilterAndAnnotate(commands);

        return new RunResult
        {
            Commands = validated.ToList(),
            Context = new ContextSummary
            {
                Branch = context.CurrentBranch,
                HasUncommittedChanges = !string.IsNullOrWhiteSpace(context.StatusPorcelain),
                MergeState = context.MergeState.ToString()
            }
        };
    }

    private async Task<ConflictsResult> HandleConflictsAsync(JsonElement? paramsElement)
    {
        var context = await _gitInspector.BuildRepoContextAsync();
        var analysis = await _conflictResolver.AnalyzeConflictsAsync(context);

        return new ConflictsResult
        {
            MergeState = context.MergeState.ToString(),
            TotalConflicts = analysis.TotalConflicts,
            Files = analysis.Files.Select(f => new FileConflictSummary
            {
                Path = f.FilePath,
                ConflictCount = f.ConflictCount
            }).ToList()
        };
    }

    private async Task<SuggestResult> HandleSuggestAsync(JsonElement? paramsElement)
    {
        var context = await _gitInspector.BuildRepoContextAsync();
        var resolutions = await _conflictResolver.SuggestResolutionsAsync(context);

        return new SuggestResult
        {
            Resolutions = resolutions.Select(r => new ResolutionSummary
            {
                FilePath = r.FilePath,
                Strategy = r.Strategy.ToString(),
                Description = r.Description
            }).ToList()
        };
    }

    private async Task<StatusResult> HandleStatusAsync()
    {
        var context = await _gitInspector.BuildRepoContextAsync();

        return new StatusResult
        {
            Branch = context.CurrentBranch,
            Status = context.StatusPorcelain,
            LastCommit = context.LastCommit,
            MergeState = context.MergeState.ToString(),
            ConflictCount = context.ConflictedFiles.Count
        };
    }

    private ProvidersResult HandleProviders()
    {
        return new ProvidersResult
        {
            Providers = _providerFactory.AvailableProviders.ToList()
        };
    }

    private static string CreateSuccessResponse(object? id, object? result)
    {
        var response = new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
        return JsonSerializer.Serialize(response, JsonRpcJsonContext.Default.JsonRpcResponse);
    }

    private static string CreateErrorResponse(object? id, int code, string message)
    {
        var response = new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message }
        };
        return JsonSerializer.Serialize(response, JsonRpcJsonContext.Default.JsonRpcErrorResponse);
    }
}

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

public class JsonRpcErrorResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class RunResult
{
    [JsonPropertyName("commands")]
    public List<GeneratedCommand> Commands { get; set; } = [];

    [JsonPropertyName("context")]
    public ContextSummary? Context { get; set; }
}

public class ContextSummary
{
    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "";

    [JsonPropertyName("hasUncommittedChanges")]
    public bool HasUncommittedChanges { get; set; }

    [JsonPropertyName("mergeState")]
    public string MergeState { get; set; } = "";
}

public class ConflictsResult
{
    [JsonPropertyName("mergeState")]
    public string MergeState { get; set; } = "";

    [JsonPropertyName("totalConflicts")]
    public int TotalConflicts { get; set; }

    [JsonPropertyName("files")]
    public List<FileConflictSummary> Files { get; set; } = [];
}

public class FileConflictSummary
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("conflictCount")]
    public int ConflictCount { get; set; }
}

public class SuggestResult
{
    [JsonPropertyName("resolutions")]
    public List<ResolutionSummary> Resolutions { get; set; } = [];
}

public class ResolutionSummary
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = "";

    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

public class StatusResult
{
    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("lastCommit")]
    public string LastCommit { get; set; } = "";

    [JsonPropertyName("mergeState")]
    public string MergeState { get; set; } = "";

    [JsonPropertyName("conflictCount")]
    public int ConflictCount { get; set; }
}

public class ProvidersResult
{
    [JsonPropertyName("providers")]
    public List<string> Providers { get; set; } = [];
}

