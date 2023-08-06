using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Sentry;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manager for the current inference client
/// Has observable shared properties for shared info like model names
/// </summary>
public partial class InferenceClientManager : ObservableObject, IInferenceClientManager
{
    private readonly IApiFactory apiFactory;
    
    // Current 
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsConnected))]
    private ComfyClient? client;
    
    [MemberNotNullWhen(true, nameof(Client))]
    public bool IsConnected => Client is not null;
    
    [ObservableProperty]
    private IReadOnlyCollection<string>? modelNames;

    [ObservableProperty]
    private IReadOnlyCollection<string>? samplers;
    
    public InferenceClientManager(IApiFactory apiFactory)
    {
        this.apiFactory = apiFactory;
    }

    private async Task LoadSharedPropertiesAsync()
    {
        if (!IsConnected) throw new InvalidOperationException("Client is not connected");
        
        ModelNames = await Client.GetModelNamesAsync();
        Samplers = await Client.GetSamplerNamesAsync();
    }
    
    protected void ClearSharedProperties()
    {
        ModelNames = null;
        Samplers = null;
    }

    public async Task ConnectAsync()
    {
        if (IsConnected) return;
        
        var tempClient = new ComfyClient(apiFactory, new Uri("http://127.0.0.1:8188"));
        await tempClient.ConnectAsync();
        Client = tempClient;
        await LoadSharedPropertiesAsync();
    }
    
    public async Task ConnectAsync(PackagePair packagePair)
    {
        if (IsConnected) return;

        if (packagePair.BasePackage is not ComfyUI)
        {
            throw new ArgumentException("Base package is not ComfyUI", nameof(packagePair));
        }

        var tempClient = new ComfyClient(apiFactory, new Uri("http://127.0.0.1:8188"));

        // Add output dir if available
        if (packagePair.InstalledPackage.FullPath is { } path)
        {
            tempClient.OutputImagesDir = new DirectoryPath(path, "output");
        }
        
        await tempClient.ConnectAsync();
        Client = tempClient;
        await LoadSharedPropertiesAsync();
    }
    
    public async Task CloseAsync()
    {
        if (!IsConnected) return;
        
        await Client.CloseAsync();
        Client = null;
        ClearSharedProperties();
    }
    
    public void Dispose()
    {
        Client?.Dispose();
        Client = null;
        GC.SuppressFinalize(this);
    }
    
    ~InferenceClientManager()
    {
        Dispose();
    }
}
