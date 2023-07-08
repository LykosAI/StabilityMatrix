using StabilityMatrix.Models;

namespace StabilityMatrix.DesignData;

public class MockCheckpointFolder : CheckpointFolder
{
    public MockCheckpointFolder() : base(null!, null!, null!, null!, useCategoryVisibility: false)
    {
    }
}
