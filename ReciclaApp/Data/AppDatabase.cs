using SQLite;

namespace ReciclaApp.Data;

public sealed class AppDatabase
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AppDatabase()
    {
        _connection = new SQLiteAsyncConnection(
            DatabaseConstants.DatabasePath,
            DatabaseConstants.Flags);
    }

    public string DatabasePath => DatabaseConstants.DatabasePath;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            await _connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
            await _connection.ExecuteAsync("PRAGMA synchronous=NORMAL;");
            await _connection.ExecuteAsync("PRAGMA temp_store=MEMORY;");
            await _connection.ExecuteAsync("PRAGMA busy_timeout=5000;");

            await _connection.CreateTableAsync<UserEntity>();
            await _connection.CreateTableAsync<ProjectEntity>();
            await _connection.CreateTableAsync<ActivityEntity>();
            await _connection.CreateTableAsync<SiteEntity>();
            await _connection.CreateTableAsync<WasteTypeEntity>();
            await _connection.CreateTableAsync<WasteCatalogEntity>();
            await _connection.CreateTableAsync<RecordEntity>();
            await _connection.CreateTableAsync<RecordWasteEntity>();
            await _connection.CreateTableAsync<WastePhotoEntity>();
            await _connection.CreateTableAsync<DisposalEvidenceEntity>();
            await _connection.CreateTableAsync<SyncQueueEntity>();
            await _connection.CreateTableAsync<AppSettingEntity>();

            await SetSettingAsync("schema_version", DatabaseConstants.SchemaVersion.ToString());
            await SeedReferenceDataAsync();

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<List<ProjectEntity>> GetProjectsAsync()
    {
        await InitializeAsync();
        return await _connection.Table<ProjectEntity>()
            .Where(x => x.IsActive && x.DeletedAtUtc == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<List<ActivityEntity>> GetActivitiesAsync(string? projectId = null)
    {
        await InitializeAsync();
        var all = await _connection.Table<ActivityEntity>()
            .Where(x => x.IsActive && x.DeletedAtUtc == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return string.IsNullOrWhiteSpace(projectId)
            ? all
            : all.Where(x => string.IsNullOrWhiteSpace(x.ProjectId) || x.ProjectId == projectId).ToList();
    }

    public async Task<List<SiteEntity>> GetSitesAsync()
    {
        await InitializeAsync();
        return await _connection.Table<SiteEntity>()
            .Where(x => x.IsActive && x.DeletedAtUtc == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<List<WasteTypeEntity>> GetWasteTypesAsync()
    {
        await InitializeAsync();
        return await _connection.Table<WasteTypeEntity>()
            .Where(x => x.IsActive && x.DeletedAtUtc == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<List<WasteCatalogEntity>> GetWasteCatalogAsync(string? wasteTypeId = null)
    {
        await InitializeAsync();
        var all = await _connection.Table<WasteCatalogEntity>()
            .Where(x => x.IsActive && x.DeletedAtUtc == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return string.IsNullOrWhiteSpace(wasteTypeId)
            ? all
            : all.Where(x => x.WasteTypeId == wasteTypeId).ToList();
    }

    public async Task<UserEntity> GetOrCreateLocalUserAsync(string username, string displayName)
    {
        await InitializeAsync();

        var normalizedUsername = username.Trim().ToLowerInvariant();
        var existing = await _connection.Table<UserEntity>()
            .FirstOrDefaultAsync(x => x.Username == normalizedUsername && x.DeletedAtUtc == null);

        if (existing is not null)
        {
            existing.DisplayName = displayName.Trim();
            existing.LastLoginAtUtc = DateTime.UtcNow;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.LocalRevision++;
            await _connection.UpdateAsync(existing);
            return existing;
        }

        var user = new UserEntity
        {
            Username = normalizedUsername,
            DisplayName = displayName.Trim(),
            LastLoginAtUtc = DateTime.UtcNow,
            SyncState = (int)SyncState.Pending
        };

        await InsertWithSyncQueueAsync(user, nameof(UserEntity), SyncOperationType.Upsert);
        return user;
    }

    public async Task<RecordEntity> CreateRecordAsync(
        string userId,
        string projectId,
        string activityId,
        string siteId,
        string? notes = null,
        string? deviceId = null)
    {
        await InitializeAsync();

        var record = new RecordEntity
        {
            UserId = userId,
            ProjectId = projectId,
            ActivityId = activityId,
            SiteId = siteId,
            RecordedAtUtc = DateTime.UtcNow,
            Status = (int)RegistroStatus.Draft,
            Notes = notes?.Trim(),
            DeviceId = deviceId,
            SyncState = (int)SyncState.Pending
        };

        await InsertWithSyncQueueAsync(record, nameof(RecordEntity), SyncOperationType.Upsert);
        return record;
    }

    public async Task<RecordWasteEntity> CreateRecordWasteAsync(
        string recordId,
        string wasteCatalogId,
        double quantity,
        string? unit = null,
        string? observation = null)
    {
        await InitializeAsync();

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser mayor que cero.");
        }

        var catalog = await _connection.FindAsync<WasteCatalogEntity>(wasteCatalogId)
            ?? throw new InvalidOperationException("El residuo seleccionado no existe en el catálogo local.");

        var item = new RecordWasteEntity
        {
            RecordId = recordId,
            WasteCatalogId = wasteCatalogId,
            RecordedAtUtc = DateTime.UtcNow,
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? catalog.DefaultUnit : unit.Trim(),
            Observation = observation?.Trim(),
            SyncState = (int)SyncState.Pending
        };

        await InsertWithSyncQueueAsync(item, nameof(RecordWasteEntity), SyncOperationType.Upsert);
        return item;
    }

    public async Task<WastePhotoEntity> AddWastePhotoAsync(
        string recordWasteId,
        string localPath,
        string fileName,
        string mimeType,
        long sizeBytes,
        string? contentHash = null)
    {
        await InitializeAsync();

        var photo = new WastePhotoEntity
        {
            RecordWasteId = recordWasteId,
            LocalPath = localPath,
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            ContentHash = contentHash,
            CapturedAtUtc = DateTime.UtcNow,
            SyncState = (int)SyncState.Pending
        };

        await InsertWithSyncQueueAsync(photo, nameof(WastePhotoEntity), SyncOperationType.UploadMedia);
        return photo;
    }

    public async Task<DisposalEvidenceEntity> AddDisposalEvidenceAsync(
        string recordId,
        string localPath,
        string fileName,
        string mimeType,
        long sizeBytes,
        string? contentHash = null)
    {
        await InitializeAsync();

        var evidence = new DisposalEvidenceEntity
        {
            RecordId = recordId,
            LocalPath = localPath,
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            ContentHash = contentHash,
            CapturedAtUtc = DateTime.UtcNow,
            SyncState = (int)SyncState.Pending
        };

        await InsertWithSyncQueueAsync(evidence, nameof(DisposalEvidenceEntity), SyncOperationType.UploadMedia);
        return evidence;
    }

    public async Task<List<RecordEntity>> GetRecentRecordsAsync(int limit = 50)
    {
        await InitializeAsync();

        var records = await _connection.Table<RecordEntity>()
            .Where(x => x.DeletedAtUtc == null)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ToListAsync();

        return records.Take(Math.Max(1, limit)).ToList();
    }

    public async Task<List<RecordWasteEntity>> GetRecordWasteAsync(string recordId)
    {
        await InitializeAsync();
        return await _connection.Table<RecordWasteEntity>()
            .Where(x => x.RecordId == recordId && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ToListAsync();
    }

    public async Task<List<WastePhotoEntity>> GetWastePhotosAsync(string recordWasteId)
    {
        await InitializeAsync();
        return await _connection.Table<WastePhotoEntity>()
            .Where(x => x.RecordWasteId == recordWasteId && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.CapturedAtUtc)
            .ToListAsync();
    }

    public async Task<List<DisposalEvidenceEntity>> GetDisposalEvidenceAsync(string recordId)
    {
        await InitializeAsync();
        return await _connection.Table<DisposalEvidenceEntity>()
            .Where(x => x.RecordId == recordId && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.CapturedAtUtc)
            .ToListAsync();
    }

    public async Task CompleteRecordAsync(string recordId)
    {
        await InitializeAsync();
        var record = await _connection.FindAsync<RecordEntity>(recordId)
            ?? throw new InvalidOperationException("El registro no existe.");

        record.Status = (int)RegistroStatus.Completed;
        MarkPending(record);

        await UpdateWithSyncQueueAsync(record, nameof(RecordEntity), SyncOperationType.Upsert);
    }

    public async Task SoftDeleteRecordAsync(string recordId)
    {
        await InitializeAsync();
        var record = await _connection.FindAsync<RecordEntity>(recordId)
            ?? throw new InvalidOperationException("El registro no existe.");

        record.DeletedAtUtc = DateTime.UtcNow;
        MarkPending(record);

        await UpdateWithSyncQueueAsync(record, nameof(RecordEntity), SyncOperationType.Delete);
    }

    public async Task<List<SyncQueueEntity>> GetPendingSyncOperationsAsync(int limit = 100)
    {
        await InitializeAsync();
        var now = DateTime.UtcNow;

        var pending = await _connection.Table<SyncQueueEntity>()
            .Where(x => !x.IsCompleted)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        return pending
            .Where(x => x.NextAttemptAtUtc is null || x.NextAttemptAtUtc <= now)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    public async Task MarkSyncOperationCompletedAsync(string queueId)
    {
        await InitializeAsync();
        var item = await _connection.FindAsync<SyncQueueEntity>(queueId);
        if (item is null)
        {
            return;
        }

        item.IsCompleted = true;
        item.LastAttemptAtUtc = DateTime.UtcNow;
        item.LastError = null;
        await _connection.UpdateAsync(item);
    }

    public async Task MarkSyncOperationFailedAsync(string queueId, string error)
    {
        await InitializeAsync();
        var item = await _connection.FindAsync<SyncQueueEntity>(queueId);
        if (item is null)
        {
            return;
        }

        item.RetryCount++;
        item.LastAttemptAtUtc = DateTime.UtcNow;
        item.LastError = error.Length > 1000 ? error[..1000] : error;

        var delayMinutes = Math.Min(60, Math.Pow(2, Math.Min(item.RetryCount, 6)));
        item.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(delayMinutes);
        await _connection.UpdateAsync(item);
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await InitializeAsync();
        var setting = await _connection.FindAsync<AppSettingEntity>(key);
        return setting?.Value;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        var existing = await _connection.FindAsync<AppSettingEntity>(key);
        if (existing is null)
        {
            await _connection.InsertAsync(new AppSettingEntity
            {
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow
            });
            return;
        }

        existing.Value = value;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _connection.UpdateAsync(existing);
    }

    private async Task InsertWithSyncQueueAsync<T>(
        T entity,
        string entityType,
        SyncOperationType operationType)
        where T : SyncableEntity
    {
        var queueItem = CreateQueueItem(entityType, entity.Id, operationType);

        await _connection.RunInTransactionAsync(connection =>
        {
            connection.Insert(entity);
            connection.Insert(queueItem);
        });
    }

    private async Task UpdateWithSyncQueueAsync<T>(
        T entity,
        string entityType,
        SyncOperationType operationType)
        where T : SyncableEntity
    {
        var queueItem = CreateQueueItem(entityType, entity.Id, operationType);

        await _connection.RunInTransactionAsync(connection =>
        {
            connection.Update(entity);
            connection.Insert(queueItem);
        });
    }

    private static SyncQueueEntity CreateQueueItem(
        string entityType,
        string entityId,
        SyncOperationType operationType) => new()
    {
        EntityType = entityType,
        EntityId = entityId,
        OperationType = (int)operationType,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static void MarkPending(SyncableEntity entity)
    {
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.LocalRevision++;
        entity.SyncState = (int)SyncState.Pending;
    }

    private async Task SeedReferenceDataAsync()
    {
        if (await _connection.Table<ProjectEntity>().CountAsync() > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var dot = SeedSynced(new ProjectEntity
        {
            Id = "project-dot",
            Code = "DOT",
            Name = "DOT",
            SortOrder = 1
        }, now);

        var maintenance = SeedSynced(new ActivityEntity
        {
            Id = "activity-maintenance",
            ProjectId = dot.Id,
            Name = "Mantenimiento",
            SortOrder = 1
        }, now);

        var collection = SeedSynced(new ActivityEntity
        {
            Id = "activity-collection",
            ProjectId = dot.Id,
            Name = "Recolección",
            SortOrder = 2
        }, now);

        var pisco = SeedSynced(new SiteEntity
        {
            Id = "site-pisco",
            Code = "PISCO",
            Name = "Pisco",
            SortOrder = 1
        }, now);

        var lima = SeedSynced(new SiteEntity
        {
            Id = "site-lima",
            Code = "LIMA",
            Name = "Lima",
            SortOrder = 2
        }, now);

        var nonHazardous = SeedSynced(new WasteTypeEntity
        {
            Id = "waste-type-non-hazardous",
            Code = "NO_PELIGROSO",
            Name = "No peligroso",
            SortOrder = 1
        }, now);

        var hazardous = SeedSynced(new WasteTypeEntity
        {
            Id = "waste-type-hazardous",
            Code = "PELIGROSO",
            Name = "Peligroso",
            SortOrder = 2
        }, now);

        var paper = SeedSynced(new WasteCatalogEntity
        {
            Id = "waste-paper-cardboard",
            WasteTypeId = nonHazardous.Id,
            Code = "PAPEL_CARTON",
            Name = "Papel y cartón",
            DefaultUnit = "kg",
            IsHazardous = false,
            SortOrder = 1
        }, now);

        var glass = SeedSynced(new WasteCatalogEntity
        {
            Id = "waste-glass",
            WasteTypeId = nonHazardous.Id,
            Code = "VIDRIO",
            Name = "Vidrio",
            DefaultUnit = "kg",
            IsHazardous = false,
            SortOrder = 2
        }, now);

        var contaminatedContainers = SeedSynced(new WasteCatalogEntity
        {
            Id = "waste-contaminated-containers",
            WasteTypeId = hazardous.Id,
            Code = "RECIPIENTES_CONTAMINADOS",
            Name = "Recipientes contaminados",
            DefaultUnit = "kg",
            IsHazardous = true,
            SortOrder = 1
        }, now);

        await _connection.RunInTransactionAsync(connection =>
        {
            connection.Insert(dot);
            connection.Insert(maintenance);
            connection.Insert(collection);
            connection.Insert(pisco);
            connection.Insert(lima);
            connection.Insert(nonHazardous);
            connection.Insert(hazardous);
            connection.Insert(paper);
            connection.Insert(glass);
            connection.Insert(contaminatedContainers);
        });
    }

    private static T SeedSynced<T>(T entity, DateTime now) where T : SyncableEntity
    {
        entity.CreatedAtUtc = now;
        entity.UpdatedAtUtc = now;
        entity.SyncState = (int)SyncState.Synced;
        entity.LocalRevision = 1;
        return entity;
    }
}
