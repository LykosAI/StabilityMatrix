using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Models.Packages;

public class A3WebUI: BasePackage
{
    public override string Name => "stable-diffusion-webui";
    public override string DisplayName => "Stable Diffusion WebUI";
    public override string Author => "AUTOMATIC1111";
    public override string GithubUrl => "https://github.com/AUTOMATIC1111/stable-diffusion-webui";
    public override async Task DownloadPackage()
    {
        var githubApi = RestService.For<IGithubApi>("https://api.github.com");
        var latestRelease = await githubApi.GetLatestRelease("AUTOMATIC1111", "stable-diffusion-webui");
        var downloadUrl = $"https://api.github.com/repos/AUTOMATIC1111/stable-diffusion-webui/zipball/{latestRelease.TagName}";

        if (!Directory.Exists(DownloadLocation.Replace($"{Name}.zip", "")))
        {
            Directory.CreateDirectory(DownloadLocation.Replace($"{Name}.zip", ""));
        }
        
        using var client = new HttpClient {Timeout = TimeSpan.FromMinutes(5)};
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "1.0"));
        await using var file = new FileStream(DownloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);

        long contentLength = 0;
        var retryCount = 0;
        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        while (contentLength == 0 && retryCount++ < 5)
        {
            response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            contentLength = response.Content.Headers.ContentLength ?? 0;
            Debug.WriteLine("Retrying...");
            Thread.Sleep(50);
        }

        var isIndeterminate = contentLength == 0;
        
        await using var stream = await response.Content.ReadAsStreamAsync();
        var totalBytesRead = 0;
        while (true)
        {
            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
            await file.WriteAsync(buffer, 0, bytesRead);
            
            totalBytesRead += bytesRead;

            if (isIndeterminate)
            {
                OnDownloadProgressChanged(-1);
            }
            else
            {
                var progress = (int) (totalBytesRead * 100d / contentLength);
                Debug.WriteLine($"Progress; {progress}");
                OnDownloadProgressChanged(progress);
            }
        }

        await file.FlushAsync();
        OnDownloadComplete(DownloadLocation);
    }

    public string CommandLineArgs => $"{GetVramOption()} {GetXformersOption()}";
    
    private static string GetVramOption()
    {
        var vramGb = HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024;

        return vramGb switch
        {
            < 4 => "--lowvram",
            < 8 => "--medvram",
            _ => string.Empty
        };
    }
    
    private static string GetXformersOption()
    {
        var gpuName = HardwareHelper.GetGpuChipName();
        return gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "--xformers" : string.Empty;
    }
}
