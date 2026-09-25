SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Empresas', N'U') IS NULL OR OBJECT_ID(N'dbo.PuntosResiduo', N'U') IS NULL
        THROW 51001, 'Ejecute primero database/20260925_trazabilidad_residuos.sql.', 1;

    /* ============================================================
       1. Roles globales
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Codigo = 'AMBIENTAL')
        INSERT INTO dbo.Roles (Codigo, Nombre, Descripcion, EsActivo)
        VALUES ('AMBIENTAL', N'Ambiental', N'Aprueba y supervisa todos los controles de generación.', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Codigo = 'RESPONSABLE_OPERATIVO')
        INSERT INTO dbo.Roles (Codigo, Nombre, Descripcion, EsActivo)
        VALUES ('RESPONSABLE_OPERATIVO', N'Responsable operativo', N'Crea controles de generación y administra el personal asignado.', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Codigo = 'REGISTRADOR')
        INSERT INTO dbo.Roles (Codigo, Nombre, Descripcion, EsActivo)
        VALUES ('REGISTRADOR', N'Registrador', N'Registra residuos en controles de generación donde está asignado.', 1);

    DECLARE @RolResponsable INT = (SELECT TOP 1 RolId FROM dbo.Roles WHERE Codigo = 'RESPONSABLE_OPERATIVO');
    DECLARE @RolRegistrador INT = (SELECT TOP 1 RolId FROM dbo.Roles WHERE Codigo = 'REGISTRADOR');

    -- Compatibilidad: SUPERVISOR -> RESPONSABLE_OPERATIVO y GENERAL -> REGISTRADOR.
    INSERT INTO dbo.UsuarioRoles (UsuarioId, RolId, CreadoUtc)
    SELECT ur.UsuarioId, @RolResponsable, SYSUTCDATETIME()
    FROM dbo.UsuarioRoles ur
    INNER JOIN dbo.Roles r ON r.RolId = ur.RolId AND r.Codigo = 'SUPERVISOR'
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.UsuarioRoles x
        WHERE x.UsuarioId = ur.UsuarioId AND x.RolId = @RolResponsable);

    INSERT INTO dbo.UsuarioRoles (UsuarioId, RolId, CreadoUtc)
    SELECT ur.UsuarioId, @RolRegistrador, SYSUTCDATETIME()
    FROM dbo.UsuarioRoles ur
    INNER JOIN dbo.Roles r ON r.RolId = ur.RolId AND r.Codigo = 'GENERAL'
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.UsuarioRoles x
        WHERE x.UsuarioId = ur.UsuarioId AND x.RolId = @RolRegistrador);

    /* ============================================================
       2. Estados del control
       ============================================================ */
    IF OBJECT_ID(N'dbo.EstadosControlGeneracion', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EstadosControlGeneracion
        (
            EstadoControlGeneracionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EstadosControlGeneracion PRIMARY KEY,
            Codigo NVARCHAR(40) NOT NULL,
            Nombre NVARCHAR(80) NOT NULL,
            CONSTRAINT UQ_EstadosControlGeneracion_Codigo UNIQUE (Codigo)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'PENDIENTE_APROBACION')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('PENDIENTE_APROBACION', N'Pendiente de aprobación');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'ACTIVO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('ACTIVO', N'Activo');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'RECHAZADO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('RECHAZADO', N'Rechazado');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'SUSPENDIDO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('SUSPENDIDO', N'Suspendido');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'CERRADO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('CERRADO', N'Cerrado');
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'ANULADO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('ANULADO', N'Anulado');

    /* ============================================================
       3. Control de generación
       ============================================================ */
    IF OBJECT_ID(N'dbo.ControlesGeneracion', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ControlesGeneracion
        (
            ControlGeneracionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ControlesGeneracion PRIMARY KEY,
            Codigo NVARCHAR(80) NOT NULL,
            SedeId UNIQUEIDENTIFIER NOT NULL,
            EmpresaResponsableId UNIQUEIDENTIFIER NOT NULL,
            ProyectoId UNIQUEIDENTIFIER NOT NULL,
            ActividadId UNIQUEIDENTIFIER NOT NULL,
            PuntoGeneracionId UNIQUEIDENTIFIER NULL,
            DescripcionTrabajo NVARCHAR(300) NULL,
            FechaInicio DATETIME2 NOT NULL,
            FechaFin DATETIME2 NULL,
            EstadoControlGeneracionId INT NOT NULL,
            Observacion NVARCHAR(500) NULL,
            MotivoUltimoCambio NVARCHAR(500) NULL,
            CreadoPorUsuarioId UNIQUEIDENTIFIER NOT NULL,
            CreadoUtc DATETIME2 NOT NULL,
            ActualizadoUtc DATETIME2 NULL,
            AprobadoPorUsuarioId UNIQUEIDENTIFIER NULL,
            AprobadoUtc DATETIME2 NULL,
            Eliminado BIT NOT NULL,
            CONSTRAINT UQ_ControlesGeneracion_Codigo UNIQUE (Codigo),
            CONSTRAINT CK_ControlesGeneracion_Fechas CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio),
            CONSTRAINT FK_ControlesGeneracion_Sedes FOREIGN KEY (SedeId) REFERENCES dbo.Sedes(SedeId),
            CONSTRAINT FK_ControlesGeneracion_Empresas FOREIGN KEY (EmpresaResponsableId) REFERENCES dbo.Empresas(EmpresaId),
            CONSTRAINT FK_ControlesGeneracion_Proyectos FOREIGN KEY (ProyectoId) REFERENCES dbo.Proyectos(ProyectoId),
            CONSTRAINT FK_ControlesGeneracion_Actividades FOREIGN KEY (ActividadId) REFERENCES dbo.Actividades(ActividadId),
            CONSTRAINT FK_ControlesGeneracion_PuntosResiduo FOREIGN KEY (PuntoGeneracionId) REFERENCES dbo.PuntosResiduo(PuntoResiduoId),
            CONSTRAINT FK_ControlesGeneracion_Estados FOREIGN KEY (EstadoControlGeneracionId) REFERENCES dbo.EstadosControlGeneracion(EstadoControlGeneracionId),
            CONSTRAINT FK_ControlesGeneracion_CreadoPor FOREIGN KEY (CreadoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId),
            CONSTRAINT FK_ControlesGeneracion_AprobadoPor FOREIGN KEY (AprobadoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
        );

        CREATE INDEX IX_ControlesGeneracion_Sede_Fecha ON dbo.ControlesGeneracion(SedeId, FechaInicio);
        CREATE INDEX IX_ControlesGeneracion_Empresa_Fecha ON dbo.ControlesGeneracion(EmpresaResponsableId, FechaInicio);
        CREATE INDEX IX_ControlesGeneracion_Estado ON dbo.ControlesGeneracion(EstadoControlGeneracionId);
    END;

    /* ============================================================
       4. Participantes y alcance por control
       ============================================================ */
    IF OBJECT_ID(N'dbo.ControlGeneracionUsuarios', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ControlGeneracionUsuarios
        (
            ControlGeneracionUsuarioId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ControlGeneracionUsuarios PRIMARY KEY,
            ControlGeneracionId UNIQUEIDENTIFIER NOT NULL,
            UsuarioId UNIQUEIDENTIFIER NOT NULL,
            RolControl NVARCHAR(20) NOT NULL,
            EsPrincipal BIT NOT NULL,
            FechaDesde DATETIME2 NOT NULL,
            FechaHasta DATETIME2 NULL,
            EsActivo BIT NOT NULL,
            AsignadoPorUsuarioId UNIQUEIDENTIFIER NOT NULL,
            CreadoUtc DATETIME2 NOT NULL,
            ActualizadoUtc DATETIME2 NULL,
            CONSTRAINT CK_ControlGeneracionUsuarios_RolControl CHECK (RolControl IN ('RESPONSABLE','REGISTRADOR')),
            CONSTRAINT CK_ControlGeneracionUsuarios_Fechas CHECK (FechaHasta IS NULL OR FechaHasta >= FechaDesde),
            CONSTRAINT FK_ControlGeneracionUsuarios_Control FOREIGN KEY (ControlGeneracionId) REFERENCES dbo.ControlesGeneracion(ControlGeneracionId),
            CONSTRAINT FK_ControlGeneracionUsuarios_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(UsuarioId),
            CONSTRAINT FK_ControlGeneracionUsuarios_AsignadoPor FOREIGN KEY (AsignadoPorUsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
        );

        CREATE INDEX IX_ControlGeneracionUsuarios_Control_Usuario_Rol ON dbo.ControlGeneracionUsuarios(ControlGeneracionId, UsuarioId, RolControl);
        CREATE INDEX IX_ControlGeneracionUsuarios_Usuario_Activo ON dbo.ControlGeneracionUsuarios(UsuarioId, EsActivo);
    END;

    /* ============================================================
       5. Los eventos de generación quedan bajo un control
       ============================================================ */
    IF COL_LENGTH('dbo.Registros', 'ControlGeneracionId') IS NULL
        ALTER TABLE dbo.Registros ADD ControlGeneracionId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Registros_ControlesGeneracion_ControlGeneracionId')
        ALTER TABLE dbo.Registros
            ADD CONSTRAINT FK_Registros_ControlesGeneracion_ControlGeneracionId
            FOREIGN KEY (ControlGeneracionId) REFERENCES dbo.ControlesGeneracion(ControlGeneracionId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Registros_ControlGeneracionId' AND object_id = OBJECT_ID('dbo.Registros'))
        CREATE INDEX IX_Registros_ControlGeneracionId ON dbo.Registros(ControlGeneracionId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/*
REGLAS DE APLICACIÓN:
- RESPONSABLE_OPERATIVO crea ControlesGeneracion en PENDIENTE_APROBACION.
- AMBIENTAL tiene alcance global y es el único rol que aprueba o modifica la cabecera del control.
- Un control debe estar ACTIVO para admitir nuevos Registros.
- RESPONSABLE y REGISTRADOR ven todos los Registros del control donde tienen asignación vigente.
- La edición de un Registro continúa limitada a su autor.
- Un control ACTIVO no debe quedar sin al menos un RESPONSABLE vigente; esta regla se valida en la API.
- La columna Registros.ControlGeneracionId queda NULL temporalmente para compatibilidad con registros históricos.
*/
