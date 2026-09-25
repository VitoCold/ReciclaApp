using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class EstadoControlGeneracionConfiguration : IEntityTypeConfiguration<EstadoControlGeneracion>
{
    public void Configure(EntityTypeBuilder<EstadoControlGeneracion> entity)
    {
        entity.ToTable("EstadosControlGeneracion");
        entity.HasKey(x => x.EstadoControlGeneracionId);
        entity.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        entity.HasIndex(x => x.Codigo).IsUnique();
    }
}

public sealed class ControlGeneracionConfiguration : IEntityTypeConfiguration<ControlGeneracion>
{
    public void Configure(EntityTypeBuilder<ControlGeneracion> entity)
    {
        entity.ToTable("ControlesGeneracion");
        entity.HasKey(x => x.ControlGeneracionId);
        entity.Property(x => x.Codigo).HasMaxLength(80).IsRequired();
        entity.Property(x => x.DescripcionTrabajo).HasMaxLength(300);
        entity.Property(x => x.Observacion).HasMaxLength(500);
        entity.Property(x => x.MotivoUltimoCambio).HasMaxLength(500);
        entity.HasIndex(x => x.Codigo).IsUnique();
        entity.HasIndex(x => new { x.SedeId, x.FechaInicio });
        entity.HasIndex(x => new { x.EmpresaResponsableId, x.FechaInicio });
        entity.HasIndex(x => x.EstadoControlGeneracionId);

        entity.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EmpresaResponsable).WithMany().HasForeignKey(x => x.EmpresaResponsableId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Proyecto).WithMany().HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Actividad).WithMany().HasForeignKey(x => x.ActividadId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PuntoGeneracion).WithMany().HasForeignKey(x => x.PuntoGeneracionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EstadoControlGeneracion).WithMany().HasForeignKey(x => x.EstadoControlGeneracionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreadoPorUsuario).WithMany().HasForeignKey(x => x.CreadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.AprobadoPorUsuario).WithMany().HasForeignKey(x => x.AprobadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ControlGeneracionUsuarioConfiguration : IEntityTypeConfiguration<ControlGeneracionUsuario>
{
    public void Configure(EntityTypeBuilder<ControlGeneracionUsuario> entity)
    {
        entity.ToTable("ControlGeneracionUsuarios", table =>
            table.HasCheckConstraint("CK_ControlGeneracionUsuarios_RolControl", "[RolControl] IN ('RESPONSABLE','REGISTRADOR')"));
        entity.HasKey(x => x.ControlGeneracionUsuarioId);
        entity.Property(x => x.RolControl).HasMaxLength(20).IsRequired();
        entity.HasIndex(x => new { x.ControlGeneracionId, x.UsuarioId, x.RolControl });
        entity.HasIndex(x => new { x.UsuarioId, x.EsActivo });

        entity.HasOne(x => x.ControlGeneracion)
            .WithMany(x => x.Usuarios)
            .HasForeignKey(x => x.ControlGeneracionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.AsignadoPorUsuario).WithMany().HasForeignKey(x => x.AsignadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RegistroControlGeneracionConfiguration : IEntityTypeConfiguration<Registro>
{
    public void Configure(EntityTypeBuilder<Registro> entity)
    {
        entity.HasIndex(x => x.ControlGeneracionId);
        entity.HasOne(x => x.ControlGeneracion)
            .WithMany(x => x.Registros)
            .HasForeignKey(x => x.ControlGeneracionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
