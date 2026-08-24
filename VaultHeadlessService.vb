Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Text.Json

Public Class HeadlessOptions
    Public Property CatalogPath As String = ""
    Public Property VaultRootPath As String = ""
End Class

Public Class HeadlessIngestResult
    Public Property IngestedArtifacts As New List(Of ArtifactModel)
    Public Property RequestedPathCount As Integer
    Public Property Mode As IngestMode
    Public ReadOnly Property ExitCode As Integer
        Get
            If RequestedPathCount > IngestedArtifacts.Count Then
                Return 3
            End If

            Return 0
        End Get
    End Property
End Class

Public Class HeadlessVerifyResult
    Public Property HealthReport As VaultHealthReport
    Public Property RepairReport As VaultRepairReport
    Public Property ExitCode As Integer
End Class

Public Class HeadlessExportResult
    Public Property Validation As CatalogBackupValidationResult
End Class

Public Class HeadlessReportResult
    Public Property OutputPath As String = ""
    Public Property HealthReport As VaultHealthReport
    Public Property RepairReport As VaultRepairReport
End Class

Public Class HeadlessRepairPreviewResult
    Public Property HealthReport As VaultHealthReport
    Public Property RepairCandidates As New List(Of RepairCandidate)
End Class

Public Class HeadlessRepairApplyResult
    Public Property Applied As Boolean
    Public Property Candidates As New List(Of RepairCandidate)
    Public Property AppliedCount As Integer
    Public Property FailedCount As Integer
    Public Property SkippedCount As Integer
End Class

Public Class HeadlessRescanResult
    Public Property Applied As Boolean
    Public Property OrphanFiles As New List(Of String)
    Public Property AdoptedArtifacts As New List(Of ArtifactModel)
End Class

Public Class HeadlessThumbnailRebuildResult
    Public Property Applied As Boolean
    Public Property CandidateCount As Integer
    Public Property RebuiltCount As Integer
    Public Property FailedCount As Integer
End Class

Public Class HeadlessPackageResult
    Public Property OutputPath As String = ""
    Public Property ArtifactCount As Integer
    Public Property FileCount As Integer
    Public Property IsZip As Boolean
End Class

Public Class VaultHeadlessService
    Private Const CatalogFolderName As String = "catalog"
    Private Const ItemsFolderName As String = "items"

    Private ReadOnly _catalogService As CatalogService
    Private ReadOnly _ingestionService As IngestionService
    Private ReadOnly _searchService As VaultSearchService

    Public Sub New(Optional options As HeadlessOptions = Nothing)
        Dim effectiveOptions = If(options, New HeadlessOptions())
        _catalogService = New CatalogService(effectiveOptions.CatalogPath, effectiveOptions.VaultRootPath)
        _ingestionService = New IngestionService()
        _searchService = New VaultSearchService()
    End Sub

    Public ReadOnly Property CatalogPath As String
        Get
            Return _catalogService.CatalogPath
        End Get
    End Property

    Public Function LoadCatalog() As CatalogData
        Dim catalog = _catalogService.LoadOrCreate()
        catalog.ActiveHashes = HashRegistry.NormalizeActiveHashes(catalog.ActiveHashes)
        CatalogService.EnsureVaultFolders(catalog.VaultRootPath)
        Return catalog
    End Function

    Public Function Ingest(paths As IEnumerable(Of String), Optional modeOverride As IngestMode? = Nothing) As HeadlessIngestResult
        Dim catalog = LoadCatalog()
        Dim requestedPaths = If(paths, Enumerable.Empty(Of String)()).Where(Function(path) Not String.IsNullOrWhiteSpace(path)).ToList()
        Dim mode = If(modeOverride.HasValue, modeOverride.Value, ParseIngestMode(catalog.DefaultIngestMode))
        Dim ingested = _ingestionService.Ingest(requestedPaths, catalog.VaultRootPath, Nothing, mode, catalog.ActiveHashes)

        If ingested.Count > 0 Then
            Dim captureRecord = IngestionService.CreateCaptureRecord(ingested, mode)
            If captureRecord IsNot Nothing Then
                catalog.CaptureRecords.Insert(0, captureRecord)
            End If

            For Each artifact In ingested
                catalog.Artifacts.Insert(0, artifact)
            Next

            catalog.Activities.Insert(0, New ActivityEntryModel With {
                .ActionText = $"CLI ingested {ingested.Count:N0} file(s)",
                .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  {mode.ToString().ToLowerInvariant()} into vault",
                .Icon = "CLI"
            })
            RefreshDerivedCatalogLists(catalog)
            _catalogService.Save(catalog)
        End If

        Return New HeadlessIngestResult With {
            .IngestedArtifacts = ingested,
            .RequestedPathCount = requestedPaths.Count,
            .Mode = mode
        }
    End Function

    Public Function Verify(Optional failOn As String = "any") As HeadlessVerifyResult
        Dim catalog = LoadCatalog()
        Dim healthReport = MainViewModel.BuildVaultHealthReport(catalog.Artifacts, catalog.VaultRootPath, New ThumbnailService(), New HashService(), Nothing, catalog.ActiveHashes)
        Dim repairReport = MainViewModel.BuildRepairReport(catalog.Artifacts, catalog.VaultRootPath, healthReport, Nothing, catalog.ActiveHashes)
        Dim thresholdMet = FindingsMeetThreshold(healthReport, failOn)

        Return New HeadlessVerifyResult With {
            .HealthReport = healthReport,
            .RepairReport = repairReport,
            .ExitCode = If(thresholdMet, 2, 0)
        }
    End Function

    Public Function Search(options As VaultSearchOptions) As List(Of ArtifactModel)
        Dim catalog = LoadCatalog()
        Return _searchService.Search(catalog.Artifacts, catalog.VaultRootPath, options)
    End Function

    Public Function ExportSnapshot(outputFolder As String) As HeadlessExportResult
        Dim catalog = LoadCatalog()
        Dim exportRoot = If(String.IsNullOrWhiteSpace(outputFolder), Path.Combine(catalog.VaultRootPath, "exports"), outputFolder)
        Dim validation = _catalogService.ExportSnapshotWithValidation(catalog, exportRoot)
        Return New HeadlessExportResult With {.Validation = validation}
    End Function

    Public Function GenerateReport(outputPath As String, format As String) As HeadlessReportResult
        Dim catalog = LoadCatalog()
        Dim healthReport = MainViewModel.BuildVaultHealthReport(catalog.Artifacts, catalog.VaultRootPath, New ThumbnailService(), New HashService(), Nothing, catalog.ActiveHashes)
        Dim repairReport = MainViewModel.BuildRepairReport(catalog.Artifacts, catalog.VaultRootPath, healthReport, Nothing, catalog.ActiveHashes)
        Dim normalizedFormat = If(format, "text").Trim().ToLowerInvariant()
        Dim destination = If(String.IsNullOrWhiteSpace(outputPath), BuildDefaultReportPath(catalog.VaultRootPath, normalizedFormat), outputPath)
        Dim directoryPath = Path.GetDirectoryName(destination)

        If Not String.IsNullOrWhiteSpace(directoryPath) Then
            Directory.CreateDirectory(directoryPath)
        End If

        If normalizedFormat = "json" Then
            File.WriteAllText(destination, JsonSerializer.Serialize(New With {
                .generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                .vaultRoot = catalog.VaultRootPath,
                .summary = healthReport.Summary,
                .findings = healthReport.Findings,
                .repair = repairReport
            }, JsonOptions))
        Else
            File.WriteAllText(destination, BuildTextReport(catalog.VaultRootPath, healthReport, repairReport))
        End If

        Return New HeadlessReportResult With {
            .OutputPath = destination,
            .HealthReport = healthReport,
            .RepairReport = repairReport
        }
    End Function

    Public Function RepairPreview() As HeadlessRepairPreviewResult
        Dim catalog = LoadCatalog()
        Dim healthReport = MainViewModel.BuildVaultHealthReport(catalog.Artifacts, catalog.VaultRootPath, New ThumbnailService(), New HashService(), Nothing, catalog.ActiveHashes)
        Return New HeadlessRepairPreviewResult With {
            .HealthReport = healthReport,
            .RepairCandidates = healthReport.Findings.Select(Function(finding) MainViewModel.BuildRepairCandidate(finding)).ToList()
        }
    End Function

    Public Function Repair(apply As Boolean) As HeadlessRepairApplyResult
        Dim catalog = LoadCatalog()
        Dim preview = RepairPreview()
        Dim result As New HeadlessRepairApplyResult With {
            .Applied = apply,
            .Candidates = preview.RepairCandidates
        }

        If Not apply Then
            result.SkippedCount = result.Candidates.Count
            Return result
        End If

        Dim logService As New RepairLogService()
        For Each candidate In result.Candidates
            If Not candidate.CanRepairAutomatically Then
                result.SkippedCount += 1
                AppendRepairLog(logService, catalog.VaultRootPath, candidate, "Skipped", "Review-only candidate requires manual action")
                Continue For
            End If

            Dim artifact = FindArtifactForCandidate(catalog.Artifacts, candidate)
            If artifact Is Nothing OrElse Not ApplyRepairCandidate(catalog.VaultRootPath, artifact, candidate, catalog.ActiveHashes) Then
                result.FailedCount += 1
                AppendRepairLog(logService, catalog.VaultRootPath, candidate, "Failed", "Repair could not be completed safely")
            Else
                result.AppliedCount += 1
                AppendRepairLog(logService, catalog.VaultRootPath, candidate, "Applied", "Repair completed")
            End If
        Next

        If result.AppliedCount > 0 Then
            _catalogService.Save(catalog)
        End If

        Return result
    End Function

    Public Function Rescan(apply As Boolean) As HeadlessRescanResult
        Dim catalog = LoadCatalog()
        Dim result As New HeadlessRescanResult With {.Applied = apply}
        result.OrphanFiles = FindOrphanStoredFiles(catalog.Artifacts, catalog.VaultRootPath).ToList()

        If Not apply Then
            Return result
        End If

        For Each orphan In result.OrphanFiles
            Try
                Dim adopted = _ingestionService.CreateArtifactFromStoredFile(orphan, catalog.VaultRootPath, "", catalog.ActiveHashes)
                adopted.Notes = $"Adopted during CLI vault rescan at {DateTime.Now:yyyy-MM-dd HH:mm}"
                catalog.Artifacts.Insert(0, adopted)
                result.AdoptedArtifacts.Add(adopted)
            Catch ex As Exception
                Debug.WriteLine($"Failed to adopt orphan file '{orphan}'. {ex.Message}")
            End Try
        Next

        If result.AdoptedArtifacts.Count > 0 Then
            catalog.Activities.Insert(0, New ActivityEntryModel With {
                .ActionText = $"CLI adopted {result.AdoptedArtifacts.Count:N0} orphan file(s)",
                .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  vault rescan",
                .Icon = "CLI"
            })
            RefreshDerivedCatalogLists(catalog)
            _catalogService.Save(catalog)
        End If

        Return result
    End Function

    Public Function RebuildThumbnails(apply As Boolean) As HeadlessThumbnailRebuildResult
        Dim catalog = LoadCatalog()
        Dim thumbnailService As New ThumbnailService()
        Dim candidates = catalog.Artifacts.
            Where(Function(artifact) artifact IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(artifact.Path) AndAlso File.Exists(artifact.Path)).
            ToList()
        Dim result As New HeadlessThumbnailRebuildResult With {
            .Applied = apply,
            .CandidateCount = candidates.Count
        }

        If Not apply Then
            Return result
        End If

        For Each artifact In candidates
            Dim thumbnail = thumbnailService.GenerateForArtifact(artifact, catalog.VaultRootPath)
            If String.Equals(thumbnail.Status, ThumbnailService.GeneratedStatus, StringComparison.OrdinalIgnoreCase) Then
                artifact.ThumbnailRelativePath = thumbnail.RelativePath
                artifact.ThumbnailStatus = thumbnail.Status
                result.RebuiltCount += 1
            ElseIf String.Equals(thumbnail.Status, ThumbnailService.GenerationFailedStatus, StringComparison.OrdinalIgnoreCase) Then
                result.FailedCount += 1
            End If
        Next

        If result.RebuiltCount > 0 Then
            _catalogService.Save(catalog)
        End If

        Return result
    End Function

    Public Function CreatePackage(outputPath As String, zipPackage As Boolean) As HeadlessPackageResult
        Dim catalog = LoadCatalog()
        Dim normalizedOutput = If(outputPath, "")
        Dim packageRoot = If(String.IsNullOrWhiteSpace(normalizedOutput), Path.Combine(catalog.VaultRootPath, "exports", $"filecabinet-package-{DateTime.Now:yyyyMMdd-HHmmss}"), normalizedOutput)
        Dim workingRoot = If(zipPackage, Path.Combine(Path.GetTempPath(), $"filecabinet-package-{Guid.NewGuid():N}"), packageRoot)
        Dim result As New HeadlessPackageResult With {
            .ArtifactCount = catalog.Artifacts.Count,
            .IsZip = zipPackage
        }

        Directory.CreateDirectory(workingRoot)
        Directory.CreateDirectory(Path.Combine(workingRoot, CatalogFolderName))
        Directory.CreateDirectory(Path.Combine(workingRoot, "integrity"))
        Directory.CreateDirectory(Path.Combine(workingRoot, ItemsFolderName))
        Directory.CreateDirectory(Path.Combine(workingRoot, "thumbnails"))
        Directory.CreateDirectory(Path.Combine(workingRoot, "extracted-text"))
        Directory.CreateDirectory(Path.Combine(workingRoot, "repair-logs"))

        File.WriteAllText(Path.Combine(workingRoot, "manifest.json"), JsonSerializer.Serialize(New With {
            .format = "Filing Cabinet deterministic vault package",
            .version = 1,
            .createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            .vaultRoot = catalog.VaultRootPath,
            .artifactCount = catalog.Artifacts.Count
        }, JsonOptions))
        File.WriteAllText(Path.Combine(workingRoot, CatalogFolderName, "catalog.json"), JsonSerializer.Serialize(catalog, JsonOptions))
        File.WriteAllLines(Path.Combine(workingRoot, CatalogFolderName, "catalog.jsonl"), catalog.Artifacts.Select(Function(artifact) JsonSerializer.Serialize(artifact, JsonLineOptions)))

        Dim verifyResult = Verify("any")
        File.WriteAllText(Path.Combine(workingRoot, "integrity", "vault-health.json"), JsonSerializer.Serialize(New With {
            .summary = verifyResult.HealthReport.Summary,
            .findings = verifyResult.HealthReport.Findings,
            .repair = verifyResult.RepairReport
        }, JsonOptions))

        CopyFolderIfExists(Path.Combine(catalog.VaultRootPath, ItemsFolderName), Path.Combine(workingRoot, ItemsFolderName), result)
        CopyFolderIfExists(Path.Combine(catalog.VaultRootPath, "thumbnails"), Path.Combine(workingRoot, "thumbnails"), result)
        CopyFolderIfExists(Path.Combine(catalog.VaultRootPath, "extracted-text"), Path.Combine(workingRoot, "extracted-text"), result)
        CopyFileIfExists(Path.Combine(catalog.VaultRootPath, CatalogFolderName, "repair-log.jsonl"), Path.Combine(workingRoot, "repair-logs", "repair-log.jsonl"), result)

        If zipPackage Then
            Dim zipPath = If(normalizedOutput.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), normalizedOutput, $"{normalizedOutput}.zip")
            If String.IsNullOrWhiteSpace(normalizedOutput) Then
                zipPath = $"{packageRoot}.zip"
            End If

            Dim zipDirectory = Path.GetDirectoryName(zipPath)
            If Not String.IsNullOrWhiteSpace(zipDirectory) Then
                Directory.CreateDirectory(zipDirectory)
            End If

            If File.Exists(zipPath) Then
                File.Delete(zipPath)
            End If

            ZipFile.CreateFromDirectory(workingRoot, zipPath)
            Directory.Delete(workingRoot, recursive:=True)
            result.OutputPath = zipPath
        Else
            result.OutputPath = workingRoot
        End If

        Return result
    End Function

    Private Shared Function ParseIngestMode(value As String) As IngestMode
        Dim parsed As IngestMode
        If [Enum].TryParse(value, ignoreCase:=True, result:=parsed) Then
            Return parsed
        End If

        Return IngestMode.Move
    End Function

    Private Shared Function FindingsMeetThreshold(report As VaultHealthReport, failOn As String) As Boolean
        Dim normalized = If(failOn, "any").Trim().ToLowerInvariant()
        If report Is Nothing OrElse report.Findings.Count = 0 Then
            Return False
        End If

        Select Case normalized
            Case "high"
                Return report.Findings.Any(Function(finding) String.Equals(finding.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
            Case "medium"
                Return report.Findings.Any(Function(finding) String.Equals(finding.RiskLevel, "Medium", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(finding.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
            Case Else
                Return report.Findings.Count > 0
        End Select
    End Function

    Private Shared Sub RefreshDerivedCatalogLists(catalog As CatalogData)
        catalog.Categories = catalog.Artifacts.
            Where(Function(artifact) Not String.IsNullOrWhiteSpace(artifact.Category)).
            GroupBy(Function(artifact) artifact.Category, StringComparer.OrdinalIgnoreCase).
            Select(Function(group) New CategoryModel With {.Name = group.First().Category, .Count = group.Count().ToString("N0")}).
            OrderBy(Function(category) category.Name).
            ToList()

        catalog.Tags = catalog.Artifacts.
            Where(Function(artifact) artifact.Tags IsNot Nothing).
            SelectMany(Function(artifact) artifact.Tags).
            Where(Function(tag) Not String.IsNullOrWhiteSpace(tag)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(tag) tag).
            ToList()
    End Sub

    Private Shared Function FindArtifactForCandidate(artifacts As IEnumerable(Of ArtifactModel), candidate As RepairCandidate) As ArtifactModel
        If candidate Is Nothing Then
            Return Nothing
        End If

        Return If(artifacts, Enumerable.Empty(Of ArtifactModel)()).
            FirstOrDefault(Function(artifact) artifact IsNot Nothing AndAlso String.Equals(artifact.Name, candidate.Subject, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Function ApplyRepairCandidate(vaultRootPath As String, artifact As ArtifactModel, candidate As RepairCandidate, activeHashes As String) As Boolean
        Try
            Select Case candidate.ActionType
                Case "RegenerateThumbnail"
                    Dim thumbnail = New ThumbnailService().GenerateForArtifact(artifact, vaultRootPath)
                    artifact.ThumbnailRelativePath = thumbnail.RelativePath
                    artifact.ThumbnailStatus = thumbnail.Status
                    Return String.Equals(thumbnail.Status, ThumbnailService.GeneratedStatus, StringComparison.OrdinalIgnoreCase)
                Case "RecomputeHash"
                    Dim hashes = New HashService().ComputeHashes(artifact.Path, activeHashes)
                    HashRegistry.ApplyHashesToArtifact(artifact, hashes)
                    Return True
                Case "ReExtractText"
                    Dim extraction = _ingestionService.ExtractTextForArtifact(artifact, vaultRootPath)
                    artifact.ExtractedTextRelativePath = extraction.RelativePath
                    artifact.ExtractedTextStatus = extraction.Status
                    Return String.Equals(extraction.Status, "Extracted", StringComparison.OrdinalIgnoreCase) OrElse
                        String.Equals(extraction.Status, "Extracted (truncated)", StringComparison.OrdinalIgnoreCase)
                Case "RebindPath"
                    Dim resolvedPath = ResolveVaultRelativePath(vaultRootPath, artifact.RelativePath)
                    If String.IsNullOrWhiteSpace(resolvedPath) OrElse Not IsPathInsideDirectory(resolvedPath, vaultRootPath) OrElse Not File.Exists(resolvedPath) Then
                        Return False
                    End If

                    artifact.Path = resolvedPath
                    Return True
                Case Else
                    Return False
            End Select
        Catch
            Return False
        End Try
    End Function

    Private Shared Sub AppendRepairLog(logService As RepairLogService, vaultRootPath As String, candidate As RepairCandidate, result As String, detail As String)
        Try
            logService.Append(vaultRootPath, New RepairLogEntry With {
                .Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                .ActionType = If(candidate?.ActionType, ""),
                .FindingType = If(candidate?.FindingType, ""),
                .Subject = If(candidate?.Subject, ""),
                .ProposedAction = If(candidate?.ProposedAction, ""),
                .Result = result,
                .Detail = detail,
                .MutatesCatalog = candidate IsNot Nothing AndAlso candidate.MutatesCatalog,
                .TouchesRetainedFiles = candidate IsNot Nothing AndAlso candidate.TouchesRetainedFiles
            })
        Catch
        End Try
    End Sub

    Private Shared Function FindOrphanStoredFiles(artifacts As IEnumerable(Of ArtifactModel), vaultRootPath As String) As IEnumerable(Of String)
        Dim itemsRoot = Path.Combine(vaultRootPath, ItemsFolderName)
        If Not Directory.Exists(itemsRoot) Then
            Return Enumerable.Empty(Of String)()
        End If

        Dim knownPaths = New HashSet(Of String)(
            If(artifacts, Enumerable.Empty(Of ArtifactModel)()).
                Where(Function(artifact) Not String.IsNullOrWhiteSpace(artifact.Path)).
                Select(Function(artifact) Path.GetFullPath(artifact.Path)),
            StringComparer.OrdinalIgnoreCase)

        Return Directory.EnumerateFiles(itemsRoot, "*", SearchOption.AllDirectories).
            Where(Function(candidatePath) Not knownPaths.Contains(Path.GetFullPath(candidatePath))).
            ToList()
    End Function

    Private Shared Function ResolveVaultRelativePath(vaultRootPath As String, relativePath As String) As String
        If String.IsNullOrWhiteSpace(relativePath) Then
            Return ""
        End If

        If Path.IsPathRooted(relativePath) Then
            Return relativePath
        End If

        If String.IsNullOrWhiteSpace(vaultRootPath) Then
            Return ""
        End If

        Return Path.Combine(vaultRootPath, relativePath)
    End Function

    Private Shared Function IsPathInsideDirectory(candidatePath As String, directoryPath As String) As Boolean
        If String.IsNullOrWhiteSpace(candidatePath) OrElse String.IsNullOrWhiteSpace(directoryPath) Then
            Return False
        End If

        Try
            Dim fullPath = Path.GetFullPath(candidatePath)
            Dim fullDirectory = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

            If String.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If

            fullDirectory &= Path.DirectorySeparatorChar
            Return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    Private Shared Sub CopyFolderIfExists(sourceRoot As String, destinationRoot As String, result As HeadlessPackageResult)
        If Not Directory.Exists(sourceRoot) Then
            Return
        End If

        For Each sourcePath In Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            Dim relativePath = Path.GetRelativePath(sourceRoot, sourcePath)
            CopyFileIfExists(sourcePath, Path.Combine(destinationRoot, relativePath), result)
        Next
    End Sub

    Private Shared Sub CopyFileIfExists(sourcePath As String, destinationPath As String, result As HeadlessPackageResult)
        If Not File.Exists(sourcePath) Then
            Return
        End If

        Dim destinationDirectory = Path.GetDirectoryName(destinationPath)
        If Not String.IsNullOrWhiteSpace(destinationDirectory) Then
            Directory.CreateDirectory(destinationDirectory)
        End If

        File.Copy(sourcePath, destinationPath, overwrite:=True)
        result.FileCount += 1
    End Sub

    Private Shared Function BuildDefaultReportPath(vaultRootPath As String, format As String) As String
        Dim extension = If(String.Equals(format, "json", StringComparison.OrdinalIgnoreCase), "json", "txt")
        Return Path.Combine(vaultRootPath, "exports", $"vault-health-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}")
    End Function

    Private Shared Function BuildTextReport(vaultRootPath As String, healthReport As VaultHealthReport, repairReport As VaultRepairReport) As String
        Dim builder As New StringBuilder()
        builder.AppendLine("Filing Cabinet Vault Health Report")
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
        builder.AppendLine($"Vault: {vaultRootPath}")
        builder.AppendLine(healthReport.Summary)
        builder.AppendLine(repairReport.Summary)

        If Not String.IsNullOrWhiteSpace(healthReport.Detail) Then
            builder.AppendLine(healthReport.Detail)
        End If

        For Each finding In healthReport.Findings
            builder.AppendLine($"- [{finding.RiskLevel}] {finding.FindingType}: {finding.Subject} - {finding.ProposedAction}")
        Next

        Return builder.ToString()
    End Function

    Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
        .WriteIndented = True
    }

    Private Shared ReadOnly JsonLineOptions As New JsonSerializerOptions()
End Class

