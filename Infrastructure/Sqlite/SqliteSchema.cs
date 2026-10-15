namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// SQLite veritabanının şema sözleşmesi. Tablo/sütun adları koda dağılmaz; burada
/// sürümlenmiş sabitler ve CREATE ifadeleri olarak tanımlanır. FOREIGN KEY kontrolleri
/// bağlantı düzeyinde aktifleştirilir; UNIQUE/CHECK kısıtları veri bütünlüğünü korur.
/// </summary>
internal static class SqliteSchema
{
    public const string SchemaVersion = "1.6";
    public const string SchemaVersionKey = "SchemaVersion";

    public static readonly string[] CreateStatements =
    {
        // --- Uygulama ayarları (şema sürümü vb.) ---
        """
        CREATE TABLE IF NOT EXISTS AppSettings (
            SettingKey   TEXT PRIMARY KEY,
            SettingValue TEXT NOT NULL
        );
        """,

        // --- Ortak node kataloğu (bir node hem başlık hem öğe olabilir) ---
        """
        CREATE TABLE IF NOT EXISTS Nodes (
            NodeId       TEXT PRIMARY KEY,
            Name         TEXT NOT NULL,
            Description  TEXT NULL,
            IsActive     INTEGER NOT NULL DEFAULT 1,
            CanBeRoot    INTEGER NOT NULL DEFAULT 1,
            DisplayOrder INTEGER NOT NULL DEFAULT 0,
            CreatedAt    TEXT NOT NULL,
            UpdatedAt    TEXT NOT NULL,
            CreatedBy    TEXT NULL,
            UpdatedBy    TEXT NULL
        );
        """,

        // --- Başlık–öğe (parent–child) ağaç bağlantıları ---
        """
        CREATE TABLE IF NOT EXISTS NodeRelations (
            NodeRelationId TEXT PRIMARY KEY,
            ParentNodeId   TEXT NOT NULL,
            ChildNodeId    TEXT NOT NULL,
            DisplayOrder   INTEGER NOT NULL DEFAULT 0,
            PositionX      REAL NULL,
            PositionY      REAL NULL,
            IsActive       INTEGER NOT NULL DEFAULT 1,
            CreatedAt      TEXT NOT NULL,
            UpdatedAt      TEXT NOT NULL,
            FOREIGN KEY (ParentNodeId) REFERENCES Nodes(NodeId),
            FOREIGN KEY (ChildNodeId)  REFERENCES Nodes(NodeId),
            CHECK (ParentNodeId <> ChildNodeId),
            UNIQUE (ParentNodeId, ChildNodeId)
        );
        """,

        // --- Bağlantıya özel bilgi kaydı (her bağlantı için en fazla bir kayıt) ---
        // İçerik iş birimince doldurulur: ekran adı, batch adı, kısa açıklama.
        """
        CREATE TABLE IF NOT EXISTS NodeRelationDetails (
            Id               INTEGER PRIMARY KEY AUTOINCREMENT,
            NodeRelationId   TEXT NOT NULL UNIQUE,
            ScreenName       TEXT NULL,
            BatchName        TEXT NULL,
            ShortDescription TEXT NULL,
            CreatedAt        TEXT NULL,
            LastUpdatedAt    TEXT NULL,
            UpdatedBy        TEXT NULL,
            FOREIGN KEY (NodeRelationId) REFERENCES NodeRelations(NodeRelationId)
        );
        """,

        // --- Tek yazıcı kilidi (uygulama seviyesi düzenleme kilidi) ---
        """
        CREATE TABLE IF NOT EXISTS ApplicationWriteLock (
            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
            LockKey         TEXT NOT NULL,
            UserName        TEXT NULL,
            MachineName     TEXT NULL,
            ProcessId       INTEGER NULL,
            OwnerToken      TEXT NOT NULL,
            AcquiredAt      TEXT NOT NULL,
            LastHeartbeatAt TEXT NOT NULL,
            ExpiresAt       TEXT NOT NULL,
            IsActive        INTEGER NOT NULL DEFAULT 1
        );
        """,

        // --- Denetim izi (kim, ne zaman, hangi işlemi yaptı) ---
        """
        CREATE TABLE IF NOT EXISTS AuditLog (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Timestamp    TEXT NOT NULL,
            UserName     TEXT NULL,
            MachineName  TEXT NULL,
            ActionType   TEXT NOT NULL,
            EntityType   TEXT NULL,
            EntityId     TEXT NULL,
            ParentNodeId TEXT NULL,
            OldValueJson TEXT NULL,
            NewValueJson TEXT NULL,
            IsSuccessful INTEGER NOT NULL DEFAULT 1,
            ErrorMessage TEXT NULL
        );
        """,

        // Aynı kaynak için aynı anda en fazla bir aktif kilit (atomik kilit alımını garanti eder).
        "CREATE UNIQUE INDEX IF NOT EXISTS UX_WriteLock_Active ON ApplicationWriteLock(LockKey) WHERE IsActive = 1;",
        "CREATE INDEX IF NOT EXISTS IX_Nodes_CanBeRoot ON Nodes(CanBeRoot);",
        "CREATE INDEX IF NOT EXISTS IX_NodeRelations_Parent ON NodeRelations(ParentNodeId);",
        "CREATE INDEX IF NOT EXISTS IX_NodeRelations_Child ON NodeRelations(ChildNodeId);",
    };

    /// <summary>
    /// Artık kullanılmayan (eski sayısal etki sistemi) tablolar. Migration bunları
    /// çocuk→ebeveyn sırasıyla düşürür; sıra, FOREIGN KEY bağımlılıkları içindir.
    /// </summary>
    public static readonly string[] ObsoleteTables =
    {
        "ScenarioResults", "Scenarios", "ImpactResults", "MetricHistory",
        "Relationships", "Metrics", "ImpactRules", "Products",
    };
}
