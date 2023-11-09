using System;
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
        var imageWidthBytes = inputImage[0x10..0x14];
        var imageHeightBytes = inputImage[0x14..0x18];
        var imageWidth = BitConverter.ToInt32(imageWidthBytes.Reverse().ToArray());
        var imageHeight = BitConverter.ToInt32(imageHeightBytes.Reverse().ToArray());

        generationParameters.Width = imageWidth;
        generationParameters.Height = imageHeight;

        var idatIndex = SearchBytes(inputImage, Idat);
        var iendIndex = SearchBytes(inputImage, Iend);

        var textEndIndex = idatIndex - 4; // go back 4 cuz we don't want the length
        var existingData = inputImage[..textEndIndex];

        var smprojJson = JsonSerializer.Serialize(projectDocument);
        var smprojChunk = BuildTextChunk("smproj", smprojJson);

        var paramsData =
            $"{generationParameters.PositivePrompt}\nNegative prompt: {generationParameters.NegativePrompt}\n"
            + $"Steps: {generationParameters.Steps}, Sampler: {generationParameters.Sampler}, "
            + $"CFG scale: {generationParameters.CfgScale}, Seed: {generationParameters.Seed}, "
            + $"Size: {imageWidth}x{imageHeight}, "
            + $"Model hash: {generationParameters.ModelHash}, Model: {generationParameters.ModelName}";
        var paramsChunk = BuildTextChunk("parameters", paramsData);

        var paramsJson = JsonSerializer.Serialize(generationParameters);
        var paramsJsonChunk = BuildTextChunk("parameters-json", paramsJson);

        // Go back 4 from the idat index because we need the length of the data
        idatIndex -= 4;

        // Go forward 8 from the iend index because we need the crc
        iendIndex += 8;
        var actualImageData = inputImage[idatIndex..iendIndex];

        var finalImage = existingData
            .Concat(paramsChunk)
            .Concat(paramsJsonChunk)
            .Concat(smprojChunk)
            .Concat(actualImageData);

        return finalImage.ToArray();
    }

    public static byte[] RemoveMetadata(byte[] inputImage)
    {
        var firstTextIndex = SearchBytes(inputImage, Text);
        if (firstTextIndex == -1)
            return inputImage;

        // Don't want the size bytes either
        firstTextIndex -= 4;
        var existingHeader = inputImage[..firstTextIndex];

        // Go back 4 from the idat index because we need the length of the data
        var idatIndex = SearchBytes(inputImage, Idat) - 4;

        // Go forward 8 from the iend index because we need the crc
        var iendIndex = SearchBytes(inputImage, Iend) + 8;

        var actualImageData = inputImage[idatIndex..iendIndex];
        var finalImage = existingHeader.Concat(actualImageData);

        return finalImage.ToArray();
    }

    private static byte[] BuildTextChunk(string key, string value)
    {
        var textData = $"{key}\0{value}";
        var dataBytes = Encoding.UTF8.GetBytes(textData);
        var textDataLength = BitConverter.GetBytes(dataBytes.Length).Reverse();
        var textDataBytes = Text.Concat(dataBytes).ToArray();
        var crc = BitConverter.GetBytes(Crc32Algorithm.Compute(textDataBytes));

        return textDataLength.Concat(textDataBytes).Concat(crc).ToArray();
    }

    private static int SearchBytes(byte[] haystack, byte[] needle)
    {
        var limit = haystack.Length - needle.Length;
        for (var i = 0; i <= limit; i++)
        {
            var k = 0;
            for (; k < needle.Length; k++)
            {
                if (needle[k] != haystack[i + k])
                    break;
            }

            if (k == needle.Length)
                return i;
        }

        return -1;
    }
}
