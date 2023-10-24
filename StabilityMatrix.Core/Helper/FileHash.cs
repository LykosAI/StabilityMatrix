using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Blake3;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Helper;

public static class FileHash
{
    public static async Task<string> GetHashAsync(
        HashAlgorithm hashAlgorithm,
        Stream stream,
        byte[] buffer,
        Action<ulong>? progress = default
    )
    {
        ulong totalBytesRead = 0;

        using (hashAlgorithm)
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            {
                totalBytesRead += (ulong)bytesRead;
                hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
                progress?.Invoke(totalBytesRead);
            }
            hashAlgorithm.TransformFinalBlock(buffer, 0, 0);
            var hash = hashAlgorithm.Hash;
            if (hash == null || hash.Length == 0)
            {
                throw new InvalidOperationException("Hash algorithm did not produce a hash.");
            }
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    public static async Task<string> GetSha256Async(
        string filePath,
        IProgress<ProgressReport>? progress = default
    )
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Could not find file: {filePath}");
        }

        var totalBytes = Convert.ToUInt64(new FileInfo(filePath).Length);
        var shared = ArrayPool<byte>.Shared;
        var buffer = shared.Rent((int)FileTransfers.GetBufferSize(totalBytes));
        try
        {
            await using var stream = File.OpenRead(filePath);

            var hash = await GetHashAsync(
                    SHA256.Create(),
                    stream,
                    buffer,
                    totalBytesRead =>
                    {
                        progress?.Report(
                            new ProgressReport(
                                totalBytesRead,
                                totalBytes,
                                type: ProgressType.Hashing
                            )
                        );
                    }
                )
                .ConfigureAwait(false);
            return hash;
        }
        finally
        {
            shared.Return(buffer);
        }
    }

    public static async Task<string> GetBlake3Async(
        string filePath,
        IProgress<ProgressReport>? progress = default
    )
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Could not find file: {filePath}");
        }

        var totalBytes = Convert.ToUInt64(new FileInfo(filePath).Length);
        var readBytes = 0ul;
        var shared = ArrayPool<byte>.Shared;
        var buffer = shared.Rent((int)FileTransfers.GetBufferSize(totalBytes));
        try
        {
            await using var stream = File.OpenRead(filePath);
            using var hasher = Hasher.New();
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }
                readBytes += (ulong)bytesRead;
                hasher.Update(buffer.AsSpan(0, bytesRead));
                progress?.Report(new ProgressReport(readBytes, totalBytes));
            }
            return hasher.Finalize().ToString();
        }
        finally
        {
            shared.Return(buffer);
        }
    }

    public static async Task<Hash> GetBlake3Async(
        Stream stream,
        IProgress<ProgressReport>? progress = default
    )
    {
        var totalBytes = Convert.ToUInt64(stream.Length);
        var readBytes = 0ul;
        var shared = ArrayPool<byte>.Shared;
        var buffer = shared.Rent((int)FileTransfers.GetBufferSize(totalBytes));
        try
        {
            using var hasher = Hasher.New();
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }
                readBytes += (ulong)bytesRead;
                hasher.Update(buffer.AsSpan(0, bytesRead));
                progress?.Report(new ProgressReport(readBytes, totalBytes));
            }
            return hasher.Finalize();
        }
        finally
        {
            shared.Return(buffer);
        }
    }

    /// <summary>
    /// Get the Blake3 hash of a span of data with multi-threading.
    /// </summary>
    public static Hash GetBlake3Parallel(ReadOnlySpan<byte> data)
    {
        using var hasher = Hasher.New();
        hasher.UpdateWithJoin(data);
        return hasher.Finalize();
    }

    /// <summary>
    /// Task.Run wrapped <see cref="GetBlake3Parallel"/>
    /// </summary>
    public static Task<Hash> GetBlake3ParallelAsync(ReadOnlyMemory<byte> data)
    {
        return Task.Run(() => GetBlake3Parallel(data.Span));
    }

    /// <summary>
    /// Get the Blake3 hash of a file as memory-mapped with multi-threading.
    /// </summary>
    public static Hash GetBlake3MemoryMappedParallel(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(filePath);
        }

        var totalBytes = Convert.ToInt64(new FileInfo(filePath).Length);

        using var hasher = Hasher.New();

        // Memory map
        using var fileStream = File.OpenRead(filePath);
        using var memoryMappedFile = MemoryMappedFile.CreateFromFile(
            fileStream,
            null,
            totalBytes,
            MemoryMappedFileAccess.Read,
            HandleInheritability.None,
            false
        );

        using var accessor = memoryMappedFile.CreateViewAccessor(
            0,
            totalBytes,
            MemoryMappedFileAccess.Read
        );

        Debug.Assert(accessor.Capacity == fileStream.Length);

        var buffer = new byte[accessor.Capacity];
        accessor.ReadArray(0, buffer, 0, buffer.Length);

        hasher.UpdateWithJoin(buffer);
        return hasher.Finalize();
    }

    /// <summary>
    /// Task.Run wrapped <see cref="GetBlake3MemoryMappedParallel"/>
    /// </summary>
    public static Task<Hash> GetBlake3MemoryMappedParallelAsync(string filePath)
    {
        return Task.Run(() => GetBlake3MemoryMappedParallel(filePath));
    }
}
