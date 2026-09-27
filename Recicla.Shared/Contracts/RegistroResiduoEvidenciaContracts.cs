namespace Recicla.Shared.Contracts;

public sealed record GuardarUbicacionResiduoRequest(
    double Latitud,
    double Longitud,
    double? PrecisionMetros,
    DateTime CapturadaUtc);

public sealed record RegistroResiduoEvidenciaDto(
    Guid RegistroResiduoId,
    double? Latitud,
    double? Longitud,
    double? PrecisionMetros,
    DateTime? UbicacionCapturadaUtc,
    int CantidadFotos)
{
    public bool TieneUbicacion => Latitud.HasValue && Longitud.HasValue;
    public bool TieneFoto => CantidadFotos > 0;
    public bool EvidenciaCompleta => TieneUbicacion && TieneFoto;
}
