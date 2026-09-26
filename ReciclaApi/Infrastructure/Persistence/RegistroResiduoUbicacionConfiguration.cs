using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class RegistroResiduoUbicacionConfiguration : IEntityTypeConfiguration<RegistroResiduoUbicacion>
{
    public void Configure(EntityTypeBuilder<RegistroResiduoUbicacion> entity)
    {
        entity.ToTable("RegistroResiduoUbicaciones");
        entity.HasKey(x => x.RegistroResiduoId);
        entity.Property(x => x.Latitud).IsRequired();
        entity.Property(x => x.Longitud).IsRequired();
        entity.Property(x => x.PrecisionMetros);
        entity.Property(x => x.CapturadaUtc).IsRequired();
        entity.Property(x => x.ActualizadoUtc).IsRequired();
        entity.HasOne(x => x.RegistroResiduo)
            .WithOne()
            .HasForeignKey<RegistroResiduoUbicacion>(x => x.RegistroResiduoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
