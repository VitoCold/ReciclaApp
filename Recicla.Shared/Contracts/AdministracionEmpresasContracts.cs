namespace Recicla.Shared.Contracts;

public sealed record AdministracionEmpresaDto(
    Guid EmpresaId,
    string Codigo,
    string? Ruc,
    string RazonSocial,
    string? NombreComercial,
    string? Direccion,
    string? Telefono,
    string? Email,
    bool EsGestoraResiduos,
    string? NumeroAutorizacion,
    bool EsActivo,
    bool TieneUsoHistorico,
    int ControlesAbiertos,
    int RetirosRegistrados,
    int DisposicionesRegistradas);

public sealed record GuardarAdministracionEmpresaRequest(
    string Codigo,
    string? Ruc,
    string RazonSocial,
    string? NombreComercial,
    string? Direccion,
    string? Telefono,
    string? Email,
    bool EsGestoraResiduos,
    string? NumeroAutorizacion);

public sealed record CambiarEstadoAdministracionEmpresaRequest(bool EsActivo);
