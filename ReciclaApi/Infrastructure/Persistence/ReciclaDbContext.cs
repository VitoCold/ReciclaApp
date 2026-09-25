using Microsoft.EntityFrameworkCore;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class ReciclaDbContext(DbContextOptions<ReciclaDbContext> options) : DbContext(options)
{
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<Sede> Sedes => Set<Sede>();
    public DbSet<PuntoResiduo> PuntosResiduo => Set<PuntoResiduo>();
    public DbSet<UsuarioSede> UsuarioSedes => Set<UsuarioSede>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<Actividad> Actividades => Set<Actividad>();
    public DbSet<ClasificacionResiduo> ClasificacionesResiduo => Set<ClasificacionResiduo>();
    public DbSet<TipoResiduo> TiposResiduo => Set<TipoResiduo>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<ResiduoCatalogo> ResiduosCatalogo => Set<ResiduoCatalogo>();
    public DbSet<EstadoRegistro> EstadosRegistro => Set<EstadoRegistro>();
    public DbSet<EstadoSincronizacion> EstadosSincronizacion => Set<EstadoSincronizacion>();
    public DbSet<Registro> Registros => Set<Registro>();
    public DbSet<RegistroResiduo> RegistroResiduos => Set<RegistroResiduo>();
    public DbSet<RegistroResiduoFoto> RegistroResiduoFotos => Set<RegistroResiduoFoto>();
    public DbSet<MovimientoResiduo> MovimientosResiduo => Set<MovimientoResiduo>();
    public DbSet<EmpresaGestora> EmpresasGestoras => Set<EmpresaGestora>();
    public DbSet<EstadoRetiro> EstadosRetiro => Set<EstadoRetiro>();
    public DbSet<Retiro> Retiros => Set<Retiro>();
    public DbSet<RetiroDetalle> RetiroDetalles => Set<RetiroDetalle>();
    public DbSet<EstadoDisposicion> EstadosDisposicion => Set<EstadoDisposicion>();
    public DbSet<TipoTratamiento> TiposTratamiento => Set<TipoTratamiento>();
    public DbSet<DisposicionFinal> DisposicionesFinales => Set<DisposicionFinal>();
    public DbSet<DisposicionFinalEvidencia> DisposicionFinalEvidencias => Set<DisposicionFinalEvidencia>();
    public DbSet<Disposicion> Disposiciones => Set<Disposicion>();
    public DbSet<DisposicionEvidencia> DisposicionEvidencias => Set<DisposicionEvidencia>();
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.RolId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(250);
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(x => x.UsuarioId);
            entity.Property(x => x.UsuarioNombre).HasColumnName("Usuario").HasMaxLength(80).IsRequired();
            entity.Property(x => x.Nombres).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Apellidos).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(160);
            entity.HasIndex(x => x.UsuarioNombre).IsUnique();
        });

        modelBuilder.Entity<UsuarioRol>(entity =>
        {
            entity.ToTable("UsuarioRoles");
            entity.HasKey(x => new { x.UsuarioId, x.RolId });
            entity.HasOne(x => x.Usuario).WithMany(x => x.UsuarioRoles).HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Rol).WithMany(x => x.UsuarioRoles).HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sede>(entity =>
        {
            entity.ToTable("Sedes");
            entity.HasKey(x => x.SedeId);
            entity.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<PuntoResiduo>(entity =>
        {
            entity.ToTable("PuntosResiduo");
            entity.HasKey(x => x.PuntoResiduoId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Tipo).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.SedeId, x.Codigo }).IsUnique();
            entity.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UsuarioSede>(entity =>
        {
            entity.ToTable("UsuarioSedes");
            entity.HasKey(x => new { x.UsuarioId, x.SedeId });
            entity.HasOne(x => x.Usuario).WithMany(x => x.UsuarioSedes).HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Proyecto>(entity =>
        {
            entity.ToTable("Proyectos");
            entity.HasKey(x => x.ProyectoId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Actividad>(entity =>
        {
            entity.ToTable("Actividades");
            entity.HasKey(x => x.ActividadId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => new { x.ProyectoId, x.Codigo }).IsUnique();
            entity.HasOne(x => x.Proyecto).WithMany(x => x.Actividades).HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClasificacionResiduo>(entity =>
        {
            entity.ToTable("ClasificacionesResiduo");
            entity.HasKey(x => x.ClasificacionResiduoId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ColorHex).HasMaxLength(10);
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<TipoResiduo>(entity =>
        {
            entity.ToTable("TiposResiduo");
            entity.HasKey(x => x.TipoResiduoId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<UnidadMedida>(entity =>
        {
            entity.ToTable("UnidadesMedida");
            entity.HasKey(x => x.UnidadMedidaId);
            entity.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<ResiduoCatalogo>(entity =>
        {
            entity.ToTable("ResiduosCatalogo");
            entity.HasKey(x => x.ResiduoId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(250);
            entity.HasIndex(x => x.Codigo).IsUnique();
            entity.HasOne(x => x.TipoResiduo).WithMany().HasForeignKey(x => x.TipoResiduoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClasificacionResiduo).WithMany().HasForeignKey(x => x.ClasificacionResiduoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UnidadMedidaDefault).WithMany().HasForeignKey(x => x.UnidadMedidaDefaultId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EstadoRegistro>(entity =>
        {
            entity.ToTable("EstadosRegistro");
            entity.HasKey(x => x.EstadoRegistroId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<EstadoSincronizacion>(entity =>
        {
            entity.ToTable("EstadosSincronizacion");
            entity.HasKey(x => x.EstadoSincronizacionId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(250);
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Registro>(entity =>
        {
            entity.ToTable("Registros");
            entity.HasKey(x => x.RegistroId);
            entity.Property(x => x.CodigoLocal).HasMaxLength(80);
            entity.Property(x => x.Observacion).HasMaxLength(500);
            entity.Property(x => x.OrigenDispositivo).HasMaxLength(120);
            entity.HasIndex(x => x.FechaRegistro);
            entity.HasIndex(x => new { x.SedeId, x.FechaRegistro });
            entity.HasIndex(x => new { x.RegistradoPorUsuarioId, x.FechaRegistro });
            entity.HasOne(x => x.RegistradoPorUsuario).WithMany().HasForeignKey(x => x.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Proyecto).WithMany().HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Actividad).WithMany().HasForeignKey(x => x.ActividadId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PuntoGeneracion).WithMany().HasForeignKey(x => x.PuntoGeneracionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoRegistro).WithMany().HasForeignKey(x => x.EstadoRegistroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RegistroResiduo>(entity =>
        {
            entity.ToTable("RegistroResiduos");
            entity.HasKey(x => x.RegistroResiduoId);
            entity.Property(x => x.Cantidad).HasPrecision(18, 3);
            entity.Property(x => x.Observacion).HasMaxLength(500);
            entity.HasIndex(x => x.RegistroId);
            entity.HasOne(x => x.Registro).WithMany(x => x.Residuos).HasForeignKey(x => x.RegistroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TipoResiduo).WithMany().HasForeignKey(x => x.TipoResiduoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Residuo).WithMany().HasForeignKey(x => x.ResiduoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UnidadMedida).WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RegistroResiduoFoto>(entity =>
        {
            entity.ToTable("RegistroResiduoFotos");
            entity.HasKey(x => x.FotoId);
            entity.Property(x => x.NombreArchivo).HasMaxLength(260).IsRequired();
            entity.Property(x => x.RutaLocal).HasMaxLength(500);
            entity.Property(x => x.UrlNube).HasMaxLength(1000);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.HashArchivo).HasMaxLength(128);
            entity.HasIndex(x => x.RegistroResiduoId);
            entity.HasOne(x => x.RegistroResiduo).WithMany(x => x.Fotos).HasForeignKey(x => x.RegistroResiduoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TomadaPorUsuario).WithMany().HasForeignKey(x => x.TomadaPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimientoResiduo>(entity =>
        {
            entity.ToTable("MovimientosResiduo");
            entity.HasKey(x => x.MovimientoResiduoId);
            entity.Property(x => x.Cantidad).HasPrecision(18, 3);
            entity.Property(x => x.Observacion).HasMaxLength(500);
            entity.HasIndex(x => new { x.RegistroResiduoId, x.FechaMovimiento });
            entity.HasOne(x => x.RegistroResiduo).WithMany().HasForeignKey(x => x.RegistroResiduoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PuntoOrigen).WithMany().HasForeignKey(x => x.PuntoOrigenId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PuntoDestino).WithMany().HasForeignKey(x => x.PuntoDestinoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UnidadMedida).WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RegistradoPorUsuario).WithMany().HasForeignKey(x => x.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmpresaGestora>(entity =>
        {
            entity.ToTable("EmpresasGestoras");
            entity.HasKey(x => x.EmpresaGestoraId);
            entity.Property(x => x.Ruc).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RazonSocial).HasMaxLength(180).IsRequired();
            entity.Property(x => x.NombreComercial).HasMaxLength(180);
            entity.Property(x => x.Direccion).HasMaxLength(250);
            entity.Property(x => x.Telefono).HasMaxLength(40);
            entity.Property(x => x.Email).HasMaxLength(160);
            entity.Property(x => x.NumeroAutorizacion).HasMaxLength(100);
            entity.HasIndex(x => x.Ruc).IsUnique();
        });

        modelBuilder.Entity<EstadoRetiro>(entity =>
        {
            entity.ToTable("EstadosRetiro");
            entity.HasKey(x => x.EstadoRetiroId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Retiro>(entity =>
        {
            entity.ToTable("Retiros");
            entity.HasKey(x => x.RetiroId);
            entity.Property(x => x.Codigo).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Vehiculo).HasMaxLength(120);
            entity.Property(x => x.Placa).HasMaxLength(20);
            entity.Property(x => x.Conductor).HasMaxLength(160);
            entity.Property(x => x.DocumentoTransporte).HasMaxLength(100);
            entity.Property(x => x.Observacion).HasMaxLength(500);
            entity.HasIndex(x => x.Codigo).IsUnique();
            entity.HasIndex(x => new { x.SedeId, x.FechaRetiro });
            entity.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EmpresaGestora).WithMany().HasForeignKey(x => x.EmpresaGestoraId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoRetiro).WithMany().HasForeignKey(x => x.EstadoRetiroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RetiroDetalle>(entity =>
        {
            entity.ToTable("RetiroDetalles");
            entity.HasKey(x => x.RetiroDetalleId);
            entity.Property(x => x.Cantidad).HasPrecision(18, 3);
            entity.HasIndex(x => x.RetiroId);
            entity.HasIndex(x => x.RegistroResiduoId);
            entity.HasOne(x => x.Retiro).WithMany(x => x.Detalles).HasForeignKey(x => x.RetiroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RegistroResiduo).WithMany().HasForeignKey(x => x.RegistroResiduoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UnidadMedida).WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EstadoDisposicion>(entity =>
        {
            entity.ToTable("EstadosDisposicion");
            entity.HasKey(x => x.EstadoDisposicionId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<TipoTratamiento>(entity =>
        {
            entity.ToTable("TiposTratamiento");
            entity.HasKey(x => x.TipoTratamientoId);
            entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<DisposicionFinal>(entity =>
        {
            entity.ToTable("DisposicionesFinales");
            entity.HasKey(x => x.DisposicionFinalId);
            entity.Property(x => x.CodigoDocumento).HasMaxLength(100);
            entity.Property(x => x.Observacion).HasMaxLength(500);
            entity.HasIndex(x => x.RetiroId);
            entity.HasOne(x => x.Retiro).WithMany(x => x.DisposicionesFinales).HasForeignKey(x => x.RetiroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EmpresaGestora).WithMany().HasForeignKey(x => x.EmpresaGestoraId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TipoTratamiento).WithMany().HasForeignKey(x => x.TipoTratamientoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoDisposicion).WithMany().HasForeignKey(x => x.EstadoDisposicionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DisposicionFinalEvidencia>(entity =>
        {
            entity.ToTable("DisposicionFinalEvidencias");
            entity.HasKey(x => x.EvidenciaId);
            entity.Property(x => x.TipoEvidencia).HasMaxLength(40).IsRequired();
            entity.Property(x => x.NombreArchivo).HasMaxLength(260).IsRequired();
            entity.Property(x => x.RutaLocal).HasMaxLength(500);
            entity.Property(x => x.UrlNube).HasMaxLength(1000);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.HashArchivo).HasMaxLength(128);
            entity.HasIndex(x => x.DisposicionFinalId);
            entity.HasOne(x => x.DisposicionFinal).WithMany(x => x.Evidencias).HasForeignKey(x => x.DisposicionFinalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Modelo legado: se conserva durante la migración de endpoints y pantallas.
        modelBuilder.Entity<Disposicion>(entity =>
        {
            entity.ToTable("Disposiciones");
            entity.HasKey(x => x.DisposicionId);
            entity.Property(x => x.CodigoDocumento).HasMaxLength(80);
            entity.Property(x => x.EmpresaDisposicion).HasMaxLength(180);
            entity.Property(x => x.Observacion).HasMaxLength(500);
            entity.HasIndex(x => x.RegistroId);
            entity.HasOne(x => x.Registro).WithMany(x => x.Disposiciones).HasForeignKey(x => x.RegistroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DisposicionEvidencia>(entity =>
        {
            entity.ToTable("DisposicionEvidencias");
            entity.HasKey(x => x.EvidenciaId);
            entity.Property(x => x.TipoEvidencia).HasMaxLength(40).IsRequired();
            entity.Property(x => x.NombreArchivo).HasMaxLength(260).IsRequired();
            entity.Property(x => x.RutaLocal).HasMaxLength(500);
            entity.Property(x => x.UrlNube).HasMaxLength(1000);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.HashArchivo).HasMaxLength(128);
            entity.HasIndex(x => x.DisposicionId);
            entity.HasOne(x => x.Disposicion).WithMany(x => x.Evidencias).HasForeignKey(x => x.DisposicionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SyncOutbox>(entity =>
        {
            entity.ToTable("SyncOutbox");
            entity.HasKey(x => x.SyncOutboxId);
            entity.Property(x => x.Entidad).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Operacion).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.Procesado, x.CreadoUtc });
        });

        modelBuilder.Entity<Auditoria>(entity =>
        {
            entity.ToTable("Auditoria");
            entity.HasKey(x => x.AuditoriaId);
            entity.Property(x => x.Entidad).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Accion).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IpOrigen).HasMaxLength(80);
            entity.Property(x => x.Dispositivo).HasMaxLength(120);
            entity.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
