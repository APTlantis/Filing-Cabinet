Imports System.Diagnostics
Imports System.IO
Imports System.Text.Json

Public Class CatalogService
    Private Shared ReadOnly CatalogOptions As New JsonSerializerOptions With {
        .WriteIndented = True,
        .PropertyNameCaseInsensitive = True
    }

    Public ReadOnly Property CatalogPath As String
    Public ReadOnly Property DefaultVaultRootPath As String

    Public Sub New(Optional catalogPathOverride As String = "", Optional defaultVaultRootPathOverride As String = "")
        If String.IsNullOrWhiteSpace(catalogPathOverride) Then
            CatalogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Filing Cabinet",
                "catalog.json")
        Else
            CatalogPath = catalogPathOverride
        End If

        If String.IsNullOrWhiteSpace(defaultVaultRootPathOverride) Then
            DefaultVaultRootPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Filing Cabinet Vault")
        Else
            DefaultVaultRootPath = defaultVaultRootPathOverride
        End If
    End Sub

    Public Function LoadOrCreate() As CatalogData
        Try
            If File.Exists(CatalogPath) Then
                Dim json = File.ReadAllText(CatalogPath)
                Dim loaded = JsonSerializer.Deserialize(Of CatalogData)(json, CatalogOptions)

                If IsUsable(loaded) Then
                    EnsureCatalogDefaults(loaded)
                    Return loaded
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"Catalog load failed; creating a new catalog. {ex.Message}")
        End Try

        Dim created = CreateEmptyCatalog()
        Save(created)
        Return created
    End Function

    Private Sub EnsureCatalogDefaults(catalog As CatalogData)
        catalog.SchemaVersion = Math.Max(1, catalog.SchemaVersion)

        If String.IsNullOrWhiteSpace(catalog.VaultRootPath) Then
            catalog.VaultRootPath = DefaultVaultRootPath
        End If

        If String.IsNullOrWhiteSpace(catalog.CurrentVaultId) Then
            catalog.CurrentVaultId = "main"
        End If

        If String.IsNullOrWhiteSpace(catalog.DefaultIngestMode) Then
            catalog.DefaultIngestMode = "Move"
        End If

        If String.IsNullOrWhiteSpace(catalog.DuplicatePolicy) Then
            catalog.DuplicatePolicy = "Rename"
        End If

        If String.IsNullOrWhiteSpace(catalog.TableDensity) Then
            catalog.TableDensity = "Comfortable"
        End If

        If String.IsNullOrWhiteSpace(catalog.ColumnPreset) Then
            catalog.ColumnPreset = "Full"
        End If

        If String.IsNullOrWhiteSpace(catalog.ActiveScope) Then
            catalog.ActiveScope = "All"
        End If

        If catalog.Vaults Is Nothing Then
            catalog.Vaults = New List(Of VaultModel)
        End If

        If catalog.CaptureRecords Is Nothing Then
            catalog.CaptureRecords = New List(Of CaptureRecordModel)
        End If

        If catalog.Vaults.Count = 0 Then
            catalog.Vaults.Add(New VaultModel With {
                .Id = catalog.CurrentVaultId,
                .Name = "MainVault",
                .Path = catalog.VaultRootPath,
                .IsSelected = True
            })
        End If

        For Each vault In catalog.Vaults
            If String.IsNullOrWhiteSpace(vault.Path) Then
                vault.Path = catalog.VaultRootPath
            End If
        Next

        EnsureVaultFolders(catalog.VaultRootPath)
        Save(catalog)
    End Sub

    Public Sub Save(catalog As CatalogData)
        Dim catalogDirectory = Path.GetDirectoryName(CatalogPath)

        If Not String.IsNullOrWhiteSpace(catalogDirectory) Then
            Directory.CreateDirectory(catalogDirectory)
        End If

        Dim json = JsonSerializer.Serialize(catalog, CatalogOptions)
        Dim tempPath = $"{CatalogPath}.{Guid.NewGuid():N}.tmp"
        Dim backupPath = $"{CatalogPath}.bak"

        File.WriteAllText(tempPath, json)

        If File.Exists(CatalogPath) Then
            File.Replace(tempPath, CatalogPath, backupPath, ignoreMetadataErrors:=True)
        Else
            File.Move(tempPath, CatalogPath)
        End If
    End Sub

    Public Function ExportSnapshot(catalog As CatalogData, exportsRoot As String) As String
        If catalog Is Nothing Then
            Throw New ArgumentNullException(NameOf(catalog))
        End If

        Directory.CreateDirectory(exportsRoot)
        catalog.LastBackupPath = Path.Combine(exportsRoot, $"catalog-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json")
        File.WriteAllText(catalog.LastBackupPath, JsonSerializer.Serialize(catalog, CatalogOptions))
        Save(catalog)
        Return catalog.LastBackupPath
    End Function

    Public Function ExportSnapshotWithValidation(catalog As CatalogData, exportsRoot As String) As CatalogBackupValidationResult
        Dim backupPath = ExportSnapshot(catalog, exportsRoot)
        Return ValidateBackup(backupPath)
    End Function

    Public Function ValidateBackup(backupPath As String) As CatalogBackupValidationResult
        Dim result As New CatalogBackupValidationResult With {
            .BackupPath = If(backupPath, "")
        }

        If String.IsNullOrWhiteSpace(backupPath) Then
            result.Detail = "Backup path is empty."
            Return result
        End If

        If Not File.Exists(backupPath) Then
            result.Detail = "Backup file does not exist."
            Return result
        End If

        Try
            Dim json = File.ReadAllText(backupPath)
            Dim loaded = JsonSerializer.Deserialize(Of CatalogData)(json, CatalogOptions)

            If Not IsUsable(loaded) Then
                result.Detail = "Backup JSON loaded but required catalog collections are missing."
                Return result
            End If

            If loaded.Categories Is Nothing OrElse
                loaded.Tags Is Nothing OrElse
                loaded.Activities Is Nothing OrElse
                loaded.CaptureRecords Is Nothing OrElse
                loaded.Stats Is Nothing Then
                result.Detail = "Backup JSON is missing one or more optional catalog collections."
                Return result
            End If

            result.IsValid = True
            result.Detail = $"Validated {loaded.Artifacts.Count:N0} artifact record(s)."
            Return result
        Catch ex As Exception
            result.Detail = $"Backup validation failed: {ex.Message}"
            Return result
        End Try
    End Function

    Private Shared Function IsUsable(catalog As CatalogData) As Boolean
        Return catalog IsNot Nothing AndAlso
            catalog.Vaults IsNot Nothing AndAlso
            catalog.Artifacts IsNot Nothing
    End Function

    Public Shared Sub EnsureVaultFolders(vaultRootPath As String)
        If String.IsNullOrWhiteSpace(vaultRootPath) Then
            Return
        End If

        Directory.CreateDirectory(vaultRootPath)
        For Each child In {"items", "catalog", "quarantine", "exports", "thumbnails", "extracted-text"}
            Directory.CreateDirectory(Path.Combine(vaultRootPath, child))
        Next
    End Sub

    Private Function CreateEmptyCatalog() As CatalogData
        Dim root = DefaultVaultRootPath
        EnsureVaultFolders(root)

        Return New CatalogData With {
            .SchemaVersion = 1,
            .CurrentVaultId = "main",
            .VaultRootPath = root,
            .DefaultIngestMode = "Move",
            .DuplicatePolicy = "Rename",
            .TableDensity = "Comfortable",
            .ColumnPreset = "Full",
            .ActiveScope = "All",
            .Vaults = New List(Of VaultModel) From {
                New VaultModel With {.Id = "main", .Name = "MainVault", .Path = root, .IsSelected = True}
            },
            .Activities = New List(Of ActivityEntryModel) From {
                New ActivityEntryModel With {
                    .ActionText = "Created local vault",
                    .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  {root}",
                    .Icon = ""
                }
            }
        }
    End Function

End Class

