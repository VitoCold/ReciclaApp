using SQLite;

namespace ReciclaApp.Data;

public abstract class SyncableEntity
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    public int SyncState { get; set; } = (int)Data.SyncState.Pending;
    public long LocalRevision { get; set; } = 1;
    public string? RemoteRevision { get; set; }
}

[Table("users")]
public sealed class UserEntity : SyncableEntity
{
    [Indexed(Unique = true)]
    [MaxLength(120)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }
}

[Table("projects")]
public sealed class ProjectEntity : SyncableEntity
{
    [Indexed(Unique = true)]
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

[Table("activities")]
public sealed class ActivityEntity : SyncableEntity
{
    [Indexed]
    public string? ProjectId { get; set; }

    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

[Table("sites")]
public sealed class SiteEntity : SyncableEntity
{
    [Indexed(Unique = true)]
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(240)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

[Table("waste_types")]
public sealed class WasteTypeEntity : SyncableEntity
{
    [Indexed(Unique = true)]
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

[Table("waste_catalog")]
public sealed class WasteCatalogEntity : SyncableEntity
{
    [Indexed]
    public string WasteTypeId { get; set; } = string.Empty;

    [Indexed(Unique = true)]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(180)]
    public string Name { get; set; } = string.Empty;

    public bool IsHazardous { get; set; }

    [MaxLength(30)]
    public string DefaultUnit { get; set; } = "kg";

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

[Table("records")]
public sealed class RecordEntity : SyncableEntity
{
    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string ProjectId { get; set; } = string.Empty;

    [Indexed]
    public string ActivityId { get; set; } = string.Empty;

    [Indexed]
    public string SiteId { get; set; } = string.Empty;

    [Indexed]
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public int Status { get; set; } = (int)RegistroStatus.Draft;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(80)]
    public string? DeviceId { get; set; }
}

[Table("record_waste")]
public sealed class RecordWasteEntity : SyncableEntity
{
    [Indexed]
    public string RecordId { get; set; } = string.Empty;

    [Indexed]
    public string WasteCatalogId { get; set; } = string.Empty;

    [Indexed]
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public double Quantity { get; set; }

    [MaxLength(30)]
    public string Unit { get; set; } = "kg";

    [MaxLength(600)]
    public string? Observation { get; set; }
}

[Table("waste_photos")]
public sealed class WastePhotoEntity : SyncableEntity
{
    [Indexed]
    public string RecordWasteId { get; set; } = string.Empty;

    [MaxLength(600)]
    public string LocalPath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? RemoteUrl { get; set; }

    [MaxLength(180)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string MimeType { get; set; } = "image/jpeg";

    public long SizeBytes { get; set; }

    [MaxLength(128)]
    public string? ContentHash { get; set; }

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

[Table("disposal_evidence")]
public sealed class DisposalEvidenceEntity : SyncableEntity
{
    [Indexed]
    public string RecordId { get; set; } = string.Empty;

    [MaxLength(600)]
    public string LocalPath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? RemoteUrl { get; set; }

    [MaxLength(180)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string MimeType { get; set; } = "image/jpeg";

    public long SizeBytes { get; set; }

    [MaxLength(128)]
    public string? ContentHash { get; set; }

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

[Table("sync_queue")]
public sealed class SyncQueueEntity
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Indexed]
    [MaxLength(80)]
    public string EntityType { get; set; } = string.Empty;

    [Indexed]
    public string EntityId { get; set; } = string.Empty;

    public int OperationType { get; set; } = (int)SyncOperationType.Upsert;

    public string? PayloadJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public bool IsCompleted { get; set; }
}

[Table("app_settings")]
public sealed class AppSettingEntity
{
    [PrimaryKey]
    [MaxLength(120)]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
