SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.RegistroResiduoUbicaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistroResiduoUbicaciones
    (
        RegistroResiduoId UNIQUEIDENTIFIER NOT NULL,
        Latitud FLOAT NOT NULL,
        Longitud FLOAT NOT NULL,
        PrecisionMetros FLOAT NULL,
        CapturadaUtc DATETIME2 NOT NULL,
        ActualizadoUtc DATETIME2 NOT NULL,
        CONSTRAINT PK_RegistroResiduoUbicaciones PRIMARY KEY (RegistroResiduoId),
        CONSTRAINT FK_RegistroResiduoUbicaciones_RegistroResiduos
            FOREIGN KEY (RegistroResiduoId)
            REFERENCES dbo.RegistroResiduos (RegistroResiduoId)
    );
END;

COMMIT TRANSACTION;
