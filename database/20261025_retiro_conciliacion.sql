SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.RetiroDetalles', N'U') IS NULL
        THROW 51020, 'No existe dbo.RetiroDetalles. Ejecute primero la migración de trazabilidad.', 1;

    IF COL_LENGTH('dbo.RetiroDetalles', 'TipoRetiro') IS NULL
        ALTER TABLE dbo.RetiroDetalles ADD TipoRetiro NVARCHAR(10) NULL;

    IF COL_LENGTH('dbo.RetiroDetalles', 'SaldoEstimadoAntes') IS NULL
        ALTER TABLE dbo.RetiroDetalles ADD SaldoEstimadoAntes DECIMAL(18,3) NULL;

    IF COL_LENGTH('dbo.RetiroDetalles', 'DiferenciaConciliacion') IS NULL
        ALTER TABLE dbo.RetiroDetalles ADD DiferenciaConciliacion DECIMAL(18,3) NULL;

    /* Los retiros previos no conocían la intención TOTAL/PARCIAL.
       Se conservan como PARCIAL para no reinterpretar historia. */
    UPDATE dbo.RetiroDetalles
    SET TipoRetiro = ISNULL(TipoRetiro, 'PARCIAL'),
        SaldoEstimadoAntes = ISNULL(SaldoEstimadoAntes, Cantidad);

    ALTER TABLE dbo.RetiroDetalles ALTER COLUMN TipoRetiro NVARCHAR(10) NOT NULL;
    ALTER TABLE dbo.RetiroDetalles ALTER COLUMN SaldoEstimadoAntes DECIMAL(18,3) NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_RetiroDetalles_TipoRetiro')
    BEGIN
        ALTER TABLE dbo.RetiroDetalles
        ADD CONSTRAINT CK_RetiroDetalles_TipoRetiro
        CHECK (TipoRetiro IN ('PARCIAL', 'TOTAL'));
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
