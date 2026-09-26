namespace ReciclaApi.Domain;

public sealed class RegistroResiduoUbicacion
{
    public Guid RegistroResiduoId { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double? PrecisionMetros { get; set; }
    public DateTime CapturadaUtc { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
    public RegistroResiduo RegistroResiduo { get; set; } = null!;
}
