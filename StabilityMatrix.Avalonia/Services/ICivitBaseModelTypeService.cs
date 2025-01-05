namespace StabilityMatrix.Avalonia.Services;

public interface ICivitBaseModelTypeService
{
    Task<List<string>> GetBaseModelTypes(bool forceRefresh = false, bool includeAllOption = true);
    void ClearCache();
}
