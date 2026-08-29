using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class CivitFileTests
{
    [TestMethod]
    public void GetFileSpecificDownloadUrl_MetadataStyleUrl_AppendsFileId()
    {
        var file = new CivitFile
        {
            Id = 3054868,
            DownloadUrl =
                "https://civitai.com/api/download/models/3174361?type=Model&format=SafeTensor&fp=fp32",
        };

        Assert.AreEqual(
            "https://civitai.com/api/download/models/3174361?type=Model&format=SafeTensor&fp=fp32&fileId=3054868",
            file.GetFileSpecificDownloadUrl()
        );
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_BareVersionUrl_AppendsFileId()
    {
        var file = new CivitFile
        {
            Id = 3054867,
            DownloadUrl = "https://civitai.com/api/download/models/3174361",
        };

        Assert.AreEqual(
            "https://civitai.com/api/download/models/3174361?fileId=3054867",
            file.GetFileSpecificDownloadUrl()
        );
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_FileIdSubstringOfOtherParam_StillAppendsFileId()
    {
        var file = new CivitFile
        {
            Id = 3054868,
            DownloadUrl = "https://civitai.com/api/download/models/3174361?notfileId=5",
        };

        Assert.AreEqual(
            "https://civitai.com/api/download/models/3174361?notfileId=5&fileId=3054868",
            file.GetFileSpecificDownloadUrl()
        );
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_UrlWithFragment_InsertsFileIdBeforeFragment()
    {
        var file = new CivitFile
        {
            Id = 3054868,
            DownloadUrl = "https://civitai.com/api/download/models/3174361#section",
        };

        Assert.AreEqual(
            "https://civitai.com/api/download/models/3174361?fileId=3054868#section",
            file.GetFileSpecificDownloadUrl()
        );
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_UrlAlreadyHasFileId_Unchanged()
    {
        var file = new CivitFile
        {
            Id = 3054868,
            DownloadUrl = "https://civitai.com/api/download/models/3174361?fileId=3054868",
        };

        Assert.AreEqual(
            "https://civitai.com/api/download/models/3174361?fileId=3054868",
            file.GetFileSpecificDownloadUrl()
        );
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_NonCivitaiUrl_Unchanged()
    {
        // Raw storage URL as returned by the tRPC fallback — must pass through untouched
        // since extra query parameters would break a presigned URL.
        var file = new CivitFile
        {
            Id = 123,
            DownloadUrl = "https://storage.example.org/bucket/model.safetensors?sig=abc",
        };

        Assert.AreEqual(
            "https://storage.example.org/bucket/model.safetensors?sig=abc",
            file.GetFileSpecificDownloadUrl()
        );
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_NonDownloadCivitaiUrl_Unchanged()
    {
        var file = new CivitFile { Id = 123, DownloadUrl = "https://civitai.com/models/2804527" };

        Assert.AreEqual("https://civitai.com/models/2804527", file.GetFileSpecificDownloadUrl());
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_MissingFileId_Unchanged()
    {
        var file = new CivitFile { Id = 0, DownloadUrl = "https://civitai.com/api/download/models/3174361" };

        Assert.AreEqual("https://civitai.com/api/download/models/3174361", file.GetFileSpecificDownloadUrl());
    }

    [TestMethod]
    public void GetFileSpecificDownloadUrl_EmptyUrl_Unchanged()
    {
        var file = new CivitFile { Id = 123, DownloadUrl = string.Empty };

        Assert.AreEqual(string.Empty, file.GetFileSpecificDownloadUrl());
    }
}
