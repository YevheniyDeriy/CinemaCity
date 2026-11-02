using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Data;

namespace CinemaCity.AuthService.Repositories;

public sealed class EfRepository<TEntity>(AuthDbContext dbContext) : IRepository<TEntity>
    where TEntity : class
{
    public IQueryable<TEntity> Query()
    {
        return dbContext.Set<TEntity>();
    }

    public IEnumerable<TEntity> Local => dbContext.Set<TEntity>().Local;

    public void Add(TEntity entity)
    {
        dbContext.Set<TEntity>().Add(entity);
    }

    public void AddRange(IEnumerable<TEntity> entities)
    {
        dbContext.Set<TEntity>().AddRange(entities);
    }

    public void Remove(TEntity entity)
    {
        dbContext.Set<TEntity>().Remove(entity);
    }

    public void RemoveRange(IEnumerable<TEntity> entities)
    {
        dbContext.Set<TEntity>().RemoveRange(entities);
    }
}
