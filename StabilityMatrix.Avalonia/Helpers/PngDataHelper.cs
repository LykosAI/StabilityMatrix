using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Force.Crc32;
using NLog;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Helpers;

public static class PngDataHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Idat = { 0x49, 0x44, 0x41, 0x54 };
    private static readonly byte[] Text = { 0x74, 0x45, 0x58, 0x74 };
    private static readonly byte[] Iend = { 0x49, 0x45, 0x4E, 0x44 };
    private static readonly byte[] InternationalText = { 0x69, 0x54, 0x58, 0x74 };

    private static readonly JsonSerializerOptions UnicodeJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static byte[] AddMetadata(
        Stream inputStream,
        GenerationParameters generationParameters,
        InferenceProjectDocument projectDocument
    )
    {
        using var ms = new MemoryStream();
        inputStream.CopyTo(ms);
        return AddMetadata(ms.ToArray(), generationParameters, projectDocument);
    }

    public static byte[] AddMetadata(
        byte[] inputImage,
        GenerationParameters generationParameters,
        InferenceProjectDocument projectDocument
    )
    {
        // Validate PNG header
        if (inputImage.Length < 8 || !inputImage[..8].AsSpan().SequenceEqual(PngHeader))
        {
            Logger.Warn(
                "AddMetadata: Image data ({Size} bytes) does not have a valid PNG header, "
                    + "the file may not actually be a PNG. Returning image as-is",
                inputImage.Length
            );
            return inputImage;
        }

        using var memoryStream = new MemoryStream();
        var position = 8; // Skip the PNG signature
        memoryStream.Write(inputImage, 0, position);

        var metadataInserted = false;

        while (position + 12 <= inputImage.Length)
        {
            var chunkLength = BitConverter.ToInt32(
                inputImage[position..(position + 4)].AsEnumerable().Reverse().ToArray(),
                0
            );

            var totalChunkSize = chunkLength + 12; // 4 (length) + 4 (type) + data + 4 (CRC)

            // Validate chunk bounds
            if (chunkLength < 0 || position + totalChunkSize > inputImage.Length)
            {
                // Malformed chunk — write remaining bytes as-is and stop parsing
                Logger.Warn(
                    "Malformed PNG chunk at position {Position}: declared length {ChunkLength} "
                        + "exceeds image size {ImageSize}. Image may be truncated or corrupted",
                    position,
                    chunkLength,
                    inputImage.Length
                );
                memoryStream.Write(inputImage, position, inputImage.Length - position);
                break;
            }

            var chunkType = Encoding.ASCII.GetString(inputImage[(position + 4)..(position + 8)]);

            switch (chunkType)
            {
                case "IHDR":
                {
                    var imageWidthBytes = inputImage[(position + 8)..(position + 12)];
                    var imageHeightBytes = inputImage[(position + 12)..(position + 16)];
                    var imageWidth = BitConverter.ToInt32(imageWidthBytes.AsEnumerable().Reverse().ToArray());
                    var imageHeight = BitConverter.ToInt32(
                        imageHeightBytes.AsEnumerable().Reverse().ToArray()
                    );

                    generationParameters.Width = imageWidth;
                    generationParameters.Height = imageHeight;
                    break;
                }
                case "IDAT" when !metadataInserted:
                {
                    var smprojJson = JsonSerializer.Serialize(projectDocument, UnicodeJsonOptions);
                    var smprojChunk = BuildTextChunk("smproj", smprojJson);

                    var paramsData =
                        $"{generationParameters.PositivePrompt}\nNegative prompt: {generationParameters.NegativePrompt}\n"
                        + $"Steps: {generationParameters.Steps}, Sampler: {generationParameters.Sampler}, "
                        + $"CFG scale: {generationParameters.CfgScale}, Seed: {generationParameters.Seed}, "
                        + $"Size: {generationParameters.Width}x{generationParameters.Height}, "
                        + $"Model hash: {generationParameters.ModelHash}, Model: {generationParameters.ModelName}";
                    var paramsChunk = BuildTextChunk("parameters", paramsData);

                    var paramsJson = JsonSerializer.Serialize(generationParameters, UnicodeJsonOptions);
                    var paramsJsonChunk = BuildTextChunk("parameters-json", paramsJson);

                    memoryStream.Write(paramsChunk, 0, paramsChunk.Length);
                    memoryStream.Write(paramsJsonChunk, 0, paramsJsonChunk.Length);
                    memoryStream.Write(smprojChunk, 0, smprojChunk.Length);

                    metadataInserted = true; // Ensure we only insert the metadata once
                    break;
                }
            }

            // Write the current chunk to the output stream
            memoryStream.Write(inputImage, position, totalChunkSize); // Write the length, type, data, and CRC
            position += totalChunkSize;
        }

        return memoryStream.ToArray();
    }

    public static byte[] RemoveMetadata(byte[] inputImage)
    {
        // Validate PNG header
        if (inputImage.Length < 8 || !inputImage[..8].AsSpan().SequenceEqual(PngHeader))
        {
            Logger.Warn(
                "RemoveMetadata: Image data ({Size} bytes) does not have a valid PNG header, "
                    + "the file may not actually be a PNG. Returning image as-is",
                inputImage.Length
            );
            return inputImage;
        }

        using var memoryStream = new MemoryStream();
        var position = 8; // Skip the PNG signature
        memoryStream.Write(inputImage, 0, position);

        while (position + 12 <= inputImage.Length)
        {
            var chunkLength = BitConverter.ToInt32(
                inputImage[position..(position + 4)].AsEnumerable().Reverse().ToArray(),
                0
            );

            var totalChunkSize = chunkLength + 12; // 4 (length) + 4 (type) + data + 4 (CRC)

            // Validate chunk bounds
            if (chunkLength < 0 || position + totalChunkSize > inputImage.Length)
            {
                // Malformed chunk — write remaining bytes as-is and stop parsing
                Logger.Warn(
                    "Malformed PNG chunk at position {Position}: declared length {ChunkLength} "
                        + "exceeds image size {ImageSize}. Image may be truncated or corrupted",
                    position,
                    chunkLength,
                    inputImage.Length
                );
                memoryStream.Write(inputImage, position, inputImage.Length - position);
                break;
            }

            var chunkType = Encoding.ASCII.GetString(inputImage[(position + 4)..(position + 8)]);

            // If the chunk is not a text chunk, write it to the output
            if (chunkType != "tEXt" && chunkType != "zTXt" && chunkType != "iTXt")
            {
                memoryStream.Write(inputImage, position, totalChunkSize); // Write the length, type, data, and CRC
            }

            // Move to the next chunk
            position += totalChunkSize;
        }

        return memoryStream.ToArray();
    }

    private static byte[] BuildTextChunk(string key, string value)
    {
        // Use iTXt chunk for non-Latin-1 characters (per PNG specification,
        // tEXt chunks only support Latin-1 / ISO 8859-1 encoding)
        if (value.Any(c => c > 0xFF))
        {
            return BuildInternationalTextChunk(key, value);
        }

        var textData = $"{key}\0{value}";
        var dataBytes = Encoding.UTF8.GetBytes(textData);
        var textDataLength = BitConverter.GetBytes(dataBytes.Length).AsEnumerable().Reverse().ToArray();
        var textDataBytes = Text.Concat(dataBytes).ToArray();
        var crc = BitConverter
            .GetBytes(Crc32Algorithm.Compute(textDataBytes))
            .AsEnumerable()
            .Reverse()
            .ToArray();

        return textDataLength.Concat(textDataBytes).Concat(crc).ToArray();
    }

    private static byte[] BuildInternationalTextChunk(string key, string value)
    {
        // iTXt chunk format (uncompressed):
        // Keyword(Latin-1) \0 CompressionFlag(0) CompressionMethod(0) LanguageTag \0 TranslatedKeyword \0 Text(UTF-8)
        var keyBytes = Encoding.Latin1.GetBytes(key);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        byte[] dataBytes = [.. keyBytes, 0, 0, 0, 0, 0, .. valueBytes];
        var dataLength = BitConverter.GetBytes(dataBytes.Length).AsEnumerable().Reverse().ToArray();
        var chunkTypeAndData = InternationalText.Concat(dataBytes).ToArray();
        var crc = BitConverter
            .GetBytes(Crc32Algorithm.Compute(chunkTypeAndData))
            .AsEnumerable()
            .Reverse()
            .ToArray();

        return dataLength.Concat(chunkTypeAndData).Concat(crc).ToArray();
    }
}
