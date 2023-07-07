using System.Linq.Expressions;
using System.Reflection;
using LiteDB;
using LiteDB.Async;

namespace StabilityMatrix.Core.Extensions;

// ReSharper disable once InconsistentNaming
public static class LiteDBExtensions
{
    private static readonly Dictionary<Type, (Type PropertyType, string MemberName, bool IsList)> Mapper = new();

    public static void Register<T, TU>(Expression<Func<T, List<TU>?>> exp, string? collection = null)
    {
        var member = (exp.Body is MemberExpression body ? body.Member : null) as PropertyInfo;
        if (member == null)
            throw new ArgumentException("Expecting Member Expression");
        BsonMapper.Global.Entity<T>().DbRef(exp, collection);
        Mapper.Add(typeof(T), (typeof(TU), member.Name, true));
    }

    public static void Register<T, TU>(Expression<Func<T, TU?>> exp, string? collection = null)
    {
        var member = (exp.Body is MemberExpression body ? body.Member : null) as PropertyInfo;
        if (member == null)
            throw new ArgumentException("Expecting Member Expression");
        BsonMapper.Global.Entity<T>().DbRef(exp, collection);
        Mapper.Add(typeof(T), (typeof(TU), member.Name, false));
    }

    public static ILiteCollection<T>? IncludeAll<T>(this ILiteCollection<T> col)
    {
        if (!Mapper.ContainsKey(typeof(T))) return null;

        var stringList = new List<string>();
        var key = typeof(T);
        var values = new List<string>();
        var flag = true;
        while (Mapper.TryGetValue(key, out var tuple))
        {
            var str = tuple.MemberName + (tuple.IsList ? "[*]" : "");
            values.Add(flag ? "$." + str : str);
            stringList.Add(string.Join(".", values));
            key = tuple.PropertyType;
            flag = false;
        }

        return stringList.Aggregate(col, (current, keySelector) => current.Include((BsonExpression) keySelector));
    }
    
    public static ILiteCollectionAsync<T> IncludeAll<T>(this ILiteCollectionAsync<T> col)
    {
        if (!Mapper.ContainsKey(typeof(T))) return col;

        var stringList = new List<string>();
        var key = typeof(T);
        var values = new List<string>();
        var flag = true;
        while (Mapper.TryGetValue(key, out var tuple))
        {
            var str = tuple.MemberName + (tuple.IsList ? "[*]" : "");
            values.Add(flag ? "$." + str : str);
            stringList.Add(string.Join(".", values));
            key = tuple.PropertyType;
            flag = false;
        }

        return stringList.Aggregate(col, (current, keySelector) => current.Include((BsonExpression) keySelector));
    }
}
