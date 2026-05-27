using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class CivitModelsRequestTests
{
    [TestMethod]
    public void Clone_ShouldCopyValues_AndCloneArrays()
    {
        var request = new CivitModelsRequest
        {
            Limit = 25,
            Page = 2,
            Query = "portrait",
            Tag = "anime",
            Username = "artist",
            Types = [CivitModelType.Checkpoint, CivitModelType.VAE],
            Sort = CivitSortMode.HighestRated,
            Period = CivitPeriod.AllTime,
            Rating = 5,
            Favorites = true,
            Hidden = false,
            PrimaryFileOnly = true,
            AllowDerivatives = true,
            AllowDifferentLicenses = false,
            AllowCommercialUse = CivitCommercialUse.Image,
            Nsfw = "true",
            BaseModels = ["SD 1.5", "SDXL 1.0"],
            CommaSeparatedModelIds = "123,456",
            Cursor = "cursor-1",
        };

        var clone = request.Clone();

        Assert.AreNotSame(request, clone);
        Assert.AreEqual(request.Limit, clone.Limit);
        Assert.AreEqual(request.Page, clone.Page);
        Assert.AreEqual(request.Query, clone.Query);
        Assert.AreEqual(request.Tag, clone.Tag);
        Assert.AreEqual(request.Username, clone.Username);
        Assert.AreEqual(request.Sort, clone.Sort);
        Assert.AreEqual(request.Period, clone.Period);
        Assert.AreEqual(request.Rating, clone.Rating);
        Assert.AreEqual(request.Favorites, clone.Favorites);
        Assert.AreEqual(request.Hidden, clone.Hidden);
        Assert.AreEqual(request.PrimaryFileOnly, clone.PrimaryFileOnly);
        Assert.AreEqual(request.AllowDerivatives, clone.AllowDerivatives);
        Assert.AreEqual(request.AllowDifferentLicenses, clone.AllowDifferentLicenses);
        Assert.AreEqual(request.AllowCommercialUse, clone.AllowCommercialUse);
        Assert.AreEqual(request.Nsfw, clone.Nsfw);
        Assert.AreEqual(request.CommaSeparatedModelIds, clone.CommaSeparatedModelIds);
        Assert.AreEqual(request.Cursor, clone.Cursor);
        CollectionAssert.AreEqual(request.Types, clone.Types);
        CollectionAssert.AreEqual(request.BaseModels, clone.BaseModels);
        Assert.AreNotSame(request.Types, clone.Types);
        Assert.AreNotSame(request.BaseModels, clone.BaseModels);

        Assert.IsNotNull(clone.Types);
        Assert.IsNotNull(clone.BaseModels);
        Assert.IsNotNull(request.Types);
        Assert.IsNotNull(request.BaseModels);

        clone.Types[0] = CivitModelType.LORA;
        clone.BaseModels[0] = "Changed";

        Assert.AreEqual(CivitModelType.Checkpoint, request.Types[0]);
        Assert.AreEqual("SD 1.5", request.BaseModels[0]);
    }
}
