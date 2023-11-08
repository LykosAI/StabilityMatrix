using System.Linq.Expressions;

namespace StabilityMatrix.Core.Helper;


/*/// <summary>
/// Context helper for setting properties to one value on entry and another on dispose.
/// </summary>
public class ContextManager<T, TProperty> : IDisposable
{
    private Accessor accessor;
    
    public ContextManager(Expression<Func<T, TProperty>> expression, T context, TProperty value)
    {
        var accessorInfo = ((MemberExpression) expression.Body).Member;
        accessor = Accessors.Find(accessorInfo) ?? throw new ArgumentException("Accessor not found", nameof(expression));

        originalValue = (TProperty)propertyInfo.GetValue(context);
        propertyInfo.SetValue(context, value);
    }
}*/
