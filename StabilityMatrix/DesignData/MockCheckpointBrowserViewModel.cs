using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using StabilityMatrix.Models.Api;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.DesignData;

[DesignOnly(true)]
public class MockCheckpointBrowserViewModel : CheckpointBrowserViewModel
{
    public MockCheckpointBrowserViewModel() : base(null!, null!, null!, null!)
    {
        ModelCards = new ObservableCollection<CheckpointBrowserCardViewModel>
        {
            new(null!, null!, null!, null!)
            {
                CivitModel = new()
                {
                    Name = "bb95 Furry Mix",
                    ModelVersions = new[]
                    {
                        new CivitModelVersion
                        {
                            Name = "v7.0",
                            Images = new[]
                            {
                                new CivitImage
                                {
                                    Url =
                                        "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/1547f350-461a-4cd0-a753-0544aa81e4fc/width=450/00000-4137473915.jpeg"
                                }
                            }
                        }
                    }
                },
                CardImage = new BitmapImage(new Uri(
                    "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/1547f350-461a-4cd0-a753-0544aa81e4fc/width=450/00000-4137473915.jpeg"))
            }
        };
    }
}
