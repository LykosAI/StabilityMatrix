using System.Text;

namespace StabilityMatrix.Core.Helper.Webp;

public class WebpReader(Stream stream) : BinaryReader(stream, Encoding.ASCII, true)
{
    private uint headerFileSize;

    public bool GetIsAnimatedFlag()
    {
        ReadHeader();

        while (BaseStream.Position < headerFileSize)
        {
            if (ReadVoidChunk() is "ANMF" or "ANIM")
            {
                return true;
            }
        }

        return false;
    }

    private void ReadHeader()
    {
        // RIFF
        var riff = ReadBytes(4);
        if (!riff.SequenceEqual([.."RIFF"u8]))
        {
            throw new InvalidDataException("Invalid RIFF header");
        }

        // Size: uint32
        headerFileSize = ReadUInt32();

        // WEBP
        var webp = ReadBytes(4);
        if (!webp.SequenceEqual([.."WEBP"u8]))
        {
            throw new InvalidDataException("Invalid WEBP header");
        }
    }

    // Read a single chunk and discard its contents
    private string ReadVoidChunk()
    {
        // FourCC: 4 bytes in ASCII
        var result = ReadBytes(4);

        // Size: uint32
        var size = ReadUInt32();

        BaseStream.Seek(size, SeekOrigin.Current);

        return Encoding.ASCII.GetString(result);
    }
}
