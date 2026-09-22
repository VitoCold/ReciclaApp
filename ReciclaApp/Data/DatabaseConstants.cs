using SQLite;

namespace ReciclaApp.Data;

public static class DatabaseConstants
{
    public const string DatabaseFilename = "reciclaapp.db3";
    public const int SchemaVersion = 1;

    public const string SchemaVersionKey = "schema_version";
    public const string CurrentUserIdKey = "current_user_id";
    public const string CurrentRecordIdKey = "current_record_id";
    public const string CurrentRecordWasteIdKey = "current_record_waste_id";
    public const string LastCatalogSyncUtcKey = "last_catalog_sync_utc";

    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);

    public const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;
}

public enum SyncState
{
    Synced = 0,
    Pending = 1,
    Failed = 2
}

public enum RegistroStatus
{
    Draft = 0,
    Completed = 1
}

public enum SyncOperationType
{
    Upsert = 0,
    Delete = 1,
    UploadMedia = 2
}
