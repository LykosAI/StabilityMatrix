using System;
using Avalonia.Interactivity;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs;

public partial class BetterAsyncImage
{
    public class AsyncImageFailedEventArgs : RoutedEventArgs
    {
        internal AsyncImageFailedEventArgs(Exception? errorException = null, string errorMessage = "")
            : base(FailedEvent)
        {
            ErrorException = errorException;
            ErrorMessage = errorMessage;
        }

        public Exception? ErrorException { get; private set; }
        public string ErrorMessage { get; private set; }
    }
}
