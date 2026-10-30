using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace server.Extension;

public static class EnumerableExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncreaseOne<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        where TKey : notnull
        where TValue : struct, IIncrementOperators<TValue>
    {
        ++CollectionsMarshal.GetValueRefOrAddDefault(dict, key, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TResult> SelectList<TSource, TResult>(this List<TSource> list, Func<TSource, TResult> selector)
    {
        List<TResult> results = new(list.Count);
        foreach (ref readonly TSource source in CollectionsMarshal.AsSpan(list))
        {
            results.Add(selector(source));
        }

        return results;
    }
}
