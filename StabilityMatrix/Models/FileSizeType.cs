using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Extensions;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Models;

public class FileSizeType
{
    public double SizeInKB { get; private set; }

    public string HumanReadableRepresentation { get; private set; }

    public FileSizeType(double sizeInKB)
    {
        SizeInKB = sizeInKB;
        HumanReadableRepresentation = ConvertToHumanReadable();
    }

    private string ConvertToHumanReadable()
    {
        string[] sizeUnits = { "KB", "MB", "GB", "TB" };
        double size = SizeInKB;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return string.Format("{0} {1}", size.ToString("0.##", CultureInfo.InvariantCulture), sizeUnits[unitIndex]);
    }

    public override string ToString()
    {
        return HumanReadableRepresentation;
    }
}
