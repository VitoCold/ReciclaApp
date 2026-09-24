using Microsoft.EntityFrameworkCore;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Infrastructure.Repositories;

public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query(bool tracking = false);
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}

public sealed class Repository<TEntity>(ReciclaDbContext context) : IRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _dbSet = context.Set<TEntity>();

    public IQueryable<TEntity> Query(bool tracking = false) =>
        tracking ? _dbSet : _dbSet.AsNoTracking();

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default) =>
        await _dbSet.FindAsync([id], cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _dbSet.AddAsync(entity, cancellationToken).AsTask();

    public void Update(TEntity entity) => _dbSet.Update(entity);

    public void Remove(TEntity entity) => _dbSet.Remove(entity);
}

public interface IRegistroRepository
{
    Task<IReadOnlyCollection<Registro>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<Registro?> ObtenerDetalleAsync(Guid registroId, Guid usuarioId, bool tracking, CancellationToken cancellationToken = default);
}

public sealed class RegistroRepository(ReciclaDbContext context) : IRegistroRepository
{
    public async Task<IReadOnlyCollection<Registro>> ListarPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default) =>
        await context.Registros
            .AsNoTracking()
            .Where(x => x.RegistradoPorUsuarioId == usuarioId && !x.Eliminado)
            .Include(x => x.Proyecto)
            .Include(x => x.Actividad)
            .Include(x => x.Sede)
            .Include(x => x.EstadoRegistro)
            .Include(x => x.Residuos.Where(r => !r.Eliminado))
            .OrderByDescending(x => x.FechaRegistro)
            .ToListAsync(cancellationToken);

    public async Task<Registro?> ObtenerDetalleAsync(
        Guid registroId,
        Guid usuarioId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Registro> query = context.Registros;
        if (!tracking)
            query = query.AsNoTracking();

        return await query
            .Where(x => x.RegistroId == registroId && x.RegistradoPorUsuarioId == usuarioId && !x.Eliminado)
            .Include(x => x.Proyecto)
            .Include(x => x.Actividad)
            .Include(x => x.Sede)
            .Include(x => x.EstadoRegistro)
            .Include(x => x.Residuos.Where(r => !r.Eliminado))
                .ThenInclude(x => x.Fotos.Where(f => !f.Eliminado))
            .Include(x => x.Disposiciones.Where(d => !d.Eliminado))
                .ThenInclude(x => x.Evidencias.Where(e => !e.Eliminado))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;
    IRegistroRepository Registros { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class UnitOfWork(
    ReciclaDbContext context,
    IRegistroRepository registroRepository) : IUnitOfWork
{
    public IRegistroRepository Registros { get; } = registroRepository;

    public IRepository<TEntity> Repository<TEntity>() where TEntity : class =>
        new Repository<TEntity>(context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
