using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Avalonia.Helpers;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
internal enum Win32ClipboardFormat : uint
{
    CF_TEXT = 1,
    CF_BITMAP = 2,
    CF_SYLK = 4,
    CF_DIF = 5,
    CF_TIFF = 6,
    CF_OEMTEXT = 7,
    CF_DIB = 8,
    CF_PALETTE = 9,
    CF_PENDATA = 10,
    CF_RIFF = 11,
    CF_WAVE = 12,
    CF_UNICODETEXT = 13
}
