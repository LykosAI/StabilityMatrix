using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.DesignData;

[DesignOnly(true)]
public class MockCheckpointBrowserViewModel : CheckpointBrowserViewModel
{
    public MockCheckpointBrowserViewModel() : base(null!, null!, null!, null!, null!, null!)
    {
        ModelCards = new ObservableCollection<CheckpointBrowserCardViewModel>
        {
            new(null!, null!, null!, null!, null!,
                fixedImage: new BitmapImage(new Uri("https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/01bb1e4b-f88b-441a-b2bf-c1ec0bd91727/width=450/00015-1007989405.jpeg")))
            {
                CivitModel = new CivitModel
                {
                    Name = "Ghibli Background",
                    Type = CivitModelType.Checkpoint,
                    ModelVersions = new List<CivitModelVersion>
                    {
                        new()
                        {
                            Name = "v5.0",
                            Files = new List<CivitFile>
                            {
                                new()
                                {
                                    Name = "File",
                                    SizeKb = 844110,
                                }
                            }
                        }
                    }
                },
            },
            new(null!, null!, null!, null!, null!,
                fixedImage: new BitmapImage(new Uri("https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/fcb56b61-2cd6-40f4-a75d-e9448c0822d6/width=1152/29933-2136606019-masterpiece.jpeg")))
            {
                CivitModel = new CivitModel
                {
                    Name = "Indigo Furry mix",
                    Type = CivitModelType.Checkpoint,
                    Nsfw = false,
                    ModelVersions = new List<CivitModelVersion>
                    {
                        new()
                        {
                            Name = "v45_hybrid",
                            Files = new List<CivitFile>
                            {
                                new()
                                {
                                    Name = "Pruned Model fp16",
                                    SizeKb = 1990000,
                                }
                            }
                        }
                    }
                },
            }
        };
        ModelCardsView = CollectionViewSource.GetDefaultView(ModelCards);
    }
}
