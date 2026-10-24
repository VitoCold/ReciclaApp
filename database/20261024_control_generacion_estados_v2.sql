SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.EstadosControlGeneracion', N'U') IS NULL
        THROW 51020, 'La tabla dbo.EstadosControlGeneracion no existe. Ejecute primero la migración base de controles de generación.', 1;

    IF OBJECT_ID(N'dbo.ControlesGeneracion', N'U') IS NULL
        THROW 51021, 'La tabla dbo.ControlesGeneracion no existe. Ejecute primero la migración base de controles de generación.', 1;

    /* ============================================================
       1. Normalizar estados heredados conservando sus referencias
       ============================================================ */

    -- PENDIENTE_APROBACION -> PENDIENTE_REVISION
    IF EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'PENDIENTE_APROBACION')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'PENDIENTE_REVISION')
        BEGIN
            UPDATE dbo.EstadosControlGeneracion
            SET Codigo = 'PENDIENTE_REVISION',
                Nombre = N'Pendiente de revisión'
            WHERE Codigo = 'PENDIENTE_APROBACION';
        END
        ELSE
        BEGIN
            DECLARE @PendienteViejoId INT =
                (SELECT TOP 1 EstadoControlGeneracionId FROM dbo.EstadosControlGeneracion WHERE Codigo = 'PENDIENTE_APROBACION');
            DECLARE @PendienteNuevoId INT =
                (SELECT TOP 1 EstadoControlGeneracionId FROM dbo.EstadosControlGeneracion WHERE Codigo = 'PENDIENTE_REVISION');

            UPDATE dbo.ControlesGeneracion
            SET EstadoControlGeneracionId = @PendienteNuevoId
            WHERE EstadoControlGeneracionId = @PendienteViejoId;

            DELETE FROM dbo.EstadosControlGeneracion
            WHERE EstadoControlGeneracionId = @PendienteViejoId;
        END
    END;

    -- RECHAZADO -> OBSERVADO
    IF EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'RECHAZADO')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'OBSERVADO')
        BEGIN
            UPDATE dbo.EstadosControlGeneracion
            SET Codigo = 'OBSERVADO',
                Nombre = N'Observado'
            WHERE Codigo = 'RECHAZADO';
        END
        ELSE
        BEGIN
            DECLARE @RechazadoId INT =
                (SELECT TOP 1 EstadoControlGeneracionId FROM dbo.EstadosControlGeneracion WHERE Codigo = 'RECHAZADO');
            DECLARE @ObservadoId INT =
                (SELECT TOP 1 EstadoControlGeneracionId FROM dbo.EstadosControlGeneracion WHERE Codigo = 'OBSERVADO');

            UPDATE dbo.ControlesGeneracion
            SET EstadoControlGeneracionId = @ObservadoId
            WHERE EstadoControlGeneracionId = @RechazadoId;

            DELETE FROM dbo.EstadosControlGeneracion
            WHERE EstadoControlGeneracionId = @RechazadoId;
        END
    END;

    -- CERRADO -> FINALIZADO
    IF EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'CERRADO')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'FINALIZADO')
        BEGIN
            UPDATE dbo.EstadosControlGeneracion
            SET Codigo = 'FINALIZADO',
                Nombre = N'Finalizado'
            WHERE Codigo = 'CERRADO';
        END
        ELSE
        BEGIN
            DECLARE @CerradoId INT =
                (SELECT TOP 1 EstadoControlGeneracionId FROM dbo.EstadosControlGeneracion WHERE Codigo = 'CERRADO');
            DECLARE @FinalizadoId INT =
                (SELECT TOP 1 EstadoControlGeneracionId FROM dbo.EstadosControlGeneracion WHERE Codigo = 'FINALIZADO');

            UPDATE dbo.ControlesGeneracion
            SET EstadoControlGeneracionId = @FinalizadoId
            WHERE EstadoControlGeneracionId = @CerradoId;

            DELETE FROM dbo.EstadosControlGeneracion
            WHERE EstadoControlGeneracionId = @CerradoId;
        END
    END;

    /* ============================================================
       2. Asegurar la máquina de estados v2
       ============================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'BORRADOR')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('BORRADOR', N'Borrador');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'PENDIENTE_REVISION')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('PENDIENTE_REVISION', N'Pendiente de revisión');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'OBSERVADO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('OBSERVADO', N'Observado');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'APROBADO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('APROBADO', N'Aprobado');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'ACTIVO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('ACTIVO', N'Activo');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'FINALIZADO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('FINALIZADO', N'Finalizado');

    -- Estados excepcionales que se conservan fuera del flujo normal.
    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'SUSPENDIDO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('SUSPENDIDO', N'Suspendido');

    IF NOT EXISTS (SELECT 1 FROM dbo.EstadosControlGeneracion WHERE Codigo = 'ANULADO')
        INSERT INTO dbo.EstadosControlGeneracion (Codigo, Nombre) VALUES ('ANULADO', N'Anulado');

    UPDATE dbo.EstadosControlGeneracion
    SET Nombre = CASE Codigo
        WHEN 'BORRADOR' THEN N'Borrador'
        WHEN 'PENDIENTE_REVISION' THEN N'Pendiente de revisión'
        WHEN 'OBSERVADO' THEN N'Observado'
        WHEN 'APROBADO' THEN N'Aprobado'
        WHEN 'ACTIVO' THEN N'Activo'
        WHEN 'FINALIZADO' THEN N'Finalizado'
        WHEN 'SUSPENDIDO' THEN N'Suspendido'
        WHEN 'ANULADO' THEN N'Anulado'
        ELSE Nombre
    END
    WHERE Codigo IN (
        'BORRADOR',
        'PENDIENTE_REVISION',
        'OBSERVADO',
        'APROBADO',
        'ACTIVO',
        'FINALIZADO',
        'SUSPENDIDO',
        'ANULADO');

    /*
      Nota deliberada:
      Los controles que ya estaban ACTIVO permanecen ACTIVO. En la versión
      anterior, la aprobación Ambiental activaba el control inmediatamente,
      por lo que no es posible inferir de forma segura si una actividad histórica
      ya había iniciado o solo había sido aprobada.
    */

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
