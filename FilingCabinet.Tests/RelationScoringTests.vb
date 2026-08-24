Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports System.IO

Namespace FilingCabinet.Tests
    <TestClass>
    Public Class RelationScoringTests
        <TestMethod>
        Sub DuplicateHashAndSharedTagsProduceExplainableRelation()
            Dim selected = Artifact("release-manifest.toml", "Manifests / Config", "Text", "abc123", {"release", "config"})
            Dim candidate = Artifact("release-notes.toml", "Manifests / Config", "Text", "abc123", {"release", "review"})

            Dim relation = Global.FilingCabinet.MainViewModel.BuildArtifactRelation(selected, candidate)

            Assert.IsNotNull(relation)
            Assert.IsTrue(relation.Score >= 21)
            CollectionAssert.Contains(relation.Reasons, "duplicate SHA-256")
            Assert.IsTrue(relation.Reasons.Any(Function(reason) reason.Contains("shared tag: release")))
            CollectionAssert.Contains(relation.Reasons, "same category")
            CollectionAssert.Contains(relation.Reasons, "same type family")
        End Sub

        <TestMethod>
        Sub SharedCaptureRecordProducesExplicitRelationReason()
            Dim selected = Artifact("driver.inf", "Manifests / Config", "Text", "hash-a", {})
            selected.CaptureId = "capture-123"
            selected.CaptureName = "2026-08-23 19:02 - Downloads intake"

            Dim candidate = Artifact("setup.exe", "Software / Installers", "Installer", "hash-b", {})
            candidate.CaptureId = "capture-123"
            candidate.CaptureName = selected.CaptureName

            Dim relation = Global.FilingCabinet.MainViewModel.BuildArtifactRelation(selected, candidate)

            Assert.IsNotNull(relation)
            CollectionAssert.Contains(relation.Reasons, "same capture record")
        End Sub

        <TestMethod>
        Sub SameOriginalFolderAndDateBatchProduceRelation()
            Dim root = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim sourceRoot = Path.Combine(root, "source")
            Directory.CreateDirectory(sourceRoot)

            Try
                Dim selected = Artifact("camera-roll-raw.zip", "Archives", "Archive", "hash-a", {})
                selected.OriginalPath = Path.Combine(sourceRoot, "camera-roll-raw.zip")
                selected.IngestedAt = "2026-05-23 16:00"

                Dim candidate = Artifact("camera-roll-preview.png", "Images", "Image", "hash-b", {})
                candidate.OriginalPath = Path.Combine(sourceRoot, "camera-roll-preview.png")
                candidate.IngestedAt = "2026-05-23 17:30"

                Dim relation = Global.FilingCabinet.MainViewModel.BuildArtifactRelation(selected, candidate)

                Assert.IsNotNull(relation)
                CollectionAssert.Contains(relation.Reasons, "same original folder")
                CollectionAssert.Contains(relation.Reasons, "same date batch")
                Assert.IsTrue(relation.Reasons.Any(Function(reason) reason.Contains("matching name token: camera")))
            Finally
                If Directory.Exists(root) Then
                    Directory.Delete(root, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub ExpandedSignalsProduceInspectableRelationReasons()
            Dim root = Path.Combine(Path.GetTempPath(), "FilingCabinetTests", Guid.NewGuid().ToString("N"))
            Dim extractedRoot = Path.Combine(root, "extracted-text")
            Directory.CreateDirectory(extractedRoot)

            Try
                Dim selected = Artifact("aptlantis-release-v1.2.0-manifest.json", "Manifests / Config", "Text", "abcdef1234560000", {"release"})
                selected.OriginalPath = Path.Combine(root, "source", "aptlantis-release-v1.2.0-manifest.json")
                selected.SourceProvenance = "Aptlantis release share"
                selected.IngestedAt = "2026-05-23 09:00"
                selected.ExtractedTextRelativePath = Path.Combine("extracted-text", "selected.txt")

                Dim candidate = Artifact("aptlantis-installer-v1.2.0.json", "Manifests / Config", "Text", "abcdef1234569999", {"installer"})
                candidate.OriginalPath = Path.Combine(root, "source", "aptlantis-installer-v1.2.0.json")
                candidate.SourceProvenance = "Aptlantis release share"
                candidate.IngestedAt = "2026-05-23 11:30"
                candidate.ExtractedTextRelativePath = Path.Combine("extracted-text", "candidate.txt")

                File.WriteAllText(Path.Combine(extractedRoot, "selected.txt"), "deterministic manifest retention context restore")
                File.WriteAllText(Path.Combine(extractedRoot, "candidate.txt"), "deterministic installer manifest verification context")

                Dim relation = Global.FilingCabinet.MainViewModel.BuildArtifactRelation(selected, candidate, root)

                Assert.IsNotNull(relation)
                CollectionAssert.Contains(relation.Reasons, "same ingest session")
                CollectionAssert.Contains(relation.Reasons, "shared extension family: manifest/config")
                Assert.IsTrue(relation.Reasons.Any(Function(reason) reason.Contains("shared provenance token: aptlantis")))
                Assert.IsTrue(relation.Reasons.Any(Function(reason) reason.Contains("shared release marker: v1.2.0")))
                Assert.IsTrue(relation.Reasons.Any(Function(reason) reason.Contains("shared hash prefix: abcdef123456")))
                Assert.IsTrue(relation.Reasons.Any(Function(reason) reason.Contains("shared extracted keyword:")))
            Finally
                If Directory.Exists(root) Then
                    Directory.Delete(root, recursive:=True)
                End If
            End Try
        End Sub

        <TestMethod>
        Sub NamedProjectManifestProducesSharedProjectOriginSignal()
            Dim selected = Artifact("FilingCabinet.vbproj", "Source Code", "Text", "hash-a", {})
            selected.OriginalPath = "E:\repos\FilingCabinet\FilingCabinet.vbproj"

            Dim candidate = Artifact("FilingCabinet-1.4.4.0-win-x64.msi", "Installers", "Installer", "hash-b", {})
            candidate.OriginalPath = "E:\repos\FilingCabinet\installer\FilingCabinet-1.4.4.0-win-x64.msi"
            candidate.SourceProvenance = "Built from FilingCabinet.vbproj"

            Dim keys = Global.FilingCabinet.MainViewModel.ManifestOriginKeys(selected)
            Assert.IsTrue(keys.Contains("filingcabinet", StringComparer.OrdinalIgnoreCase), "Should extract 'filingcabinet' from .vbproj stem")

            Dim relation = Global.FilingCabinet.MainViewModel.BuildArtifactRelation(selected, candidate)
            Assert.IsNotNull(relation)
            Assert.IsTrue(relation.Reasons.Any(Function(r) r.Contains("shared project origin") AndAlso r.Contains("filingcabinet")),
                          "Relation should cite shared project origin: filingcabinet")
        End Sub

        <TestMethod>
        Sub GenericManifestFilenameUsesParentDirectoryAsProjectKey()
            Dim selected = Artifact("package.json", "Source Code", "Text", "hash-c", {})
            selected.OriginalPath = "C:\work\my-webapp\package.json"

            Dim candidate = Artifact("package-lock.json", "Source Code", "Text", "hash-d", {})
            candidate.OriginalPath = "C:\work\my-webapp\package-lock.json"

            Dim keys = Global.FilingCabinet.MainViewModel.ManifestOriginKeys(selected)
            Assert.IsTrue(keys.Contains("my-webapp", StringComparer.OrdinalIgnoreCase), "Should extract parent dir 'my-webapp' from generic manifest")

            Dim relation = Global.FilingCabinet.MainViewModel.BuildArtifactRelation(selected, candidate)
            Assert.IsNotNull(relation)
            Assert.IsTrue(relation.Reasons.Any(Function(r) r.Contains("shared project origin") AndAlso r.Contains("my-webapp")),
                          "Relation should cite shared project origin: my-webapp")
        End Sub

        <TestMethod>
        Sub UnrelatedProjectOriginsProduceNoManifestSignal()
            Dim selected = Artifact("ProjectAlpha.sln", "Source Code", "Text", "hash-e", {})
            selected.OriginalPath = "C:\work\ProjectAlpha\ProjectAlpha.sln"

            Dim candidate = Artifact("ProjectBeta.sln", "Source Code", "Text", "hash-f", {})
            candidate.OriginalPath = "C:\work\ProjectBeta\ProjectBeta.sln"

            Dim keys = Global.FilingCabinet.MainViewModel.ManifestOriginKeys(selected)
            Assert.IsTrue(keys.Contains("projectalpha", StringComparer.OrdinalIgnoreCase))

            Dim sharedKeys = Global.FilingCabinet.MainViewModel.ManifestOriginKeys(candidate)
            Assert.IsFalse(sharedKeys.Any(Function(k) k.Equals("projectalpha", StringComparison.OrdinalIgnoreCase)),
                           "Different project keys should not overlap")
        End Sub

        Private Shared Function Artifact(name As String, category As String, typeFamily As String, sha256 As String, tags As IEnumerable(Of String)) As Global.FilingCabinet.ArtifactModel
            Return New Global.FilingCabinet.ArtifactModel With {
                .Name = name,
                .Category = category,
                .TypeFamily = typeFamily,
                .Sha256 = sha256,
                .Tags = tags.ToList()
            }
        End Function
    End Class
End Namespace

