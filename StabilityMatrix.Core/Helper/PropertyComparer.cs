namespace StabilityMatrix.Core.Helper;

public class PropertyComparer<T> : IEqualityComparer<T> where T : class
{
    private Func<T, object> Expr { get; set; }
    
    public PropertyComparer(Func<T, object> expr)
    {
        Expr = expr;
    }
    public bool Equals(T? x, T? y)
    {
        if (x == null || y == null) return false;
        
        var first = Expr.Invoke(x);
        var second = Expr.Invoke(y);
        
        return first.Equals(second);
    }
    public int GetHashCode(T obj)
    {
        return obj.GetHashCode();
    }
}
