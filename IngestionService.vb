Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Xml.Linq

Public Enum IngestMode
    Copy
    Move
End Enum

Public Class IngestionService
    Private Shared ReadOnly HashServiceInstance As New HashService()
    Private Shared ReadOnly ThumbnailServiceInstance As New ThumbnailService()
    Private Shared ReadOnly ExtractableTextExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        ".txt", ".md", ".json", ".toml", ".yaml", ".yml", ".xml", ".ini", ".log", ".csv", ".ps1", ".bat", ".cmd", ".vb", ".cs", ".xaml", ".config", ".rtf", ".asc", ".pem", ".pub", ".docx"
    }

    Public Function Ingest(paths As IEnumerable(Of String), vaultRootPath As String, Optional progress As IProgress(Of IngestionProgress) = Nothing, Optional mode As IngestMode = IngestMode.Move, Optional activeHashes As String = "") As List(Of ArtifactModel)
        Dim artifacts As New List(Of ArtifactModel)

        If paths Is Nothing Then
            Return artifacts
        End If

        Report(progress, "Scanning files", "", "Scanning", 0, 0, 0, 0)
        Dim files = ExpandFiles(paths).Select(Function(path) New FileInfo(path)).Where(Function(file) file.Exists).ToList()
        Dim itemsRoot = Path.Combine(vaultRootPath, "items")
        CatalogService.EnsureVaultFolders(vaultRootPath)

        Dim capturedAt = DateTime.Now
        Dim captureId = Guid.NewGuid().ToString("N")
        Dim sourceRoot = CommonSourceRoot(files)
        Dim captureName = BuildCaptureName(capturedAt, sourceRoot)
        Dim siblingNames = files.Select(Function(file) file.Name).OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).ToList()
        Dim totalBytes = files.Sum(Function(file) file.Length)
        Dim completedBytes As Long = 0
        Dim completedFiles = 0
        Dim failedFiles = 0

        For Each source In files
            Try
                Dim destination = BuildDestinationPath(itemsRoot, source.Name)
                Dim transferLabel = If(mode = IngestMode.Move, "Move", "Copy")
                Report(progress, If(mode = IngestMode.Move, "Moving", "Copying"), source.Name, transferLabel, completedFiles, files.Count, completedBytes, totalBytes)
                CopyWithProgress(source.FullName, destination, completedBytes, totalBytes, completedFiles, files.Count, progress)

                Dim stored = New FileInfo(destination)
                Report(progress, "Hashing", source.Name, "Hash", completedFiles, files.Count, completedBytes + stored.Length, totalBytes)
                artifacts.Add(CreateArtifact(source, stored, vaultRootPath, HashRegistry.NormalizeActiveHashes(activeHashes), captureId, captureName, siblingNames))

                If mode = IngestMode.Move Then
                    Try
                        source.Delete()
                    Catch
                        Report(progress, "Moved into vault; original could not be removed", source.Name, "Original delete failed", completedFiles, files.Count, completedBytes + stored.Length, totalBytes)
                    End Try
                End If

                completedBytes += stored.Length
                completedFiles += 1
                Report(progress, "Ingested", source.Name, "Complete", completedFiles, files.Count, completedBytes, totalBytes)
            Catch
                failedFiles += 1
                Report(progress, "Skipped unreadable file", source.Name, "Failed", completedFiles, files.Count, completedBytes, totalBytes)
            End Try
        Next

        Dim summary = $"Finished ingesting {artifacts.Count:N0} file(s)"
        If failedFiles > 0 Then
            summary &= $" with {failedFiles:N0} failure(s)"
        End If

        Report(progress, summary, "", "Finished", completedFiles, files.Count, completedBytes, totalBytes)
        Return artifacts
    End Function

    Public Function CreateArtifactFromStoredFile(path As String, vaultRootPath As String, Optional originalPath As String = "", Optional activeHashes As String = "") As ArtifactModel
        If String.IsNullOrWhiteSpace(path) Then
            Throw New ArgumentException("Path is required.", NameOf(path))
        End If

        Dim stored = New FileInfo(path)
        If Not stored.Exists Then
            Throw New FileNotFoundException("Stored file was not found.", path)
        End If

        If String.IsNullOrWhiteSpace(originalPath) Then
            originalPath = stored.FullName
        End If

        Dim source = New FileInfo(originalPath)
        Dim capturedAt = DateTime.Now
        Dim sourceRoot = If(source.Exists AndAlso source.Directory IsNot Nothing, source.Directory.FullName, stored.DirectoryName)
        Dim captureName = BuildCaptureName(capturedAt, sourceRoot)

        Return CreateArtifact(source, stored, vaultRootPath, HashRegistry.NormalizeActiveHashes(activeHashes), Guid.NewGuid().ToString("N"), captureName, {stored.Name})
    End Function

    Public Shared Function CreateCaptureRecord(artifacts As IEnumerable(Of ArtifactModel), mode As IngestMode) As CaptureRecordModel
        Dim artifactList = If(artifacts, Enumerable.Empty(Of ArtifactModel)()).Where(Function(artifact) artifact IsNot Nothing).ToList()
        If artifactList.Count = 0 Then
            Return Nothing
        End If

        Dim first = artifactList.First()
        Dim sourceRoot = CommonSourceRoot(artifactList.Select(Function(artifact) New FileInfo(If(String.IsNullOrWhiteSpace(artifact.OriginalPath), artifact.Path, artifact.OriginalPath))))
        Dim itemNames = artifactList.Select(Function(artifact) artifact.Name).Where(Function(name) Not String.IsNullOrWhiteSpace(name)).OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).ToList()

        Return New CaptureRecordModel With {
            .Id = first.CaptureId,
            .DisplayName = If(String.IsNullOrWhiteSpace(first.CaptureName), BuildCaptureName(DateTime.Now, sourceRoot), first.CaptureName),
            .CapturedAt = first.IngestedAt,
            .SourceRoot = sourceRoot,
            .Method = mode.ToString(),
            .ItemCount = artifactList.Count,
            .TotalSizeBytes = artifactList.Sum(Function(artifact) artifact.SizeBytes),
            .TotalSize = FormatSize(artifactList.Sum(Function(artifact) artifact.SizeBytes)),
            .CommonParent = If(String.IsNullOrWhiteSpace(sourceRoot), "", Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))),
            .AcquisitionChannel = MostCommonAcquisitionChannel(artifactList),
            .ItemNames = itemNames
        }
    End Function

    Public Function ExtractTextForArtifact(artifact As ArtifactModel, vaultRootPath As String) As (RelativePath As String, Status As String)
        If artifact Is Nothing OrElse String.IsNullOrWhiteSpace(artifact.Path) Then
            Return ("", "Extraction failed")
        End If

        Dim stored = New FileInfo(artifact.Path)
        If Not stored.Exists Then
            Return ("", "Extraction failed")
        End If

        Return ExtractText(stored, vaultRootPath)
    End Function

    Private Shared Function ExpandFiles(paths As IEnumerable(Of String)) As IEnumerable(Of String)
        Dim files As New List(Of String)

        For Each inputPath In paths
            If String.IsNullOrWhiteSpace(inputPath) Then
                Continue For
            End If

            If File.Exists(inputPath) Then
                files.Add(inputPath)
            ElseIf Directory.Exists(inputPath) Then
                Try
                    files.AddRange(Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories))
                Catch ex As Exception
                    Debug.WriteLine($"Failed to enumerate files under '{inputPath}'. {ex.Message}")
                End Try
            End If
        Next

        Return files
    End Function

    Private Shared Function BuildDestinationPath(itemsRoot As String, fileName As String) As String
        Dim datedRoot = Path.Combine(itemsRoot, DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"))
        Directory.CreateDirectory(datedRoot)

        Dim baseName = Path.GetFileNameWithoutExtension(fileName)
        Dim extension = Path.GetExtension(fileName)
        Dim candidate = Path.Combine(datedRoot, fileName)
        Dim index = 1

        While File.Exists(candidate)
            candidate = Path.Combine(datedRoot, $"{baseName}-{index}{extension}")
            index += 1
        End While

        Return candidate
    End Function

    Private Shared Sub CopyWithProgress(sourcePath As String, destinationPath As String, baseCompletedBytes As Long, totalBytes As Long, completedFiles As Integer, totalFiles As Integer, progress As IProgress(Of IngestionProgress))
        Const bufferSize = 1024 * 1024
        Dim copiedForFile As Long = 0
        Dim fileName = Path.GetFileName(sourcePath)
        Dim lastReport = DateTime.MinValue

        Using source = New FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan)
            Using destination = New FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan)
                Dim buffer(bufferSize - 1) As Byte

                While True
                    Dim read = source.Read(buffer, 0, buffer.Length)

                    If read <= 0 Then
                        Exit While
                    End If

                    destination.Write(buffer, 0, read)
                    copiedForFile += read

                    If (DateTime.Now - lastReport).TotalMilliseconds >= 120 Then
                        Report(progress, "Transferring", fileName, "Transfer", completedFiles, totalFiles, baseCompletedBytes + copiedForFile, totalBytes)
                        lastReport = DateTime.Now
                    End If
                End While
            End Using
        End Using
    End Sub

    Private Shared Sub Report(progress As IProgress(Of IngestionProgress), status As String, currentFile As String, stage As String, filesCompleted As Integer, filesTotal As Integer, bytesCompleted As Long, bytesTotal As Long)
        If progress Is Nothing Then
            Return
        End If

        progress.Report(New IngestionProgress With {
            .Status = status,
            .CurrentFile = currentFile,
            .CurrentStage = stage,
            .FilesCompleted = filesCompleted,
            .FilesTotal = filesTotal,
            .BytesCompleted = bytesCompleted,
            .BytesTotal = bytesTotal
        })
    End Sub

    Private Shared Function CreateArtifact(source As FileInfo, stored As FileInfo, vaultRootPath As String, activeHashes As String, captureId As String, captureName As String, siblingNames As IEnumerable(Of String)) As ArtifactModel
        Dim category = InferCategory(source.Extension)
        Dim typeName = InferType(source.Extension)
        Dim typeFamily = InferTypeFamily(source.Extension)
        Dim tags = InferTags(source, category)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        Dim computedHashes = HashServiceInstance.ComputeHashes(stored.FullName, activeHashes)
        Dim extraction = ExtractText(stored, vaultRootPath)
        Dim allSiblingNames = If(siblingNames, Enumerable.Empty(Of String)()).ToList()
        Dim siblings = allSiblingNames.
            Where(Function(name) Not String.Equals(name, source.Name, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()
        Dim artifact = New ArtifactModel With {
            .Id = Guid.NewGuid().ToString("N"),
            .Name = stored.Name,
            .Type = typeName,
            .TypeFamily = typeFamily,
            .Category = category,
            .Size = FormatSize(stored.Length),
            .SizeBytes = stored.Length,
            .DateModified = stored.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
            .Path = stored.FullName,
            .RelativePath = Path.GetRelativePath(vaultRootPath, stored.FullName),
            .Created = stored.CreationTime.ToString("yyyy-MM-dd HH:mm"),
            .Blake3 = computedHashes.Blake3,
            .Sha256 = computedHashes.Sha256,
            .KangarooTwelve = computedHashes.KangarooTwelve,
            .Sha3_256 = computedHashes.Sha3_256,
            .Md5 = computedHashes.Md5,
            .Whirlpool = computedHashes.Whirlpool,
            .Skein = computedHashes.Skein,
            .Hashes = computedHashes.Hashes,
            .HashStatus = "Verified",
            .ExtractedTextRelativePath = extraction.RelativePath,
            .ExtractedTextStatus = extraction.Status,
            .Rating = 0,
            .Notes = $"Ingested from {source.FullName}",
            .SourceProvenance = source.DirectoryName,
            .AcquisitionMethod = "Ingest",
            .TrustClassification = "Unknown",
            .RetentionPriority = "Normal",
            .ArchiveStatus = "Active",
            .OriginalPath = source.FullName,
            .IngestedAt = nowText,
            .CaptureId = captureId,
            .CaptureName = captureName,
            .SourceParentSnapshot = BuildSourceParentSnapshot(source.FullName),
            .OriginalExtension = source.Extension.TrimStart("."c).ToLowerInvariant(),
            .DetectedFamily = typeFamily,
            .AcquisitionChannel = InferAcquisitionChannel(source.FullName),
            .SiblingCount = siblings.Count,
            .SiblingNames = siblings.Take(25).ToList(),
            .Tags = tags
        }

        Dim thumbnail = ThumbnailServiceInstance.GenerateForArtifact(artifact, vaultRootPath)
        artifact.ThumbnailRelativePath = thumbnail.RelativePath
        artifact.ThumbnailStatus = thumbnail.Status

        Return artifact
    End Function

    Private Shared Function ExtractText(stored As FileInfo, vaultRootPath As String) As (RelativePath As String, Status As String)
        If Not ExtractableTextExtensions.Contains(stored.Extension) Then
            Return ("", "Not extractable")
        End If

        Try
            Const maxChars = 1024 * 1024
            Dim extractedText = If(String.Equals(stored.Extension, ".docx", StringComparison.OrdinalIgnoreCase),
                ExtractDocxText(stored.FullName),
                ReadTextFile(stored.FullName, maxChars))

            Dim extractedRoot = Path.Combine(vaultRootPath, "extracted-text", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"))
            Directory.CreateDirectory(extractedRoot)
            Dim extractedName = $"{Path.GetFileNameWithoutExtension(stored.Name)}-{Guid.NewGuid():N}.txt"
            Dim extractedPath = Path.Combine(extractedRoot, extractedName)
            File.WriteAllText(extractedPath, extractedText)

            Dim status = If(extractedText.Length >= maxChars, "Extracted (truncated)", "Extracted")
            Return (Path.GetRelativePath(vaultRootPath, extractedPath), status)
        Catch
            Return ("", "Extraction failed")
        End Try
    End Function

    Private Shared Function ReadTextFile(path As String, maxChars As Integer) As String
        Dim builder As New StringBuilder()

        Using reader As New StreamReader(path, detectEncodingFromByteOrderMarks:=True)
            Dim buffer(4095) As Char

            While builder.Length < maxChars
                Dim remaining = Math.Min(buffer.Length, maxChars - builder.Length)
                Dim read = reader.Read(buffer, 0, remaining)

                If read <= 0 Then
                    Exit While
                End If

                builder.Append(buffer, 0, read)
            End While
        End Using

        Return builder.ToString()
    End Function

    Private Shared Function ExtractDocxText(path As String) As String
        Using archive = ZipFile.OpenRead(path)
            Dim documentEntry = archive.GetEntry("word/document.xml")
            If documentEntry Is Nothing Then
                Return ""
            End If

            Using stream = documentEntry.Open()
                Dim document = XDocument.Load(stream)
                Dim wordNamespace = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main")
                Dim paragraphs = document.Descendants(wordNamespace + "p").
                    Select(Function(paragraph) String.Concat(paragraph.Descendants(wordNamespace + "t").Select(Function(textNode) textNode.Value)).Trim()).
                    Where(Function(text) Not String.IsNullOrWhiteSpace(text))

                Return String.Join(vbCrLf & vbCrLf, paragraphs)
            End Using
        End Using
    End Function

    Private Shared Function InferTypeFamily(extension As String) As String
        Select Case InferCategory(extension)
            Case "Images"
                Return "Image"
            Case "Documents"
                Return "Document"
            Case "Spreadsheets"
                Return "Spreadsheet"
            Case "Presentations"
                Return "Presentation"
            Case "Manifests / Config"
                Return "Text"
            Case "Audio"
                Return "Audio"
            Case "Video"
                Return "Video"
            Case "Archives"
                Return "Archive"
            Case "Software / Installers"
                Return "Installer"
            Case "ISOs / Disk Images"
                Return "Disk Image"
            Case Else
                Return "File"
        End Select
    End Function

    Private Shared Function CommonSourceRoot(files As IEnumerable(Of FileInfo)) As String
        Dim directories = If(files, Enumerable.Empty(Of FileInfo)()).
            Select(Function(file)
                       If file Is Nothing Then
                           Return ""
                       End If

                       If file.Directory IsNot Nothing Then
                           Return file.Directory.FullName
                       End If

                       Return ""
                   End Function).
            Where(Function(directory) Not String.IsNullOrWhiteSpace(directory)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()

        If directories.Count = 0 Then
            Return ""
        End If

        If directories.Count = 1 Then
            Return directories(0)
        End If

        Dim splitDirectories = directories.
            Select(Function(directory) Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Split({Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries)).
            ToList()

        Dim sharedParts As New List(Of String)
        Dim maxParts = splitDirectories.Min(Function(parts) parts.Length)

        For index = 0 To maxParts - 1
            Dim partIndex = index
            Dim candidate = splitDirectories(0)(index)
            If splitDirectories.All(Function(parts) String.Equals(parts(partIndex), candidate, StringComparison.OrdinalIgnoreCase)) Then
                sharedParts.Add(candidate)
            Else
                Exit For
            End If
        Next

        If sharedParts.Count = 0 Then
            Return Path.GetPathRoot(directories(0))
        End If

        Dim root = Path.GetPathRoot(directories(0))
        If String.IsNullOrWhiteSpace(root) Then
            Return Path.Combine(sharedParts.ToArray())
        End If

        Dim rootName = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        If sharedParts.Count > 0 AndAlso String.Equals(sharedParts(0), rootName, StringComparison.OrdinalIgnoreCase) Then
            sharedParts.RemoveAt(0)
        End If

        Return Path.Combine({root}.Concat(sharedParts).ToArray())
    End Function

    Private Shared Function BuildCaptureName(capturedAt As DateTime, sourceRoot As String) As String
        Dim sourceLabel = "file"

        If Not String.IsNullOrWhiteSpace(sourceRoot) Then
            sourceLabel = Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        End If

        If String.IsNullOrWhiteSpace(sourceLabel) Then
            sourceLabel = "root"
        End If

        Return $"{capturedAt:yyyy-MM-dd HH:mm} - {sourceLabel} intake"
    End Function

    Private Shared Function BuildSourceParentSnapshot(sourcePath As String) As List(Of String)
        Dim names As New List(Of String)

        Try
            Dim directory = New DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(sourcePath)))

            While directory IsNot Nothing AndAlso names.Count < 3
                If Not String.IsNullOrWhiteSpace(directory.Name) Then
                    names.Insert(0, directory.Name)
                End If

                directory = directory.Parent
            End While
        Catch
            Return New List(Of String)
        End Try

        Return names
    End Function

    Private Shared Function InferAcquisitionChannel(sourcePath As String) As String
        If String.IsNullOrWhiteSpace(sourcePath) Then
            Return "Unknown"
        End If

        Try
            Dim fullPath = Path.GetFullPath(sourcePath)
            If fullPath.StartsWith("\\", StringComparison.OrdinalIgnoreCase) Then
                Return "Network path"
            End If

            Dim downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            Dim desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)

            If IsUnderDirectory(fullPath, downloads) Then
                Return "Downloads folder"
            End If

            If IsUnderDirectory(fullPath, desktop) Then
                Return "Desktop"
            End If

            Dim root = Path.GetPathRoot(fullPath)
            If Not String.IsNullOrWhiteSpace(root) Then
                Dim drive = New DriveInfo(root)
                If drive.DriveType = DriveType.Removable Then
                    Return "Removable drive"
                End If
            End If
        Catch
            Return "Unknown"
        End Try

        Return "Local path"
    End Function

    Private Shared Function IsUnderDirectory(candidatePath As String, directory As String) As Boolean
        If String.IsNullOrWhiteSpace(candidatePath) OrElse String.IsNullOrWhiteSpace(directory) Then
            Return False
        End If

        Dim normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Dim normalizedPath = Path.GetFullPath(candidatePath)
        Return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function MostCommonAcquisitionChannel(artifacts As IEnumerable(Of ArtifactModel)) As String
        Return If(artifacts, Enumerable.Empty(Of ArtifactModel)()).
            Select(Function(artifact) artifact.AcquisitionChannel).
            Where(Function(channel) Not String.IsNullOrWhiteSpace(channel)).
            GroupBy(Function(channel) channel, StringComparer.OrdinalIgnoreCase).
            OrderByDescending(Function(group) group.Count()).
            ThenBy(Function(group) group.Key).
            Select(Function(group) group.Key).
            FirstOrDefault()
    End Function

    Private Shared Function InferCategory(extension As String) As String
        Select Case extension.ToLowerInvariant()
            Case ".iso", ".img", ".vhd", ".vhdx"
                Return "ISOs / Disk Images"
            Case ".exe", ".msi", ".msix", ".appx"
                Return "Software / Installers"
            Case ".zip", ".7z", ".rar", ".tar", ".gz", ".xz"
                Return "Archives"
            Case ".asc", ".gpg", ".pgp", ".pem", ".key", ".pub"
                Return "Keys / Security"
            Case ".toml", ".json", ".yaml", ".yml", ".xml", ".ini", ".config"
                Return "Manifests / Config"
            Case ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff"
                Return "Images"
            Case ".mp3", ".wav", ".flac", ".ogg", ".m4a"
                Return "Audio"
            Case ".mp4", ".mov", ".mkv", ".avi", ".webm"
                Return "Video"
            Case ".torrent"
                Return "Torrents"
            Case ".pdf", ".doc", ".docx", ".odt", ".txt", ".md", ".rtf"
                Return "Documents"
            Case ".xls", ".xlsx", ".ods"
                Return "Spreadsheets"
            Case ".ppt", ".pptx", ".odp"
                Return "Presentations"
            Case Else
                Return "Other"
        End Select
    End Function

    Private Shared Function InferType(extension As String) As String
        If String.IsNullOrWhiteSpace(extension) Then
            Return "File"
        End If

        Select Case extension.ToLowerInvariant()
            Case ".iso"
                Return "ISO Image"
            Case ".exe"
                Return "Installer"
            Case ".msix"
                Return "MSIX Installer"
            Case ".asc"
                Return "PGP Key"
            Case ".gpg"
                Return "GPG Encrypted File"
            Case ".toml"
                Return "TOML Document"
            Case ".json"
                Return "JSON Document"
            Case ".pdf"
                Return "PDF Document"
            Case ".doc"
                Return "Word Document"
            Case ".docx"
                Return "Word Document"
            Case ".txt"
                Return "Text Document"
            Case ".md"
                Return "Markdown Document"
            Case ".rtf"
                Return "Rich Text Document"
            Case ".xls", ".xlsx"
                Return "Spreadsheet"
            Case ".ppt", ".pptx"
                Return "Presentation"
            Case ".png"
                Return "Image (PNG)"
            Case ".torrent"
                Return "Torrent"
            Case Else
                Return extension.TrimStart("."c).ToUpperInvariant() & " File"
        End Select
    End Function

    Private Shared Function InferTags(source As FileInfo, category As String) As List(Of String)
        Dim tags As New List(Of String)
        Dim extension = source.Extension.TrimStart("."c).ToLowerInvariant()

        If Not String.IsNullOrWhiteSpace(extension) Then
            tags.Add(extension)
        End If

        For Each part In category.Split({"/"c, " "c}, StringSplitOptions.RemoveEmptyEntries)
            Dim cleaned = part.Trim().ToLowerInvariant()

            If cleaned.Length > 2 AndAlso Not tags.Contains(cleaned) Then
                tags.Add(cleaned)
            End If
        Next

        Return tags.Take(5).ToList()
    End Function

    Private Shared Function FormatSize(bytes As Long) As String
        Dim units = {"B", "KB", "MB", "GB", "TB"}
        Dim value = CDbl(bytes)
        Dim unitIndex = 0

        While value >= 1024 AndAlso unitIndex < units.Length - 1
            value /= 1024
            unitIndex += 1
        End While

        If unitIndex = 0 Then
            Return $"{bytes} B"
        End If

        Return $"{value:0.##} {units(unitIndex)}"
    End Function

End Class
