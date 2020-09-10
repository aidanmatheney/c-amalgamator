namespace CAmalgamator
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class EnumerableExtensions
    {
        public static IOrderedEnumerable<TSource> OrderByElement<TSource>(this IEnumerable<TSource> source)
        {
            Guard.NotNull(source, nameof(source));
            return source.OrderBy(element => element);
        }

        public static IOrderedEnumerable<TSource> OrderByElement<TSource>(this IEnumerable<TSource> source, IComparer<TSource> comparer)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(comparer, nameof(comparer));
            return source.OrderBy(element => element, comparer);
        }

        public static IOrderedEnumerable<TSource> OrderByElementDescending<TSource>(this IEnumerable<TSource> source)
        {
            Guard.NotNull(source, nameof(source));
            return source.OrderByDescending(element => element);
        }

        public static IOrderedEnumerable<TSource> OrderByElementDescending<TSource>(this IEnumerable<TSource> source, IComparer<TSource> comparer)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(comparer, nameof(comparer));
            return source.OrderByDescending(element => element, comparer);
        }

        public static IAsyncEnumerable<TSource> AsAsync<TSource>(this IEnumerable<TSource> source)
        {
            Guard.NotNull(source, nameof(source));
            return AsyncEnumerable.Create(_ => new AsyncSyncEnumerator<TSource>(source.GetEnumerator()));
        }

        private sealed class AsyncSyncEnumerator<TSource> : IAsyncEnumerator<TSource>
        {
            private readonly IEnumerator<TSource> _syncEnumerator;

            public AsyncSyncEnumerator(IEnumerator<TSource> syncEnumerator)
            {
                Guard.NotNull(syncEnumerator, nameof(syncEnumerator));
                _syncEnumerator = syncEnumerator;
            }

            public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_syncEnumerator.MoveNext());

            public TSource Current => _syncEnumerator.Current;

            public ValueTask DisposeAsync()
            {
                _syncEnumerator.Dispose();
                return default;
            }
        }
    }
}