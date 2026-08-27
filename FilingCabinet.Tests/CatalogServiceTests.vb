Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports System.IO
Imports System.Text.Json

Namespace FilingCabinet.Tests
    <TestClass>
    Public Class CatalogServiceTests
        <TestMethod>
        Sub LoadOrCreateSupportsEmptyVaultAndCreatesFolders()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim catalogPath = Path.Combine(workspace, "appdata", "catalog.json")
            Dim vaultRoot = Path.Combine(workspace, "vault")

            Try
                Dim service As New Global.FilingCabinet.CatalogService(catalogPath, vaultRoot)
                Dim catalog = service.LoadOrCreate()

                Assert.AreEqual(1, catalog.SchemaVersion)
                Assert.AreEqual(vaultRoot, catalog.VaultRootPath)
                Assert.AreEqual("Move", catalog.DefaultIngestMode)
                Assert.IsEmpty(catalog.Artifacts)
                Assert.IsEmpty(catalog.CaptureRecords)
                Assert.IsTrue(File.Exists(catalogPath))
                Assert.IsTrue(Directory.Exists(Path.Combine(vaultRoot, "items")))
                Assert.IsTrue(Directory.Exists(Path.Combine(vaultRoot, "quarantine")))
                Assert.IsTrue(Directory.Exists(Path.Combine(vaultRoot, "exports")))
                Assert.IsTrue(Directory.Exists(Path.Combine(vaultRoot, "extracted-text")))
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub LoadOrCreateReplacesUnreadableCatalog()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim catalogPath = Path.Combine(workspace, "appdata", "catalog.json")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath))
            File.WriteAllText(catalogPath, "{not json")

            Try
                Dim service As New Global.FilingCabinet.CatalogService(catalogPath, vaultRoot)

                Dim catalog = service.LoadOrCreate()

                Assert.AreEqual(1, catalog.SchemaVersion)
                Assert.IsEmpty(catalog.Artifacts)
                Assert.IsTrue(File.Exists(catalogPath))
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub ExportSnapshotWritesPortableCatalogCopy()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim catalogPath = Path.Combine(workspace, "appdata", "catalog.json")
            Dim vaultRoot = Path.Combine(workspace, "vault")

            Try
                Dim service As New Global.FilingCabinet.CatalogService(catalogPath, vaultRoot)
                Dim catalog = service.LoadOrCreate()
                catalog.Artifacts.Add(New Global.FilingCabinet.ArtifactModel With {
                    .Id = "artifact-1",
                    .Name = "sample.txt",
                    .RelativePath = Path.Combine("items", "sample.txt")
                })
                catalog.CaptureRecords.Add(New Global.FilingCabinet.CaptureRecordModel With {
                    .Id = "capture-1",
                    .DisplayName = "2026-08-23 19:02 - Downloads intake",
                    .Method = "Copy",
                    .ItemCount = 1
                })

                Dim backupPath = service.ExportSnapshot(catalog, Path.Combine(vaultRoot, "exports"))
                Dim json = File.ReadAllText(backupPath)
                Dim exported = JsonSerializer.Deserialize(Of Global.FilingCabinet.CatalogData)(json)

                Assert.IsTrue(File.Exists(backupPath))
                Assert.AreEqual(backupPath, catalog.LastBackupPath)
                Assert.IsNotNull(exported)
                Assert.AreEqual(1, exported.Artifacts.Count)
                Assert.AreEqual("artifact-1", exported.Artifacts(0).Id)
                Assert.AreEqual(1, exported.CaptureRecords.Count)
                Assert.AreEqual("capture-1", exported.CaptureRecords(0).Id)
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub ExportSnapshotWithValidationConfirmsReadableCatalogBackup()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim catalogPath = Path.Combine(workspace, "appdata", "catalog.json")
            Dim vaultRoot = Path.Combine(workspace, "vault")

            Try
                Dim service As New Global.FilingCabinet.CatalogService(catalogPath, vaultRoot)
                Dim catalog = service.LoadOrCreate()
                catalog.Artifacts.Add(New Global.FilingCabinet.ArtifactModel With {
                    .Id = "artifact-1",
                    .Name = "sample.txt"
                })

                Dim validation = service.ExportSnapshotWithValidation(catalog, Path.Combine(vaultRoot, "exports"))

                Assert.IsTrue(validation.IsValid)
                Assert.IsTrue(File.Exists(validation.BackupPath))
                StringAssert.Contains(validation.Detail, "1 artifact")
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub SaveReplacesCatalogAtomicallyAndKeepsLastGoodBackup()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim catalogPath = Path.Combine(workspace, "appdata", "catalog.json")
            Dim vaultRoot = Path.Combine(workspace, "vault")

            Try
                Dim service As New Global.FilingCabinet.CatalogService(catalogPath, vaultRoot)
                Dim catalog = service.LoadOrCreate()
                catalog.Artifacts.Add(New Global.FilingCabinet.ArtifactModel With {.Id = "first", .Name = "first.txt"})
                service.Save(catalog)

                catalog.Artifacts.Add(New Global.FilingCabinet.ArtifactModel With {.Id = "second", .Name = "second.txt"})
                service.Save(catalog)

                Dim reloaded = service.LoadOrCreate()
                Dim backupPath = $"{catalogPath}.bak"

                Assert.AreEqual(2, reloaded.Artifacts.Count)
                Assert.IsTrue(File.Exists(backupPath))
                StringAssert.Contains(File.ReadAllText(backupPath), "first.txt")
                Assert.IsFalse(Directory.EnumerateFiles(Path.GetDirectoryName(catalogPath), "*.tmp").Any())
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub ValidateBackupRejectsCorruptOrIncompleteBackup()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim backupPath = Path.Combine(workspace, "exports", "catalog-backup-corrupt.json")
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath))

            Try
                File.WriteAllText(backupPath, "{""SchemaVersion"":1,""Vaults"":null,""Artifacts"":null}")
                Dim service As New Global.FilingCabinet.CatalogService(Path.Combine(workspace, "appdata", "catalog.json"), Path.Combine(workspace, "vault"))

                Dim validation = service.ValidateBackup(backupPath)

                Assert.IsFalse(validation.IsValid)
                StringAssert.Contains(validation.Detail, "required catalog collections")
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub LoadOrCreateAddsPreferenceDefaultsToOlderCatalog()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim catalogPath = Path.Combine(workspace, "appdata", "catalog.json")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath))
            File.WriteAllText(catalogPath, $"{{""SchemaVersion"":1,""CurrentVaultId"":""main"",""VaultRootPath"":""{vaultRoot.Replace("\", "\\")}"",""DefaultIngestMode"":""Move"",""DuplicatePolicy"":""Rename"",""Vaults"":[{{""Id"":""main"",""Name"":""MainVault"",""Path"":""{vaultRoot.Replace("\", "\\")}""}}],""Artifacts"":[{{""Id"":""artifact-1"",""Name"":""legacy.txt""}}]}}")

            Try
                Dim service As New Global.FilingCabinet.CatalogService(catalogPath, vaultRoot)
                Dim catalog = service.LoadOrCreate()

                Assert.AreEqual("Comfortable", catalog.TableDensity)
                Assert.AreEqual("Full", catalog.ColumnPreset)
                Assert.AreEqual("All", catalog.ActiveScope)
                Assert.AreEqual("", catalog.SearchText)
                Assert.AreEqual("", catalog.TagSearchText)
                Assert.AreEqual("", catalog.SelectedTag)
                Assert.AreEqual("", catalog.SelectedCategory)
                Assert.IsNotNull(catalog.CaptureRecords)
                Assert.AreEqual(1, catalog.Artifacts.Count)
                Assert.AreEqual("Unknown", catalog.Artifacts(0).TrustClassification)
                Assert.AreEqual("Not specified", catalog.Artifacts(0).Purpose)
                Assert.AreEqual("Unknown / legacy", catalog.Artifacts(0).Provenance)
                Assert.AreEqual("Normal", catalog.Artifacts(0).RetentionPriority)
                Assert.AreEqual("Active", catalog.Artifacts(0).ArchiveStatus)

                catalog.Artifacts(0).RetentionReason = "Keep for recovery"
                catalog.Artifacts(0).WhyThisMatters = "Documents restore context"
                catalog.Artifacts(0).SourceProvenance = "Aptlantis release share"
                catalog.Artifacts(0).AcquisitionMethod = "Manual import"
                catalog.Artifacts(0).Purpose = "Recovery"
                catalog.Artifacts(0).Provenance = "Official / vendor"
                catalog.Artifacts(0).TrustClassification = "Trusted"
                catalog.Artifacts(0).RetentionPriority = "High"
                catalog.Artifacts(0).ArchiveStatus = "Archived"
                service.Save(catalog)

                Dim reloaded = service.LoadOrCreate()
                Assert.AreEqual("Keep for recovery", reloaded.Artifacts(0).RetentionReason)
                Assert.AreEqual("Documents restore context", reloaded.Artifacts(0).WhyThisMatters)
                Assert.AreEqual("Aptlantis release share", reloaded.Artifacts(0).SourceProvenance)
                Assert.AreEqual("Manual import", reloaded.Artifacts(0).AcquisitionMethod)
                Assert.AreEqual("Recovery", reloaded.Artifacts(0).Purpose)
                Assert.AreEqual("Official / vendor", reloaded.Artifacts(0).Provenance)
                Assert.AreEqual("Trusted", reloaded.Artifacts(0).TrustClassification)
                Assert.AreEqual("High", reloaded.Artifacts(0).RetentionPriority)
                Assert.AreEqual("Archived", reloaded.Artifacts(0).ArchiveStatus)
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub
    End Class
End Namespace

