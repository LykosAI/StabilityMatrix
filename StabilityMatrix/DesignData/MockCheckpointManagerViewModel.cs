using System.ComponentModel;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.DesignData;

[DesignOnly(true)]
public class MockCheckpointManagerViewModel : CheckpointManagerViewModel
{
    public MockCheckpointManagerViewModel() : base(null!)
    {
        CheckpointFolders = new()
        {
            new()
            {
                Title = "Stable Diffusion",
                CheckpointFiles = new()
                {
                    new()
                    {
                        Title = "Stable Diffusion v1.5",
                        FileName = "v1-5-pruned-emaonly.safetensors",
                    },
                    new()
                    {
                        Title = "Scenery Mix",
                        FileName = "scenery-mix.pt",
                    },
                    new()
                    {
                        Title = "Example Realistic",
                        FileName = "exr-v21.safetensors",
                    },
                    new()
                    {
                        Title = "Painting e12",
                        FileName = "painting-e12.pt",
                    },
                }
            },
            new()
            {
                Title = "Lora",
                CheckpointFiles = new()
                {
                    new()
                    {
                        Title = "Detail Tweaker LoRA",
                        FileName = "add_detail.safetensors",
                    },
                    new()
                    {
                        Title = "Armor Suit LoRa",
                        FileName = "ArmorSuit_v1.safetensors",
                    },
                }
            }
        };
    }
}
