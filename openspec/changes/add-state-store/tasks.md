# Tasks: add-state-store

TDD for production behavior. Integration tests use the real SQLite provider
against a temporary file (no Testcontainers, no in-memory fake). Marker/store
work lands in `PrCenter.Persistence` + `PrCenter.Persistence.Tests`; startup
migration and the Development gate wire through `PrCenter.Web`.

## 1. Marker entity and context

- [x] 1.1 Add the `LastSeenMarker` entity (`PullRequestId` string key, `SeenAt` DateTimeOffset) in `PrCenter.Persistence`
- [x] 1.2 Configure it on `PrCenterDbContext` via `OnModelCreating` (Fluent API; key on `PullRequestId`) and expose its `DbSet`
- [x] 1.3 Confirm architecture tests stay green (entity + context remain in Persistence; Core stays EF-free)

## 2. Integration-test harness (real SQLite file)

- [x] 2.1 Add a disposable test harness/fixture that creates a unique temp `.db` file, builds `DbContextOptions<PrCenterDbContext>` for it, applies migrations, and deletes the file (and `-wal`/`-shm`) on dispose
- [x] 2.2 Write a failing round-trip test using the harness (set then get) to prove the harness + provider before the store exists
- [x] 2.3 Keep the harness reusable (documented) for #4 and #7

## 3. StateStore behavior (TDD)

- [x] 3.1 Tests first (against the real-file harness): set-then-get returns the instant; get with no marker returns null; set-again updates the same row (one row remains); guard tests for null/whitespace id
- [x] 3.2 Implement `StateStore.GetLastSeenAsync` (FindAsync -> instant or null) and `SetLastSeenAsync` (upsert via find-or-add + SaveChanges) to green, with `ThrowIfNullOrWhiteSpace(pullRequestId)` guards and `<exception>` docs
- [x] 3.3 Delete the stub `NotImplementedException` tests in `StateStoreTests` (issue #6: never port stub tests forward); `TokenVaultTests` stub stays for #4

## 4. Migration

- [x] 4.1 Add the initial EF migration (`InitialCreate`) for the marker table; ensure `dotnet ef` tooling is available (design-time factory if the host cannot be constructed at design time) -- pulled forward: the harness (task 2) applies it. Added `Microsoft.EntityFrameworkCore.Design` (PrivateAssets=all) and `PrCenterDbContextFactory`
- [x] 4.2 Verify the migration applies cleanly to a fresh temp file in an integration test (table exists, round-trip works) -- covered by the task-2 harness round-trip test (`SqliteTestDatabase` runs `Database.Migrate()` on a fresh temp file; `MigratedDatabase_PersistsAndReadsBackAMarker` proves the table via insert+read)

## 5. Connection configuration and Development gate

- [x] 5.1 Configure WAL journal mode, a busy timeout, and a 5-second command timeout on the SQLite connection/context (no retrying execution strategy)
- [x] 5.2 Add an `isDevelopment` parameter to `AddPersistenceAdapter`; enable `EnableSensitiveDataLogging` + detailed errors only when set; update `PrCenter.Web` to pass `builder.Environment.IsDevelopment()`
- [x] 5.3 Tests: the Development flag toggles sensitive data logging on/off (assert via context options); update the existing `PersistenceServiceCollectionExtensionsTests` for the new parameter and the Web DI resolution test

## 6. Startup migration

- [ ] 6.1 Apply pending migrations on host startup (`Database.MigrateAsync()`), ordered before the app-lock/unlock gate so the schema is created while locked
- [ ] 6.2 Test/verify: startup against a schema-less temp file produces the marker table before requests are served, without needing a decrypted key

## 7. Wrap-up

- [ ] 7.1 `dotnet build` clean (warnings-as-errors) and `dotnet test` green across all projects
- [ ] 7.2 `dotnet csharpier check .` clean
- [ ] 7.3 Coverage: `PrCenter.Persistence` marker/store paths covered; note that startup wiring in `PrCenter.Web` is exercised by the DI/host tests
- [ ] 7.4 Comment on issue #6: state-store half done (stub tests deleted, guards + guard tests landed); token half remains for #4 -- do not close
