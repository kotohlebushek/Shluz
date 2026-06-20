namespace Shluz.Repositories;

public interface IRepository<TEntity, in TKey>
{
    IReadOnlyCollection<TEntity> All { get; }
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
}

public sealed class InMemoryRepository<TEntity, TKey> : IRepository<TEntity, TKey>
{
    private readonly List<TEntity> _items = [];
    private readonly object _syncRoot = new();

    public IReadOnlyCollection<TEntity> All
    {
        get { lock (_syncRoot) return _items.ToArray(); }
    }

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot) _items.Add(entity);
        return Task.CompletedTask;
    }
}
