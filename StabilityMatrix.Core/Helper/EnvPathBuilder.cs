namespace StabilityMatrix.Core.Helper;

public class EnvPathBuilder(params string[] initialPaths)
{
    private readonly SortedSet<string> paths = [..initialPaths];

    public EnvPathBuilder AddPath(string path)
    {
        paths.Add(path);
        return this;
    }

    public EnvPathBuilder RemovePath(string path)
    {
        paths.Remove(path);
        return this;
    }

    public override string ToString()
    {
        return string.Join(Compat.PathDelimiter, paths);
    }
}
