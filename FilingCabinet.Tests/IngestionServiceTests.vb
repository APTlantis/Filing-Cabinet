Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports System.IO
Imports System.IO.Compression

Namespace FilingCabinet.Tests
    <TestClass>
    Public Class IngestionServiceTests
        <TestMethod>
        Sub MoveIngestCreatesVaultArtifactAndRemovesOriginal()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim sourceRoot = Path.Combine(workspace, "source")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(sourceRoot)
            Dim sourcePath = Path.Combine(sourceRoot, "manifest.toml")
            File.WriteAllText(sourcePath, "name = ""demo""")

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifacts = service.Ingest({sourcePath}, vaultRoot, Nothing, Global.FilingCabinet.IngestMode.Move)

                Assert.AreEqual(1, artifacts.Count)
                Assert.IsFalse(File.Exists(sourcePath), "Move intake should remove the original after transfer.")
                Assert.IsTrue(File.Exists(artifacts(0).Path))
                Assert.AreEqual(sourcePath, artifacts(0).OriginalPath)
                Assert.AreEqual("Manifests / Config", artifacts(0).Category)
                Assert.AreEqual("toml", artifacts(0).OriginalExtension)
                Assert.AreEqual("Text", artifacts(0).DetectedFamily)
                Assert.IsFalse(String.IsNullOrWhiteSpace(artifacts(0).CaptureId))
                Assert.IsFalse(String.IsNullOrWhiteSpace(artifacts(0).CaptureName))
                Assert.AreEqual("Verified", artifacts(0).HashStatus)
                Assert.IsFalse(String.IsNullOrWhiteSpace(artifacts(0).Id))
                Assert.IsFalse(String.IsNullOrWhiteSpace(artifacts(0).RelativePath))
                Assert.AreEqual("Extracted", artifacts(0).ExtractedTextStatus)
                Assert.IsFalse(String.IsNullOrWhiteSpace(artifacts(0).ExtractedTextRelativePath))
                Assert.IsTrue(File.Exists(Path.Combine(vaultRoot, artifacts(0).ExtractedTextRelativePath)))
                Assert.AreEqual(Global.FilingCabinet.ThumbnailService.FallbackCardStatus, artifacts(0).ThumbnailStatus)
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub IngestCapturesBatchContextAndSiblingFacts()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim sourceRoot = Path.Combine(workspace, "Downloads", "Intel-NIC")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(sourceRoot)
            Dim setupPath = Path.Combine(sourceRoot, "setup.exe")
            Dim readmePath = Path.Combine(sourceRoot, "readme.txt")
            Dim firmwarePath = Path.Combine(sourceRoot, "firmware.bin")
            File.WriteAllBytes(setupPath, {1, 2, 3})
            File.WriteAllText(readmePath, "driver notes")
            File.WriteAllBytes(firmwarePath, {4, 5, 6})

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifacts = service.Ingest({sourceRoot}, vaultRoot, Nothing, Global.FilingCabinet.IngestMode.Copy)

                Assert.AreEqual(3, artifacts.Count)
                Assert.IsTrue(artifacts.All(Function(artifact) String.Equals(artifact.CaptureId, artifacts(0).CaptureId, StringComparison.OrdinalIgnoreCase)))
                Assert.IsTrue(artifacts.All(Function(artifact) artifact.SiblingCount = 2))
                Assert.IsTrue(artifacts.All(Function(artifact) artifact.SourceParentSnapshot.Contains("Intel-NIC")))
                Assert.IsTrue(artifacts.All(Function(artifact) Not String.IsNullOrWhiteSpace(artifact.AcquisitionChannel)))

                Dim setup = artifacts.Single(Function(artifact) artifact.OriginalPath = setupPath)
                Assert.AreEqual("exe", setup.OriginalExtension)
                Assert.AreEqual("Installer", setup.DetectedFamily)
                CollectionAssert.Contains(setup.SiblingNames, "readme.txt")
                CollectionAssert.Contains(setup.SiblingNames, "firmware.bin")

                Dim capture = Global.FilingCabinet.IngestionService.CreateCaptureRecord(artifacts, Global.FilingCabinet.IngestMode.Copy)
                Assert.IsNotNull(capture)
                Assert.AreEqual(artifacts(0).CaptureId, capture.Id)
                Assert.AreEqual("Copy", capture.Method)
                Assert.AreEqual(3, capture.ItemCount)
                Assert.AreEqual(sourceRoot, capture.SourceRoot)
                Assert.AreEqual("Intel-NIC", capture.CommonParent)
                CollectionAssert.Contains(capture.ItemNames, "setup.exe")
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub CopyIngestRenamesDuplicateDestinations()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim sourceRoot = Path.Combine(workspace, "source")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(sourceRoot)
            Dim firstPath = Path.Combine(sourceRoot, "same.txt")
            Dim secondRoot = Path.Combine(workspace, "second")
            Directory.CreateDirectory(secondRoot)
            Dim secondPath = Path.Combine(secondRoot, "same.txt")
            File.WriteAllText(firstPath, "first")
            File.WriteAllText(secondPath, "second")

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifacts = service.Ingest({firstPath, secondPath}, vaultRoot, Nothing, Global.FilingCabinet.IngestMode.Copy)

                Assert.AreEqual(2, artifacts.Count)
                Assert.IsTrue(File.Exists(firstPath), "Copy intake should preserve the original.")
                Assert.IsTrue(File.Exists(secondPath), "Copy intake should preserve the original.")
                Assert.AreNotEqual(artifacts(0).Path, artifacts(1).Path)
                Assert.IsTrue(File.Exists(artifacts(0).Path))
                Assert.IsTrue(File.Exists(artifacts(1).Path))
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub IngestStoresOnlyRequestedActiveHashes()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim sourceRoot = Path.Combine(workspace, "source")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(sourceRoot)
            Dim sourcePath = Path.Combine(sourceRoot, "legacy.txt")
            File.WriteAllText(sourcePath, "legacy")

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifacts = service.Ingest({sourcePath}, vaultRoot, Nothing, Global.FilingCabinet.IngestMode.Copy, "MD5")
                Dim artifact = artifacts(0)

                Assert.IsFalse(String.IsNullOrWhiteSpace(artifact.Md5))
                Assert.AreEqual("", artifact.Sha256)
                Assert.AreEqual("", artifact.Blake3)
                Assert.AreEqual("", artifact.KangarooTwelve)
                Assert.AreEqual("", artifact.Sha3_256)
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub IngestStoresRequestedMappedHashes()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim sourceRoot = Path.Combine(workspace, "source")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(sourceRoot)
            Dim sourcePath = Path.Combine(sourceRoot, "payload.txt")
            File.WriteAllText(sourcePath, "abc")

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifacts = service.Ingest({sourcePath}, vaultRoot, Nothing, Global.FilingCabinet.IngestMode.Copy, "crc32,xxhash64")
                Dim artifact = artifacts(0)

                Assert.AreEqual("", artifact.Sha256)
                Assert.AreEqual("352441c2", artifact.Hashes("crc32"))
                Assert.AreEqual("44bc2cf5ad770999", artifact.Hashes("xxhash64"))
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub StoredFileAdoptionCreatesCatalogReadyArtifact()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Dim itemsRoot = Path.Combine(vaultRoot, "items")
            Directory.CreateDirectory(itemsRoot)
            Dim storedPath = Path.Combine(itemsRoot, "orphan.json")
            File.WriteAllText(storedPath, "{""ok"":true}")

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifact = service.CreateArtifactFromStoredFile(storedPath, vaultRoot)

                Assert.AreEqual("orphan.json", artifact.Name)
                Assert.AreEqual("Manifests / Config", artifact.Category)
                Assert.AreEqual("Text", artifact.TypeFamily)
                Assert.AreEqual("Verified", artifact.HashStatus)
                Assert.AreEqual(Path.Combine("items", "orphan.json"), artifact.RelativePath)
                Assert.AreEqual(storedPath, artifact.OriginalPath)
                Assert.AreEqual("Extracted", artifact.ExtractedTextStatus)
                Assert.IsTrue(File.Exists(Path.Combine(vaultRoot, artifact.ExtractedTextRelativePath)))
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub BinaryLikeStoredFileIsMarkedNotExtractable()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Dim itemsRoot = Path.Combine(vaultRoot, "items")
            Directory.CreateDirectory(itemsRoot)
            Dim storedPath = Path.Combine(itemsRoot, "disk.iso")
            File.WriteAllBytes(storedPath, {0, 1, 2, 3})

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifact = service.CreateArtifactFromStoredFile(storedPath, vaultRoot)

                Assert.AreEqual("Not extractable", artifact.ExtractedTextStatus)
                Assert.AreEqual("", artifact.ExtractedTextRelativePath)
                Assert.AreEqual(Global.FilingCabinet.ThumbnailService.FallbackCardStatus, artifact.ThumbnailStatus)
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub DocxIngestExtractsReadableText()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim sourceRoot = Path.Combine(workspace, "source")
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Directory.CreateDirectory(sourceRoot)
            Dim sourcePath = Path.Combine(sourceRoot, "notes.docx")
            CreateMinimalDocx(sourcePath, "Quarterly archive notes")

            Try
                Dim service As New Global.FilingCabinet.IngestionService()
                Dim artifacts = service.Ingest({sourcePath}, vaultRoot, Nothing, Global.FilingCabinet.IngestMode.Copy)
                Dim artifact = artifacts(0)
                Dim extractedPath = Path.Combine(vaultRoot, artifact.ExtractedTextRelativePath)

                Assert.AreEqual("Documents", artifact.Category)
                Assert.AreEqual("Document", artifact.TypeFamily)
                Assert.AreEqual("Word Document", artifact.Type)
                Assert.AreEqual("Extracted", artifact.ExtractedTextStatus)
                Assert.IsTrue(File.Exists(extractedPath))
                StringAssert.Contains(File.ReadAllText(extractedPath), "Quarterly archive notes")
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub ExtractTextForArtifactRebuildsMissingTextIndex()
            Dim workspace = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim vaultRoot = Path.Combine(workspace, "vault")
            Dim itemRoot = Path.Combine(vaultRoot, "items")
            Directory.CreateDirectory(itemRoot)
            Dim storedPath = Path.Combine(itemRoot, "notes.txt")
            File.WriteAllText(storedPath, "repairable text")

            Try
                Dim artifact As New Global.FilingCabinet.ArtifactModel With {
                    .Name = "notes.txt",
                    .Path = storedPath
                }

                Dim extraction = New Global.FilingCabinet.IngestionService().ExtractTextForArtifact(artifact, vaultRoot)

                Assert.AreEqual("Extracted", extraction.Status)
                Assert.IsFalse(String.IsNullOrWhiteSpace(extraction.RelativePath))
                StringAssert.Contains(File.ReadAllText(Path.Combine(vaultRoot, extraction.RelativePath)), "repairable text")
            Finally
                If Directory.Exists(workspace) Then
                    Directory.Delete(workspace, recursive:=True)
                End If
            End Try
        End Sub

        Private Shared Sub CreateMinimalDocx(path As String, text As String)
            Using archive = ZipFile.Open(path, ZipArchiveMode.Create)
                Dim documentEntry = archive.CreateEntry("word/document.xml")
                Using writer As New StreamWriter(documentEntry.Open())
                    writer.Write($"<?xml version=""1.0"" encoding=""UTF-8""?><w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""><w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body></w:document>")
                End Using
            End Using
        End Sub
    End Class
End Namespace

