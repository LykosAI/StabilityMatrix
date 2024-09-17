using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Force.Crc32;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Helpers;

public static class PngDataHelper
{
    private static readonly byte[] Idat = { 0x49, 0x44, 0x41, 0x54 };
    private static readonly byte[] Text = { 0x74, 0x45, 0x58, 0x74 };
    private static readonly byte[] Iend = { 0x49, 0x45, 0x4E, 0x44 };

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
        using var memoryStream = new MemoryStream();
        var position = 8; // Skip the PNG signature
        memoryStream.Write(inputImage, 0, position);

        var metadataInserted = false;

        while (position < inputImage.Length)
        {
            var chunkLength = BitConverter.ToInt32(
                inputImage[position..(position + 4)].Reverse().ToArray(),
                0
            );
            var chunkType = Encoding.ASCII.GetString(inputImage[(position + 4)..(position + 8)]);

            switch (chunkType)
            {
                case "IHDR":
                {
                    var imageWidthBytes = inputImage[(position + 8)..(position + 12)];
                    var imageHeightBytes = inputImage[(position + 12)..(position + 16)];
                    var imageWidth = BitConverter.ToInt32(imageWidthBytes.Reverse().ToArray());
                    var imageHeight = BitConverter.ToInt32(imageHeightBytes.Reverse().ToArray());

                    generationParameters.Width = imageWidth;
                    generationParameters.Height = imageHeight;
                    break;
                }
                case "IDAT" when !metadataInserted:
                {
                    var smprojJson = JsonSerializer.Serialize(projectDocument);
                    var smprojChunk = BuildTextChunk("smproj", smprojJson);

                    var paramsData =
                        $"{generationParameters.PositivePrompt}\nNegative prompt: {generationParameters.NegativePrompt}\n"
                        + $"Steps: {generationParameters.Steps}, Sampler: {generationParameters.Sampler}, "
                        + $"CFG scale: {generationParameters.CfgScale}, Seed: {generationParameters.Seed}, "
                        + $"Size: {generationParameters.Width}x{generationParameters.Height}, "
                        + $"Model hash: {generationParameters.ModelHash}, Model: {generationParameters.ModelName}";
                    var paramsChunk = BuildTextChunk("parameters", paramsData);

                    var paramsJson = JsonSerializer.Serialize(generationParameters);
                    var paramsJsonChunk = BuildTextChunk("parameters-json", paramsJson);

                    memoryStream.Write(paramsChunk, 0, paramsChunk.Length);
                    memoryStream.Write(paramsJsonChunk, 0, paramsJsonChunk.Length);
                    memoryStream.Write(smprojChunk, 0, smprojChunk.Length);

                    metadataInserted = true; // Ensure we only insert the metadata once
                    break;
                }
            }

            // Write the current chunk to the output stream
            memoryStream.Write(inputImage, position, chunkLength + 12); // Write the length, type, data, and CRC
            position += chunkLength + 12;
        }

        return memoryStream.ToArray();
    }

    public static byte[] RemoveMetadata(byte[] inputImage)
    {
        using var memoryStream = new MemoryStream();
        var position = 8; // Skip the PNG signature
        memoryStream.Write(inputImage, 0, position);

        while (position < inputImage.Length)
        {
            var chunkLength = BitConverter.ToInt32(
                inputImage[position..(position + 4)].Reverse().ToArray(),
                0
            );
            var chunkType = Encoding.ASCII.GetString(inputImage[(position + 4)..(position + 8)]);

            // If the chunk is not a text chunk, write it to the output
            if (chunkType != "tEXt" && chunkType != "zTXt" && chunkType != "iTXt")
            {
                memoryStream.Write(inputImage, position, chunkLength + 12); // Write the length, type, data, and CRC
            }

            // Move to the next chunk
            position += chunkLength + 12;
        }

        return memoryStream.ToArray();
    }

    private static byte[] BuildTextChunk(string key, string value)
    {
        var textData = $"{key}\0{value}";
        var dataBytes = Encoding.UTF8.GetBytes(textData);
        var textDataLength = BitConverter.GetBytes(dataBytes.Length).Reverse();
        var textDataBytes = Text.Concat(dataBytes).ToArray();
        var crc = BitConverter.GetBytes(Crc32Algorithm.Compute(textDataBytes)).Reverse();

        return textDataLength.Concat(textDataBytes).Concat(crc).ToArray();
    }
}
