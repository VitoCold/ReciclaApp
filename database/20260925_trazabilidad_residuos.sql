SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* ============================================================
       1. Maestros de empresas y puntos de residuos
       ============================================================ */
    IF OBJECT_ID(N'dbo.Empresas', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Empresas
        (
            EmpresaId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Empresas PRIMARY KEY,
            Codigo NVARCHAR(40) NOT NULL,
            Ruc NVARCHAR(20) NULL,
            RazonSocial NVARCHAR(180) NOT NULL,
            NombreComercial NVARCHAR(180) NULL,
            Direccion NVARCHAR(250) NULL,
            Telefono NVARCHAR(50) NULL,
            Email NVARCHAR(160) NULL,
            EsGestoraResiduos BIT NOT NULL,
            NumeroAutorizacion NVARCHAR(120) NULL,
            EsActivo BIT NOT NULL,
            CONSTRAINT UQ_Empresas_Codigo UNIQUE (Codigo)
        );

        CREATE INDEX IX_Empresas_Ruc ON dbo.Empresas(Ruc);
    END;

    IF OBJECT_ID(N'dbo.PuntosResiduo', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PuntosResiduo
        (
            PuntoResiduoId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PuntosResiduo PRIMARY KEY,
            SedeId UNIQUEIDENTIFIER NOT NULL,
            Codigo NVARCHAR(40) NOT NULL,
            Nombre NVARCHAR(160) NOT NULL,
            Tipo NVARCHAR(20) NOT NULL,
            EsActivo BIT NOT NULL,
            CONSTRAINT CK_PuntosResiduo_Tipo CHECK (Tipo IN ('GENERACION','ALMACENAMIENTO','AMBOS')),
            CONSTRAINT UQ_PuntosResiduo_Sede_Codigo UNIQUE (SedeId, Codigo),
            CONSTRAINT FK_PuntosResiduo_Sedes FOREIGN KEY (SedeId) REFERENCES dbo.Sedes(SedeId)
        );
    END;

    /* ============================================================
       2. Contexto adicional del registro de generación
       ============================================================ */
    IF COL_LENGTH('dbo.Registros', 'PuntoGeneracionId') IS NULL
        ALTER TABLE dbo.Registros ADD PuntoGeneracionId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH('dbo.Registros', 'EmpresaResponsableId') IS NULL
        ALTER TABLE dbo.Registros ADD EmpresaResponsableId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Registros_PuntosResiduo_PuntoGeneracionId')
        ALTER TABLE dbo.Registros
            ADD CONSTRAINT FK_Registros_PuntosResiduo_PuntoGeneracionId
            FOREIGN KEY (PuntoGeneracionId) REFERENCES dbo.PuntosResiduo(PuntoResiduoId);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Registros_Empresas_EmpresaResponsableId')
        ALTER TABLE dbo.Registros
            ADD CONSTRAINT FK_Registros_Empresas_EmpresaResponsableId
            FOREIGN KEY (EmpresaResponsableId) REFERENCES dbo.Empresas(EmpresaId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Registros_PuntoGeneracionId' AND object_id = OBJECT_ID('dbo.Registros'))
        CREATE INDEX IX_Registros_PuntoGeneracionId ON dbo.Registros(PuntoGeneracionId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Registros_EmpresaResponsableId' AND object_id = OBJECT_ID('dbo.Registros'))
        CREATE INDEX IX_Registros_EmpresaResponsableId ON dbo.Registros(EmpresaResponsableId);

    /* ============================================================
       3. Estados y tipos propios del retiro/disposición
       ============================================================ */
    IF OBJECT_ID(N'dbo.EstadosRetiro', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EstadosRetiro
        (
            EstadoRetiroId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EstadosRetiro PRIMARY KEY,
            Codigo NVARCHAR(40) NOT NULL,
            Nombre NVARCHAR(80) NOT NULL,
            CONSTRAINT UQ_EstadosRetiro_Codigo UNIQUE (Codigo)
        );
    END;

    IF OBJECT_ID(N'dbo.EstadosDisposicion', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EstadosDisposicion
        (
            EstadoDisposicionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EstadosDisposicion PRIMARY KEY,
            Codigo NVARCHAR(40) NOT NULL,
            Nombre NVARCHAR(80) NOT NULL,
            CONSTRAINT UQ_EstadosDisposicion_Codigo UNIQUE (Codigo)
        );
    END;

    IF OBJECT_ID(N'dbo.TiposTratamiento', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TiposTratamiento
        (
            TipoTratamientoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TiposTratamiento PRIMARY KEY,
            Codigo NVARCHAR(40) NOT NULL,
            Nombre NVARCHAR(120) NOT NULL,
            EsValorizacion BIT NOT NULL,
            EsActivo BIT NOT NULL,
            CONSTRAINT UQ_TiposTratamiento_Codigo UNIQUE (Codigo)
        );
    END;

    /* ============================================================
       4. Movimiento interno / almacenamiento
       ============================================================ */
    IF OBJECT_ID(N'dbo.MovimientosResiduo', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MovimientosResiduo
        (
            MovimientoResiduoId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MovimientosResiduo PRIMARY KEY,
            RegistroResiduoId UNIQUEIDENTIFIER NOT NULL,
            FechaMovimiento DATETIME2 NOT NULL,
            PuntoOrigenId UNIQUEIDENTIFIER NULL,
            PuntoDestinoId UNIQUEIDENTIFIER NOT NULL,
            Cantidad DECIMAL(18,3) NOT NULL,
            UnidadMedidaId INT NOT NULL,
            Observacion NVARCHAR(500) NULL,
            EstadoSincronizacionId INT NOT NULL,
            RegistradoPorUsuarioId UNIQUEIDENTIFIER NOT NULL,
            CreadoUtc DATETIME2 NOT NULL,
            ActualizadoUtc DATETIME2 NULL,
            Eliminado BIT NOT NULL,
            CONSTRAINT CK_MovimientosResiduo_Cantidad CHECK (Cantidad > 0),
            CONSTRAINT FK_MovimientosResiduo_RegistroResiduos FOREIGN KEY (RegistroResiduoId) REFERENCES dbo.RegistroResiduos(RegistroResiduoId),
            CONSTRAINT FK_MovimientosResiduo_PuntoOrigen FOREIGN KEY (PuntoOrigenId) REFERENCES dbo.PuntosResiduo(PuntoResiduoId),
            CONSTRAINT FK_MovimientosResiduo_PuntoDestino FOREIGN KEY (PuntoDestinoId) REFERENCES dbo.PuntosResiduo(PuntoResiduoId),
            CONSTRAINT FK_MovimientosResiduo_UnidadesMedida FOREIGN KEY (UnidadMedidaId) REFERENCES dbo.UnidadesMedida(UnidadMedidaId),
            CONSTRAINT FK_MovimientosResiduo_EstadosSincronizacion FOREIGN KEY (EstadoSincronizacionId) REFERENCES dbo.EstadosSincronizacion(EstadoSincronizacionId),
            CONSTRAINT FK_MovimientosResiduo_Usuarios FOREIGN KEY (RegistradoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
        );

        CREATE INDEX IX_MovimientosResiduo_Registro_Fecha ON dbo.MovimientosResiduo(RegistroResiduoId, FechaMovimiento);
        CREATE INDEX IX_MovimientosResiduo_Destino_Fecha ON dbo.MovimientosResiduo(PuntoDestinoId, FechaMovimiento);
    END;

    /* ============================================================
       5. Retiro agrupado
       ============================================================ */
    IF OBJECT_ID(N'dbo.Retiros', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Retiros
        (
            RetiroId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Retiros PRIMARY KEY,
            Codigo NVARCHAR(80) NOT NULL,
            FechaRetiro DATETIME2 NOT NULL,
            SedeId UNIQUEIDENTIFIER NOT NULL,
            PuntoAlmacenamientoId UNIQUEIDENTIFIER NULL,
            EmpresaGestoraId UNIQUEIDENTIFIER NOT NULL,
            EstadoRetiroId INT NOT NULL,
            DocumentoTransporte NVARCHAR(120) NULL,
            Vehiculo NVARCHAR(120) NULL,
            Placa NVARCHAR(20) NULL,
            Conductor NVARCHAR(160) NULL,
            Observacion NVARCHAR(500) NULL,
            EstadoSincronizacionId INT NOT NULL,
            CreadoPorUsuarioId UNIQUEIDENTIFIER NOT NULL,
            CreadoUtc DATETIME2 NOT NULL,
            ActualizadoUtc DATETIME2 NULL,
            Eliminado BIT NOT NULL,
            CONSTRAINT UQ_Retiros_Codigo UNIQUE (Codigo),
            CONSTRAINT FK_Retiros_Sedes FOREIGN KEY (SedeId) REFERENCES dbo.Sedes(SedeId),
            CONSTRAINT FK_Retiros_PuntosResiduo FOREIGN KEY (PuntoAlmacenamientoId) REFERENCES dbo.PuntosResiduo(PuntoResiduoId),
            CONSTRAINT FK_Retiros_Empresas FOREIGN KEY (EmpresaGestoraId) REFERENCES dbo.Empresas(EmpresaId),
            CONSTRAINT FK_Retiros_EstadosRetiro FOREIGN KEY (EstadoRetiroId) REFERENCES dbo.EstadosRetiro(EstadoRetiroId),
            CONSTRAINT FK_Retiros_EstadosSincronizacion FOREIGN KEY (EstadoSincronizacionId) REFERENCES dbo.EstadosSincronizacion(EstadoSincronizacionId),
            CONSTRAINT FK_Retiros_Usuarios FOREIGN KEY (CreadoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
        );

        CREATE INDEX IX_Retiros_Sede_Fecha ON dbo.Retiros(SedeId, FechaRetiro);
    END;

    IF OBJECT_ID(N'dbo.RetiroDetalles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RetiroDetalles
        (
            RetiroDetalleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RetiroDetalles PRIMARY KEY,
            RetiroId UNIQUEIDENTIFIER NOT NULL,
            RegistroResiduoId UNIQUEIDENTIFIER NOT NULL,
            Cantidad DECIMAL(18,3) NOT NULL,
            UnidadMedidaId INT NOT NULL,
            Observacion NVARCHAR(500) NULL,
            CreadoUtc DATETIME2 NOT NULL,
            CONSTRAINT CK_RetiroDetalles_Cantidad CHECK (Cantidad > 0),
            CONSTRAINT UQ_RetiroDetalles_Retiro_RegistroResiduo UNIQUE (RetiroId, RegistroResiduoId),
            CONSTRAINT FK_RetiroDetalles_Retiros FOREIGN KEY (RetiroId) REFERENCES dbo.Retiros(RetiroId),
            CONSTRAINT FK_RetiroDetalles_RegistroResiduos FOREIGN KEY (RegistroResiduoId) REFERENCES dbo.RegistroResiduos(RegistroResiduoId),
            CONSTRAINT FK_RetiroDetalles_UnidadesMedida FOREIGN KEY (UnidadMedidaId) REFERENCES dbo.UnidadesMedida(UnidadMedidaId)
        );

        CREATE INDEX IX_RetiroDetalles_RegistroResiduo ON dbo.RetiroDetalles(RegistroResiduoId);
    END;

    /* ============================================================
       6. Disposición / valorización final
       ============================================================ */
    IF OBJECT_ID(N'dbo.DisposicionesFinales', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.DisposicionesFinales
        (
            DisposicionFinalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DisposicionesFinales PRIMARY KEY,
            RetiroId UNIQUEIDENTIFIER NOT NULL,
            EmpresaGestoraId UNIQUEIDENTIFIER NOT NULL,
            FechaDisposicion DATETIME2 NULL,
            TipoTratamientoId INT NOT NULL,
            CodigoDocumento NVARCHAR(120) NULL,
            EstadoDisposicionId INT NOT NULL,
            Observacion NVARCHAR(500) NULL,
            EstadoSincronizacionId INT NOT NULL,
            CreadoPorUsuarioId UNIQUEIDENTIFIER NOT NULL,
            CreadoUtc DATETIME2 NOT NULL,
            ActualizadoUtc DATETIME2 NULL,
            Eliminado BIT NOT NULL,
            CONSTRAINT FK_DisposicionesFinales_Retiros FOREIGN KEY (RetiroId) REFERENCES dbo.Retiros(RetiroId),
            CONSTRAINT FK_DisposicionesFinales_Empresas FOREIGN KEY (EmpresaGestoraId) REFERENCES dbo.Empresas(EmpresaId),
            CONSTRAINT FK_DisposicionesFinales_TiposTratamiento FOREIGN KEY (TipoTratamientoId) REFERENCES dbo.TiposTratamiento(TipoTratamientoId),
            CONSTRAINT FK_DisposicionesFinales_EstadosDisposicion FOREIGN KEY (EstadoDisposicionId) REFERENCES dbo.EstadosDisposicion(EstadoDisposicionId),
            CONSTRAINT FK_DisposicionesFinales_EstadosSincronizacion FOREIGN KEY (EstadoSincronizacionId) REFERENCES dbo.EstadosSincronizacion(EstadoSincronizacionId),
            CONSTRAINT FK_DisposicionesFinales_Usuarios FOREIGN KEY (CreadoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
        );

        CREATE INDEX IX_DisposicionesFinales_Retiro ON dbo.DisposicionesFinales(RetiroId);
        CREATE INDEX IX_DisposicionesFinales_Empresa_Fecha ON dbo.DisposicionesFinales(EmpresaGestoraId, FechaDisposicion);
    END;

    IF OBJECT_ID(N'dbo.DisposicionFinalEvidencias', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.DisposicionFinalEvidencias
        (
            EvidenciaId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DisposicionFinalEvidencias PRIMARY KEY,
            DisposicionFinalId UNIQUEIDENTIFIER NOT NULL,
            TipoEvidencia NVARCHAR(40) NOT NULL,
            NombreArchivo NVARCHAR(260) NOT NULL,
            RutaLocal NVARCHAR(500) NULL,
            UrlNube NVARCHAR(1000) NULL,
            ContentType NVARCHAR(120) NULL,
            HashArchivo NVARCHAR(128) NULL,
            TamanoBytes BIGINT NULL,
            EstadoSincronizacionId INT NOT NULL,
            CreadoPorUsuarioId UNIQUEIDENTIFIER NOT NULL,
            CreadoUtc DATETIME2 NOT NULL,
            Eliminado BIT NOT NULL,
            CONSTRAINT FK_DisposicionFinalEvidencias_DisposicionesFinales FOREIGN KEY (DisposicionFinalId) REFERENCES dbo.DisposicionesFinales(DisposicionFinalId),
            CONSTRAINT FK_DisposicionFinalEvidencias_EstadosSincronizacion FOREIGN KEY (EstadoSincronizacionId) REFERENCES dbo.EstadosSincronizacion(EstadoSincronizacionId),
            CONSTRAINT FK_DisposicionFinalEvidencias_Usuarios FOREIGN KEY (CreadoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
        );

        CREATE INDEX IX_DisposicionFinalEvidencias_DisposicionFinal ON dbo.DisposicionFinalEvidencias(DisposicionFinalId);
    END;

    /* ============================================================
       7. Estados y catálogos
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosRegistro WHERE Codigo = 'EN_PROCESO')
        INSERT INTO dbo.EstadosRegistro (Codigo, Nombre) VALUES ('EN_PROCESO', N'En proceso');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosRegistro WHERE Codigo = 'REGISTRADO')
        INSERT INTO dbo.EstadosRegistro (Codigo, Nombre) VALUES ('REGISTRADO', N'Registrado');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosRetiro WHERE Codigo = 'PROGRAMADO')
        INSERT INTO dbo.EstadosRetiro (Codigo, Nombre) VALUES ('PROGRAMADO', N'Programado');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosRetiro WHERE Codigo = 'EN_RETIRO')
        INSERT INTO dbo.EstadosRetiro (Codigo, Nombre) VALUES ('EN_RETIRO', N'En retiro');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosRetiro WHERE Codigo = 'RETIRADO')
        INSERT INTO dbo.EstadosRetiro (Codigo, Nombre) VALUES ('RETIRADO', N'Retirado');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosRetiro WHERE Codigo = 'ANULADO')
        INSERT INTO dbo.EstadosRetiro (Codigo, Nombre) VALUES ('ANULADO', N'Anulado');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosDisposicion WHERE Codigo = 'PENDIENTE')
        INSERT INTO dbo.EstadosDisposicion (Codigo, Nombre) VALUES ('PENDIENTE', N'Pendiente');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosDisposicion WHERE Codigo = 'DOCUMENTADA')
        INSERT INTO dbo.EstadosDisposicion (Codigo, Nombre) VALUES ('DOCUMENTADA', N'Documentada');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosDisposicion WHERE Codigo = 'VALIDADA')
        INSERT INTO dbo.EstadosDisposicion (Codigo, Nombre) VALUES ('VALIDADA', N'Validada');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosDisposicion WHERE Codigo = 'OBSERVADA')
        INSERT INTO dbo.EstadosDisposicion (Codigo, Nombre) VALUES ('OBSERVADA', N'Observada');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosDisposicion WHERE Codigo = 'ANULADA')
        INSERT INTO dbo.EstadosDisposicion (Codigo, Nombre) VALUES ('ANULADA', N'Anulada');

    IF NOT EXISTS (SELECT 1 FROM dbo.TiposTratamiento WHERE Codigo = 'DISPOSICION_FINAL')
        INSERT INTO dbo.TiposTratamiento (Codigo, Nombre, EsValorizacion, EsActivo) VALUES ('DISPOSICION_FINAL', N'Disposición final', 0, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.TiposTratamiento WHERE Codigo = 'RECICLAJE')
        INSERT INTO dbo.TiposTratamiento (Codigo, Nombre, EsValorizacion, EsActivo) VALUES ('RECICLAJE', N'Reciclaje', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.TiposTratamiento WHERE Codigo = 'REUTILIZACION')
        INSERT INTO dbo.TiposTratamiento (Codigo, Nombre, EsValorizacion, EsActivo) VALUES ('REUTILIZACION', N'Reutilización', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.TiposTratamiento WHERE Codigo = 'VALORIZACION')
        INSERT INTO dbo.TiposTratamiento (Codigo, Nombre, EsValorizacion, EsActivo) VALUES ('VALORIZACION', N'Valorización', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.TiposTratamiento WHERE Codigo = 'TRATAMIENTO')
        INSERT INTO dbo.TiposTratamiento (Codigo, Nombre, EsValorizacion, EsActivo) VALUES ('TRATAMIENTO', N'Tratamiento', 0, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.UnidadesMedida WHERE Codigo = 'M3')
        INSERT INTO dbo.UnidadesMedida (Codigo, Nombre) VALUES ('M3', N'Metros cúbicos');

    /* ============================================================
       8. Catálogo de residuos alineado al formato CTG-GAM-FO-RS-002
       ============================================================ */
    DECLARE @TipoSolido UNIQUEIDENTIFIER = (SELECT TOP 1 TipoResiduoId FROM dbo.TiposResiduo WHERE Codigo = 'SOLIDO');
    DECLARE @Kg INT = (SELECT TOP 1 UnidadMedidaId FROM dbo.UnidadesMedida WHERE Codigo = 'KG');
    DECLARE @NoPeligroso INT = (SELECT TOP 1 ClasificacionResiduoId FROM dbo.ClasificacionesResiduo WHERE Codigo = 'NO_PELIGROSO');
    DECLARE @Peligroso INT = (SELECT TOP 1 ClasificacionResiduoId FROM dbo.ClasificacionesResiduo WHERE Codigo = 'PELIGROSO');

    IF @TipoSolido IS NULL OR @Kg IS NULL OR @NoPeligroso IS NULL OR @Peligroso IS NULL
        THROW 51000, 'Faltan catálogos base SOLIDO/KG/NO_PELIGROSO/PELIGROSO.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'PLASTICO')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @NoPeligroso, 'PLASTICO', N'Plástico', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'PAPEL_CARTON')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @NoPeligroso, 'PAPEL_CARTON', N'Papel y cartón', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'METALES')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @NoPeligroso, 'METALES', N'Metales', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'VIDRIO')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @NoPeligroso, 'VIDRIO', N'Vidrio', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'ORGANICOS')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @NoPeligroso, 'ORGANICOS', N'Orgánicos', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'GENERALES')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @NoPeligroso, 'GENERALES', N'Generales', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'OTROS_NO_PELIGROSO')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @NoPeligroso, 'OTROS_NO_PELIGROSO', N'Otros no peligrosos', N'Especificar el residuo en la observación.', @Kg, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'RECIPIENTES_CONTAMINADOS')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @Peligroso, 'RECIPIENTES_CONTAMINADOS', N'Recipientes contaminados', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'TRAPOS_CONTAMINADOS')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @Peligroso, 'TRAPOS_CONTAMINADOS', N'Trapos contaminados', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'TUBERIA_PE_GAS_NATURAL')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @Peligroso, 'TUBERIA_PE_GAS_NATURAL', N'Tubería de polietileno con gas natural', NULL, @Kg, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ResiduosCatalogo WHERE Codigo = 'OTROS_PELIGROSO')
        INSERT INTO dbo.ResiduosCatalogo VALUES (NEWID(), @TipoSolido, @Peligroso, 'OTROS_PELIGROSO', N'Otros peligrosos', N'Especificar el residuo en la observación.', @Kg, 1);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/*
IMPORTANTE:
- Este script es aditivo. No elimina dbo.Disposiciones ni dbo.DisposicionEvidencias.
- Esas tablas quedan como legado hasta migrar API/UI al flujo Retiros -> DisposicionesFinales.
- Empresas y PuntosResiduo no se precargan porque requieren datos reales de operación.
*/
