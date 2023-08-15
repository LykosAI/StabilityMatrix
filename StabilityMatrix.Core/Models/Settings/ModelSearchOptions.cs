using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Models.Settings;

public record ModelSearchOptions(CivitPeriod SelectedPeriod, CivitSortMode SortMode, CivitModelType SelectedModelType, string SelectedBaseModelType);
