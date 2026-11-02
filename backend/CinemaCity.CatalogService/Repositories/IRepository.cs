namespace CinemaCity.CatalogService.Repositories;

public interface IRepository<TEntity>
    where TEntity : class
{
    IQueryable<TEntity> Query();

    IEnumerable<TEntity> Local { get; }

    void Add(TEntity entity);

    void AddRange(IEnumerable<TEntity> entities);

    void Remove(TEntity entity);

    void RemoveRange(IEnumerable<TEntity> entities);
}
