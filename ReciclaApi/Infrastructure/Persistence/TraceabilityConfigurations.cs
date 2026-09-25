using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> entity)
    {
        entity.ToTable("Empresas");
        entity.HasKey(x => x.EmpresaId);
        entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Ruc).HasMaxLength(20);
        entity.Property(x => x.RazonSocial).HasMaxLength(180).IsRequired();
        entity.Property(x => x.NombreComercial).HasMaxLength(180);
        entity.Property(x => x.Direccion).HasMaxLength(250);
        entity.Property(x => x.Telefono).HasMaxLength(50);
        entity.Property(x => x.Email).HasMaxLength(160);
        entity.Property(x => x.NumeroAutorizacion).HasMaxLength(120);
        entity.HasIndex(x => x.Codigo).IsUnique();
        entity.HasIndex(x => x.Ruc);
    }
}

public sealed class PuntoResiduoConfiguration : IEntityTypeConfiguration<PuntoResiduo>
{
    public void Configure(EntityTypeBuilder<PuntoResiduo> entity)
    {
        entity.ToTable("PuntosResiduo", table =>
            table.HasCheckConstraint(
                "CK_PuntosResiduo_Tipo",
                "[Tipo] IN ('GENERACION','ALMACENAMIENTO','AMBOS')"));
        entity.HasKey(x => x.PuntoResiduoId);
        entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        entity.HasIndex(x => new { x.SedeId, x.Codigo }).IsUnique();
        entity.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EstadoRetiroConfiguration : IEntityTypeConfiguration<EstadoRetiro>
{
    public void Configure(EntityTypeBuilder<EstadoRetiro> entity)
    {
        entity.ToTable("EstadosRetiro");
        entity.HasKey(x => x.EstadoRetiroId);
        entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        entity.HasIndex(x => x.Codigo).IsUnique();
    }
}

public sealed class EstadoDisposicionConfiguration : IEntityTypeConfiguration<EstadoDisposicion>
{
    public void Configure(EntityTypeBuilder<EstadoDisposicion> entity)
    {
        entity.ToTable("EstadosDisposicion");
        entity.HasKey(x => x.EstadoDisposicionId);
        entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        entity.HasIndex(x => x.Codigo).IsUnique();
    }
}

public sealed class TipoTratamientoConfiguration : IEntityTypeConfiguration<TipoTratamiento>
{
    public void Configure(EntityTypeBuilder<TipoTratamiento> entity)
    {
        entity.ToTable("TiposTratamiento");
        entity.HasKey(x => x.TipoTratamientoId);
        entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        entity.HasIndex(x => x.Codigo).IsUnique();
    }
}

public sealed class RegistroTraceabilityConfiguration : IEntityTypeConfiguration<Registro>
{
    public void Configure(EntityTypeBuilder<Registro> entity)
    {
        entity.HasIndex(x => x.PuntoGeneracionId);
        entity.HasIndex(x => x.EmpresaResponsableId);
        entity.HasOne(x => x.PuntoGeneracion)
            .WithMany()
            .HasForeignKey(x => x.PuntoGeneracionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EmpresaResponsable)
            .WithMany()
            .HasForeignKey(x => x.EmpresaResponsableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MovimientoResiduoConfiguration : IEntityTypeConfiguration<MovimientoResiduo>
{
    public void Configure(EntityTypeBuilder<MovimientoResiduo> entity)
    {
        entity.ToTable("MovimientosResiduo", table =>
            table.HasCheckConstraint("CK_MovimientosResiduo_Cantidad", "[Cantidad] > 0"));
        entity.HasKey(x => x.MovimientoResiduoId);
        entity.Property(x => x.Cantidad).HasPrecision(18, 3);
        entity.Property(x => x.Observacion).HasMaxLength(500);
        entity.HasIndex(x => new { x.RegistroResiduoId, x.FechaMovimiento });
        entity.HasIndex(x => new { x.PuntoDestinoId, x.FechaMovimiento });
        entity.HasOne(x => x.RegistroResiduo).WithMany().HasForeignKey(x => x.RegistroResiduoId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PuntoOrigen).WithMany().HasForeignKey(x => x.PuntoOrigenId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PuntoDestino).WithMany().HasForeignKey(x => x.PuntoDestinoId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UnidadMedida).WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RegistradoPorUsuario).WithMany().HasForeignKey(x => x.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RetiroConfiguration : IEntityTypeConfiguration<Retiro>
{
    public void Configure(EntityTypeBuilder<Retiro> entity)
    {
        entity.ToTable("Retiros");
        entity.HasKey(x => x.RetiroId);
        entity.Property(x => x.Codigo).HasMaxLength(80).IsRequired();
        entity.Property(x => x.DocumentoTransporte).HasMaxLength(120);
        entity.Property(x => x.Vehiculo).HasMaxLength(120);
        entity.Property(x => x.Placa).HasMaxLength(20);
        entity.Property(x => x.Conductor).HasMaxLength(160);
        entity.Property(x => x.Observacion).HasMaxLength(500);
        entity.HasIndex(x => x.Codigo).IsUnique();
        entity.HasIndex(x => new { x.SedeId, x.FechaRetiro });
        entity.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PuntoAlmacenamiento).WithMany().HasForeignKey(x => x.PuntoAlmacenamientoId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EmpresaGestora).WithMany().HasForeignKey(x => x.EmpresaGestoraId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EstadoRetiro).WithMany().HasForeignKey(x => x.EstadoRetiroId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RetiroDetalleConfiguration : IEntityTypeConfiguration<RetiroDetalle>
{
    public void Configure(EntityTypeBuilder<RetiroDetalle> entity)
    {
        entity.ToTable("RetiroDetalles", table =>
            table.HasCheckConstraint("CK_RetiroDetalles_Cantidad", "[Cantidad] > 0"));
        entity.HasKey(x => x.RetiroDetalleId);
        entity.Property(x => x.Cantidad).HasPrecision(18, 3);
        entity.Property(x => x.Observacion).HasMaxLength(500);
        entity.HasIndex(x => new { x.RetiroId, x.RegistroResiduoId }).IsUnique();
        entity.HasIndex(x => x.RegistroResiduoId);
        entity.HasOne(x => x.Retiro).WithMany(x => x.Detalles).HasForeignKey(x => x.RetiroId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RegistroResiduo).WithMany().HasForeignKey(x => x.RegistroResiduoId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UnidadMedida).WithMany().HasForeignKey(x => x.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DisposicionFinalConfiguration : IEntityTypeConfiguration<DisposicionFinal>
{
    public void Configure(EntityTypeBuilder<DisposicionFinal> entity)
    {
        entity.ToTable("DisposicionesFinales");
        entity.HasKey(x => x.DisposicionFinalId);
        entity.Property(x => x.CodigoDocumento).HasMaxLength(120);
        entity.Property(x => x.Observacion).HasMaxLength(500);
        entity.HasIndex(x => x.RetiroId);
        entity.HasIndex(x => new { x.EmpresaGestoraId, x.FechaDisposicion });
        entity.HasOne(x => x.Retiro).WithMany(x => x.DisposicionesFinales).HasForeignKey(x => x.RetiroId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EmpresaGestora).WithMany().HasForeignKey(x => x.EmpresaGestoraId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.TipoTratamiento).WithMany().HasForeignKey(x => x.TipoTratamientoId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EstadoDisposicion).WithMany().HasForeignKey(x => x.EstadoDisposicionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EstadoSincronizacion).WithMany().HasForeignKey(x => x.EstadoSincronizacionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DisposicionFinalEvidenciaConfiguration : IEntityTypeConfiguration<DisposicionFinalEvidencia>
{
    public void Configure(EntityTypeBuilder<DisposicionFinalEvidencia> entity)
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
    }
}
