Imports Microsoft.VisualStudio.TestTools.UnitTesting

Namespace FilingCabinet.Tests
    <TestClass>
    Public Class RepairCandidateTests
        <TestMethod>
        Sub MissingThumbnailMapsToRegenerateThumbnailCandidate()
            Dim candidate = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Missing thumbnail"))

            Assert.AreEqual("RegenerateThumbnail", candidate.ActionType)
            Assert.IsTrue(candidate.CanRepairAutomatically)
            Assert.IsTrue(candidate.RequiresOperatorApproval)
            Assert.AreEqual("Approval required", candidate.ApprovalText)
        End Sub

        <TestMethod>
        Sub MissingHashMapsToRecomputeHashCandidate()
            Dim candidate = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Missing hash"))

            Assert.AreEqual("RecomputeHash", candidate.ActionType)
            Assert.IsTrue(candidate.CanRepairAutomatically)
            Assert.IsTrue(candidate.RequiresOperatorApproval)
            Assert.AreEqual("Expensive automatic", candidate.RepairGroup)
            Assert.AreEqual("Can apply; reads retained file", candidate.SelectionState)
            StringAssert.Contains(candidate.ActionExplanation, "left off by default")
        End Sub

        <TestMethod>
        Sub MissingExtractedTextMapsToReExtractTextCandidate()
            Dim candidate = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Missing extracted text"))

            Assert.AreEqual("ReExtractText", candidate.ActionType)
            Assert.IsTrue(candidate.CanRepairAutomatically)
            Assert.IsTrue(candidate.RequiresOperatorApproval)
        End Sub

        <TestMethod>
        Sub ReviewFindingsRemainReviewOnly()
            Dim duplicate = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Duplicate hash"))
            Dim orphanThumbnail = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Orphan thumbnail"))
            Dim hashMismatch = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Hash mismatch"))
            Dim outsideVault = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("File outside vault"))
            Dim incompleteMetadata = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Incomplete metadata"))

            Assert.AreEqual("ReviewOnly", duplicate.ActionType)
            Assert.IsFalse(duplicate.CanRepairAutomatically)
            Assert.AreEqual("ReviewOnly", orphanThumbnail.ActionType)
            Assert.IsFalse(orphanThumbnail.CanRepairAutomatically)
            Assert.AreEqual("ReviewOnly", hashMismatch.ActionType)
            Assert.IsFalse(hashMismatch.CanRepairAutomatically)
            Assert.AreEqual("ReviewOnly", outsideVault.ActionType)
            Assert.IsFalse(outsideVault.CanRepairAutomatically)
            Assert.AreEqual("ReviewOnly", incompleteMetadata.ActionType)
            Assert.IsFalse(incompleteMetadata.CanRepairAutomatically)
            Assert.AreEqual("Review-only; cannot apply automatically", duplicate.SelectionState)
            Assert.AreEqual("Disabled: review-only finding", duplicate.SelectionHelp)
            StringAssert.Contains(duplicate.ActionExplanation, "Not selectable")
        End Sub

        <TestMethod>
        Sub PathRebindCandidateMapsToSafeCatalogRepair()
            Dim candidate = Global.FilingCabinet.MainViewModel.BuildRepairCandidate(Finding("Path rebind candidate"))

            Assert.AreEqual("RebindPath", candidate.ActionType)
            Assert.IsTrue(candidate.CanRepairAutomatically)
            Assert.IsTrue(candidate.RequiresOperatorApproval)
            Assert.AreEqual("Catalog only", candidate.RepairImpact)
            StringAssert.Contains(candidate.ActionExplanation, "Will run RebindPath")
        End Sub

        Private Shared Function Finding(findingType As String) As Global.FilingCabinet.VaultHealthFinding
            Return New Global.FilingCabinet.VaultHealthFinding With {
                .FindingType = findingType,
                .Subject = "sample",
                .ProposedAction = "Review"
            }
        End Function
    End Class
End Namespace

