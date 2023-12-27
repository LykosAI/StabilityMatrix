using System.Text;
using ExifLibrary;

namespace StabilityMatrix.Core.Helper;

public class MyTiffFile(MemoryStream stream, Encoding encoding) : TIFFFile(stream, encoding);
