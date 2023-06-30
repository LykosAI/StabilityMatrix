using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Models;

public record ModelSearchOptions(CivitPeriod SelectedPeriod, CivitSortMode SortMode, CivitModelType SelectedModelType);
