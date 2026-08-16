using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubgMortarRanger.Configuration;

public sealed class AtomicJsonFile<T> where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
        };

    private readonly string _path;
    private readonly SemaphoreSlim _gate;

    public AtomicJsonFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _gate = AtomicJsonPathLocks.Get(_path);
    }

    public Task<T?> ReadAsync(CancellationToken cancellationToken = default) =>
        ExecuteExclusiveAsync(ReadCoreAsync, cancellationToken);

    public Task WriteAsync(
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ExecuteExclusiveAsync(
            token => WriteCoreAsync(value, token),
            cancellationToken);
    }

    internal async Task<TResult> ExecuteExclusiveAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task ExecuteExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<T?> ReadCoreAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        await using var stream = new FileStream(
            _path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            SerializerOptions,
            cancellationToken);
    }

    internal async Task WriteCoreAsync(
        T value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    value,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

internal static class AtomicJsonPathLocks
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates =
        new(StringComparer.OrdinalIgnoreCase);

    public static SemaphoreSlim Get(string path) =>
        Gates.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
}
