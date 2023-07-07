using System;
using System.Linq.Expressions;
using System.Reflection;

namespace StabilityMatrix.Helper;

public static class Expressions
{
    public static (string propertyName, Expression<Action<T, TValue>> assigner) 
        GetAssigner<T, TValue>(Expression<Func<T, TValue>> propertyAccessor)
    {
        if (propertyAccessor.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException(
                $"Expression must be a member expression, not {propertyAccessor.Body.NodeType}");
        }

        var propertyInfo = memberExpression.Member as PropertyInfo;
        if (propertyInfo == null)
        {
            throw new ArgumentException(
                $"Expression member must be a property, not {memberExpression.Member.MemberType}");
        }
        
        var propertyName = propertyInfo.Name;
        var typeParam = Expression.Parameter(typeof(T));
        var valueParam = Expression.Parameter(typeof(TValue));
        var expr = Expression.Lambda<Action<T, TValue>>(
            Expression.Assign(
                Expression.MakeMemberAccess(typeParam, propertyInfo),
                valueParam), typeParam, valueParam);
        return (propertyName, expr);
    }
}
