// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

namespace Avalonia.Gif.Decoding
{
    [Serializable]
    public class InvalidGifStreamException : Exception
    {
        public InvalidGifStreamException() { }

        public InvalidGifStreamException(string message)
            : base(message) { }

        public InvalidGifStreamException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
