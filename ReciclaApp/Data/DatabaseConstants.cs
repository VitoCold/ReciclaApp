using SQLite;

namespace ReciclaApp.Data;

public static class DatabaseConstants
{
    public const string DatabaseFilename = "reciclaapp.db3";
    public const int SchemaVersion = 1;

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
