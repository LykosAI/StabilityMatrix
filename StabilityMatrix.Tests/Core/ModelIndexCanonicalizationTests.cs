using System.Globalization;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

/// <summary>
/// Tests for model index canonicalization with case-conflicting model folder paths
/// (possible on case-sensitive file systems, GitHub issues #1149 / #1357 / #1715).
/// </summary>
[TestClass]
public class ModelIndexCanonicalizationTests
{
    private static readonly Collation OrdinalCollation = new(
        CultureInfo.InvariantCulture.LCID,
        CompareOptions.Ordinal
    );

    private static readonly Collation IgnoreCaseCollation = new(
        CultureInfo.InvariantCulture.LCID,
        CompareOptions.IgnoreCase
    );

    [TestMethod]
    [DataRow("TextEncoders", SharedFolderType.TextEncoders)]
    [DataRow("Lora", SharedFolderType.Lora)]
    [DataRow("DiffusionModels", SharedFolderType.DiffusionModels)]
    public void ParseSharedFolderType_CanonicalName_ResolvesType(string folderName, SharedFolderType expected)
    {
        Assert.AreEqual(expected, ModelIndexService.ParseSharedFolderType(folderName));
    }

    [TestMethod]
    [DataRow("textencoders", SharedFolderType.TextEncoders)]
    [DataRow("TEXTENCODERS", SharedFolderType.TextEncoders)]
    [DataRow("lora", SharedFolderType.Lora)]
    [DataRow("stablediffusion", SharedFolderType.StableDiffusion)]
    public void ParseSharedFolderType_CaseVariantName_ResolvesType(
        string folderName,
        SharedFolderType expected
    )
    {
        Assert.AreEqual(expected, ModelIndexService.ParseSharedFolderType(folderName));
    }

    [TestMethod]
    [DataRow("text_encoders")]
    [DataRow("diffusion_models")]
    [DataRow("unet")]
    [DataRow("SomeRandomFolder")]
    public void ParseSharedFolderType_UnmappedName_ResolvesUnknown(string folderName)
    {
        // ComfyUI-native folder names (underscored) and arbitrary folders are not aliased to
        // shared folder types; they index as Unknown. Notably "diffusion_models" must stay
        // Unknown: SwarmUI launch creates it as a junction of DiffusionModels inside the models
        // directory, and typing both would double-list every model.
        Assert.AreEqual(SharedFolderType.Unknown, ModelIndexService.ParseSharedFolderType(folderName));
    }

    [TestMethod]
    public void DeduplicateForDbCollation_IgnoreCaseCollation_DropsCaseOnlyDuplicates()
    {
        var models = CaseConflictingModels();

        var result = ModelIndexService.DeduplicateForDbCollation(
            models,
            IgnoreCaseCollation,
            NullLogger.Instance
        );

        // The unet/Unet pair differs only in case and must collapse to one entry;
        // text_encoders/TextEncoders differ by more than case and must both survive.
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(
            1,
            result.Count(m =>
                string.Equals(m.RelativePath, "unet/model.gguf", StringComparison.OrdinalIgnoreCase)
            )
        );
        Assert.IsTrue(result.Any(m => m.RelativePath == "text_encoders/clip_l.safetensors"));
        Assert.IsTrue(result.Any(m => m.RelativePath == "TextEncoders/clip_l.safetensors"));
    }

    [TestMethod]
    public void DeduplicateForDbCollation_OrdinalCollation_KeepsCaseOnlyDistinctPaths()
    {
        var models = CaseConflictingModels();

        var result = ModelIndexService.DeduplicateForDbCollation(
            models,
            OrdinalCollation,
            NullLogger.Instance
        );

        // Ordinal collation treats case-only variants as distinct keys, so nothing is dropped
        // (a case-sensitive file system can legitimately hold both files).
        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void DeduplicateForDbCollation_OutputInserts_UnderCaseInsensitiveDb()
    {
        // Default LiteDB collation is case-insensitive: raw case-conflicting paths are expected
        // to fail the unique _id index (the #1357 / #1149 crash), while the deduplicated set
        // must insert cleanly.
        using var db = new LiteDatabase(":memory:");
        Assert.IsTrue(
            db.Collation.SortOptions.HasFlag(CompareOptions.IgnoreCase),
            "Premise: default LiteDB collation is case-insensitive"
        );

        var rawCollection = db.GetCollection<LocalModelFile>("RawLocalModelFiles");
        Assert.ThrowsException<LiteException>(() => rawCollection.Insert(CaseConflictingModels()));

        var dedupedCollection = db.GetCollection<LocalModelFile>("LocalModelFiles");
        var deduplicated = ModelIndexService.DeduplicateForDbCollation(
            CaseConflictingModels(),
            db.Collation,
            NullLogger.Instance
        );

        dedupedCollection.Insert(deduplicated);

        Assert.AreEqual(deduplicated.Count, dedupedCollection.Count());
    }

    [TestMethod]
    public void GetKeyComparer_MatchesCollationSemantics()
    {
        Assert.AreSame(StringComparer.Ordinal, ModelIndexService.GetKeyComparer(OrdinalCollation));
        Assert.AreSame(
            StringComparer.OrdinalIgnoreCase,
            ModelIndexService.GetKeyComparer(
                new Collation(CultureInfo.InvariantCulture.LCID, CompareOptions.OrdinalIgnoreCase)
            )
        );

        var ignoreCaseComparer = ModelIndexService.GetKeyComparer(IgnoreCaseCollation);
        Assert.IsTrue(ignoreCaseComparer.Equals("unet/model.gguf", "Unet/model.gguf"));
        Assert.IsFalse(
            ignoreCaseComparer.Equals("text_encoders/clip_l.safetensors", "TextEncoders/clip_l.safetensors")
        );
    }

    private static List<LocalModelFile> CaseConflictingModels() =>
        [
            new LocalModelFile
            {
                RelativePath = "unet/model.gguf",
                SharedFolderType = SharedFolderType.Unknown,
            },
            new LocalModelFile
            {
                RelativePath = "Unet/model.gguf",
                SharedFolderType = SharedFolderType.Unknown,
            },
            new LocalModelFile
            {
                RelativePath = "text_encoders/clip_l.safetensors",
                SharedFolderType = SharedFolderType.Unknown,
            },
            new LocalModelFile
            {
                RelativePath = "TextEncoders/clip_l.safetensors",
                SharedFolderType = SharedFolderType.TextEncoders,
            },
        ];
}
