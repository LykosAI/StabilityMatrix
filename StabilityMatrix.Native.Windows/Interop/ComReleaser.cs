namespace StabilityMatrix.Native.Windows.Interop
{
    internal sealed class ComReleaser<T> : IDisposable
        where T : class
    {
        public T? Item { get; private set; }

        public ComReleaser(T obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (!Marshal.IsComObject(obj))
                throw new ArgumentOutOfRangeException(nameof(obj));
            Item = obj;
        }

        public void Dispose()
        {
            if (Item != null)
            {
                Marshal.FinalReleaseComObject(Item);
                Item = null;
            }
        }
    }
}
