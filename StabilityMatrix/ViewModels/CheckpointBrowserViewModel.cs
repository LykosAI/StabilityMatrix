using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Api;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointBrowserViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ICivitApi civitApi;
    
    [ObservableProperty]
    private string? searchQuery;

    [ObservableProperty] 
    private ObservableCollection<CivitModel> civitModels;

    [ObservableProperty] 
    private bool showNsfw;

    public CheckpointBrowserViewModel(ICivitApi civitApi)
    {
        this.civitApi = civitApi;
    }

    [RelayCommand]
    private async Task SearchModels()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }
        
        var models = await civitApi.GetModels(new CivitModelsRequest
        {
            Query = SearchQuery,
            Limit = 10,
            Nsfw = ShowNsfw.ToString().ToLower(),
            Sort = CivitSortMode.HighestRated
        });
        
        CivitModels = new ObservableCollection<CivitModel>(models.Items);
        
        Logger.Debug($"Found {models.Items.Length} models");
    }
    
}
