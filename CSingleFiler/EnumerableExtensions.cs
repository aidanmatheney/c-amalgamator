namespace CSingleFiler
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class EnumerableExtensions
    {
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