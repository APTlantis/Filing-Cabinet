Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Input

Public Class MainViewModel
    Implements INotifyPropertyChanged

    Private Const LargeArtifactThresholdBytes As Long = 1024L * 1024L * 1024L
    Public Const AutoHashVerificationLimitBytes As Long = 1024L * 1024L * 1024L
    Private Shared ReadOnly ReleaseMarkerRegexTimeout As TimeSpan = TimeSpan.FromMilliseconds(250)
    Private Shared ReadOnly StandardHashDisplayIds As IReadOnlyList(Of String) = New List(Of String) From {
        "SHA256",
        "BLAKE3",
        "KangarooTwelve",
        "SHA3-256",
        "Skein"
    }
    Private Shared ReadOnly HashSettingCategoryOrder As IReadOnlyList(Of String) = New List(Of String) From {
        "Recommended",
        "Modern strong hashes",
        "Legacy cryptographic hashes",
        "Compatibility checksums",
        "Compatibility non-crypto hashes"
    }

    Private ReadOnly _catalogService As CatalogService
    Private ReadOnly _ingestionService As IngestionService
    Private ReadOnly _hashService As HashService
    Private ReadOnly _previewService As PreviewService
    Private ReadOnly _thumbnailService As ThumbnailService
    Private ReadOnly _repairLogService As RepairLogService
    Private _catalog As CatalogData
    Private _selectedArtifact As ArtifactModel
    Private _currentVault As VaultModel
    Private _filteredArtifacts As ICollectionView
    Private _searchText As String = ""
    Private _tagSearchText As String = ""
    Private _selectedCategory As CategoryModel
    Private _selectedTag As String
    Private _activeScope As String = "All"
    Private _ingestStatus As String = "Ready to ingest files"
    Private _editName As String = ""
    Private _editCategory As String = ""
    Private _editTagsText As String = ""
    Private _editRating As Integer
    Private _editNotes As String = ""
    Private _editRetentionReason As String = ""
    Private _editWhyThisMatters As String = ""
    Private _editSourceProvenance As String = ""
    Private _editAcquisitionMethod As String = ""
    Private _editTrustClassification As String = "Unknown"
    Private _editRetentionPriority As String = "Normal"
    Private _editArchiveStatus As String = "Active"
    Private _editStatus As String = "No pending edits"
    Private _actionStatus As String = "Select an artifact to run actions"
    Private _selectedPreview As ArtifactPreview = New ArtifactPreview With {.Kind = ArtifactPreviewKind.GenericFile, .Message = "No preview"}
    Private _rightPanelTab As String = "Preview"
    Private _ingestProgress As Double
    Private _ingestDetail As String = ""
    Private _isIngesting As Boolean
    Private _isLoadingEditor As Boolean
    Private _ingestMode As IngestMode = IngestMode.Move
    Private _settingsText As String = ""
    Private _repairStatus As String = "Repair checks not run"
    Private _artifactRowHeight As Integer = 34
    Private _columnPresetIndex As Integer
    Private _isLoadingCatalog As Boolean
    Private _isVaultMaintenanceRunning As Boolean
    Private _vaultMaintenanceProgress As Double
    Private _vaultMaintenanceStatus As String = "Vault maintenance ready"
    Private _vaultMaintenanceDetail As String = ""
    Private _quarantineCount As Integer
    Private _quarantineCountVersion As Integer
    Private _previewLoadVersion As Integer
    Private _searchVersion As Integer
    Private _searchExtractedTextMatches As HashSet(Of String)
    Private _searchExtractedText As String = ""
    Private _sameSourceBatchScopeKeys As HashSet(Of String)
    Private _sameSourceBatchScopeSelectedKey As String = ""
    Private _isRefreshingFilters As Boolean
    Private _isSettingsVisible As Boolean
    Private _isVaultHealthVisible As Boolean
    Private _hasVaultHealthAnalysis As Boolean
    Private _isBulkUpdatingRepairSelection As Boolean
    Private _repairCandidateFilter As String = "All"
    Private _findingTypeFilter As String = "All"

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Property Vaults As New ObservableCollection(Of VaultModel)
    Public Property Stats As New ObservableCollection(Of StatCardModel)
    Public Property Categories As New ObservableCollection(Of CategoryModel)
    Public Property Tags As New ObservableCollection(Of String)
    Public Property Activities As New ObservableCollection(Of ActivityEntryModel)
    Public Property Artifacts As New ObservableCollection(Of ArtifactModel)
    Public Property RelatedArtifacts As New ObservableCollection(Of ArtifactRelationModel)
    Public Property VaultHealthFindings As New ObservableCollection(Of VaultHealthFinding)
    Public Property RepairCandidates As New ObservableCollection(Of RepairCandidate)
    Public Property RepairHistory As New ObservableCollection(Of RepairLogEntry)
    Public Property HelpDocuments As New ObservableCollection(Of HelpDocumentModel)
    Private _filteredTags As ICollectionView
    Private _filteredRepairCandidates As ICollectionView
    Public Property ClearFiltersCommand As ICommand
    Public Property ShowAllItemsCommand As ICommand
    Public Property ShowRecentCommand As ICommand
    Public Property ShowStarredCommand As ICommand
    Public Property ShowQuarantineCommand As ICommand
    Public Property ShowUnverifiedCommand As ICommand
    Public Property ShowMissingPreviewCommand As ICommand
    Public Property ShowRepairNeededCommand As ICommand
    Public Property ShowDuplicateCandidatesCommand As ICommand
    Public Property ShowSameSourceBatchCommand As ICommand
    Public Property ShowLargeArtifactsCommand As ICommand
    Public Property RemoveCurrentVaultCommand As ICommand
    Public Property SaveArtifactCommand As ICommand
    Public Property RevertArtifactCommand As ICommand
    Public Property OpenLocationCommand As ICommand
    Public Property OpenFileCommand As ICommand
    Public Property RestoreArtifactCommand As ICommand
    Public Property PermanentlyDeleteArtifactCommand As ICommand
    Public Property HashCheckCommand As ICommand
    Public Property ToggleHashOptionCommand As ICommand
    Public Property RefreshCommand As ICommand
    Public Property ToggleStarCommand As ICommand
    Public Property AddTagsCommand As ICommand
    Public Property ToggleIngestModeCommand As ICommand
    Public Property QuarantineCommand As ICommand
    Public Property ShowSettingsCommand As ICommand
    Public Property CloseSettingsCommand As ICommand
    Public Property ShowVaultHealthCommand As ICommand
    Public Property CloseVaultHealthCommand As ICommand
    Public Property BackupCatalogCommand As ICommand
    Public Property RepairCatalogCommand As ICommand
    Public Property RescanVaultCommand As ICommand
    Public Property ApplySelectedRepairCandidatesCommand As ICommand
    Public Property SelectSafeRepairCandidatesCommand As ICommand
    Public Property SelectVisibleRepairCandidatesCommand As ICommand
    Public Property ClearVisibleRepairCandidatesCommand As ICommand
    Public Property SelectRepairActionCommand As ICommand
    Public Property SelectAllAutomaticRepairCandidatesCommand As ICommand
    Public Property ClearRepairSelectionCommand As ICommand
    Public Property VerifySmallHashesCommand As ICommand
    Public Property SortByNameCommand As ICommand
    Public Property SortByDateCommand As ICommand
    Public Property ToggleDensityCommand As ICommand
    Public Property CycleColumnPresetCommand As ICommand
    Public Property OpenHelpDocumentCommand As ICommand
    Public Property OpenDocsFolderCommand As ICommand
    Public Property ShowAboutCommand As ICommand

    Public Sub New()
        _catalogService = New CatalogService()
        _ingestionService = New IngestionService()
        _hashService = New HashService()
        _thumbnailService = New ThumbnailService()
        _previewService = New PreviewService(_thumbnailService)
        _repairLogService = New RepairLogService()
        ClearFiltersCommand = New RelayCommand(Sub(parameter) ClearFilters())
        ShowAllItemsCommand = New RelayCommand(Sub(parameter) SetScope("All"))
        ShowRecentCommand = New RelayCommand(Sub(parameter) SetScope("Recent"))
        ShowStarredCommand = New RelayCommand(Sub(parameter) SetScope("Starred"))
        ShowQuarantineCommand = New RelayCommand(Sub(parameter) SetScope("Quarantine"))
        ShowUnverifiedCommand = New RelayCommand(Sub(parameter) SetScope("Unverified"))
        ShowMissingPreviewCommand = New RelayCommand(Sub(parameter) SetScope("Missing preview"))
        ShowRepairNeededCommand = New RelayCommand(Sub(parameter) SetScope("Repair needed"))
        ShowDuplicateCandidatesCommand = New RelayCommand(Sub(parameter) SetScope("Duplicate candidates"))
        ShowSameSourceBatchCommand = New RelayCommand(Sub(parameter) SetScope("Same source batch"))
        ShowLargeArtifactsCommand = New RelayCommand(Sub(parameter) SetScope("Large artifacts"))
        RemoveCurrentVaultCommand = New RelayCommand(Sub(parameter) RemoveCurrentVault(), Function(parameter) CurrentVault IsNot Nothing AndAlso Vaults.Count > 1)
        SaveArtifactCommand = New RelayCommand(Sub(parameter) SaveArtifactEdits(), Function(parameter) SelectedArtifact IsNot Nothing)
        RevertArtifactCommand = New RelayCommand(Sub(parameter) LoadEditorFromSelected(), Function(parameter) SelectedArtifact IsNot Nothing)
        OpenLocationCommand = New RelayCommand(Sub(parameter) OpenSelectedLocation(), Function(parameter) SelectedArtifact IsNot Nothing)
        OpenFileCommand = New RelayCommand(Sub(parameter) OpenSelectedFile(), Function(parameter) SelectedArtifact IsNot Nothing)
        RestoreArtifactCommand = New RelayCommand(Sub(parameter) RestoreSelectedArtifact(TryCast(parameter, String)), Function(parameter) SelectedArtifact IsNot Nothing)
        PermanentlyDeleteArtifactCommand = New RelayCommand(Sub(parameter) PermanentlyDeleteSelectedArtifact(), Function(parameter) SelectedArtifact IsNot Nothing)
        HashCheckCommand = New AsyncRelayCommand(Function(parameter) CheckSelectedHashAsync(), Function(parameter) SelectedArtifact IsNot Nothing AndAlso Not IsVaultMaintenanceRunning, AddressOf HandleAsyncCommandException)
        ToggleHashOptionCommand = New RelayCommand(Sub(parameter) ToggleHashOption(parameter))
        RefreshCommand = New AsyncRelayCommand(Function(parameter) RefreshVaultStateAsync(), Function(parameter) Not IsVaultMaintenanceRunning, AddressOf HandleAsyncCommandException)
        ToggleStarCommand = New RelayCommand(Sub(parameter) ToggleSelectedStar(), Function(parameter) SelectedArtifact IsNot Nothing)
        AddTagsCommand = New RelayCommand(Sub(parameter) FocusTagEditing(), Function(parameter) SelectedArtifact IsNot Nothing)
        ToggleIngestModeCommand = New RelayCommand(Sub(parameter) ToggleIngestMode())
        QuarantineCommand = New RelayCommand(Sub(parameter) QuarantineSelectedArtifact(), Function(parameter) SelectedArtifact IsNot Nothing)
        ShowSettingsCommand = New RelayCommand(Sub(parameter) ShowSettings())
        CloseSettingsCommand = New RelayCommand(Sub(parameter) CloseSettings())
        ShowVaultHealthCommand = New RelayCommand(Sub(parameter) ShowVaultHealth())
        CloseVaultHealthCommand = New RelayCommand(Sub(parameter) CloseVaultHealth())
        BackupCatalogCommand = New RelayCommand(Sub(parameter) BackupCatalog())
        RepairCatalogCommand = New AsyncRelayCommand(Function(parameter) RepairCatalogAsync(), Function(parameter) Not IsVaultMaintenanceRunning, AddressOf HandleAsyncCommandException)
        RescanVaultCommand = New AsyncRelayCommand(Function(parameter) RescanVaultAsync(), Function(parameter) Not IsVaultMaintenanceRunning, AddressOf HandleAsyncCommandException)
        ApplySelectedRepairCandidatesCommand = New AsyncRelayCommand(Function(parameter) ApplySelectedRepairCandidatesAsync(), Function(parameter) Not IsVaultMaintenanceRunning AndAlso RepairCandidates.Any(Function(candidate) candidate.IsSelected AndAlso candidate.CanRepairAutomatically), AddressOf HandleAsyncCommandException)
        SelectSafeRepairCandidatesCommand = New RelayCommand(Sub(parameter) SelectSafeRepairCandidates(), Function(parameter) RepairCandidates.Any(Function(candidate) candidate.CanRepairAutomatically))
        SelectVisibleRepairCandidatesCommand = New RelayCommand(Sub(parameter) SelectVisibleRepairCandidates(), Function(parameter) RepairCandidates.Any(Function(candidate) candidate.CanRepairAutomatically))
        ClearVisibleRepairCandidatesCommand = New RelayCommand(Sub(parameter) ClearVisibleRepairCandidates(), Function(parameter) RepairCandidates.Any(Function(candidate) candidate.IsSelected))
        SelectRepairActionCommand = New RelayCommand(Sub(parameter) SelectRepairAction(TryCast(parameter, String)), Function(parameter) RepairCandidates.Any(Function(candidate) candidate.CanRepairAutomatically))
        SelectAllAutomaticRepairCandidatesCommand = New RelayCommand(Sub(parameter) SelectAllAutomaticRepairCandidates(), Function(parameter) RepairCandidates.Any(Function(candidate) candidate.CanRepairAutomatically))
        ClearRepairSelectionCommand = New RelayCommand(Sub(parameter) ClearRepairSelection(), Function(parameter) RepairCandidates.Any(Function(candidate) candidate.IsSelected))
        VerifySmallHashesCommand = New AsyncRelayCommand(Function(parameter) VerifySmallHashesAsync(), Function(parameter) Not IsVaultMaintenanceRunning, AddressOf HandleAsyncCommandException)
        SortByNameCommand = New RelayCommand(Sub(parameter) ApplySort(NameOf(ArtifactModel.Name)))
        SortByDateCommand = New RelayCommand(Sub(parameter) ApplySort(NameOf(ArtifactModel.DateModified)))
        ToggleDensityCommand = New RelayCommand(Sub(parameter) ToggleDensity())
        CycleColumnPresetCommand = New RelayCommand(Sub(parameter) CycleColumnPreset())
        OpenHelpDocumentCommand = New RelayCommand(Sub(parameter) OpenDocumentationPath(TryCast(parameter, String)))
        OpenDocsFolderCommand = New RelayCommand(Sub(parameter) OpenDocsFolder())
        ShowAboutCommand = New RelayCommand(Sub(parameter) ShowAbout())
        ReplaceCollection(HelpDocuments, DefaultHelpDocuments())
        LoadCatalog()
    End Sub

    Public Property CurrentVault As VaultModel
        Get
            Return _currentVault
        End Get
        Set(value As VaultModel)
            If Not ReferenceEquals(_currentVault, value) Then
                _currentVault = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(CurrentVaultTitle))
                OnPropertyChanged(NameOf(CurrentVaultSummary))
                OnPropertyChanged(NameOf(StorageUsedText))
                OnPropertyChanged(NameOf(StorageTotalText))
                OnPropertyChanged(NameOf(LastBackupDisplay))
                If _catalog IsNot Nothing AndAlso value IsNot Nothing Then
                    _catalog.CurrentVaultId = value.Id
                    _catalog.VaultRootPath = value.Path
                    _catalogService.Save(_catalog)
                    RefreshRepairHistory()
                End If
            End If
        End Set
    End Property

    Public Property SelectedArtifact As ArtifactModel
        Get
            Return _selectedArtifact
        End Get
        Set(value As ArtifactModel)
            If Not ReferenceEquals(_selectedArtifact, value) Then
                _selectedArtifact = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(StoredFileStatus))
                OnPropertyChanged(NameOf(SelectedBlake3Display))
                OnPropertyChanged(NameOf(SelectedSha256Display))
                OnPropertyChanged(NameOf(SelectedHashDisplays))
                ActionStatus = If(StoredFileExists(), "Stored file ready", "Stored file missing")
                LoadPreviewForSelectedAsync()
                LoadEditorFromSelected()
                RebuildRelatedArtifacts()
                RaiseSelectedArtifactCommandState()
                If String.Equals(ActiveScope, "Same source batch", StringComparison.OrdinalIgnoreCase) Then
                    InvalidateDiscoveryScopeCache()
                    If Not _isRefreshingFilters Then
                        RefreshFilters(preserveSelection:=True)
                    End If
                End If
            End If
        End Set
    End Property

    Public Property FilteredArtifacts As ICollectionView
        Get
            Return _filteredArtifacts
        End Get
        Private Set(value As ICollectionView)
            If Not ReferenceEquals(_filteredArtifacts, value) Then
                _filteredArtifacts = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property FilteredTags As ICollectionView
        Get
            Return _filteredTags
        End Get
        Private Set(value As ICollectionView)
            If Not ReferenceEquals(_filteredTags, value) Then
                _filteredTags = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property FilteredRepairCandidates As ICollectionView
        Get
            Return _filteredRepairCandidates
        End Get
        Private Set(value As ICollectionView)
            If Not ReferenceEquals(_filteredRepairCandidates, value) Then
                _filteredRepairCandidates = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property RepairCandidateFilter As String
        Get
            Return _repairCandidateFilter
        End Get
        Set(value As String)
            Dim normalized = If(String.IsNullOrWhiteSpace(value), "All", value)
            If _repairCandidateFilter <> normalized Then
                _repairCandidateFilter = normalized
                OnPropertyChanged()
                RefreshRepairCandidateView()
            End If
        End Set
    End Property

    Public Property FindingTypeFilter As String
        Get
            Return _findingTypeFilter
        End Get
        Set(value As String)
            Dim normalized = If(String.IsNullOrWhiteSpace(value), "All", value)
            If _findingTypeFilter <> normalized Then
                _findingTypeFilter = normalized
                OnPropertyChanged()
                RefreshRepairCandidateView()
            End If
        End Set
    End Property

    Public Property SearchText As String
        Get
            Return _searchText
        End Get
        Set(value As String)
            If _searchText <> value Then
                _searchText = If(value, "")
                OnPropertyChanged()
                ScheduleSearchRefresh()
                SaveUiPreferences()
            End If
        End Set
    End Property

    Public Property TagSearchText As String
        Get
            Return _tagSearchText
        End Get
        Set(value As String)
            Dim normalized = If(value, "")
            If _tagSearchText <> normalized Then
                _tagSearchText = normalized
                OnPropertyChanged()
                RefreshTagFilter()
                SaveUiPreferences()
            End If
        End Set
    End Property

    Public Property SelectedCategory As CategoryModel
        Get
            Return _selectedCategory
        End Get
        Set(value As CategoryModel)
            If Not ReferenceEquals(_selectedCategory, value) Then
                _selectedCategory = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(FilterTitle))
                RefreshFilters()
                SaveUiPreferences()
            End If
        End Set
    End Property

    Public Property SelectedTag As String
        Get
            Return _selectedTag
        End Get
        Set(value As String)
            If _selectedTag <> value Then
                _selectedTag = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(FilterTitle))
                RefreshFilters()
                SaveUiPreferences()
            End If
        End Set
    End Property

    Public Property ActiveScope As String
        Get
            Return _activeScope
        End Get
        Set(value As String)
            Dim normalized = NormalizeScope(value)
            If _activeScope <> normalized Then
                _activeScope = normalized
                InvalidateDiscoveryScopeCache()
                OnPropertyChanged()
                OnPropertyChanged(NameOf(FilterTitle))
                OnPropertyChanged(NameOf(ActiveScopeText))
                RefreshFilters(preserveSelection:=True)
                SaveUiPreferences()
            End If
        End Set
    End Property

    Public Property IngestStatus As String
        Get
            Return _ingestStatus
        End Get
        Set(value As String)
            If _ingestStatus <> value Then
                _ingestStatus = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property EditName As String
        Get
            Return _editName
        End Get
        Set(value As String)
            SetEditValue(_editName, If(value, ""), NameOf(EditName))
        End Set
    End Property

    Public Property EditCategory As String
        Get
            Return _editCategory
        End Get
        Set(value As String)
            SetEditValue(_editCategory, If(value, ""), NameOf(EditCategory))
        End Set
    End Property

    Public Property EditTagsText As String
        Get
            Return _editTagsText
        End Get
        Set(value As String)
            SetEditValue(_editTagsText, If(value, ""), NameOf(EditTagsText))
        End Set
    End Property

    Public Property EditRating As Integer
        Get
            Return _editRating
        End Get
        Set(value As Integer)
            SetEditValue(_editRating, Math.Max(0, Math.Min(5, value)), NameOf(EditRating))
        End Set
    End Property

    Public Property EditNotes As String
        Get
            Return _editNotes
        End Get
        Set(value As String)
            SetEditValue(_editNotes, If(value, ""), NameOf(EditNotes))
        End Set
    End Property

    Public Property EditRetentionReason As String
        Get
            Return _editRetentionReason
        End Get
        Set(value As String)
            SetEditValue(_editRetentionReason, If(value, ""), NameOf(EditRetentionReason))
        End Set
    End Property

    Public Property EditWhyThisMatters As String
        Get
            Return _editWhyThisMatters
        End Get
        Set(value As String)
            SetEditValue(_editWhyThisMatters, If(value, ""), NameOf(EditWhyThisMatters))
        End Set
    End Property

    Public Property EditSourceProvenance As String
        Get
            Return _editSourceProvenance
        End Get
        Set(value As String)
            SetEditValue(_editSourceProvenance, If(value, ""), NameOf(EditSourceProvenance))
        End Set
    End Property

    Public Property EditAcquisitionMethod As String
        Get
            Return _editAcquisitionMethod
        End Get
        Set(value As String)
            SetEditValue(_editAcquisitionMethod, If(value, ""), NameOf(EditAcquisitionMethod))
        End Set
    End Property

    Public Property EditTrustClassification As String
        Get
            Return _editTrustClassification
        End Get
        Set(value As String)
            SetEditValue(_editTrustClassification, NormalizeChoice(value, TrustClassificationOptions, "Unknown"), NameOf(EditTrustClassification))
        End Set
    End Property

    Public Property EditRetentionPriority As String
        Get
            Return _editRetentionPriority
        End Get
        Set(value As String)
            SetEditValue(_editRetentionPriority, NormalizeChoice(value, RetentionPriorityOptions, "Normal"), NameOf(EditRetentionPriority))
        End Set
    End Property

    Public Property EditArchiveStatus As String
        Get
            Return _editArchiveStatus
        End Get
        Set(value As String)
            SetEditValue(_editArchiveStatus, NormalizeChoice(value, ArchiveStatusOptions, "Active"), NameOf(EditArchiveStatus))
        End Set
    End Property

    Public Property EditStatus As String
        Get
            Return _editStatus
        End Get
        Set(value As String)
            If _editStatus <> value Then
                _editStatus = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property ActionStatus As String
        Get
            Return _actionStatus
        End Get
        Set(value As String)
            If _actionStatus <> value Then
                _actionStatus = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property IngestProgress As Double
        Get
            Return _ingestProgress
        End Get
        Set(value As Double)
            If Math.Abs(_ingestProgress - value) > 0.01 Then
                _ingestProgress = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property IngestDetail As String
        Get
            Return _ingestDetail
        End Get
        Set(value As String)
            If _ingestDetail <> value Then
                _ingestDetail = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property IsIngesting As Boolean
        Get
            Return _isIngesting
        End Get
        Set(value As Boolean)
            If _isIngesting <> value Then
                _isIngesting = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property IsVaultMaintenanceRunning As Boolean
        Get
            Return _isVaultMaintenanceRunning
        End Get
        Private Set(value As Boolean)
            If _isVaultMaintenanceRunning <> value Then
                _isVaultMaintenanceRunning = value
                OnPropertyChanged()
                RaiseMaintenanceCommandState()
            End If
        End Set
    End Property

    Public Property VaultMaintenanceProgress As Double
        Get
            Return _vaultMaintenanceProgress
        End Get
        Private Set(value As Double)
            Dim normalized = Math.Max(0, Math.Min(100, value))
            If Math.Abs(_vaultMaintenanceProgress - normalized) > 0.01 Then
                _vaultMaintenanceProgress = normalized
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property VaultMaintenanceStatus As String
        Get
            Return _vaultMaintenanceStatus
        End Get
        Private Set(value As String)
            If _vaultMaintenanceStatus <> value Then
                _vaultMaintenanceStatus = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property VaultMaintenanceDetail As String
        Get
            Return _vaultMaintenanceDetail
        End Get
        Private Set(value As String)
            If _vaultMaintenanceDetail <> value Then
                _vaultMaintenanceDetail = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property CurrentIngestMode As IngestMode
        Get
            Return _ingestMode
        End Get
        Set(value As IngestMode)
            If _ingestMode <> value Then
                _ingestMode = value
                If _catalog IsNot Nothing Then
                    _catalog.DefaultIngestMode = value.ToString()
                    _catalogService.Save(_catalog)
                End If
                OnPropertyChanged()
                OnPropertyChanged(NameOf(IngestModeText))
                OnPropertyChanged(NameOf(IngestModeDetail))
                OnPropertyChanged(NameOf(SettingsText))
            End If
        End Set
    End Property

    Public Property SelectedPreview As ArtifactPreview
        Get
            Return _selectedPreview
        End Get
        Set(value As ArtifactPreview)
            If value Is Nothing Then
                value = New ArtifactPreview With {.Kind = ArtifactPreviewKind.GenericFile, .Message = "No preview"}
            End If

            If Not ReferenceEquals(_selectedPreview, value) Then
                _selectedPreview = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(IsImagePreview))
                OnPropertyChanged(NameOf(IsTextPreview))
                OnPropertyChanged(NameOf(IsGenericPreview))
                OnPropertyChanged(NameOf(IsMissingPreview))
            End If
        End Set
    End Property

    Public Property RightPanelTab As String
        Get
            Return _rightPanelTab
        End Get
        Set(value As String)
            Dim normalized = NormalizeRightPanelTab(value)
            If _rightPanelTab <> normalized Then
                _rightPanelTab = normalized
                OnPropertyChanged()
                OnPropertyChanged(NameOf(IsPreviewTabSelected))
                OnPropertyChanged(NameOf(IsDetailsTabSelected))
                OnPropertyChanged(NameOf(IsRelationsTabSelected))
            End If
        End Set
    End Property

    Public ReadOnly Property CurrentVaultTitle As String
        Get
            If CurrentVault Is Nothing Then
                Return "No vault selected"
            End If

            Return CurrentVault.DisplayName
        End Get
    End Property

    Public ReadOnly Property CurrentVaultSummary As String
        Get
            Return $"{Artifacts.Count:N0} items  •  {FormatSize(Artifacts.Sum(Function(a) a.SizeBytes))} retained"
        End Get
    End Property

    Public ReadOnly Property FilterTitle As String
        Get
            Dim parts As New List(Of String)

            If Not String.Equals(ActiveScope, "All", StringComparison.OrdinalIgnoreCase) Then
                parts.Add(ActiveScope)
            End If

            If SelectedCategory IsNot Nothing Then
                parts.Add(SelectedCategory.Name)
            End If

            If Not String.IsNullOrWhiteSpace(SelectedTag) Then
                parts.Add("#" & SelectedTag)
            End If

            If parts.Count = 0 AndAlso String.IsNullOrWhiteSpace(SearchText) Then
                Return "ALL ITEMS"
            End If

            If Not String.IsNullOrWhiteSpace(SearchText) Then
                parts.Add("search")
            End If

            Return "FILTERED ITEMS - " & String.Join(" / ", parts)
        End Get
    End Property

    Public ReadOnly Property ActiveScopeText As String
        Get
            Return $"View: {ActiveScope}"
        End Get
    End Property

    Public ReadOnly Property StorageUsedText As String
        Get
            Return $"{FormatSize(Artifacts.Sum(Function(a) a.SizeBytes))} cataloged"
        End Get
    End Property

    Public ReadOnly Property StorageTotalText As String
        Get
            If Not Directory.Exists(VaultRootPath) Then
                Return "vault unavailable"
            End If

            Dim root = Path.GetPathRoot(Path.GetFullPath(VaultRootPath))
            If String.IsNullOrWhiteSpace(root) Then
                Return "storage unknown"
            End If

            Dim drive = New DriveInfo(root)
            Return $"{FormatSize(drive.AvailableFreeSpace)} free"
        End Get
    End Property

    Public ReadOnly Property IngestModeText As String
        Get
            If CurrentIngestMode = IngestMode.Move Then
                Return "Move into vault"
            End If

            Return "Copy into vault"
        End Get
    End Property

    Public ReadOnly Property IngestModeDetail As String
        Get
            If CurrentIngestMode = IngestMode.Move Then
                Return "Default intake removes the original after a verified transfer."
            End If

            Return "Default intake keeps the original file in place."
        End Get
    End Property

    Public ReadOnly Property DuplicatePolicyText As String
        Get
            Return "Duplicates: rename safely"
        End Get
    End Property

    Public ReadOnly Property InboxCountText As String
        Get
            Return Artifacts.Where(Function(a) IsRecentArtifact(a)).Count().ToString("N0")
        End Get
    End Property

    Public ReadOnly Property StarredCountText As String
        Get
            Return Artifacts.Where(Function(a) a.IsStarred).Count().ToString("N0")
        End Get
    End Property

    Public ReadOnly Property QuarantineCountText As String
        Get
            Return _quarantineCount.ToString("N0")
        End Get
    End Property


    Public ReadOnly Property SettingsText As String
        Get
            Return _settingsText
        End Get
    End Property

    Public Property IsSettingsVisible As Boolean
        Get
            Return _isSettingsVisible
        End Get
        Set(value As Boolean)
            If _isSettingsVisible <> value Then
                _isSettingsVisible = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public Property IsVaultHealthVisible As Boolean
        Get
            Return _isVaultHealthVisible
        End Get
        Set(value As Boolean)
            If _isVaultHealthVisible <> value Then
                _isVaultHealthVisible = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(IsDashboardVisible))
                OnPropertyChanged(NameOf(IsVaultHealthWorkspaceVisible))
            End If
        End Set
    End Property

    Public ReadOnly Property IsDashboardVisible As Boolean
        Get
            Return Not IsVaultHealthVisible
        End Get
    End Property

    Public ReadOnly Property IsVaultHealthWorkspaceVisible As Boolean
        Get
            Return IsVaultHealthVisible
        End Get
    End Property

    Private Sub ShowVaultHealth()
        IsVaultHealthVisible = True
        ActionStatus = "Vault health workspace ready"
    End Sub

    Private Sub CloseVaultHealth()
        If IsVaultMaintenanceRunning Then
            ActionStatus = "Vault health is still running; wait for the results before closing."
            Return
        End If

        IsVaultHealthVisible = False
    End Sub

    Private Function IsHashActive(hashName As String) As Boolean
        If _catalog Is Nothing Then Return False
        Return HashRegistry.IsActive(_catalog.ActiveHashes, hashName)
    End Function

    Private Sub SetHashActive(hashName As String, value As Boolean)
        If _catalog Is Nothing Then Return
        Dim hashes = HashRegistry.ParseActiveHashIds(_catalog.ActiveHashes).ToList()
        Dim exists = hashes.Contains(hashName, StringComparer.OrdinalIgnoreCase)
        If value AndAlso Not exists Then
            hashes.Add(hashName)
        ElseIf Not value AndAlso exists Then
            If hashes.Count = 1 Then
                ActionStatus = "At least one hash must remain active"
                RaiseHashSettingChanges()
                Return
            End If

            hashes.RemoveAll(Function(h) String.Equals(h, hashName, StringComparison.OrdinalIgnoreCase))
        Else
            Return
        End If
        _catalog.ActiveHashes = HashRegistry.NormalizeActiveHashes(String.Join(",", hashes))
        _catalogService.Save(_catalog)
        RaiseHashSettingChanges()
        ActionStatus = $"Active hashes: {ActiveHashSummary}"
    End Sub

    Private Sub RaiseHashSettingChanges()
        OnPropertyChanged(NameOf(HashSettingGroups))
        OnPropertyChanged(NameOf(HashSettingOptions))
        OnPropertyChanged(NameOf(ActiveHashSummary))
        OnPropertyChanged(NameOf(HashSettingsNote))
        OnPropertyChanged(NameOf(SettingsText))
        OnPropertyChanged(NameOf(SelectedHashDisplays))
    End Sub

    Public ReadOnly Property HashSettingGroups As IEnumerable(Of HashSettingGroupModel)
        Get
            Dim activeHashes = If(_catalog?.ActiveHashes, HashRegistry.DefaultActiveHashes)
            Return HashRegistry.Options.
                GroupBy(Function(optionItem) optionItem.Category).
                OrderBy(Function(group) CategorySortIndex(group.Key)).
                ThenBy(Function(group) group.Key).
                Select(Function(group) New HashSettingGroupModel With {
                    .Name = group.Key,
                    .Summary = HashCategorySummary(group.Key),
                    .Options = group.Select(Function(optionItem) BuildHashSettingOption(optionItem, activeHashes)).ToList()
                }).
                ToList()
        End Get
    End Property

    Public ReadOnly Property HashSettingOptions As IEnumerable(Of HashSettingOptionModel)
        Get
            Dim activeHashes = If(_catalog?.ActiveHashes, HashRegistry.DefaultActiveHashes)
            Return HashRegistry.Options.Select(Function(optionItem) BuildHashSettingOption(optionItem, activeHashes)).ToList()
        End Get
    End Property

    Private Function BuildHashSettingOption(optionItem As HashOption, activeHashes As String) As HashSettingOptionModel
        Return New HashSettingOptionModel With {
                .Id = optionItem.Id,
                .DisplayName = optionItem.DisplayName,
                .Note = optionItem.Note,
                .Detail = optionItem.Detail,
                .Category = optionItem.Category,
                .IsActive = HashRegistry.IsActive(activeHashes, optionItem.Id),
                .ToggleCommand = ToggleHashOptionCommand
            }
    End Function

    Private Shared Function CategorySortIndex(category As String) As Integer
        Dim index = HashSettingCategoryOrder.ToList().FindIndex(Function(item) String.Equals(item, category, StringComparison.OrdinalIgnoreCase))
        Return If(index >= 0, index, Integer.MaxValue)
    End Function

    Private Shared Function HashCategorySummary(category As String) As String
        Select Case category
            Case "Recommended"
                Return "Start here. New vaults record SHA-256 only unless you opt into more evidence."
            Case "Modern strong hashes"
                Return "Good additions when you want multiple modern cryptographic fingerprints."
            Case "Legacy cryptographic hashes"
                Return "For checking older published digests; avoid these for new trust decisions."
            Case "Compatibility checksums"
                Return "For matching old tools, archives, and device checksums; not security evidence."
            Case "Compatibility non-crypto hashes"
                Return "For matching historical hash values in manifests or tooling; not security evidence."
            Case Else
                Return ""
        End Select
    End Function

    Public ReadOnly Property ActiveHashSummary As String
        Get
            If _catalog Is Nothing Then
                Return HashRegistry.NormalizeActiveHashes("")
            End If

            Dim activeNames = HashRegistry.ParseActiveHashIds(_catalog.ActiveHashes).
                Select(Function(hashId)
                           Dim optionItem = HashRegistry.Options.FirstOrDefault(Function(item) String.Equals(item.Id, hashId, StringComparison.OrdinalIgnoreCase))
                           Return If(optionItem?.DisplayName, hashId)
                       End Function)
            Return String.Join(", ", activeNames)
        End Get
    End Property

    Public ReadOnly Property HashSettingsNote As String
        Get
            If _catalog Is Nothing Then
                Return ""
            End If

            Dim legacyNotes = HashRegistry.Options.
                Where(Function(optionItem) HashRegistry.IsActive(_catalog.ActiveHashes, optionItem.Id) AndAlso Not String.IsNullOrWhiteSpace(optionItem.Note)).
                Select(Function(optionItem) $"{optionItem.DisplayName}: {optionItem.Note}")
            Return String.Join("  ", legacyNotes)
        End Get
    End Property

    Public ReadOnly Property SelectedHashDisplays As IEnumerable(Of HashDisplayModel)
        Get
            Return BuildHashDisplayRows(SelectedArtifact, If(_catalog?.ActiveHashes, HashRegistry.DefaultActiveHashes))
        End Get
    End Property

    Private Sub ToggleHashOption(parameter As Object)
        Dim optionItem = TryCast(parameter, HashSettingOptionModel)
        If optionItem Is Nothing Then Return

        SetHashActive(optionItem.Id, optionItem.IsActive)
    End Sub

    Public ReadOnly Property LastBackupDisplay As String
        Get
            If _catalog Is Nothing OrElse String.IsNullOrWhiteSpace(_catalog.LastBackupPath) Then
                Return "No catalog backup yet"
            End If

            Return _catalog.LastBackupPath
        End Get
    End Property

    Public ReadOnly Property RecallStatusText As String
        Get
            Return "Deterministic metadata, text, hashes, and relations"
        End Get
    End Property

    Public Property ArtifactRowHeight As Integer
        Get
            Return _artifactRowHeight
        End Get
        Set(value As Integer)
            Dim normalized = Math.Max(28, Math.Min(42, value))
            If _artifactRowHeight <> normalized Then
                _artifactRowHeight = normalized
                OnPropertyChanged()
                OnPropertyChanged(NameOf(DensityText))
                SaveUiPreferences()
            End If
        End Set
    End Property

    Public ReadOnly Property DensityText As String
        Get
            If ArtifactRowHeight <= 30 Then
                Return "Comfortable rows"
            End If

            Return "Compact rows"
        End Get
    End Property

    Public ReadOnly Property ColumnPresetText As String
        Get
            Return $"Columns: {ColumnPresetName}"
        End Get
    End Property

    Public ReadOnly Property ShowTypeColumn As Boolean
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property ShowCategoryColumn As Boolean
        Get
            Return _columnPresetIndex = 0
        End Get
    End Property

    Public ReadOnly Property ShowSizeColumn As Boolean
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property ShowDateColumn As Boolean
        Get
            Return _columnPresetIndex <= 1
        End Get
    End Property

    Public ReadOnly Property ShowTagsColumn As Boolean
        Get
            Return _columnPresetIndex = 0
        End Get
    End Property

    Public ReadOnly Property RelatedArtifactsSummary As String
        Get
            If SelectedArtifact Is Nothing Then
                Return "No artifact selected"
            End If

            If RelatedArtifacts.Count = 0 Then
                Return "No close relations found yet"
            End If

            If RelatedArtifacts.Count > 5 Then
                Return $"Top 5 of {RelatedArtifacts.Count:N0} related item(s)"
            End If

            Return $"{RelatedArtifacts.Count:N0} related item(s)"
        End Get
    End Property

    Public ReadOnly Property PreviewRelatedArtifacts As IEnumerable(Of ArtifactRelationModel)
        Get
            Return RelatedArtifacts.Take(5).ToList()
        End Get
    End Property

    Public ReadOnly Property RepairStatus As String
        Get
            Return _repairStatus
        End Get
    End Property

    Public ReadOnly Property VaultHealthSummary As String
        Get
            If Not _hasVaultHealthAnalysis Then
                Return "No health analysis run"
            End If

            Dim findings = VaultHealthFindings.Count
            Dim repairable = RepairCandidates.Where(Function(candidate) candidate.CanRepairAutomatically).Count()
            Dim reviewOnly = RepairCandidates.Count - repairable
            Dim selected = RepairCandidates.Where(Function(candidate) candidate.IsSelected).Count()
            Return $"{findings:N0} finding(s), {repairable:N0} repairable, {reviewOnly:N0} review-only, {selected:N0} selected"
        End Get
    End Property

    Public ReadOnly Property VaultHealthBreakdown As String
        Get
            If Not _hasVaultHealthAnalysis Then
                Return "Analysis pending"
            End If

            If VaultHealthFindings.Count = 0 Then
                Return "Analysis complete: no findings."
            End If

            Dim groups = VaultHealthFindings.
                GroupBy(Function(finding) finding.FindingType).
                OrderBy(Function(group) group.Key).
                Select(Function(group) $"{group.Count():N0} {group.Key}")

            Return String.Join("  |  ", groups)
        End Get
    End Property

    Public ReadOnly Property HealthFindingCountText As String
        Get
            Return VaultHealthFindings.Count.ToString("N0")
        End Get
    End Property

    Public ReadOnly Property HealthRepairableCountText As String
        Get
            Return RepairCandidates.Where(Function(candidate) candidate.CanRepairAutomatically).Count().ToString("N0")
        End Get
    End Property

    Public ReadOnly Property HealthReviewOnlyCountText As String
        Get
            Return RepairCandidates.Where(Function(candidate) Not candidate.CanRepairAutomatically).Count().ToString("N0")
        End Get
    End Property

    Public ReadOnly Property HealthSelectedCountText As String
        Get
            Return RepairCandidates.Where(Function(candidate) candidate.IsSelected).Count().ToString("N0")
        End Get
    End Property

    Public ReadOnly Property HealthDeferredHashCountText As String
        Get
            Return VaultHealthFindings.
                Where(Function(finding) String.Equals(finding.FindingType, "Hash verification deferred", StringComparison.OrdinalIgnoreCase)).
                Count().
                ToString("N0")
        End Get
    End Property

    Public ReadOnly Property HealthRiskBreakdownRows As IEnumerable(Of HealthBreakdownRow)
        Get
            Return BuildBreakdownRows(VaultHealthFindings.GroupBy(Function(finding) If(finding.RiskLevel, "Unknown")), BlueSlatePalette.Danger)
        End Get
    End Property

    Public ReadOnly Property HealthFindingBreakdownRows As IEnumerable(Of HealthBreakdownRow)
        Get
            Return BuildBreakdownRows(VaultHealthFindings.GroupBy(Function(finding) If(finding.FindingType, "Unknown")), BlueSlatePalette.Action)
        End Get
    End Property

    Public ReadOnly Property HealthRepairActionBreakdownRows As IEnumerable(Of HealthBreakdownRow)
        Get
            Return BuildBreakdownRows(RepairCandidates.GroupBy(Function(candidate) If(candidate.ActionType, "Unknown")), BlueSlatePalette.Success)
        End Get
    End Property

    Public ReadOnly Property SelectedRepairSummary As String
        Get
            Dim selected = RepairCandidates.Where(Function(candidate) candidate.IsSelected).ToList()
            If selected.Count = 0 Then
                Return "No repair candidates selected"
            End If

            Dim repairable = selected.Where(Function(candidate) candidate.CanRepairAutomatically).Count()
            Dim reviewOnly = selected.Count - repairable
            Dim expensive = selected.Where(Function(candidate) candidate.IsExpensiveAutomatic).Count()
            Return $"{repairable:N0} selected automatic repair(s), {expensive:N0} expensive hash repair(s); {reviewOnly:N0} review-only row(s) will be skipped"
        End Get
    End Property

    Public ReadOnly Property RepairCandidateFilterOptions As IEnumerable(Of String)
        Get
            Return {"All", "Automatic", "Expensive automatic", "Review-only", "Selected", "Unselected"}
        End Get
    End Property

    Public ReadOnly Property FindingTypeFilterOptions As IEnumerable(Of String)
        Get
            Dim options As New List(Of String) From {"All"}
            options.AddRange(VaultHealthFindings.
                Select(Function(finding) finding.FindingType).
                Where(Function(value) Not String.IsNullOrWhiteSpace(value)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                OrderBy(Function(value) value))
            Return options
        End Get
    End Property

    Public ReadOnly Property RepairHistorySummary As String
        Get
            If RepairHistory.Count = 0 Then
                Return "No repair history recorded"
            End If

            Return $"{RepairHistory.Count:N0} recent repair log entr{If(RepairHistory.Count = 1, "y", "ies")}"
        End Get
    End Property

    Public ReadOnly Property CatalogPath As String
        Get
            Return _catalogService.CatalogPath
        End Get
    End Property

    Public ReadOnly Property VaultRootPath As String
        Get
            If _catalog Is Nothing OrElse String.IsNullOrWhiteSpace(_catalog.VaultRootPath) Then
                Return _catalogService.DefaultVaultRootPath
            End If

            Return _catalog.VaultRootPath
        End Get
    End Property

    Public ReadOnly Property CategoryNames As IEnumerable(Of String)
        Get
            Return Categories.Select(Function(category) category.Name)
        End Get
    End Property

    Public ReadOnly Property TrustClassificationOptions As IEnumerable(Of String)
        Get
            Return {"Unknown", "Trusted", "Unverified", "Questionable"}
        End Get
    End Property

    Public ReadOnly Property RetentionPriorityOptions As IEnumerable(Of String)
        Get
            Return {"Normal", "High", "Cold archive", "Review later"}
        End Get
    End Property

    Public ReadOnly Property ArchiveStatusOptions As IEnumerable(Of String)
        Get
            Return {"Active", "Archived", "Quarantined", "Needs review"}
        End Get
    End Property

    Public ReadOnly Property StoredFileStatus As String
        Get
            If SelectedArtifact Is Nothing Then
                Return "No artifact selected"
            End If

            If StoredFileExists() Then
                Return "Stored file present"
            End If

            Return "Stored file missing"
        End Get
    End Property

    Public ReadOnly Property SelectedBlake3Display As String
        Get
            If SelectedArtifact Is Nothing OrElse String.IsNullOrWhiteSpace(SelectedArtifact.Blake3) Then
                Return "(not computed yet)"
            End If

            Return SelectedArtifact.Blake3
        End Get
    End Property

    Public ReadOnly Property SelectedSha256Display As String
        Get
            If SelectedArtifact Is Nothing OrElse String.IsNullOrWhiteSpace(SelectedArtifact.Sha256) Then
                Return "(not computed yet)"
            End If

            Return SelectedArtifact.Sha256
        End Get
    End Property

    Private Shared Function BuildHashDisplayRows(artifact As ArtifactModel, activeHashes As String) As IEnumerable(Of HashDisplayModel)
        Dim activeIds = New HashSet(Of String)(HashRegistry.ParseActiveHashIds(HashRegistry.NormalizeActiveHashes(activeHashes)), StringComparer.OrdinalIgnoreCase)
        Dim standardIds = New HashSet(Of String)(StandardHashDisplayIds, StringComparer.OrdinalIgnoreCase)

        Return HashRegistry.Options.
            Where(Function(optionItem)
                      Dim value = HashRegistry.GetArtifactHashValue(artifact, optionItem.Id)
                      Return standardIds.Contains(optionItem.Id) OrElse activeIds.Contains(optionItem.Id) OrElse Not String.IsNullOrWhiteSpace(value)
                  End Function).
            Select(Function(optionItem)
                                               Dim value = HashRegistry.GetArtifactHashValue(artifact, optionItem.Id)
                                               Dim isActive = activeIds.Contains(optionItem.Id)
                                               Dim hasValue = Not String.IsNullOrWhiteSpace(value)

                                               Return New HashDisplayModel With {
                                                   .Id = optionItem.Id,
                                                   .DisplayName = optionItem.DisplayName,
                                                   .Value = If(hasValue, value, "(not computed)"),
                                                   .Status = If(isActive, If(hasValue, "Active / computed", "Active / missing"), If(hasValue, "Inactive / retained", "Inactive / not computed")),
                                                   .IsActive = isActive,
                                                   .AccentBrush = If(isActive, If(hasValue, BlueSlatePalette.Success, BlueSlatePalette.Warning), BlueSlatePalette.TextMuted),
                                                   .AccentBackground = If(isActive, If(hasValue, BlueSlatePalette.SuccessDim, BlueSlatePalette.BuildDim), BlueSlatePalette.PanelDim)
                                               }
                                           End Function).ToList()
    End Function

    Public Property IsPreviewTabSelected As Boolean
        Get
            Return String.Equals(RightPanelTab, "Preview", StringComparison.OrdinalIgnoreCase)
        End Get
        Set(value As Boolean)
            If value Then
                RightPanelTab = "Preview"
            End If
        End Set
    End Property

    Public Property IsDetailsTabSelected As Boolean
        Get
            Return String.Equals(RightPanelTab, "Details", StringComparison.OrdinalIgnoreCase)
        End Get
        Set(value As Boolean)
            If value Then
                RightPanelTab = "Details"
            End If
        End Set
    End Property

    Public Property IsRelationsTabSelected As Boolean
        Get
            Return String.Equals(RightPanelTab, "Relations", StringComparison.OrdinalIgnoreCase)
        End Get
        Set(value As Boolean)
            If value Then
                RightPanelTab = "Relations"
            End If
        End Set
    End Property

    Public ReadOnly Property IsImagePreview As Boolean
        Get
            Return SelectedPreview IsNot Nothing AndAlso SelectedPreview.Kind = ArtifactPreviewKind.Image
        End Get
    End Property

    Public ReadOnly Property IsTextPreview As Boolean
        Get
            Return SelectedPreview IsNot Nothing AndAlso SelectedPreview.Kind = ArtifactPreviewKind.Text
        End Get
    End Property

    Public ReadOnly Property IsGenericPreview As Boolean
        Get
            Return SelectedPreview IsNot Nothing AndAlso SelectedPreview.Kind = ArtifactPreviewKind.GenericFile
        End Get
    End Property

    Public ReadOnly Property IsMissingPreview As Boolean
        Get
            Return SelectedPreview IsNot Nothing AndAlso SelectedPreview.Kind = ArtifactPreviewKind.Missing
        End Get
    End Property

    Private Shared Function NormalizeRightPanelTab(value As String) As String
        Select Case If(value, "").Trim().ToLowerInvariant()
            Case "details"
                Return "Details"
            Case Else
                Return "Preview"
        End Select
    End Function

    Private Sub LoadCatalog()
        _isLoadingCatalog = True
        _catalog = _catalogService.LoadOrCreate()
        _catalog.ActiveHashes = HashRegistry.NormalizeActiveHashes(_catalog.ActiveHashes)
        If String.Equals(_catalog.DefaultIngestMode, "Copy", StringComparison.OrdinalIgnoreCase) Then
            _ingestMode = IngestMode.Copy
        Else
            _ingestMode = IngestMode.Move
        End If

        _artifactRowHeight = If(String.Equals(_catalog.TableDensity, "Compact", StringComparison.OrdinalIgnoreCase), 28, 34)
        _columnPresetIndex = ParseColumnPreset(_catalog.ColumnPreset)
        _activeScope = NormalizeScope(_catalog.ActiveScope)
        _searchText = If(_catalog.SearchText, "")
        _tagSearchText = If(_catalog.TagSearchText, "")
        _selectedTag = If(_catalog.SelectedTag, "")

        ReplaceCollection(Vaults, _catalog.Vaults)
        ReplaceCollection(Stats, _catalog.Stats)
        ReplaceCollection(Categories, _catalog.Categories)
        ReplaceCollection(Tags, _catalog.Tags)
        ReplaceCollection(Activities, _catalog.Activities)
        HydrateArtifacts(_catalog.Artifacts)
        ReplaceCollection(Artifacts, _catalog.Artifacts)
        RebuildDerivedLists()
        _selectedCategory = Categories.FirstOrDefault(Function(category) String.Equals(category.Name, _catalog.SelectedCategory, StringComparison.OrdinalIgnoreCase))
        FilteredTags = CollectionViewSource.GetDefaultView(Tags)
        FilteredTags.Filter = AddressOf FilterTag
        FilteredArtifacts = CollectionViewSource.GetDefaultView(Artifacts)
        FilteredArtifacts.Filter = AddressOf FilterArtifact

        CurrentVault = Vaults.FirstOrDefault(Function(v) v.Id = _catalog.CurrentVaultId)

        If CurrentVault Is Nothing Then
            CurrentVault = Vaults.FirstOrDefault()
        End If

        RefreshRepairHistory()

        SelectFirstFilteredArtifact()
        _settingsText = "Open settings for vault paths, backups, and repair status"
        _isLoadingCatalog = False
        SaveUiPreferences()
        RefreshDerivedUiState()
        OnPropertyChanged(NameOf(SearchText))
        OnPropertyChanged(NameOf(TagSearchText))
        OnPropertyChanged(NameOf(SelectedTag))
        OnPropertyChanged(NameOf(SelectedCategory))
        OnPropertyChanged(NameOf(ActiveScope))
        OnPropertyChanged(NameOf(FilterTitle))
        OnPropertyChanged(NameOf(DensityText))
        OnPropertyChanged(NameOf(ColumnPresetText))
        OnPropertyChanged(NameOf(ShowCategoryColumn))
        OnPropertyChanged(NameOf(ShowDateColumn))
        OnPropertyChanged(NameOf(ShowTagsColumn))
    End Sub

    Public Async Function IngestPathsAsync(paths As IEnumerable(Of String), Optional modeOverride As IngestMode? = Nothing) As Task
        If IsIngesting Then
            IngestStatus = "Ingestion already running"
            Return
        End If

        IsIngesting = True
        IngestProgress = 0
        IngestStatus = "Starting ingest"
        IngestDetail = ""

        Dim progress = New Progress(Of IngestionProgress)(Sub(update)
                                                              IngestStatus = update.Summary
                                                              IngestDetail = If(String.IsNullOrWhiteSpace(update.CurrentFile), update.CurrentStage, $"{update.CurrentStage}: {update.CurrentFile}")
                                                              IngestProgress = update.Percent
                                                          End Sub)

        Dim ingested As List(Of ArtifactModel)
        Dim requestedMode = If(modeOverride.HasValue, modeOverride.Value, CurrentIngestMode)

        Try
            ingested = Await Task.Run(Function() _ingestionService.Ingest(paths, VaultRootPath, progress, requestedMode, _catalog.ActiveHashes))
        Finally
            IsIngesting = False
        End Try

        If ingested.Count = 0 Then
            IngestStatus = "No files were ingested"
            IngestDetail = ""
            Return
        End If

        RunOnUiThread(Sub() ApplyIngestedArtifacts(ingested, requestedMode))
    End Function

    Private Sub ApplyIngestedArtifacts(ingested As List(Of ArtifactModel), mode As IngestMode)
        Dim captureRecord = IngestionService.CreateCaptureRecord(ingested, mode)
        If captureRecord IsNot Nothing Then
            _catalog.CaptureRecords.Insert(0, captureRecord)
        End If

        For Each artifact In ingested
            Artifacts.Insert(0, artifact)
            _catalog.Artifacts.Insert(0, artifact)
        Next

        Dim activity = New ActivityEntryModel With {
            .ActionText = $"Ingested {ingested.Count:N0} file(s)",
            .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  {IngestModeActionText(mode)} into vault",
            .Icon = ""
        }

        Activities.Insert(0, activity)
        _catalog.Activities.Insert(0, activity)

        RebuildDerivedLists()
        PersistDerivedCatalogLists()
        _catalogService.Save(_catalog)
        RefreshFilters()
        SelectedArtifact = ingested.First()
        IngestStatus = $"Ingested {ingested.Count:N0} file(s) into {VaultRootPath}"
        IngestDetail = $"{IngestModeLabel(mode)}; catalog updated"
        IngestProgress = 100
        RefreshDerivedUiState()
    End Sub

    Private Shared Function IngestModeLabel(mode As IngestMode) As String
        If mode = IngestMode.Move Then
            Return "Move into vault"
        End If

        Return "Copy into vault"
    End Function

    Private Shared Function IngestModeActionText(mode As IngestMode) As String
        If mode = IngestMode.Move Then
            Return "moved"
        End If

        Return "copied"
    End Function

    Private Sub LoadEditorFromSelected()
        _isLoadingEditor = True

        If SelectedArtifact Is Nothing Then
            EditName = ""
            EditCategory = ""
            EditTagsText = ""
            EditRating = 0
            EditNotes = ""
            EditRetentionReason = ""
            EditWhyThisMatters = ""
            EditSourceProvenance = ""
            EditAcquisitionMethod = ""
            EditTrustClassification = "Unknown"
            EditRetentionPriority = "Normal"
            EditArchiveStatus = "Active"
            EditStatus = "No artifact selected"
            _isLoadingEditor = False
            Return
        End If

        EditName = SelectedArtifact.Name
        EditCategory = SelectedArtifact.Category
        EditTagsText = SelectedArtifact.TagsText
        EditRating = SelectedArtifact.Rating
        EditNotes = SelectedArtifact.Notes
        EditRetentionReason = SelectedArtifact.RetentionReason
        EditWhyThisMatters = SelectedArtifact.WhyThisMatters
        EditSourceProvenance = SelectedArtifact.SourceProvenance
        EditAcquisitionMethod = SelectedArtifact.AcquisitionMethod
        EditTrustClassification = SelectedArtifact.TrustClassification
        EditRetentionPriority = SelectedArtifact.RetentionPriority
        EditArchiveStatus = SelectedArtifact.ArchiveStatus
        EditStatus = "No pending edits"
        _isLoadingEditor = False
    End Sub

    Private Sub LoadPreviewForSelected()
        LoadPreviewForSelectedAsync()
    End Sub

    Private Async Sub LoadPreviewForSelectedAsync()
        Dim artifact = SelectedArtifact
        Dim version = Interlocked.Increment(_previewLoadVersion)

        If artifact Is Nothing Then
            SelectedPreview = New ArtifactPreview With {.Kind = ArtifactPreviewKind.GenericFile, .Message = "No preview"}
            Return
        End If

        Try
            Dim preview = Await Task.Run(Function() _previewService.LoadPreview(artifact))
            If version = _previewLoadVersion AndAlso ReferenceEquals(SelectedArtifact, artifact) Then
                SelectedPreview = preview
            End If
        Catch ex As Exception
            If version = _previewLoadVersion AndAlso ReferenceEquals(SelectedArtifact, artifact) Then
                SelectedPreview = New ArtifactPreview With {
                    .Kind = ArtifactPreviewKind.GenericFile,
                    .Message = "Preview unavailable",
                    .Title = "Preview Unavailable",
                    .Detail = ex.Message
                }
            End If
        End Try
    End Sub

    Private Sub SaveArtifactEdits()
        If SelectedArtifact Is Nothing Then
            Return
        End If

        Dim validatedName = If(EditName, "").Trim()
        If String.IsNullOrWhiteSpace(validatedName) Then
            EditStatus = "Name is required"
            Return
        End If

        Dim validatedCategory = If(EditCategory, "").Trim()
        If String.IsNullOrWhiteSpace(validatedCategory) Then
            validatedCategory = "Other"
        End If

        If validatedCategory.Length > 80 Then
            EditStatus = "Category is too long"
            Return
        End If

        SelectedArtifact.Name = validatedName
        SelectedArtifact.Category = validatedCategory
        Dim parsedTags = ParseTags(EditTagsText)
        If parsedTags.Count > 32 Then
            EditStatus = "Use 32 tags or fewer"
            Return
        End If

        If parsedTags.Any(Function(tag) tag.Length > 48) Then
            EditStatus = "Tags must be 48 characters or fewer"
            Return
        End If

        SelectedArtifact.Tags = parsedTags
        SelectedArtifact.Rating = EditRating
        SelectedArtifact.Notes = If(EditNotes, "").Trim()
        SelectedArtifact.RetentionReason = If(EditRetentionReason, "").Trim()
        SelectedArtifact.WhyThisMatters = If(EditWhyThisMatters, "").Trim()
        SelectedArtifact.SourceProvenance = If(EditSourceProvenance, "").Trim()
        SelectedArtifact.AcquisitionMethod = If(EditAcquisitionMethod, "").Trim()
        SelectedArtifact.TrustClassification = NormalizeChoice(EditTrustClassification, TrustClassificationOptions, "Unknown")
        SelectedArtifact.RetentionPriority = NormalizeChoice(EditRetentionPriority, RetentionPriorityOptions, "Normal")
        SelectedArtifact.ArchiveStatus = NormalizeChoice(EditArchiveStatus, ArchiveStatusOptions, "Active")

        RebuildDerivedLists()
        PersistDerivedCatalogLists()
        _catalog.Artifacts = Artifacts.ToList()
        _catalogService.Save(_catalog)
        RefreshFilters(preserveSelection:=True)
        EditStatus = $"Saved {SelectedArtifact.Name} at {DateTime.Now:HH:mm:ss}"
        OnPropertyChanged(NameOf(CategoryNames))
        RefreshDerivedUiState()
    End Sub

    Private Sub OpenSelectedLocation()
        If SelectedArtifact Is Nothing Then
            Return
        End If

        If Not StoredFileExists() Then
            ActionStatus = "Stored file missing"
            OnPropertyChanged(NameOf(StoredFileStatus))
            LoadPreviewForSelected()
            Return
        End If

        Process.Start(New ProcessStartInfo With {
            .FileName = "explorer.exe",
            .Arguments = $"/select,""{SelectedArtifact.Path}""",
            .UseShellExecute = True
        })
        ActionStatus = "Opened location"
    End Sub

    Private Sub OpenSelectedFile()
        If SelectedArtifact Is Nothing Then
            Return
        End If

        If Not StoredFileExists() Then
            ActionStatus = "Stored file missing"
            OnPropertyChanged(NameOf(StoredFileStatus))
            LoadPreviewForSelected()
            Return
        End If

        Process.Start(New ProcessStartInfo With {
            .FileName = SelectedArtifact.Path,
            .UseShellExecute = True
        })
        ActionStatus = "Opened file"
    End Sub

    Public Function GetSelectedArtifactName() As String
        If SelectedArtifact Is Nothing Then
            Return ""
        End If

        Return SelectedArtifact.Name
    End Function

    Private Sub RestoreSelectedArtifact(destinationFolder As String)
        If SelectedArtifact Is Nothing Then
            Return
        End If

        If Not StoredFileExists() Then
            ActionStatus = "Stored file missing"
            OnPropertyChanged(NameOf(StoredFileStatus))
            LoadPreviewForSelected()
            Return
        End If

        If String.IsNullOrWhiteSpace(destinationFolder) Then
            ActionStatus = "Choose a restore folder first"
            Return
        End If

        Try
            Directory.CreateDirectory(destinationFolder)
            Dim destination = BuildUniquePath(destinationFolder, SelectedArtifact.Name)
            File.Copy(SelectedArtifact.Path, destination, overwrite:=False)

            Dim activity = New ActivityEntryModel With {
                .ActionText = $"Restored {SelectedArtifact.Name}",
                .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  {destination}",
                .Icon = ""
            }
            Activities.Insert(0, activity)
            _catalog.Activities.Insert(0, activity)
            _catalogService.Save(_catalog)
            ActionStatus = $"Restored copy to {destination}"
        Catch ex As Exception
            ActionStatus = $"Restore failed: {ex.Message}"
        End Try
    End Sub

    Private Sub PermanentlyDeleteSelectedArtifact()
        If SelectedArtifact Is Nothing Then
            Return
        End If

        Dim artifact = SelectedArtifact

        Try
            If Not String.IsNullOrWhiteSpace(artifact.Path) AndAlso File.Exists(artifact.Path) Then
                File.Delete(artifact.Path)
            End If

            Artifacts.Remove(artifact)
            _catalog.Artifacts = Artifacts.ToList()
            Dim activity = New ActivityEntryModel With {
                .ActionText = $"Deleted {artifact.Name}",
                .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  removed from vault",
                .Icon = "",
                .IconBrush = BlueSlatePalette.Danger,
                .IconBackground = BlueSlatePalette.DangerDim
            }
            Activities.Insert(0, activity)
            _catalog.Activities.Insert(0, activity)
            RebuildDerivedLists()
            PersistDerivedCatalogLists()
            _catalogService.Save(_catalog)
            RefreshFilters()
            RefreshDerivedUiState()
            ActionStatus = $"Deleted {artifact.Name}"
        Catch ex As Exception
            ActionStatus = $"Delete failed: {ex.Message}"
        End Try
    End Sub

    Private Async Function CheckSelectedHashAsync() As Task
        If SelectedArtifact Is Nothing Then
            Return
        End If

        If IsVaultMaintenanceRunning Then
            Return
        End If

        If Not StoredFileExists() Then
            ActionStatus = "Stored file missing"
            OnPropertyChanged(NameOf(StoredFileStatus))
            LoadPreviewForSelected()
            Return
        End If

        IsVaultMaintenanceRunning = True
        VaultMaintenanceStatus = "Verifying selected artifact"
        VaultMaintenanceDetail = SelectedArtifact.Name
        ActionStatus = "Checking retained file hashes..."

        Try
            Dim artifact = SelectedArtifact
            Dim path = artifact.Path
            Dim activeHashes = HashRegistry.NormalizeActiveHashes(_catalog.ActiveHashes)
            Dim activeHashIds = HashRegistry.ParseActiveHashIds(activeHashes)
            Dim existingValues = activeHashIds.ToDictionary(Function(hashId) hashId, Function(hashId) HashRegistry.GetArtifactHashValue(artifact, hashId), StringComparer.OrdinalIgnoreCase)
            Dim hashes = Await Task.Run(Function() _hashService.ComputeHashes(path, activeHashes))
            If Not ReferenceEquals(SelectedArtifact, artifact) Then
                VaultMaintenanceStatus = "Hash check complete"
                VaultMaintenanceDetail = "Selection changed before results were applied"
                ActionStatus = "Hash check complete; selection changed"
                Return
            End If

            Dim mismatches = activeHashIds.
                Where(Function(hashId) Not String.IsNullOrWhiteSpace(existingValues(hashId)) AndAlso Not String.Equals(existingValues(hashId), HashRegistry.GetHashValue(hashes, hashId), StringComparison.OrdinalIgnoreCase)).
                ToList()
            For Each hashId In activeHashIds
                If String.IsNullOrWhiteSpace(existingValues(hashId)) Then
                    HashRegistry.SetArtifactHashValue(artifact, hashId, HashRegistry.GetHashValue(hashes, hashId))
                End If
            Next

            If mismatches.Count = 0 Then
                artifact.HashStatus = "Verified"
                ActionStatus = "Hash verified"
            Else
                artifact.HashStatus = $"{String.Join(", ", mismatches)} mismatch"
                ActionStatus = artifact.HashStatus
            End If

            _catalog.Artifacts = Artifacts.ToList()
            _catalogService.Save(_catalog)
            VaultMaintenanceStatus = "Hash check complete"
            VaultMaintenanceDetail = ActionStatus
            OnPropertyChanged(NameOf(SelectedBlake3Display))
            OnPropertyChanged(NameOf(SelectedSha256Display))
            OnPropertyChanged(NameOf(SelectedHashDisplays))
            OnPropertyChanged(NameOf(StoredFileStatus))
        Catch ex As Exception
            VaultMaintenanceStatus = "Hash check failed"
            VaultMaintenanceDetail = ex.Message
            ActionStatus = $"Hash check failed: {ex.Message}"
        Finally
            IsVaultMaintenanceRunning = False
        End Try
    End Function

    Private Sub ToggleSelectedStar()
        If SelectedArtifact Is Nothing Then
            Return
        End If

        SelectedArtifact.IsStarred = Not SelectedArtifact.IsStarred
        _catalog.Artifacts = Artifacts.ToList()
        _catalogService.Save(_catalog)
        ActionStatus = If(SelectedArtifact.IsStarred, "Marked starred", "Removed star")
        RefreshDerivedUiState()
    End Sub

    Private Sub FocusTagEditing()
        If SelectedArtifact Is Nothing Then
            Return
        End If

        If String.IsNullOrWhiteSpace(EditTagsText) Then
            EditTagsText = "review"
        ElseIf Not EditTagsText.Split({","c, ";"c}, StringSplitOptions.RemoveEmptyEntries).Any(Function(t) String.Equals(t.Trim(), "review", StringComparison.OrdinalIgnoreCase)) Then
            EditTagsText &= ", review"
        End If

        EditStatus = "Added review tag; save to persist"
    End Sub

    Private Sub ToggleIngestMode()
        CurrentIngestMode = If(CurrentIngestMode = IngestMode.Move, IngestMode.Copy, IngestMode.Move)
        IngestStatus = $"{IngestModeText} selected"
        IngestDetail = IngestModeDetail
    End Sub

    Private Sub QuarantineSelectedArtifact()
        If SelectedArtifact Is Nothing Then
            Return
        End If

        If Not StoredFileExists() Then
            ActionStatus = "Stored file missing"
            Return
        End If

        Dim artifact = SelectedArtifact
        Dim quarantineRoot = Path.Combine(VaultRootPath, "quarantine", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"))
        Directory.CreateDirectory(quarantineRoot)
        Dim destination = BuildUniquePath(quarantineRoot, Path.GetFileName(artifact.Path))

        Try
            File.Move(artifact.Path, destination)
            artifact.Path = destination
            artifact.RelativePath = Path.GetRelativePath(VaultRootPath, destination)
            artifact.Category = "Quarantine"
            artifact.Notes = $"{artifact.Notes}{vbCrLf}Quarantined at {DateTime.Now:yyyy-MM-dd HH:mm}".Trim()
            _catalog.Artifacts = Artifacts.ToList()
            _catalogService.Save(_catalog)
            RebuildDerivedLists()
            RefreshFilters(preserveSelection:=True)
            ActionStatus = "Moved to quarantine"
            RefreshDerivedUiState()
        Catch ex As Exception
            ActionStatus = $"Quarantine failed: {ex.Message}"
        End Try
    End Sub

    Private Sub ShowSettings()
        IsSettingsVisible = True
        ActionStatus = "Settings opened"
    End Sub

    Private Sub CloseSettings()
        IsSettingsVisible = False
        ActionStatus = "Settings closed"
    End Sub

    Private Sub ShowAbout()
        Dim version = GetType(MainViewModel).Assembly.GetName().Version
        _settingsText = $"Filing Cabinet {version}{vbCrLf}Personal vault and artifact manager{vbCrLf}{vbCrLf}Local-first, deterministic, inspectable, repairable, and operator-focused."
        OnPropertyChanged(NameOf(SettingsText))
        ActionStatus = "About Filing Cabinet"
    End Sub

    Private Sub OpenDocumentationPath(relativePath As String)
        Dim resolvedPath = ResolveDocumentationPath(relativePath)

        If String.IsNullOrWhiteSpace(resolvedPath) Then
            ActionStatus = $"Help document missing: {relativePath}"
            Return
        End If

        Try
            Process.Start(New ProcessStartInfo With {
                .FileName = resolvedPath,
                .UseShellExecute = True
            })
            ActionStatus = $"Opened help: {Path.GetFileName(resolvedPath)}"
        Catch ex As Exception
            ActionStatus = $"Help open failed: {ex.Message}"
        End Try
    End Sub

    Private Sub OpenDocsFolder()
        Dim docsPath = ResolveDocumentationPath("docs")

        If String.IsNullOrWhiteSpace(docsPath) Then
            ActionStatus = "Docs folder missing"
            Return
        End If

        Try
            Process.Start(New ProcessStartInfo With {
                .FileName = docsPath,
                .UseShellExecute = True
            })
            ActionStatus = "Opened docs folder"
        Catch ex As Exception
            ActionStatus = $"Docs folder open failed: {ex.Message}"
        End Try
    End Sub

    Public Shared Function ResolveDocumentationPath(relativePath As String) As String
        If String.IsNullOrWhiteSpace(relativePath) Then
            Return ""
        End If

        Dim normalizedRelativePath = relativePath.Replace("/"c, Path.DirectorySeparatorChar)
        Dim candidates As New List(Of String) From {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        }

        Dim current = New DirectoryInfo(Directory.GetCurrentDirectory())
        While current IsNot Nothing
            candidates.Add(current.FullName)
            current = current.Parent
        End While

        For Each root In candidates.Where(Function(candidate) Not String.IsNullOrWhiteSpace(candidate)).Distinct(StringComparer.OrdinalIgnoreCase)
            Dim candidatePath = Path.Combine(root, normalizedRelativePath)
            If File.Exists(candidatePath) OrElse Directory.Exists(candidatePath) Then
                Return candidatePath
            End If
        Next

        Return ""
    End Function

    Private Sub BackupCatalog()
        Try
            Dim exportsRoot = Path.Combine(VaultRootPath, "exports")
            Dim validation = _catalogService.ExportSnapshotWithValidation(_catalog, exportsRoot)
            If validation.IsValid Then
                ActionStatus = $"Catalog backup created and validated: {validation.BackupPath}"
            Else
                ActionStatus = $"Catalog backup validation failed: {validation.Detail}"
            End If
            OnPropertyChanged(NameOf(LastBackupDisplay))
            ShowSettings()
        Catch ex As Exception
            ActionStatus = $"Backup failed: {ex.Message}"
        End Try
    End Sub

    Private Async Function RepairCatalogAsync() As Task
        If IsVaultMaintenanceRunning Then
            Return
        End If

        IsVaultMaintenanceRunning = True
        VaultMaintenanceProgress = 0
        VaultMaintenanceStatus = "Analyzing vault health"
        VaultMaintenanceDetail = "Checking catalog paths, metadata, generated assets, and catalog hash coverage"
        ActionStatus = "Analyzing vault health..."
        IsVaultHealthVisible = True

        Try
            Dim artifactSnapshot = Artifacts.ToList()
            Dim vaultRoot = VaultRootPath
            Dim analyzeProgress As New Progress(Of VaultMaintenanceProgress)(Sub(progressUpdate)
                                                               VaultMaintenanceProgress = progressUpdate.Percent
                                                               VaultMaintenanceDetail = progressUpdate.ToString()
                                                           End Sub)
            Dim result = Await Task.Run(Function()
                                            Dim healthReport = BuildVaultHealthReport(artifactSnapshot, vaultRoot, New ThumbnailService(), New HashService(), analyzeProgress, _catalog.ActiveHashes)
                                            Dim repairReport = BuildRepairReport(artifactSnapshot, vaultRoot, healthReport, Nothing, _catalog.ActiveHashes)
                                            Return New VaultMaintenanceResult With {
                                                .HealthReport = healthReport,
                                                .RepairReport = repairReport
                                            }
                                        End Function)

            _repairStatus = BuildRepairStatus(result.RepairReport)
            PublishVaultHealthReport(result.HealthReport)
            OnPropertyChanged(NameOf(RepairStatus))
            VaultMaintenanceProgress = 100
            VaultMaintenanceStatus = "Analysis complete"
            VaultMaintenanceDetail = result.HealthReport.Summary
            ActionStatus = _repairStatus
        Catch ex As Exception
            VaultMaintenanceStatus = "Analysis failed"
            VaultMaintenanceDetail = ex.Message
            ActionStatus = $"Analyze failed: {ex.Message}"
        Finally
            IsVaultMaintenanceRunning = False
        End Try
    End Function

    Private Async Function VerifySmallHashesAsync() As Task
        If IsVaultMaintenanceRunning Then
            Return
        End If

        IsVaultMaintenanceRunning = True
        VaultMaintenanceProgress = 0
        VaultMaintenanceStatus = "Verifying small retained files"
        VaultMaintenanceDetail = $"Reading files up to {FormatSize(AutoHashVerificationLimitBytes)}"
        ActionStatus = "Verifying small retained file hashes..."
        IsVaultHealthVisible = True

        Try
            Dim artifactSnapshot = Artifacts.ToList()
            Dim vaultRoot = VaultRootPath
            Dim verifyProgress As New Progress(Of VaultMaintenanceProgress)(Sub(progressUpdate)
                                                               VaultMaintenanceProgress = progressUpdate.Percent
                                                               VaultMaintenanceDetail = progressUpdate.ToString()
                                                           End Sub)
            Dim healthReport = Await Task.Run(Function() BuildVaultHealthReport(artifactSnapshot, vaultRoot, New ThumbnailService(), New HashService(), verifyProgress, _catalog.ActiveHashes, verifyHashes:=True, hashVerificationLimitBytes:=AutoHashVerificationLimitBytes))

            PublishVaultHealthReport(healthReport)
            VaultMaintenanceProgress = 100
            VaultMaintenanceStatus = "Small hash verification complete"
            VaultMaintenanceDetail = healthReport.Summary
            ActionStatus = "Small hash verification complete"
        Catch ex As Exception
            VaultMaintenanceStatus = "Small hash verification failed"
            VaultMaintenanceDetail = ex.Message
            ActionStatus = $"Hash verification failed: {ex.Message}"
        Finally
            IsVaultMaintenanceRunning = False
        End Try
    End Function

    Private Async Function RescanVaultAsync() As Task
        If IsVaultMaintenanceRunning Then
            Return
        End If

        IsVaultMaintenanceRunning = True
        VaultMaintenanceProgress = 0
        VaultMaintenanceStatus = "Full rescan and orphan recovery"
        VaultMaintenanceDetail = "Preparing generated asset recovery and orphan adoption"
        ActionStatus = "Rescanning vault..."
        IsVaultHealthVisible = True

        Try
            Dim artifactSnapshot = Artifacts.ToList()
            Dim vaultRoot = VaultRootPath
            Dim rescanProgress As New Progress(Of VaultMaintenanceProgress)(Sub(progressUpdate)
                                                              VaultMaintenanceProgress = progressUpdate.Percent
                                                              VaultMaintenanceDetail = progressUpdate.ToString()
                                                          End Sub)
            Dim result = Await Task.Run(Function() BuildRescanResult(artifactSnapshot, vaultRoot, rescanProgress))

            ApplyThumbnailUpdates(result.ThumbnailUpdates)
            ApplyAdoptedArtifacts(result.AdoptedArtifacts)
            VaultMaintenanceDetail = "Finalizing health report"
            Dim refreshedSnapshot = Artifacts.ToList()
            Dim finalizeProgress As New Progress(Of VaultMaintenanceProgress)(Sub(progressUpdate)
                                                                VaultMaintenanceProgress = progressUpdate.Percent
                                                                VaultMaintenanceDetail = progressUpdate.ToString()
                                                            End Sub)
            result.HealthReport = Await Task.Run(Function() BuildVaultHealthReport(refreshedSnapshot, vaultRoot, New ThumbnailService(), New HashService(), finalizeProgress, _catalog.ActiveHashes))

            _repairStatus = BuildRepairStatus(result.RepairReport)
            RebuildDerivedLists()
            PersistDerivedCatalogLists()
            _catalog.Artifacts = Artifacts.ToList()
            _catalogService.Save(_catalog)
            RefreshFilters(preserveSelection:=True)
            RefreshDerivedUiState()
            PublishVaultHealthReport(result.HealthReport)
            OnPropertyChanged(NameOf(RepairStatus))
            VaultMaintenanceProgress = 100
            VaultMaintenanceStatus = "Rescan complete"
            VaultMaintenanceDetail = result.HealthReport.Summary
            ActionStatus = $"Rescan complete: {_repairStatus}"
        Catch ex As Exception
            VaultMaintenanceStatus = "Rescan failed"
            VaultMaintenanceDetail = ex.Message
            ActionStatus = $"Rescan failed: {ex.Message}"
        Finally
            IsVaultMaintenanceRunning = False
        End Try
    End Function

    Private Async Function RefreshVaultStateAsync() As Task
        CatalogService.EnsureVaultFolders(VaultRootPath)
        Await RepairCatalogAsync()
        RebuildDerivedLists()
        PersistDerivedCatalogLists()
        _catalog.Artifacts = Artifacts.ToList()
        _catalogService.Save(_catalog)
        RefreshFilters(preserveSelection:=True)
        RefreshDerivedUiState()
        IngestStatus = "Vault refreshed"
        IngestDetail = RepairStatus
    End Function

    Private Sub SetScope(scope As String)
        IsVaultHealthVisible = False
        ActiveScope = scope
        ActionStatus = $"Showing {ActiveScope}"
    End Sub

    Private Sub ApplySort(propertyName As String)
        If FilteredArtifacts Is Nothing Then
            Return
        End If

        FilteredArtifacts.SortDescriptions.Clear()
        Dim direction = If(propertyName = NameOf(ArtifactModel.DateModified), ListSortDirection.Descending, ListSortDirection.Ascending)
        FilteredArtifacts.SortDescriptions.Add(New SortDescription(propertyName, direction))
        FilteredArtifacts.Refresh()
        ActionStatus = $"Sorted by {propertyName}"
    End Sub

    Private Sub ToggleDensity()
        ArtifactRowHeight = If(ArtifactRowHeight <= 30, 34, 28)
        ActionStatus = If(ArtifactRowHeight <= 30, "Compact table rows", "Comfortable table rows")
    End Sub

    Private Sub CycleColumnPreset()
        _columnPresetIndex = (_columnPresetIndex + 1) Mod 3
        OnPropertyChanged(NameOf(ColumnPresetText))
        OnPropertyChanged(NameOf(ShowCategoryColumn))
        OnPropertyChanged(NameOf(ShowDateColumn))
        OnPropertyChanged(NameOf(ShowTagsColumn))
        SaveUiPreferences()
        ActionStatus = $"Table columns set to {ColumnPresetName}"
    End Sub

    Private ReadOnly Property ColumnPresetName As String
        Get
            Select Case _columnPresetIndex
                Case 1
                    Return "Compact"
                Case 2
                    Return "Minimal"
                Case Else
                    Return "Full"
            End Select
        End Get
    End Property

    Private Shared Function ParseColumnPreset(value As String) As Integer
        Select Case If(value, "").Trim().ToLowerInvariant()
            Case "compact"
                Return 1
            Case "minimal"
                Return 2
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function NormalizeScope(value As String) As String
        Select Case If(value, "").Trim().ToLowerInvariant()
            Case "recent"
                Return "Recent"
            Case "starred"
                Return "Starred"
            Case "quarantine"
                Return "Quarantine"
            Case "unverified"
                Return "Unverified"
            Case "missing preview"
                Return "Missing preview"
            Case "repair needed"
                Return "Repair needed"
            Case "duplicate candidates"
                Return "Duplicate candidates"
            Case "same source batch"
                Return "Same source batch"
            Case "large artifacts"
                Return "Large artifacts"
            Case Else
                Return "All"
        End Select
    End Function

    Private Shared Function DefaultHelpDocuments() As List(Of HelpDocumentModel)
        Return New List(Of HelpDocumentModel) From {
            New HelpDocumentModel With {.Title = "User Guide", .RelativePath = "README.md"},
            New HelpDocumentModel With {.Title = "Deliberate Retention Tradeoff", .RelativePath = "docs\Filing Cabinet — The Deliberate Retention Tradeoff.md"},
            New HelpDocumentModel With {.Title = "Trust and Verification Model", .RelativePath = "docs\Filing Cabinet — Trust and Verification Model.md"},
            New HelpDocumentModel With {.Title = "Hash Choices and Compatibility", .RelativePath = "docs\Filing Cabinet — Hash Choices and Compatibility.md"},
            New HelpDocumentModel With {.Title = "Why SHA-256 and BLAKE3", .RelativePath = "docs\Filing Cabinet — Why SHA-256 and BLAKE3.md"},
            New HelpDocumentModel With {.Title = "Repair and Recovery Guide", .RelativePath = "docs\Filing Cabinet — Repair and Recovery Guide.md"},
            New HelpDocumentModel With {.Title = "Why Determinism Matters", .RelativePath = "docs\Filing Cabinet — Why Determinism Matters.md"},
            New HelpDocumentModel With {.Title = "Why VB.NET and WPF", .RelativePath = "docs\Filing Cabinet — Why VB.NET and WPF.md"}
        }
    End Function

    Private Shared Function NormalizeChoice(value As String, allowedValues As IEnumerable(Of String), defaultValue As String) As String
        Dim normalized = If(value, "").Trim()
        Dim match = allowedValues.FirstOrDefault(Function(optionValue) String.Equals(optionValue, normalized, StringComparison.OrdinalIgnoreCase))

        If String.IsNullOrWhiteSpace(match) Then
            Return defaultValue
        End If

        Return match
    End Function

    Private Sub SaveUiPreferences()
        If _isLoadingCatalog OrElse _catalog Is Nothing Then
            Return
        End If

        _catalog.TableDensity = If(ArtifactRowHeight <= 30, "Compact", "Comfortable")
        _catalog.ColumnPreset = ColumnPresetName
        _catalog.ActiveScope = ActiveScope
        _catalog.SearchText = SearchText
        _catalog.TagSearchText = TagSearchText
        _catalog.SelectedTag = If(SelectedTag, "")
        _catalog.SelectedCategory = If(SelectedCategory?.Name, "")
        _catalogService.Save(_catalog)
    End Sub

    Private Function StoredFileExists() As Boolean
        Return SelectedArtifact IsNot Nothing AndAlso
            Not String.IsNullOrWhiteSpace(SelectedArtifact.Path) AndAlso
            File.Exists(SelectedArtifact.Path)
    End Function

    Public Sub SetVaultRoot(path As String)
        If String.IsNullOrWhiteSpace(path) Then
            Return
        End If

        CatalogService.EnsureVaultFolders(path)
        _catalog.VaultRootPath = path
        If CurrentVault IsNot Nothing Then
            CurrentVault.Path = path
            If String.IsNullOrWhiteSpace(CurrentVault.Name) Then
                CurrentVault.Name = "MainVault"
            End If
        Else
            Dim newVault = New VaultModel With {
                .Id = Guid.NewGuid().ToString("N"),
                .Name = "MainVault",
                .Path = path,
                .IsSelected = True
            }
            Vaults.Add(newVault)
            CurrentVault = newVault
        End If
        _catalog.CurrentVaultId = CurrentVault.Id
        _catalog.Vaults = Vaults.ToList()
        _catalogService.Save(_catalog)
        OnPropertyChanged(NameOf(VaultRootPath))
        OnPropertyChanged(NameOf(CurrentVaultTitle))
        OnPropertyChanged(NameOf(StorageTotalText))
        RefreshVaultStateInBackground()
        ActionStatus = $"Vault set to {path}"
    End Sub

    Private Sub RemoveCurrentVault()
        If CurrentVault Is Nothing Then
            Return
        End If

        If Vaults.Count <= 1 Then
            ActionStatus = "Keep at least one vault"
            Return
        End If

        Dim removed = CurrentVault
        Dim removedIndex = Vaults.IndexOf(removed)
        Vaults.Remove(removed)

        Dim nextIndex = Math.Min(Math.Max(removedIndex, 0), Vaults.Count - 1)
        CurrentVault = Vaults(nextIndex)
        _catalog.CurrentVaultId = CurrentVault.Id
        _catalog.VaultRootPath = CurrentVault.Path
        _catalog.Vaults = Vaults.ToList()
        _catalogService.Save(_catalog)

        OnPropertyChanged(NameOf(VaultRootPath))
        OnPropertyChanged(NameOf(CurrentVaultTitle))
        OnPropertyChanged(NameOf(StorageTotalText))
        RefreshVaultStateInBackground()
        ActionStatus = $"Removed vault {removed.DisplayName}"
    End Sub

    Private Async Sub RefreshVaultStateInBackground()
        Try
            Await RefreshVaultStateAsync()
        Catch ex As Exception
            ActionStatus = $"Vault refresh failed: {ex.Message}"
        End Try
    End Sub

    Private Shared Function ParseTags(tagsText As String) As List(Of String)
        If String.IsNullOrWhiteSpace(tagsText) Then
            Return New List(Of String)
        End If

        Return tagsText.
            Split({","c, ";"c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(tag) tag.Trim().ToLowerInvariant()).
            Where(Function(tag) tag.Length > 0).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Sub RebuildDerivedLists()
        InvalidateDiscoveryScopeCache()

        Dim rebuiltCategories = Artifacts.
            GroupBy(Function(a) a.Category).
            OrderBy(Function(g) g.Key).
            Select(Function(g) New CategoryModel With {.Name = g.Key, .Count = g.Count().ToString("N0")}).
            ToList()

        ReplaceCollection(Categories, rebuiltCategories)
        OnPropertyChanged(NameOf(CategoryNames))

        Dim rebuiltTags = Artifacts.
            SelectMany(Function(a) If(a.Tags, New List(Of String))).
            Where(Function(t) Not String.IsNullOrWhiteSpace(t)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(t) t).
            ToList()

        ReplaceCollection(Tags, rebuiltTags)
        RefreshTagFilter()
    End Sub

    Private Sub SetEditValue(Of T)(ByRef storage As T, value As T, propertyName As String)
        If EqualityComparer(Of T).Default.Equals(storage, value) Then
            Return
        End If

        storage = value
        OnPropertyChanged(propertyName)

        If Not _isLoadingEditor Then
            EditStatus = "Unsaved edits"
        End If
    End Sub

    Private Sub PersistDerivedCatalogLists()
        _catalog.Categories = Categories.ToList()
        _catalog.Tags = Tags.ToList()
    End Sub

    Private Function FilterArtifact(item As Object) As Boolean
        Dim artifact = TryCast(item, ArtifactModel)

        If artifact Is Nothing Then
            Return False
        End If

        If SelectedCategory IsNot Nothing AndAlso artifact.Category <> SelectedCategory.Name Then
            Return False
        End If

        If Not MatchesActiveScope(artifact) Then
            Return False
        End If

        If Not String.IsNullOrWhiteSpace(SelectedTag) Then
            If artifact.Tags Is Nothing OrElse Not artifact.Tags.Any(Function(t) String.Equals(t, SelectedTag, StringComparison.OrdinalIgnoreCase)) Then
                Return False
            End If
        End If

        If String.IsNullOrWhiteSpace(SearchText) Then
            Return True
        End If

        Dim needle = SearchText.Trim()
        Return ContainsText(artifact.Name, needle) OrElse
            ContainsText(artifact.Type, needle) OrElse
            ContainsText(artifact.TypeFamily, needle) OrElse
            ContainsText(artifact.Category, needle) OrElse
            ContainsText(artifact.Path, needle) OrElse
            ContainsText(artifact.OriginalPath, needle) OrElse
            ContainsText(artifact.Notes, needle) OrElse
            ContainsText(artifact.RetentionReason, needle) OrElse
            ContainsText(artifact.WhyThisMatters, needle) OrElse
            ContainsText(artifact.SourceProvenance, needle) OrElse
            ContainsText(artifact.AcquisitionMethod, needle) OrElse
            ContainsText(artifact.TrustClassification, needle) OrElse
            ContainsText(artifact.RetentionPriority, needle) OrElse
            ContainsText(artifact.ArchiveStatus, needle) OrElse
            ContainsText(artifact.TagsText, needle) OrElse
            ContainsAnyHash(artifact, needle) OrElse
            ContainsExtractedText(artifact, needle)
    End Function

    Private Shared Function ContainsText(value As String, needle As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(value) AndAlso
            value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Shared Function ContainsAnyHash(artifact As ArtifactModel, needle As String) As Boolean
        Return HashRegistry.Options.Any(Function(optionItem) ContainsText(HashRegistry.GetArtifactHashValue(artifact, optionItem.Id), needle))
    End Function

    Private Function ContainsExtractedText(artifact As ArtifactModel, needle As String) As Boolean
        If artifact Is Nothing OrElse _searchExtractedTextMatches Is Nothing OrElse
            Not String.Equals(_searchExtractedText, needle, StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If

        Return _searchExtractedTextMatches.Contains(ArtifactSearchKey(artifact))
    End Function

    Private Sub ScheduleSearchRefresh(Optional preserveSelection As Boolean = False)
        Dim needle = If(SearchText, "").Trim()
        Dim version = Interlocked.Increment(_searchVersion)

        If String.IsNullOrWhiteSpace(needle) Then
            _searchExtractedText = ""
            _searchExtractedTextMatches = Nothing
            RefreshFilters(preserveSelection:=preserveSelection)
            Return
        End If

        _searchExtractedText = needle
        _searchExtractedTextMatches = Nothing
        RefreshFilters(preserveSelection:=preserveSelection)

        Dim artifactSnapshot = Artifacts.ToList()
        Dim vaultRoot = VaultRootPath
        Task.Run(Function() BuildExtractedTextSearchMatches(artifactSnapshot, vaultRoot, needle)).
            ContinueWith(Sub(task)
                             RunOnUiThread(Sub()
                                               If version <> _searchVersion OrElse Not String.Equals(_searchExtractedText, needle, StringComparison.OrdinalIgnoreCase) Then
                                                   Return
                                               End If

                                               If task.IsFaulted Then
                                                   _searchExtractedTextMatches = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                                               Else
                                                   _searchExtractedTextMatches = task.Result
                                               End If

                                               RefreshFilters(preserveSelection:=True)
                                           End Sub)
                         End Sub)
    End Sub

    Private Shared Function BuildExtractedTextSearchMatches(artifacts As IEnumerable(Of ArtifactModel), vaultRootPath As String, needle As String) As HashSet(Of String)
        Dim matches As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        If String.IsNullOrWhiteSpace(needle) Then
            Return matches
        End If

        For Each artifact In If(artifacts, Enumerable.Empty(Of ArtifactModel)())
            If artifact Is Nothing OrElse String.IsNullOrWhiteSpace(artifact.ExtractedTextRelativePath) Then
                Continue For
            End If

            If ExtractedTextContains(artifact, vaultRootPath, needle) Then
                matches.Add(ArtifactSearchKey(artifact))
            End If
        Next

        Return matches
    End Function

    Private Shared Function ExtractedTextContains(artifact As ArtifactModel, vaultRootPath As String, needle As String) As Boolean
        Try
            Dim extractedPath = If(Path.IsPathRooted(artifact.ExtractedTextRelativePath), artifact.ExtractedTextRelativePath, Path.Combine(vaultRootPath, artifact.ExtractedTextRelativePath))
            If Not File.Exists(extractedPath) Then
                Return False
            End If

            Return File.ReadLines(extractedPath).
                Any(Function(line) line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function ArtifactSearchKey(artifact As ArtifactModel) As String
        If artifact Is Nothing Then
            Return ""
        End If

        If Not String.IsNullOrWhiteSpace(artifact.Id) Then
            Return artifact.Id
        End If

        Return If(artifact.Path, artifact.Name)
    End Function

    Private Function FilterTag(item As Object) As Boolean
        Dim tag = TryCast(item, String)
        If String.IsNullOrWhiteSpace(tag) Then
            Return False
        End If

        If String.IsNullOrWhiteSpace(TagSearchText) Then
            Return True
        End If

        Return tag.IndexOf(TagSearchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Sub RefreshTagFilter()
        If FilteredTags Is Nothing Then
            Return
        End If

        Dim previous = SelectedTag
        FilteredTags.Refresh()

        If Not String.IsNullOrWhiteSpace(previous) AndAlso Not FilteredTags.Cast(Of String)().Contains(previous, StringComparer.OrdinalIgnoreCase) Then
            SelectedTag = Nothing
        End If
    End Sub

    Private Sub RefreshFilters(Optional preserveSelection As Boolean = False)
        If FilteredArtifacts Is Nothing Then
            Return
        End If

        Dim previous = If(preserveSelection, SelectedArtifact, Nothing)
        _isRefreshingFilters = True

        Try
            FilteredArtifacts.Refresh()
            OnPropertyChanged(NameOf(FilterTitle))

            If previous IsNot Nothing AndAlso FilteredArtifacts.Cast(Of ArtifactModel)().Contains(previous) Then
                SelectedArtifact = previous
            Else
                SelectFirstFilteredArtifact()
            End If
        Finally
            _isRefreshingFilters = False
        End Try
    End Sub

    Private Sub SelectFirstFilteredArtifact()
        If FilteredArtifacts Is Nothing Then
            SelectedArtifact = Artifacts.FirstOrDefault()
            Return
        End If

        SelectedArtifact = FilteredArtifacts.Cast(Of ArtifactModel)().FirstOrDefault()
    End Sub

    Private Sub ClearFilters()
        IsVaultHealthVisible = False
        ActiveScope = "All"
        SelectedCategory = Nothing
        SelectedTag = Nothing
        SearchText = ""
        TagSearchText = ""
        RefreshFilters(preserveSelection:=True)
    End Sub

    Private Sub RefreshDerivedUiState()
        RefreshQuarantineCountAsync()
        OnPropertyChanged(NameOf(CurrentVaultSummary))
        OnPropertyChanged(NameOf(StorageUsedText))
        OnPropertyChanged(NameOf(StorageTotalText))
        OnPropertyChanged(NameOf(InboxCountText))
        OnPropertyChanged(NameOf(StarredCountText))
        OnPropertyChanged(NameOf(QuarantineCountText))
        OnPropertyChanged(NameOf(IngestModeText))
        OnPropertyChanged(NameOf(IngestModeDetail))
        OnPropertyChanged(NameOf(ActiveScopeText))
        OnPropertyChanged(NameOf(LastBackupDisplay))
        OnPropertyChanged(NameOf(RecallStatusText))
        OnPropertyChanged(NameOf(SelectedHashDisplays))
        RebuildStats()
        RebuildRelatedArtifacts()
    End Sub

    Private Async Sub RefreshQuarantineCountAsync()
        Dim version = Interlocked.Increment(_quarantineCountVersion)
        Dim vaultRoot = VaultRootPath

        Try
            Dim count = Await Task.Run(Function()
                                           Dim quarantineRoot = Path.Combine(vaultRoot, "quarantine")
                                           If Not Directory.Exists(quarantineRoot) Then
                                               Return 0
                                           End If

                                           Return Directory.EnumerateFiles(quarantineRoot, "*", SearchOption.AllDirectories).Count()
                                       End Function)

            If version = _quarantineCountVersion AndAlso _quarantineCount <> count Then
                _quarantineCount = count
                OnPropertyChanged(NameOf(QuarantineCountText))
                RebuildStats()
            End If
        Catch
        End Try
    End Sub

    Private Sub RebuildRelatedArtifacts()
        RelatedArtifacts.Clear()

        If SelectedArtifact Is Nothing Then
            OnPropertyChanged(NameOf(RelatedArtifactsSummary))
            OnPropertyChanged(NameOf(PreviewRelatedArtifacts))
            Return
        End If

        Dim related = Artifacts.
            Where(Function(a) Not ReferenceEquals(a, SelectedArtifact)).
            Select(Function(a) BuildArtifactRelation(SelectedArtifact, a, VaultRootPath)).
            Where(Function(relation) relation IsNot Nothing AndAlso relation.Score > 0).
            OrderByDescending(Function(relation) relation.Score).
            ThenBy(Function(relation) relation.Name).
            Take(10).
            ToList()

        For Each relation In related
            RelatedArtifacts.Add(relation)
        Next

        OnPropertyChanged(NameOf(RelatedArtifactsSummary))
        OnPropertyChanged(NameOf(PreviewRelatedArtifacts))
    End Sub

    Public Shared Function BuildArtifactRelation(selected As ArtifactModel, candidate As ArtifactModel, Optional vaultRootPath As String = "") As ArtifactRelationModel
        If selected Is Nothing OrElse candidate Is Nothing OrElse ReferenceEquals(selected, candidate) Then
            Return Nothing
        End If

        Dim score = 0
        Dim reasons As New List(Of String)

        If Not String.IsNullOrWhiteSpace(candidate.Sha256) AndAlso
            String.Equals(candidate.Sha256, selected.Sha256, StringComparison.OrdinalIgnoreCase) Then
            score += 12
            reasons.Add("duplicate SHA-256")
        End If

        Dim sharedTags = SharedTagNames(selected, candidate)
        If sharedTags.Count > 0 Then
            score += Math.Min(6, sharedTags.Count * 2)
            reasons.Add("shared tag: " & String.Join(", ", sharedTags.Take(3)))
        End If

        If SameText(candidate.Category, selected.Category) Then
            score += 4
            reasons.Add("same category")
        End If

        If SameText(candidate.TypeFamily, selected.TypeFamily) Then
            score += 3
            reasons.Add("same type family")
        End If

        If SameDirectory(candidate.OriginalPath, selected.OriginalPath) Then
            score += 3
            reasons.Add("same original folder")
        ElseIf SameDirectory(candidate.Path, selected.Path) Then
            score += 2
            reasons.Add("same vault folder")
        End If

        If SameDateBatch(candidate, selected) Then
            score += 2
            reasons.Add("same date batch")
        End If

        If SameIngestSession(candidate, selected) Then
            score += 3
            reasons.Add("same ingest session")
        End If

        If SameCaptureRecord(candidate, selected) Then
            score += 5
            reasons.Add("same capture record")
        End If

        Dim sharedExtension = SharedExtensionFamily(selected, candidate)
        If Not String.IsNullOrWhiteSpace(sharedExtension) Then
            score += 2
            reasons.Add("shared extension family: " & sharedExtension)
        End If

        Dim provenanceMatches = SharedProvenanceTokens(selected, candidate)
        If provenanceMatches.Count > 0 Then
            score += Math.Min(4, provenanceMatches.Count * 2)
            reasons.Add("shared provenance token: " & String.Join(", ", provenanceMatches.Take(3)))
        End If

        Dim releaseMatches = SharedReleaseMarkers(selected, candidate)
        If releaseMatches.Count > 0 Then
            score += Math.Min(4, releaseMatches.Count * 2)
            reasons.Add("shared release marker: " & String.Join(", ", releaseMatches.Take(3)))
        End If

        Dim sharedHashPrefix = SharedHashPrefixFamily(selected, candidate)
        If Not String.IsNullOrWhiteSpace(sharedHashPrefix) Then
            score += 2
            reasons.Add("shared hash prefix: " & sharedHashPrefix)
        End If

        Dim extractedKeywordMatches = SharedExtractedKeywords(selected, candidate, vaultRootPath)
        If extractedKeywordMatches.Count > 0 Then
            score += Math.Min(4, extractedKeywordMatches.Count * 2)
            reasons.Add("shared extracted keyword: " & String.Join(", ", extractedKeywordMatches.Take(3)))
        End If

        Dim manifestOriginMatches = SharedManifestOriginKeys(selected, candidate)
        If manifestOriginMatches.Count > 0 Then
            score += Math.Min(4, manifestOriginMatches.Count * 2)
            reasons.Add("shared project origin: " & String.Join(", ", manifestOriginMatches.Take(3)))
        End If

        Dim matchingNameTokens = SharedNameTokens(selected.Name, candidate.Name)
        If matchingNameTokens.Count > 0 Then
            score += Math.Min(4, matchingNameTokens.Count)
            reasons.Add("matching name token: " & String.Join(", ", matchingNameTokens.Take(3)))
        End If

        If score <= 0 Then
            Return Nothing
        End If

        Return New ArtifactRelationModel With {
            .Artifact = candidate,
            .Score = score,
            .Reasons = reasons
        }
    End Function

    Private Shared Function SharedTagNames(selected As ArtifactModel, candidate As ArtifactModel) As List(Of String)
        Dim selectedTags = If(selected.Tags, New List(Of String))
        Dim candidateTags = If(candidate.Tags, New List(Of String))

        Return candidateTags.
            Where(Function(tag) selectedTags.Any(Function(selectedTag) SameText(selectedTag, tag))).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(tag) tag).
            ToList()
    End Function

    Private Shared Function SameIngestSession(candidate As ArtifactModel, selected As ArtifactModel) As Boolean
        Dim candidateDate As DateTime
        Dim selectedDate As DateTime

        If Not DateTime.TryParse(candidate?.IngestedAt, candidateDate) OrElse Not DateTime.TryParse(selected?.IngestedAt, selectedDate) Then
            Return False
        End If

        Return candidateDate.Date = selectedDate.Date AndAlso Math.Abs((candidateDate - selectedDate).TotalHours) <= 4
    End Function

    Private Shared Function SameCaptureRecord(candidate As ArtifactModel, selected As ArtifactModel) As Boolean
        Return Not String.IsNullOrWhiteSpace(candidate?.CaptureId) AndAlso
            String.Equals(candidate.CaptureId, selected?.CaptureId, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function SharedExtensionFamily(selected As ArtifactModel, candidate As ArtifactModel) As String
        Dim selectedExtension = ExtensionFamily(selected)
        Dim candidateExtension = ExtensionFamily(candidate)

        If Not String.IsNullOrWhiteSpace(selectedExtension) AndAlso String.Equals(selectedExtension, candidateExtension, StringComparison.OrdinalIgnoreCase) Then
            Return selectedExtension
        End If

        Return ""
    End Function

    Private Shared Function ExtensionFamily(artifact As ArtifactModel) As String
        Dim extension = Path.GetExtension(If(artifact?.Name, "")).TrimStart("."c).ToLowerInvariant()
        If String.IsNullOrWhiteSpace(extension) Then
            extension = Path.GetExtension(If(artifact?.Path, "")).TrimStart("."c).ToLowerInvariant()
        End If

        If String.IsNullOrWhiteSpace(extension) Then
            Return ""
        End If

        Select Case extension
            Case "json", "yaml", "yml", "toml", "xml", "ini", "config", "conf", "manifest"
                Return "manifest/config"
            Case "md", "txt", "rtf", "log", "csv"
                Return "text"
            Case "zip", "7z", "rar", "tar", "gz", "bz2", "xz"
                Return "archive"
            Case "png", "jpg", "jpeg", "gif", "webp", "bmp", "tif", "tiff"
                Return "image"
            Case "exe", "msi", "msix", "appx", "deb", "rpm"
                Return "installer"
            Case "iso", "img", "vhd", "vhdx"
                Return "disk image"
            Case Else
                Return extension
        End Select
    End Function

    Private Shared Function SharedProvenanceTokens(selected As ArtifactModel, candidate As ArtifactModel) As List(Of String)
        Dim selectedTokens = ProvenanceTokens(selected)
        Dim candidateTokens = ProvenanceTokens(candidate)

        Return candidateTokens.
            Where(Function(token) selectedTokens.Contains(token, StringComparer.OrdinalIgnoreCase)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Shared Function ProvenanceTokens(artifact As ArtifactModel) As List(Of String)
        Dim text = String.Join(" ", {
            If(artifact?.SourceProvenance, ""),
            If(artifact?.OriginalPath, ""),
            If(artifact?.Path, ""),
            If(artifact?.RetentionReason, "")
        })

        Return GeneralTokens(text).
            Where(Function(token) token.Length >= 4).
            ToList()
    End Function

    Private Shared Function SharedReleaseMarkers(selected As ArtifactModel, candidate As ArtifactModel) As List(Of String)
        Dim selectedMarkers = ReleaseMarkers(SelectedTextForMarkers(selected))
        Dim candidateMarkers = ReleaseMarkers(SelectedTextForMarkers(candidate))

        Return candidateMarkers.
            Where(Function(marker) selectedMarkers.Contains(marker, StringComparer.OrdinalIgnoreCase)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(marker) marker).
            ToList()
    End Function

    Private Shared Function SelectedTextForMarkers(artifact As ArtifactModel) As String
        Return String.Join(" ", {
            If(artifact?.Name, ""),
            If(artifact?.OriginalPath, ""),
            If(artifact?.SourceProvenance, ""),
            If(artifact?.RetentionReason, ""),
            If(artifact?.WhyThisMatters, "")
        })
    End Function

    Private Shared Function ReleaseMarkers(value As String) As List(Of String)
        If String.IsNullOrWhiteSpace(value) Then
            Return New List(Of String)
        End If

        Return Regex.Matches(value.ToLowerInvariant(), "\bv?\d+(?:\.\d+){1,3}\b", RegexOptions.None, ReleaseMarkerRegexTimeout).
            Cast(Of Match)().
            Select(Function(match) match.Value).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(token) token).
            ToList()
    End Function

    Private Shared Function SharedHashPrefixFamily(selected As ArtifactModel, candidate As ArtifactModel) As String
        For Each selectedHash In {selected?.Sha256, selected?.Blake3}
            If String.IsNullOrWhiteSpace(selectedHash) OrElse selectedHash.Length < 12 Then
                Continue For
            End If

            For Each candidateHash In {candidate?.Sha256, candidate?.Blake3}
                If Not String.IsNullOrWhiteSpace(candidateHash) AndAlso candidateHash.Length >= 12 AndAlso
                    Not String.Equals(selectedHash, candidateHash, StringComparison.OrdinalIgnoreCase) AndAlso
                    String.Equals(selectedHash.Substring(0, 12), candidateHash.Substring(0, 12), StringComparison.OrdinalIgnoreCase) Then
                    Return selectedHash.Substring(0, 12)
                End If
            Next
        Next

        Return ""
    End Function

    Private Shared ReadOnly NamedProjectManifestExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "sln", "csproj", "vbproj", "fsproj", "esproj", "pyproj", "vcxproj", "xcodeproj", "pbxproj"
    }

    Private Shared ReadOnly GenericManifestFilenames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "package.json", "requirements.txt", "cargo.toml", "pyproject.toml", "setup.py",
        "package-lock.json", "go.mod", "pom.xml", "build.gradle", "build.gradle.kts", "gemfile", "composer.json",
        "cmakelists.txt", "makefile", "dockerfile", "podfile", "pubspec.yaml"
    }

    Private Shared Function SharedManifestOriginKeys(selected As ArtifactModel, candidate As ArtifactModel) As List(Of String)
        Dim selectedKeys = ManifestOriginKeys(selected)
        Dim candidateKeys = ManifestOriginKeys(candidate)

        Return candidateKeys.
            Where(Function(key) selectedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(key) key).
            ToList()
    End Function

    Public Shared Function ManifestOriginKeys(artifact As ArtifactModel) As List(Of String)
        If artifact Is Nothing Then
            Return New List(Of String)
        End If

        Dim keys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each sourcePath In {artifact.Name, artifact.OriginalPath, artifact.Path, artifact.SourceProvenance}
            If String.IsNullOrWhiteSpace(sourcePath) Then
                Continue For
            End If

            Try
                Dim segments = System.Text.RegularExpressions.Regex.Split(sourcePath, "[\\/\s]+").Where(Function(segment) Not String.IsNullOrWhiteSpace(segment)).ToArray()

                For i = 0 To segments.Length - 1
                    Dim segment = segments(i)
                    Dim stem = Path.GetFileNameWithoutExtension(segment)
                    Dim ext = Path.GetExtension(segment).TrimStart("."c)

                    If NamedProjectManifestExtensions.Contains(ext) AndAlso stem.Length >= 3 Then
                        keys.Add(stem.ToLowerInvariant())
                    ElseIf GenericManifestFilenames.Contains(segment) AndAlso i > 0 Then
                        Dim parentDir = segments(i - 1).ToLowerInvariant()
                        If parentDir.Length >= 3 Then
                            keys.Add(parentDir)
                        End If
                    End If
                Next
            Catch
            End Try
        Next

        Return keys.OrderBy(Function(k) k).ToList()
    End Function

    Private Shared Function SharedExtractedKeywords(selected As ArtifactModel, candidate As ArtifactModel, vaultRootPath As String) As List(Of String)
        If String.IsNullOrWhiteSpace(vaultRootPath) Then
            Return New List(Of String)
        End If

        Dim selectedKeywords = ExtractedKeywords(selected, vaultRootPath)
        Dim candidateKeywords = ExtractedKeywords(candidate, vaultRootPath)

        Return candidateKeywords.
            Where(Function(keyword) selectedKeywords.Contains(keyword, StringComparer.OrdinalIgnoreCase)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(keyword) keyword).
            ToList()
    End Function

    Private Shared Function ExtractedKeywords(artifact As ArtifactModel, vaultRootPath As String) As List(Of String)
        If artifact Is Nothing OrElse String.IsNullOrWhiteSpace(artifact.ExtractedTextRelativePath) Then
            Return New List(Of String)
        End If

        Try
            Dim extractedPath = Path.Combine(vaultRootPath, artifact.ExtractedTextRelativePath)
            If Not File.Exists(extractedPath) Then
                Return New List(Of String)
            End If

            Dim text = String.Join(" ", File.ReadLines(extractedPath).Take(80))
            Return GeneralTokens(text).
                Where(Function(token) token.Length >= 5 AndAlso Not CommonRelationStopWords.Contains(token)).
                GroupBy(Function(token) token, StringComparer.OrdinalIgnoreCase).
                OrderByDescending(Function(group) group.Count()).
                ThenBy(Function(group) group.Key).
                Select(Function(group) group.Key).
                Take(24).
                ToList()
        Catch
            Return New List(Of String)
        End Try
    End Function

    Private Shared ReadOnly CommonRelationStopWords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "about", "after", "again", "artifact", "before", "between", "could", "false", "file", "files", "from", "have", "metadata", "other", "should", "source", "their", "there", "these", "those", "true", "vault", "where", "which", "would"
    }

    Private Shared Function SameText(left As String, right As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(left) AndAlso
            String.Equals(left, right, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function SameDirectory(leftPath As String, rightPath As String) As Boolean
        If String.IsNullOrWhiteSpace(leftPath) OrElse String.IsNullOrWhiteSpace(rightPath) Then
            Return False
        End If

        Try
            Dim leftDirectory = Path.GetDirectoryName(Path.GetFullPath(leftPath))
            Dim rightDirectory = Path.GetDirectoryName(Path.GetFullPath(rightPath))
            Return Not String.IsNullOrWhiteSpace(leftDirectory) AndAlso
                String.Equals(leftDirectory, rightDirectory, StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function SameDateBatch(candidate As ArtifactModel, selected As ArtifactModel) As Boolean
        Dim candidateDate As DateTime
        Dim selectedDate As DateTime

        If TryGetBatchDate(candidate, candidateDate) AndAlso TryGetBatchDate(selected, selectedDate) Then
            Return candidateDate.Date = selectedDate.Date
        End If

        Return False
    End Function

    Private Shared Function TryGetBatchDate(artifact As ArtifactModel, ByRef parsed As DateTime) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        Return DateTime.TryParse(artifact.IngestedAt, parsed) OrElse DateTime.TryParse(artifact.DateModified, parsed)
    End Function

    Private Shared Function SharedNameTokens(leftName As String, rightName As String) As List(Of String)
        Dim leftTokens = NameTokens(leftName)
        Dim rightTokens = NameTokens(rightName)

        Return rightTokens.
            Where(Function(token) leftTokens.Contains(token, StringComparer.OrdinalIgnoreCase)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(token) token).
            ToList()
    End Function

    Private Shared Function NameTokens(name As String) As List(Of String)
        If String.IsNullOrWhiteSpace(name) Then
            Return New List(Of String)
        End If

        Return GeneralTokens(Path.GetFileNameWithoutExtension(name)).
            Where(Function(token) token.Length >= 3 AndAlso Not IsNumericToken(token)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Shared Function GeneralTokens(value As String) As List(Of String)
        If String.IsNullOrWhiteSpace(value) Then
            Return New List(Of String)
        End If

        Return value.
            Split({" "c, "-"c, "_"c, "/"c, "\"c, "."c, "("c, ")"c, "["c, "]"c, "{"c, "}"c, ":"c, ";"c, ","c, "="c, "+"c, "#"c, "!"c, "?"c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(token) token.Trim().ToLowerInvariant()).
            Where(Function(token) token.Length > 0 AndAlso token.Any(Function(ch) Char.IsLetterOrDigit(ch))).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Shared Function IsNumericToken(token As String) As Boolean
        Dim ignored As Integer
        Return Integer.TryParse(token, ignored)
    End Function

    Private Function MatchesActiveScope(artifact As ArtifactModel) As Boolean
        If String.Equals(NormalizeScope(ActiveScope), "Repair needed", StringComparison.OrdinalIgnoreCase) Then
            Return HasPublishedRepairNeed(artifact)
        End If

        If String.Equals(NormalizeScope(ActiveScope), "Same source batch", StringComparison.OrdinalIgnoreCase) Then
            Return EnsureSameSourceBatchScopeKeys().Contains(ArtifactSearchKey(artifact))
        End If

        Return ArtifactMatchesDiscoveryScope(artifact, ActiveScope, Artifacts, VaultRootPath, SelectedArtifact, _thumbnailService)
    End Function

    Private Function EnsureSameSourceBatchScopeKeys() As HashSet(Of String)
        Dim selectedKey = ArtifactSearchKey(SelectedArtifact)

        If _sameSourceBatchScopeKeys Is Nothing OrElse Not String.Equals(_sameSourceBatchScopeSelectedKey, selectedKey, StringComparison.OrdinalIgnoreCase) Then
            _sameSourceBatchScopeKeys = BuildSameSourceBatchScopeKeys(Artifacts, SelectedArtifact)
            _sameSourceBatchScopeSelectedKey = selectedKey
        End If

        Return _sameSourceBatchScopeKeys
    End Function

    Private Sub InvalidateDiscoveryScopeCache()
        _sameSourceBatchScopeKeys = Nothing
        _sameSourceBatchScopeSelectedKey = ""
    End Sub

    Private Function HasPublishedRepairNeed(artifact As ArtifactModel) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        If VaultHealthFindings.Count = 0 Then
            Return HasCheapRepairNeed(artifact, VaultRootPath, _thumbnailService)
        End If

        Return VaultHealthFindings.Any(Function(finding) FindingMatchesArtifact(finding, artifact))
    End Function

    Public Shared Function ArtifactMatchesDiscoveryScope(artifact As ArtifactModel, scope As String, artifacts As IEnumerable(Of ArtifactModel), vaultRootPath As String, Optional selectedArtifact As ArtifactModel = Nothing, Optional thumbnailService As ThumbnailService = Nothing) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        Select Case NormalizeScope(scope)
            Case "Recent"
                Return IsRecentArtifact(artifact)
            Case "Starred"
                Return artifact.IsStarred
            Case "Quarantine"
                Return String.Equals(artifact.Category, "Quarantine", StringComparison.OrdinalIgnoreCase) OrElse
                    (Not String.IsNullOrWhiteSpace(artifact.RelativePath) AndAlso artifact.RelativePath.StartsWith("quarantine", StringComparison.OrdinalIgnoreCase))
            Case "Unverified"
                Return IsUnverifiedArtifact(artifact)
            Case "Missing preview"
                Return HasMissingPreview(artifact, vaultRootPath, thumbnailService)
            Case "Repair needed"
                Return HasCheapRepairNeed(artifact, vaultRootPath, thumbnailService)
            Case "Duplicate candidates"
                Return IsDuplicateCandidate(artifact, artifacts)
            Case "Same source batch"
                Return IsSameSourceBatchArtifact(artifact, artifacts, selectedArtifact)
            Case "Large artifacts"
                Return artifact.SizeBytes >= LargeArtifactThresholdBytes
            Case Else
                Return True
        End Select
    End Function

    Private Shared Function IsRecentArtifact(artifact As ArtifactModel) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        Dim parsed As DateTime
        If DateTime.TryParse(artifact.IngestedAt, parsed) Then
            Return parsed >= DateTime.Now.AddDays(-14)
        End If

        If DateTime.TryParse(artifact.DateModified, parsed) Then
            Return parsed >= DateTime.Now.AddDays(-14)
        End If

        Return False
    End Function

    Private Shared Function IsUnverifiedArtifact(artifact As ArtifactModel) As Boolean
        Return String.Equals(artifact.TrustClassification, "Unverified", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(artifact.TrustClassification, "Questionable", StringComparison.OrdinalIgnoreCase) OrElse
            Not String.Equals(artifact.HashStatus, "Verified", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function HasMissingPreview(artifact As ArtifactModel, vaultRootPath As String, thumbnailService As ThumbnailService) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        Dim thumbService = If(thumbnailService, New ThumbnailService())
        Return thumbService.IsGeneratedThumbnailMissing(artifact, vaultRootPath) OrElse
            String.Equals(artifact.ThumbnailStatus, "Missing", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function HasCheapRepairNeed(artifact As ArtifactModel, vaultRootPath As String, thumbnailService As ThumbnailService) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        If String.IsNullOrWhiteSpace(artifact.Path) OrElse Not File.Exists(artifact.Path) Then
            Return True
        End If

        If Not String.IsNullOrWhiteSpace(vaultRootPath) AndAlso Not IsPathInsideDirectory(artifact.Path, vaultRootPath) Then
            Return True
        End If

        If String.IsNullOrWhiteSpace(artifact.Blake3) OrElse String.IsNullOrWhiteSpace(artifact.Sha256) Then
            Return True
        End If

        If HasMissingPreview(artifact, vaultRootPath, thumbnailService) Then
            Return True
        End If

        If Not String.IsNullOrWhiteSpace(artifact.ExtractedTextRelativePath) Then
            Dim extractedPath = ResolveVaultRelativePath(vaultRootPath, artifact.ExtractedTextRelativePath)
            If String.IsNullOrWhiteSpace(extractedPath) OrElse Not File.Exists(extractedPath) Then
                Return True
            End If
        End If

        Return HasIncompleteMetadata(artifact)
    End Function

    Private Shared Function FindingMatchesArtifact(finding As VaultHealthFinding, artifact As ArtifactModel) As Boolean
        If finding Is Nothing OrElse artifact Is Nothing Then
            Return False
        End If

        Return String.Equals(finding.Subject, artifact.Name, StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(finding.Subject, artifact.Id, StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(finding.Subject, artifact.Sha256, StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(finding.Subject, artifact.RelativePath, StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(finding.Subject, artifact.ThumbnailRelativePath, StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(finding.Subject, artifact.ExtractedTextRelativePath, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsDuplicateCandidate(artifact As ArtifactModel, artifacts As IEnumerable(Of ArtifactModel)) As Boolean
        If artifact Is Nothing OrElse String.IsNullOrWhiteSpace(artifact.Sha256) Then
            Return False
        End If

        Return If(artifacts, Enumerable.Empty(Of ArtifactModel)()).
            Count(Function(candidate) candidate IsNot Nothing AndAlso
                Not ReferenceEquals(candidate, artifact) AndAlso
                String.Equals(candidate.Sha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase)) > 0
    End Function

    Private Shared Function IsSameSourceBatchArtifact(artifact As ArtifactModel, artifacts As IEnumerable(Of ArtifactModel), selectedArtifact As ArtifactModel) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        If selectedArtifact IsNot Nothing Then
            Return SameSourceBatch(artifact, selectedArtifact)
        End If

        Return If(artifacts, Enumerable.Empty(Of ArtifactModel)()).
            Any(Function(candidate) candidate IsNot Nothing AndAlso Not ReferenceEquals(candidate, artifact) AndAlso SameSourceBatch(artifact, candidate))
    End Function

    Public Shared Function BuildSameSourceBatchScopeKeys(artifacts As IEnumerable(Of ArtifactModel), selectedArtifact As ArtifactModel) As HashSet(Of String)
        Dim keys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim artifactList = If(artifacts, Enumerable.Empty(Of ArtifactModel)()).Where(Function(artifact) artifact IsNot Nothing).ToList()

        If selectedArtifact IsNot Nothing Then
            For Each artifact In artifactList
                If SameSourceBatch(artifact, selectedArtifact) Then
                    keys.Add(ArtifactSearchKey(artifact))
                End If
            Next

            Return keys
        End If

        Dim grouped = artifactList.
            Select(Function(artifact)
                       Dim directory = OriginalDirectoryKey(artifact)
                       Dim ingestedAt As DateTime
                       If String.IsNullOrWhiteSpace(directory) OrElse Not DateTime.TryParse(artifact.IngestedAt, ingestedAt) Then
                           Return Nothing
                       End If

                       Return New SameSourceBatchCandidate With {
                           .Artifact = artifact,
                           .DirectoryKey = directory,
                           .IngestedAt = ingestedAt
                       }
                   End Function).
            Where(Function(candidate) candidate IsNot Nothing).
            GroupBy(Function(candidate) $"{candidate.DirectoryKey}|{candidate.IngestedAt:yyyy-MM-dd}", StringComparer.OrdinalIgnoreCase)

        For Each group In grouped
            Dim ordered = group.OrderBy(Function(candidate) candidate.IngestedAt).ToList()

            For index = 0 To ordered.Count - 1
                Dim current = ordered(index)
                Dim hasPeer = (index > 0 AndAlso Math.Abs((current.IngestedAt - ordered(index - 1).IngestedAt).TotalHours) <= 4) OrElse
                    (index < ordered.Count - 1 AndAlso Math.Abs((ordered(index + 1).IngestedAt - current.IngestedAt).TotalHours) <= 4)

                If hasPeer Then
                    keys.Add(ArtifactSearchKey(current.Artifact))
                End If
            Next
        Next

        Return keys
    End Function

    Private Shared Function SameSourceBatch(left As ArtifactModel, right As ArtifactModel) As Boolean
        If left Is Nothing OrElse right Is Nothing OrElse ReferenceEquals(left, right) Then
            Return False
        End If

        Return SameDirectory(left.OriginalPath, right.OriginalPath) AndAlso SameIngestSession(left, right)
    End Function

    Private Shared Function OriginalDirectoryKey(artifact As ArtifactModel) As String
        If String.IsNullOrWhiteSpace(artifact?.OriginalPath) Then
            Return ""
        End If

        Try
            Dim directory = Path.GetDirectoryName(Path.GetFullPath(artifact.OriginalPath))
            Return If(directory, "")
        Catch
            Return ""
        End Try
    End Function

    Private Sub HydrateArtifacts(artifacts As List(Of ArtifactModel))
        If artifacts Is Nothing Then
            Return
        End If

        For Each artifact In artifacts
            HydrateArtifact(artifact)
        Next
    End Sub

    Private Sub HydrateArtifact(artifact As ArtifactModel)
        If artifact Is Nothing Then
            Return
        End If

        EnsureArtifactId(artifact)
        EnsureArtifactTypeFamily(artifact)
        EnsureArtifactSize(artifact)
        EnsureArtifactRelativePath(artifact)
        EnsureExtractedTextStatus(artifact)
        EnsureThumbnailStatus(artifact)
        NormalizeArtifactChoices(artifact)
    End Sub

    Private Shared Sub EnsureArtifactId(artifact As ArtifactModel)
        If String.IsNullOrWhiteSpace(artifact.Id) Then
            artifact.Id = Guid.NewGuid().ToString("N")
        End If
    End Sub

    Private Shared Sub EnsureArtifactTypeFamily(artifact As ArtifactModel)
        If String.IsNullOrWhiteSpace(artifact.TypeFamily) Then
            artifact.TypeFamily = InferTypeFamilyFromCategory(artifact.Category)
        End If
    End Sub

    Private Shared Sub EnsureArtifactSize(artifact As ArtifactModel)
        If artifact.SizeBytes = 0 AndAlso Not String.IsNullOrWhiteSpace(artifact.Path) AndAlso File.Exists(artifact.Path) Then
            artifact.SizeBytes = New FileInfo(artifact.Path).Length
        End If
    End Sub

    Private Sub EnsureArtifactRelativePath(artifact As ArtifactModel)
        If Not String.IsNullOrWhiteSpace(artifact.RelativePath) OrElse String.IsNullOrWhiteSpace(artifact.Path) Then
            Return
        End If

        Try
            artifact.RelativePath = Path.GetRelativePath(VaultRootPath, artifact.Path)
        Catch
        End Try
    End Sub

    Private Shared Sub EnsureExtractedTextStatus(artifact As ArtifactModel)
        If String.IsNullOrWhiteSpace(artifact.ExtractedTextStatus) Then
            artifact.ExtractedTextStatus = If(String.IsNullOrWhiteSpace(artifact.ExtractedTextRelativePath), "Not extracted", "Extracted")
        End If
    End Sub

    Private Shared Sub EnsureThumbnailStatus(artifact As ArtifactModel)
        If String.IsNullOrWhiteSpace(artifact.ThumbnailStatus) Then
            artifact.ThumbnailStatus = If(String.IsNullOrWhiteSpace(artifact.ThumbnailRelativePath), ThumbnailService.NotApplicableStatus, ThumbnailService.GeneratedStatus)
        End If
    End Sub

    Private Shared Sub NormalizeArtifactChoices(artifact As ArtifactModel)
        artifact.TrustClassification = NormalizeChoice(artifact.TrustClassification, {"Unknown", "Trusted", "Unverified", "Questionable"}, "Unknown")
        artifact.RetentionPriority = NormalizeChoice(artifact.RetentionPriority, {"Normal", "High", "Cold archive", "Review later"}, "Normal")
        artifact.ArchiveStatus = NormalizeChoice(artifact.ArchiveStatus, {"Active", "Archived", "Quarantined", "Needs review"}, "Active")
    End Sub

    Private Shared Function InferTypeFamilyFromCategory(category As String) As String
        Select Case category
            Case "Images"
                Return "Image"
            Case "Documents", "Manifests / Config"
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

    Private Sub RebuildStats()
        Dim largeObjects = Artifacts.Where(Function(a) a.SizeBytes >= 1024L * 1024L * 1024L).Count()
        Dim activeHashes = If(_catalog?.ActiveHashes, HashRegistry.DefaultActiveHashes)
        Dim activeHashIds = HashRegistry.ParseActiveHashIds(HashRegistry.NormalizeActiveHashes(activeHashes))
        Dim indexed = Artifacts.Where(Function(a) activeHashIds.Any(Function(hashId) Not String.IsNullOrWhiteSpace(HashRegistry.GetArtifactHashValue(a, hashId)))).Count()
        Dim rebuilt = New List(Of StatCardModel) From {
            New StatCardModel With {.Label = "Total Items", .Value = Artifacts.Count.ToString("N0"), .Icon = "", .IconBrush = BlueSlatePalette.Action, .IconBackground = BlueSlatePalette.PanelBlueDim},
            New StatCardModel With {.Label = "Vault Size", .Value = FormatSize(Artifacts.Sum(Function(a) a.SizeBytes)), .Icon = "", .IconBrush = BlueSlatePalette.Archive, .IconBackground = BlueSlatePalette.TaxonomyDim},
            New StatCardModel With {.Label = "Indexed", .Value = indexed.ToString("N0"), .Icon = "", .IconBrush = BlueSlatePalette.Success, .IconBackground = BlueSlatePalette.SuccessDim},
            New StatCardModel With {.Label = "Large Objects", .Value = largeObjects.ToString("N0"), .Icon = "", .IconBrush = BlueSlatePalette.Build, .IconBackground = BlueSlatePalette.BuildDim},
            New StatCardModel With {.Label = "In Quarantine", .Value = QuarantineCountText, .Icon = "", .IconBrush = BlueSlatePalette.Danger, .IconBackground = BlueSlatePalette.DangerDim}
        }
        ReplaceCollection(Stats, rebuilt)
        If _catalog IsNot Nothing Then
            _catalog.Stats = rebuilt
        End If
    End Sub

    Public Shared Function BuildRepairReport(artifacts As IEnumerable(Of ArtifactModel), vaultRootPath As String, healthReport As VaultHealthReport, Optional orphanFiles As IEnumerable(Of String) = Nothing, Optional activeHashes As String = "") As VaultRepairReport
        Dim artifactList = If(artifacts, Enumerable.Empty(Of ArtifactModel)()).Where(Function(a) a IsNot Nothing).ToList()
        Dim missingArtifacts = artifactList.Where(Function(a) String.IsNullOrWhiteSpace(a.Path) OrElse Not File.Exists(a.Path)).ToList()
        Dim activeHashIds = HashRegistry.ParseActiveHashIds(HashRegistry.NormalizeActiveHashes(activeHashes))
        Dim derivedDuplicateGroups = activeHashIds.
            SelectMany(Function(hashId)
                           Return artifactList.
                               Where(Function(a) Not String.IsNullOrWhiteSpace(HashRegistry.GetArtifactHashValue(a, hashId))).
                               GroupBy(Function(a) HashRegistry.GetArtifactHashValue(a, hashId), StringComparer.OrdinalIgnoreCase).
                               Where(Function(g) g.Count() > 1).
                               Select(Function(g) $"{hashId}: {g.Key}")
                       End Function).
            ToList()
        Dim duplicateFindings = If(healthReport?.Findings, Enumerable.Empty(Of VaultHealthFinding)()).
            Where(Function(finding) String.Equals(finding.FindingType, "Duplicate hash", StringComparison.OrdinalIgnoreCase)).
            ToList()
        Dim duplicateSamples = If(duplicateFindings.Count > 0, duplicateFindings.Select(Function(finding) finding.Subject).ToList(), derivedDuplicateGroups)
        Dim report As New VaultRepairReport With {
            .MissingFiles = missingArtifacts.Count,
            .DuplicateHashGroups = If(duplicateFindings.Count > 0, duplicateFindings.Count, derivedDuplicateGroups.Count),
            .MissingSamples = missingArtifacts.Select(Function(a) a.Name).Take(3).ToList(),
            .DuplicateSamples = duplicateSamples.Take(3).ToList()
        }

        Dim missingThumbnailArtifacts = If(healthReport?.Findings, Enumerable.Empty(Of VaultHealthFinding)()).
            Where(Function(finding) String.Equals(finding.FindingType, "Missing thumbnail", StringComparison.OrdinalIgnoreCase)).
            Select(Function(finding) artifactList.FirstOrDefault(Function(artifact) String.Equals(artifact.Name, finding.Subject, StringComparison.OrdinalIgnoreCase))).
            Where(Function(artifact) artifact IsNot Nothing).
            ToList()
        report.MissingThumbnails = missingThumbnailArtifacts.Count
        report.ThumbnailSamples = missingThumbnailArtifacts.Select(Function(a) a.Name).Take(3).ToList()

        Dim orphanList = If(orphanFiles, FindOrphanStoredFiles(artifactList, vaultRootPath)).ToList()
        report.OrphanFiles = orphanList.Count
        report.OrphanSamples = orphanList.Select(Function(orphanPath) Path.GetFileName(orphanPath)).Take(3).ToList()

        Return report
    End Function

    Public Function BuildVaultHealthReport() As VaultHealthReport
        Return BuildVaultHealthReport(Artifacts, VaultRootPath, _thumbnailService, _hashService, Nothing, _catalog.ActiveHashes)
    End Function

    Private Function BuildRescanResult(artifactSnapshot As List(Of ArtifactModel), vaultRootPath As String, Optional progress As IProgress(Of VaultMaintenanceProgress) = Nothing) As VaultMaintenanceResult
        Dim healthReport = BuildVaultHealthReport(artifactSnapshot, vaultRootPath, New ThumbnailService(), New HashService(), progress, _catalog.ActiveHashes)
        Dim orphanFiles = FindOrphanStoredFiles(artifactSnapshot, vaultRootPath).ToList()
        Dim repairReport = BuildRepairReport(artifactSnapshot, vaultRootPath, healthReport, orphanFiles, _catalog.ActiveHashes)
        Dim result As New VaultMaintenanceResult With {
            .HealthReport = healthReport,
            .RepairReport = repairReport
        }

        Dim missingThumbnailArtifacts = healthReport.Findings.
            Where(Function(finding) String.Equals(finding.FindingType, "Missing thumbnail", StringComparison.OrdinalIgnoreCase)).
            Select(Function(finding) artifactSnapshot.FirstOrDefault(Function(artifact) String.Equals(artifact.Name, finding.Subject, StringComparison.OrdinalIgnoreCase))).
            Where(Function(artifact) artifact IsNot Nothing).
            ToList()

        For Each artifact In missingThumbnailArtifacts
            progress?.Report(New VaultMaintenanceProgress With {.Stage = "Regenerating thumbnails", .CurrentItem = artifact.Name, .ProcessedCount = result.ThumbnailUpdates.Count + 1, .TotalCount = missingThumbnailArtifacts.Count})
            Dim thumbnail = _thumbnailService.GenerateForArtifact(artifact, vaultRootPath)
            result.ThumbnailUpdates.Add(New ThumbnailRepairUpdate With {
                .Artifact = artifact,
                .RelativePath = thumbnail.RelativePath,
                .Status = thumbnail.Status
            })

            If String.Equals(thumbnail.Status, ThumbnailService.GeneratedStatus, StringComparison.OrdinalIgnoreCase) Then
                repairReport.RegeneratedThumbnails += 1
            End If
        Next

        For Each orphan In orphanFiles
            Try
                Dim adopted = _ingestionService.CreateArtifactFromStoredFile(orphan, vaultRootPath, "", _catalog.ActiveHashes)
                adopted.Notes = $"Adopted during vault rescan at {DateTime.Now:yyyy-MM-dd HH:mm}"
                result.AdoptedArtifacts.Add(adopted)
                repairReport.AdoptedFiles += 1
            Catch
            End Try
        Next

        Return result
    End Function

    Private Sub ApplyThumbnailUpdates(updates As IEnumerable(Of ThumbnailRepairUpdate))
        For Each update In If(updates, Enumerable.Empty(Of ThumbnailRepairUpdate)())
            If update?.Artifact Is Nothing Then
                Continue For
            End If

            update.Artifact.ThumbnailRelativePath = update.RelativePath
            update.Artifact.ThumbnailStatus = update.Status
        Next
    End Sub

    Private Sub ApplyAdoptedArtifacts(adoptedArtifacts As IEnumerable(Of ArtifactModel))
        Dim adoptedList = If(adoptedArtifacts, Enumerable.Empty(Of ArtifactModel)()).ToList()
        For Each adopted In adoptedList
            Artifacts.Insert(0, adopted)
            _catalog.Artifacts.Insert(0, adopted)
        Next

        If adoptedList.Count > 0 Then
            Dim activity = New ActivityEntryModel With {
                .ActionText = $"Adopted {adoptedList.Count:N0} orphan file(s)",
                .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  vault rescan",
                .Icon = ""
            }
            Activities.Insert(0, activity)
            _catalog.Activities.Insert(0, activity)
        End If
    End Sub

    Private Sub PublishVaultHealthReport(report As VaultHealthReport)
        For Each candidate In RepairCandidates
            RemoveHandler candidate.PropertyChanged, AddressOf RepairCandidate_PropertyChanged
        Next

        _hasVaultHealthAnalysis = True
        VaultHealthFindings.Clear()
        RepairCandidates.Clear()

        If report IsNot Nothing Then
            For Each finding In report.Findings
                Dim candidate = BuildRepairCandidate(finding)
                VaultHealthFindings.Add(finding)
                AddHandler candidate.PropertyChanged, AddressOf RepairCandidate_PropertyChanged
                RepairCandidates.Add(candidate)
            Next
        End If

        If FilteredRepairCandidates Is Nothing Then
            FilteredRepairCandidates = CollectionViewSource.GetDefaultView(RepairCandidates)
            FilteredRepairCandidates.Filter = AddressOf FilterRepairCandidate
        End If

        RefreshRepairCandidateView()
        OnPropertyChanged(NameOf(VaultHealthSummary))
        OnPropertyChanged(NameOf(VaultHealthBreakdown))
        OnPropertyChanged(NameOf(SelectedRepairSummary))
        OnPropertyChanged(NameOf(HealthFindingCountText))
        OnPropertyChanged(NameOf(HealthRepairableCountText))
        OnPropertyChanged(NameOf(HealthReviewOnlyCountText))
        OnPropertyChanged(NameOf(HealthSelectedCountText))
        OnPropertyChanged(NameOf(HealthDeferredHashCountText))
        OnPropertyChanged(NameOf(HealthRiskBreakdownRows))
        OnPropertyChanged(NameOf(HealthFindingBreakdownRows))
        OnPropertyChanged(NameOf(HealthRepairActionBreakdownRows))
        OnPropertyChanged(NameOf(FindingTypeFilterOptions))
        RaiseRepairCandidateCommandState()
    End Sub

    Private Sub RepairCandidate_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If String.Equals(e.PropertyName, NameOf(RepairCandidate.IsSelected), StringComparison.OrdinalIgnoreCase) Then
            If _isBulkUpdatingRepairSelection Then
                Return
            End If

            OnPropertyChanged(NameOf(VaultHealthSummary))
            OnPropertyChanged(NameOf(SelectedRepairSummary))
            OnPropertyChanged(NameOf(HealthSelectedCountText))
            RefreshRepairCandidateView()
            RaiseRepairCandidateCommandState()
        End If
    End Sub

    Private Function FilterRepairCandidate(candidateObject As Object) As Boolean
        Dim candidate = TryCast(candidateObject, RepairCandidate)
        If candidate Is Nothing Then
            Return False
        End If

        If Not String.Equals(FindingTypeFilter, "All", StringComparison.OrdinalIgnoreCase) AndAlso
            Not String.Equals(candidate.FindingType, FindingTypeFilter, StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If

        Select Case If(RepairCandidateFilter, "All").Trim().ToLowerInvariant()
            Case "automatic"
                Return candidate.CanRepairAutomatically AndAlso Not candidate.IsExpensiveAutomatic
            Case "expensive automatic"
                Return candidate.IsExpensiveAutomatic
            Case "review-only"
                Return Not candidate.CanRepairAutomatically
            Case "selected"
                Return candidate.IsSelected
            Case "unselected"
                Return Not candidate.IsSelected
            Case Else
                Return True
        End Select
    End Function

    Private Sub RefreshRepairCandidateView()
        If FilteredRepairCandidates IsNot Nothing Then
            FilteredRepairCandidates.Refresh()
        End If
    End Sub

    Private Shared Function BuildBreakdownRows(Of T)(groups As IEnumerable(Of IGrouping(Of String, T)), accentBrush As String) As IEnumerable(Of HealthBreakdownRow)
        Dim groupList = If(groups, Enumerable.Empty(Of IGrouping(Of String, T))()).
            Select(Function(group) New With {.Label = If(String.IsNullOrWhiteSpace(group.Key), "Unknown", group.Key), .Count = group.Count()}).
            OrderByDescending(Function(group) group.Count).
            ThenBy(Function(group) group.Label).
            ToList()

        If groupList.Count = 0 Then
            Return Enumerable.Empty(Of HealthBreakdownRow)()
        End If

        Dim maxCount = Math.Max(1, groupList.Max(Function(group) group.Count))
        Return groupList.Select(Function(group) New HealthBreakdownRow With {
            .Label = group.Label,
            .Count = group.Count,
            .PercentWidth = Math.Max(8, Math.Round((group.Count / CDbl(maxCount)) * 160)),
            .AccentBrush = accentBrush
        }).ToList()
    End Function

    Private Sub SelectSafeRepairCandidates()
        _isBulkUpdatingRepairSelection = True
        ApplyRepairCandidateSelection(RepairCandidates, "safe", Nothing)
        _isBulkUpdatingRepairSelection = False
        FinishBulkRepairSelection()
    End Sub

    Private Sub SelectVisibleRepairCandidates()
        _isBulkUpdatingRepairSelection = True
        ApplyRepairCandidateSelection(GetVisibleRepairCandidates(), "visible", Nothing)
        _isBulkUpdatingRepairSelection = False
        FinishBulkRepairSelection()
    End Sub

    Private Sub ClearVisibleRepairCandidates()
        _isBulkUpdatingRepairSelection = True
        ApplyRepairCandidateSelection(GetVisibleRepairCandidates(), "clear", Nothing)
        _isBulkUpdatingRepairSelection = False
        FinishBulkRepairSelection()
    End Sub

    Private Sub SelectRepairAction(actionType As String)
        If String.IsNullOrWhiteSpace(actionType) Then
            Return
        End If

        _isBulkUpdatingRepairSelection = True
        ApplyRepairCandidateSelection(RepairCandidates, "action", actionType)
        _isBulkUpdatingRepairSelection = False
        FinishBulkRepairSelection()
    End Sub

    Private Sub SelectAllAutomaticRepairCandidates()
        _isBulkUpdatingRepairSelection = True
        ApplyRepairCandidateSelection(RepairCandidates, "automatic", Nothing)
        _isBulkUpdatingRepairSelection = False
        FinishBulkRepairSelection()
    End Sub

    Private Sub ClearRepairSelection()
        _isBulkUpdatingRepairSelection = True
        ApplyRepairCandidateSelection(RepairCandidates, "clear", Nothing)
        _isBulkUpdatingRepairSelection = False
        FinishBulkRepairSelection()
    End Sub

    Private Function GetVisibleRepairCandidates() As IEnumerable(Of RepairCandidate)
        If FilteredRepairCandidates Is Nothing Then
            Return RepairCandidates
        End If

        Return FilteredRepairCandidates.Cast(Of RepairCandidate)().ToList()
    End Function

    Public Shared Sub ApplyRepairCandidateSelection(candidates As IEnumerable(Of RepairCandidate), selectionMode As String, actionType As String)
        For Each candidate In If(candidates, Enumerable.Empty(Of RepairCandidate)())
            If candidate Is Nothing Then
                Continue For
            End If

            Select Case If(selectionMode, "").Trim().ToLowerInvariant()
                Case "safe"
                    candidate.IsSelected = candidate.CanRepairAutomatically AndAlso IsSafeDefaultRepairCandidate(candidate.ActionType)
                Case "visible"
                    candidate.IsSelected = candidate.CanRepairAutomatically
                Case "automatic"
                    candidate.IsSelected = candidate.CanRepairAutomatically
                Case "action"
                    candidate.IsSelected = candidate.CanRepairAutomatically AndAlso String.Equals(candidate.ActionType, actionType, StringComparison.OrdinalIgnoreCase)
                Case "clear"
                    candidate.IsSelected = False
            End Select
        Next
    End Sub

    Private Sub FinishBulkRepairSelection()
        OnPropertyChanged(NameOf(VaultHealthSummary))
        OnPropertyChanged(NameOf(SelectedRepairSummary))
        OnPropertyChanged(NameOf(HealthSelectedCountText))
        RefreshRepairCandidateView()
        RaiseRepairCandidateCommandState()
    End Sub

    Public Function GetSelectedRepairCandidateSummary() As String
        Dim selected = RepairCandidates.Where(Function(candidate) candidate.IsSelected).ToList()
        Dim automatic = selected.Where(Function(candidate) candidate.CanRepairAutomatically).ToList()
        Dim reviewOnly = selected.Count - automatic.Count
        Dim expensive = automatic.Where(Function(candidate) candidate.IsExpensiveAutomatic).Count()
        Dim actionSummary = String.Join(", ", automatic.
            GroupBy(Function(candidate) candidate.ActionType).
            OrderBy(Function(group) group.Key).
            Select(Function(group) $"{group.Count():N0} {group.Key}"))

        If String.IsNullOrWhiteSpace(actionSummary) Then
            actionSummary = "none"
        End If

        Return $"{automatic.Count:N0} automatic repair candidate(s) will be applied ({actionSummary}). {expensive:N0} may read retained file bytes. {reviewOnly:N0} review-only candidate(s) will be skipped."
    End Function

    Private Async Function ApplySelectedRepairCandidatesAsync() As Task
        If IsVaultMaintenanceRunning Then
            Return
        End If

        Dim selected = RepairCandidates.
            Where(Function(candidate) candidate.IsSelected).
            ToList()

        If selected.Count = 0 Then
            ActionStatus = "No repair candidates selected"
            RaiseRepairCandidateCommandState()
            Return
        End If

        IsVaultMaintenanceRunning = True
        VaultMaintenanceProgress = 0
        VaultMaintenanceStatus = "Applying selected repairs"
        VaultMaintenanceDetail = $"{selected.Count:N0} selected candidate(s)"
        ActionStatus = "Applying selected repairs..."
        IsVaultHealthVisible = True

        Try
            Dim workItems = selected.
                Select(Function(candidate) New RepairApplicationWorkItem With {
                    .Candidate = candidate,
                    .Artifact = FindArtifactForCandidate(candidate)
                }).
                ToList()
            Dim vaultRoot = VaultRootPath
            Dim activeHashes = HashRegistry.NormalizeActiveHashes(_catalog.ActiveHashes)
            Dim applyProgress As IProgress(Of VaultMaintenanceProgress) = New Progress(Of VaultMaintenanceProgress)(Sub(progressUpdate)
                                                              VaultMaintenanceProgress = progressUpdate.Percent
                                                              VaultMaintenanceStatus = progressUpdate.Stage
                                                              VaultMaintenanceDetail = progressUpdate.ToString()
                                                          End Sub)
            Dim results As List(Of RepairApplicationResult) = Await Task.Run(Function() As List(Of RepairApplicationResult)
                                             Dim completed As New List(Of RepairApplicationResult)
                                             For i = 0 To workItems.Count - 1
                                                 Dim workItem = workItems(i)
                                                 Dim actionType = If(workItem?.Candidate?.ActionType, "Repair")
                                                 Dim subject = If(workItem?.Candidate?.Subject, "")
                                                 applyProgress.Report(New VaultMaintenanceProgress With {
                                                     .Stage = $"Applying {actionType}",
                                                     .CurrentItem = subject,
                                                     .ProcessedCount = i,
                                                     .TotalCount = workItems.Count,
                                                     .Detail = If(String.Equals(actionType, "RecomputeHash", StringComparison.OrdinalIgnoreCase), $"Computing {activeHashes}", "Applying selected repair")
                                                 })

                                                 completed.Add(BuildRepairApplicationResult(workItem, vaultRoot, activeHashes))

                                                 applyProgress.Report(New VaultMaintenanceProgress With {
                                                     .Stage = $"Applied {actionType}",
                                                     .CurrentItem = subject,
                                                     .ProcessedCount = i + 1,
                                                     .TotalCount = workItems.Count,
                                                     .Detail = "Repair item finished"
                                                 })
                                             Next

                                             Return completed
                                         End Function)
            Dim applied = 0
            Dim failed = 0
            Dim skipped = 0

            For Each result In results
                If result.Skipped Then
                    skipped += 1
                    AppendRepairLog(result.Candidate, "Skipped", result.Detail)
                ElseIf result.Succeeded Then
                    ApplyRepairApplicationResult(result)
                    applied += 1
                    AppendRepairLog(result.Candidate, "Applied", "Repair completed")
                Else
                    failed += 1
                    AppendRepairLog(result.Candidate, "Failed", result.Detail)
                End If
            Next

            _catalog.Artifacts = Artifacts.ToList()
            Dim activity = New ActivityEntryModel With {
                .ActionText = $"Applied {applied:N0} repair(s)",
                .DetailText = $"{DateTime.Now:yyyy-MM-dd HH:mm}  •  {failed:N0} failed, {skipped:N0} skipped; repair log updated",
                .Icon = "",
                .IconBrush = BlueSlatePalette.Success,
                .IconBackground = BlueSlatePalette.SuccessDim
            }
            Activities.Insert(0, activity)
            _catalog.Activities.Insert(0, activity)
            _catalogService.Save(_catalog)
            RefreshRepairHistory()
            If SelectedArtifact IsNot Nothing Then
                LoadPreviewForSelected()
            End If

            Dim artifactSnapshot = Artifacts.ToList()
            Dim vaultRootSnapshot = VaultRootPath
            Dim postRepairProgress As New Progress(Of VaultMaintenanceProgress)(Sub(progressUpdate)
                                                                  VaultMaintenanceProgress = progressUpdate.Percent
                                                                  VaultMaintenanceDetail = progressUpdate.ToString()
                                                              End Sub)
            Dim healthReport = Await Task.Run(Function() BuildVaultHealthReport(artifactSnapshot, vaultRootSnapshot, New ThumbnailService(), New HashService(), postRepairProgress, _catalog.ActiveHashes))
            PublishVaultHealthReport(healthReport)
            RefreshDerivedUiState()
            VaultMaintenanceProgress = 100
            VaultMaintenanceStatus = "Repairs complete"
            VaultMaintenanceDetail = healthReport.Summary
            ActionStatus = $"Applied {applied:N0} repair(s); {failed:N0} failed; {skipped:N0} skipped"
        Catch ex As Exception
            VaultMaintenanceStatus = "Repair failed"
            VaultMaintenanceDetail = ex.Message
            ActionStatus = $"Repair failed: {ex.Message}"
        Finally
            IsVaultMaintenanceRunning = False
        End Try
    End Function

    Private Function BuildRepairApplicationResult(workItem As RepairApplicationWorkItem, vaultRootPath As String, activeHashes As String) As RepairApplicationResult
        Dim result As New RepairApplicationResult With {
            .Candidate = workItem?.Candidate,
            .Artifact = workItem?.Artifact,
            .Detail = "Repair could not be completed safely"
        }

        If result.Candidate Is Nothing Then
            result.Skipped = True
            result.Detail = "Repair candidate was not available"
            Return result
        End If

        If Not result.Candidate.CanRepairAutomatically Then
            result.Skipped = True
            result.Detail = "Review-only candidate requires manual action"
            Return result
        End If

        If result.Artifact Is Nothing Then
            result.Detail = "Catalog artifact was not found"
            Return result
        End If

        Try
            ApplyRepairAction(result, vaultRootPath, activeHashes)
        Catch ex As Exception
            result.Succeeded = False
            result.Detail = ex.Message
        End Try

        Return result
    End Function

    Private Sub ApplyRepairAction(result As RepairApplicationResult, vaultRootPath As String, activeHashes As String)
        Select Case result.Candidate.ActionType
            Case "RegenerateThumbnail"
                ApplyThumbnailRepair(result, vaultRootPath)
            Case "RecomputeHash"
                ApplyHashRepair(result, activeHashes)
            Case "ReExtractText"
                ApplyTextExtractionRepair(result, vaultRootPath)
            Case "RebindPath"
                ApplyPathRebindRepair(result, vaultRootPath)
            Case Else
                SkipManualRepair(result)
        End Select
    End Sub

    Private Sub ApplyThumbnailRepair(result As RepairApplicationResult, vaultRootPath As String)
        If Not HasRetainedFile(result.Artifact) Then
            MarkMissingRetainedFile(result)
            Return
        End If

        Dim thumbnail = _thumbnailService.GenerateForArtifact(result.Artifact, vaultRootPath)
        result.ThumbnailRelativePath = thumbnail.RelativePath
        result.ThumbnailStatus = thumbnail.Status
        result.Succeeded = String.Equals(thumbnail.Status, ThumbnailService.GeneratedStatus, StringComparison.OrdinalIgnoreCase)
    End Sub

    Private Sub ApplyHashRepair(result As RepairApplicationResult, activeHashes As String)
        If Not HasRetainedFile(result.Artifact) Then
            MarkMissingRetainedFile(result)
            Return
        End If

        Dim hashes = _hashService.ComputeHashes(result.Artifact.Path, activeHashes)
        result.Hashes = hashes
        result.HashStatus = "Verified"
        result.Succeeded = True
    End Sub

    Private Sub ApplyTextExtractionRepair(result As RepairApplicationResult, vaultRootPath As String)
        If Not HasRetainedFile(result.Artifact) Then
            MarkMissingRetainedFile(result)
            Return
        End If

        Dim extraction = _ingestionService.ExtractTextForArtifact(result.Artifact, vaultRootPath)
        result.ExtractedTextRelativePath = extraction.RelativePath
        result.ExtractedTextStatus = extraction.Status
        result.Succeeded = IsSuccessfulTextExtraction(extraction.Status)
    End Sub

    Private Shared Sub ApplyPathRebindRepair(result As RepairApplicationResult, vaultRootPath As String)
        Dim resolvedPath = ResolveVaultRelativePath(vaultRootPath, result.Artifact.RelativePath)
        If Not IsResolvedVaultFile(resolvedPath, vaultRootPath) Then
            result.Detail = "Vault-relative file could not be resolved"
            Return
        End If

        result.ResolvedPath = resolvedPath
        result.Succeeded = True
    End Sub

    Private Shared Function HasRetainedFile(artifact As ArtifactModel) As Boolean
        Return Not String.IsNullOrWhiteSpace(artifact?.Path) AndAlso File.Exists(artifact.Path)
    End Function

    Private Shared Sub MarkMissingRetainedFile(result As RepairApplicationResult)
        result.Detail = "Retained file is missing"
    End Sub

    Private Shared Function IsSuccessfulTextExtraction(status As String) As Boolean
        Return String.Equals(status, "Extracted", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(status, "Extracted (truncated)", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsResolvedVaultFile(resolvedPath As String, vaultRootPath As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(resolvedPath) AndAlso
            IsPathInsideDirectory(resolvedPath, vaultRootPath) AndAlso
            File.Exists(resolvedPath)
    End Function

    Private Shared Sub SkipManualRepair(result As RepairApplicationResult)
        result.Skipped = True
        result.Detail = "Review-only candidate requires manual action"
    End Sub

    Private Sub ApplyRepairApplicationResult(result As RepairApplicationResult)
        If result Is Nothing OrElse Not result.Succeeded OrElse result.Artifact Is Nothing Then
            Return
        End If

        Select Case result.Candidate?.ActionType
            Case "RegenerateThumbnail"
                result.Artifact.ThumbnailRelativePath = result.ThumbnailRelativePath
                result.Artifact.ThumbnailStatus = result.ThumbnailStatus
            Case "RecomputeHash"
                HashRegistry.ApplyHashesToArtifact(result.Artifact, result.Hashes)
            Case "ReExtractText"
                result.Artifact.ExtractedTextRelativePath = result.ExtractedTextRelativePath
                result.Artifact.ExtractedTextStatus = result.ExtractedTextStatus
            Case "RebindPath"
                result.Artifact.Path = result.ResolvedPath
        End Select
    End Sub

    Private Sub AppendRepairLog(candidate As RepairCandidate, result As String, detail As String)
        Try
            _repairLogService.Append(VaultRootPath, New RepairLogEntry With {
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

    Private Sub RefreshRepairHistory()
        RepairHistory.Clear()

        If String.IsNullOrWhiteSpace(VaultRootPath) Then
            OnPropertyChanged(NameOf(RepairHistorySummary))
            Return
        End If

        Try
            For Each entry In _repairLogService.ReadRecent(VaultRootPath, count:=8)
                RepairHistory.Add(entry)
            Next
        Catch
        End Try

        OnPropertyChanged(NameOf(RepairHistorySummary))
    End Sub

    Private Function FindArtifactForCandidate(candidate As RepairCandidate) As ArtifactModel
        If candidate?.Finding Is Nothing Then
            Return Nothing
        End If

        Return Artifacts.FirstOrDefault(Function(artifact) String.Equals(artifact.Name, candidate.Subject, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub RaiseRepairCandidateCommandState()
        RaiseCommandState(ApplySelectedRepairCandidatesCommand)
        RaiseCommandState(SelectSafeRepairCandidatesCommand)
        RaiseCommandState(SelectVisibleRepairCandidatesCommand)
        RaiseCommandState(ClearVisibleRepairCandidatesCommand)
        RaiseCommandState(SelectRepairActionCommand)
        RaiseCommandState(SelectAllAutomaticRepairCandidatesCommand)
        RaiseCommandState(ClearRepairSelectionCommand)
    End Sub

    Private Sub RaiseMaintenanceCommandState()
        RaiseCommandState(RepairCatalogCommand)
        RaiseCommandState(RescanVaultCommand)
        RaiseCommandState(ApplySelectedRepairCandidatesCommand)
        RaiseCommandState(SelectSafeRepairCandidatesCommand)
        RaiseCommandState(SelectVisibleRepairCandidatesCommand)
        RaiseCommandState(ClearVisibleRepairCandidatesCommand)
        RaiseCommandState(SelectRepairActionCommand)
        RaiseCommandState(SelectAllAutomaticRepairCandidatesCommand)
        RaiseCommandState(ClearRepairSelectionCommand)
        RaiseCommandState(HashCheckCommand)
        RaiseCommandState(VerifySmallHashesCommand)
        RaiseCommandState(RefreshCommand)
    End Sub

    Private Sub RaiseSelectedArtifactCommandState()
        RaiseCommandState(SaveArtifactCommand)
        RaiseCommandState(RevertArtifactCommand)
        RaiseCommandState(OpenLocationCommand)
        RaiseCommandState(OpenFileCommand)
        RaiseCommandState(RestoreArtifactCommand)
        RaiseCommandState(PermanentlyDeleteArtifactCommand)
        RaiseCommandState(HashCheckCommand)
        RaiseCommandState(ToggleStarCommand)
        RaiseCommandState(AddTagsCommand)
        RaiseCommandState(QuarantineCommand)
    End Sub

    Private Sub HandleAsyncCommandException(ex As Exception)
        If ex Is Nothing Then
            Return
        End If

        ActionStatus = $"Operation failed: {ex.Message}"
        VaultMaintenanceStatus = "Operation failed"
        VaultMaintenanceDetail = ex.Message
    End Sub

    Private Shared Sub RaiseCommandState(command As ICommand)
        Dim relay = TryCast(command, RelayCommand)
        If relay IsNot Nothing Then
            relay.RaiseCanExecuteChanged()
            Return
        End If

        Dim asyncRelay = TryCast(command, AsyncRelayCommand)
        If asyncRelay IsNot Nothing Then
            asyncRelay.RaiseCanExecuteChanged()
        End If
    End Sub

    Public Shared Function BuildRepairCandidate(finding As VaultHealthFinding) As RepairCandidate
        If finding Is Nothing Then
            Return New RepairCandidate()
        End If

        Dim actionType = ResolveRepairActionType(finding.FindingType)
        Return New RepairCandidate With {
            .Finding = finding,
            .ActionType = actionType,
            .CanRepairAutomatically = IsAutomaticRepairCandidate(actionType),
            .IsExpensiveAutomatic = IsExpensiveAutomaticRepairCandidate(actionType),
            .RequiresOperatorApproval = True,
            .IsSelected = IsSafeDefaultRepairCandidate(actionType)
        }
    End Function

    Private Shared Function ResolveRepairActionType(findingType As String) As String
        Select Case If(findingType, "").Trim().ToLowerInvariant()
            Case "missing thumbnail"
                Return "RegenerateThumbnail"
            Case "missing hash"
                Return "RecomputeHash"
            Case "missing extracted text"
                Return "ReExtractText"
            Case "path rebind candidate"
                Return "RebindPath"
            Case "orphan file"
                Return "AdoptOrphan"
            Case Else
                Return "ReviewOnly"
        End Select
    End Function

    Private Shared Function IsAutomaticRepairCandidate(actionType As String) As Boolean
        Select Case actionType
            Case "RegenerateThumbnail", "RecomputeHash", "ReExtractText", "RebindPath"
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function IsExpensiveAutomaticRepairCandidate(actionType As String) As Boolean
        Return String.Equals(actionType, "RecomputeHash", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsSafeDefaultRepairCandidate(actionType As String) As Boolean
        Select Case actionType
            Case "RebindPath", "RegenerateThumbnail", "ReExtractText"
                Return True
            Case Else
                Return False
        End Select
    End Function

    Public Shared Function BuildVaultHealthReport(artifacts As IEnumerable(Of ArtifactModel), vaultRootPath As String, thumbnailService As ThumbnailService, Optional hashService As HashService = Nothing, Optional progress As IProgress(Of VaultMaintenanceProgress) = Nothing, Optional activeHashes As String = "", Optional verifyHashes As Boolean = False, Optional hashVerificationLimitBytes As Long = AutoHashVerificationLimitBytes) As VaultHealthReport
        Dim report As New VaultHealthReport()
        Dim artifactList = If(artifacts, Enumerable.Empty(Of ArtifactModel)()).ToList()
        Dim thumbService = If(thumbnailService, New ThumbnailService())
        Dim integrityService = If(hashService, New HashService())
        Dim normalizedActiveHashes = HashRegistry.NormalizeActiveHashes(activeHashes)
        Dim activeHashIds = HashRegistry.ParseActiveHashIds(normalizedActiveHashes)
        Dim total = artifactList.Count
        Dim i = 0

        For Each artifact In artifactList
            If artifact Is Nothing Then
                Continue For
            End If

            i += 1
            progress?.Report(New VaultMaintenanceProgress With {
                .Stage = "Checking",
                .CurrentItem = artifact.Name,
                .ProcessedCount = i,
                .TotalCount = total,
                .Detail = "Vault health analysis"
            })

            Dim hasArtifactPath = Not String.IsNullOrWhiteSpace(artifact.Path)
            Dim pathIsInsideVault = Not hasArtifactPath OrElse IsPathInsideDirectory(artifact.Path, vaultRootPath)
            Dim storedFileExists = hasArtifactPath AndAlso File.Exists(artifact.Path)

            If Not storedFileExists Then
                report.Findings.Add(New VaultHealthFinding With {
                    .FindingType = "Missing file",
                    .Subject = artifact.Name,
                    .Detail = "Catalog entry points to a stored file that is not present.",
                    .ProposedAction = "Keep catalog row and mark for operator review.",
                    .RiskLevel = "Medium",
                    .MutatesCatalog = False,
                    .TouchesRetainedFiles = False
                })
            End If

            If Not storedFileExists AndAlso HasRebindCandidate(artifact, vaultRootPath) Then
                report.Findings.Add(New VaultHealthFinding With {
                    .FindingType = "Path rebind candidate",
                    .Subject = artifact.Name,
                    .Detail = "Catalog absolute path is missing, but the vault-relative path resolves under the active vault root.",
                    .ProposedAction = "Review and rebind the catalog path to the resolved vault-relative file with operator approval.",
                    .RiskLevel = "Medium",
                    .MutatesCatalog = True,
                    .TouchesRetainedFiles = False
                })
            End If

            If hasArtifactPath AndAlso Not pathIsInsideVault Then
                report.Findings.Add(New VaultHealthFinding With {
                    .FindingType = "File outside vault",
                    .Subject = artifact.Name,
                    .Detail = "Catalog entry points to a file outside the active vault root.",
                    .ProposedAction = "Review the path; rebind the vault or restore/copy the artifact into the vault with operator approval.",
                    .RiskLevel = "Medium",
                    .MutatesCatalog = False,
                    .TouchesRetainedFiles = False
                })
            End If

            If storedFileExists AndAlso pathIsInsideVault Then
                AddIncompleteMetadataFinding(report, artifact)
            End If

            Dim missingActiveHashes = activeHashIds.
                Where(Function(hashId) String.IsNullOrWhiteSpace(HashRegistry.GetArtifactHashValue(artifact, hashId))).
                ToList()

            If storedFileExists AndAlso pathIsInsideVault AndAlso missingActiveHashes.Count > 0 Then
                report.Findings.Add(New VaultHealthFinding With {
                    .FindingType = "Missing hash",
                    .Subject = artifact.Name,
                    .Detail = $"Artifact is missing active integrity hash(es): {String.Join(", ", missingActiveHashes)}.",
                    .ProposedAction = "Recompute hashes from the retained file.",
                    .RiskLevel = "Low",
                    .MutatesCatalog = True,
                    .TouchesRetainedFiles = False
                })
            End If

            If storedFileExists AndAlso pathIsInsideVault Then
                If verifyHashes Then
                    AddHashMismatchFinding(report, artifact, integrityService, normalizedActiveHashes, hashVerificationLimitBytes)
                Else
                    AddDeferredHashVerificationFinding(report, artifact, normalizedActiveHashes, hashVerificationLimitBytes)
                End If
            End If

            If thumbService.IsGeneratedThumbnailMissing(artifact, vaultRootPath) Then
                report.Findings.Add(New VaultHealthFinding With {
                    .FindingType = "Missing thumbnail",
                    .Subject = artifact.Name,
                    .Detail = "Catalog references a generated thumbnail file that is not present.",
                    .ProposedAction = "Regenerate thumbnail from the retained file.",
                    .RiskLevel = "Low",
                    .MutatesCatalog = True,
                    .TouchesRetainedFiles = False
                })
            End If

            If Not String.IsNullOrWhiteSpace(artifact.ExtractedTextRelativePath) Then
                Dim extractedPath = ResolveVaultRelativePath(vaultRootPath, artifact.ExtractedTextRelativePath)
                If String.IsNullOrWhiteSpace(extractedPath) OrElse Not File.Exists(extractedPath) Then
                    report.Findings.Add(New VaultHealthFinding With {
                        .FindingType = "Missing extracted text",
                        .Subject = artifact.Name,
                        .Detail = "Catalog references an extracted-text index that is not present.",
                        .ProposedAction = "Re-extract text if the retained file format is supported.",
                        .RiskLevel = "Medium",
                        .MutatesCatalog = True,
                        .TouchesRetainedFiles = False
                    })
                End If
            End If
        Next

        For Each hashId In activeHashIds
            Dim duplicateGroups = artifactList.
                Where(Function(artifact) artifact IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(HashRegistry.GetArtifactHashValue(artifact, hashId))).
                GroupBy(Function(artifact) HashRegistry.GetArtifactHashValue(artifact, hashId), StringComparer.OrdinalIgnoreCase).
                Where(Function(group) group.Count() > 1)

            For Each duplicateGroup In duplicateGroups
                report.Findings.Add(New VaultHealthFinding With {
                    .FindingType = "Duplicate hash",
                    .Subject = $"{hashId}: {duplicateGroup.Key}",
                    .Detail = String.Join(", ", duplicateGroup.Select(Function(artifact) artifact.Name).Take(5)),
                    .ProposedAction = "Review duplicate candidates; keep or remove only by operator decision.",
                    .RiskLevel = "Low",
                    .MutatesCatalog = False,
                    .TouchesRetainedFiles = False
                })
            Next
        Next

        AddOrphanGeneratedAssetFindings(report, artifactList, vaultRootPath, "thumbnails", Function(artifact) artifact.ThumbnailRelativePath, "Orphan thumbnail", "Generated thumbnail file is not referenced by any catalog artifact.", "Review generated asset; cleanup should require operator approval.")
        AddOrphanGeneratedAssetFindings(report, artifactList, vaultRootPath, "extracted-text", Function(artifact) artifact.ExtractedTextRelativePath, "Stale extracted text", "Extracted-text index is not referenced by any catalog artifact.", "Review generated index; cleanup should require operator approval.")

        Return report
    End Function

    Private Shared Sub AddDeferredHashVerificationFinding(report As VaultHealthReport, artifact As ArtifactModel, activeHashes As String, hashVerificationLimitBytes As Long)
        Dim comparableHashIds = HashRegistry.ParseActiveHashIds(activeHashes).
            Where(Function(hashId) Not String.IsNullOrWhiteSpace(HashRegistry.GetArtifactHashValue(artifact, hashId))).
            ToList()

        If comparableHashIds.Count = 0 Then
            Return
        End If

        Dim fileSize = GetRetainedFileSizeForHashPolicy(artifact)
        If fileSize <= hashVerificationLimitBytes Then
            Return
        End If

        report.Findings.Add(New VaultHealthFinding With {
            .FindingType = "Hash verification deferred",
            .Subject = artifact.Name,
            .Detail = $"Retained file is {FormatSize(fileSize)}, above the automatic verification limit of {FormatSize(hashVerificationLimitBytes)}.",
            .ProposedAction = "Use Verify small hashes or selected hash repair when you want Filing Cabinet to read and verify retained file bytes.",
            .RiskLevel = "Low",
            .MutatesCatalog = False,
            .TouchesRetainedFiles = False
        })
    End Sub

    Private Shared Sub AddHashMismatchFinding(report As VaultHealthReport, artifact As ArtifactModel, hashService As HashService, activeHashes As String, hashVerificationLimitBytes As Long)
        Try
            Dim activeList = HashRegistry.ParseActiveHashIds(activeHashes)
            Dim comparableHashIds = activeList.
                Where(Function(hashId) Not String.IsNullOrWhiteSpace(HashRegistry.GetArtifactHashValue(artifact, hashId))).
                ToList()

            If comparableHashIds.Count = 0 Then
                Return
            End If

            Dim fileSize = GetRetainedFileSizeForHashPolicy(artifact)
            If fileSize > hashVerificationLimitBytes Then
                report.Findings.Add(New VaultHealthFinding With {
                    .FindingType = "Hash verification deferred",
                    .Subject = artifact.Name,
                    .Detail = $"Retained file is {FormatSize(fileSize)}, above the automatic verification limit of {FormatSize(hashVerificationLimitBytes)}.",
                    .ProposedAction = "Use Hash Check or a selected hash repair when you want to read and verify this large retained file.",
                    .RiskLevel = "Low",
                    .MutatesCatalog = False,
                    .TouchesRetainedFiles = False
                })
                Return
            End If

            Dim hashes = hashService.ComputeHashes(artifact.Path, activeHashes)
            Dim mismatchParts As New List(Of String)
            For Each hashId In comparableHashIds
                Dim catalogValue = HashRegistry.GetArtifactHashValue(artifact, hashId)
                If Not String.Equals(catalogValue, HashRegistry.GetHashValue(hashes, hashId), StringComparison.OrdinalIgnoreCase) Then
                    mismatchParts.Add(hashId)
                End If
            Next

            If mismatchParts.Count = 0 Then
                Return
            End If

            report.Findings.Add(New VaultHealthFinding With {
                .FindingType = "Hash mismatch",
                .Subject = artifact.Name,
                .Detail = $"{String.Join(" and ", mismatchParts)} did not match the retained file.",
                .ProposedAction = "Review the retained file before deciding whether to replace the catalog hash, restore a backup, or quarantine the artifact.",
                .RiskLevel = "High",
                .MutatesCatalog = False,
                .TouchesRetainedFiles = False
            })
        Catch ex As Exception
            report.Findings.Add(New VaultHealthFinding With {
                .FindingType = "Hash verification failed",
                .Subject = artifact.Name,
                .Detail = $"Hash verification could not read the retained file: {ex.Message}",
                .ProposedAction = "Review file permissions and storage health before retrying analysis.",
                .RiskLevel = "Medium",
                .MutatesCatalog = False,
                .TouchesRetainedFiles = False
            })
        End Try
    End Sub

    Private Shared Function GetRetainedFileSizeForHashPolicy(artifact As ArtifactModel) As Long
        If artifact Is Nothing Then
            Return 0
        End If

        If artifact.SizeBytes > 0 Then
            Return artifact.SizeBytes
        End If

        Try
            If Not String.IsNullOrWhiteSpace(artifact.Path) AndAlso File.Exists(artifact.Path) Then
                Return New FileInfo(artifact.Path).Length
            End If
        Catch
        End Try

        Return 0
    End Function

    Private Shared Sub AddIncompleteMetadataFinding(report As VaultHealthReport, artifact As ArtifactModel)
        If Not HasIncompleteMetadata(artifact) Then
            Return
        End If

        Dim missingFields As New List(Of String)

        If String.IsNullOrWhiteSpace(artifact.Id) Then
            missingFields.Add("id")
        End If

        If String.IsNullOrWhiteSpace(artifact.Name) Then
            missingFields.Add("name")
        End If

        If String.IsNullOrWhiteSpace(artifact.Type) Then
            missingFields.Add("type")
        End If

        If String.IsNullOrWhiteSpace(artifact.TypeFamily) Then
            missingFields.Add("type family")
        End If

        If String.IsNullOrWhiteSpace(artifact.Category) Then
            missingFields.Add("category")
        End If

        If String.IsNullOrWhiteSpace(artifact.Size) OrElse artifact.SizeBytes <= 0 Then
            missingFields.Add("size")
        End If

        If String.IsNullOrWhiteSpace(artifact.DateModified) Then
            missingFields.Add("date modified")
        End If

        If String.IsNullOrWhiteSpace(artifact.RelativePath) Then
            missingFields.Add("relative path")
        End If

        If String.IsNullOrWhiteSpace(artifact.Created) Then
            missingFields.Add("created")
        End If

        If String.IsNullOrWhiteSpace(artifact.IngestedAt) Then
            missingFields.Add("ingested at")
        End If

        If String.IsNullOrWhiteSpace(artifact.HashStatus) Then
            missingFields.Add("hash status")
        End If

        If String.IsNullOrWhiteSpace(artifact.ThumbnailStatus) Then
            missingFields.Add("thumbnail status")
        End If

        If String.IsNullOrWhiteSpace(artifact.ExtractedTextStatus) Then
            missingFields.Add("extracted text status")
        End If

        If missingFields.Count = 0 Then
            Return
        End If

        Dim subject = If(String.IsNullOrWhiteSpace(artifact.Name), If(String.IsNullOrWhiteSpace(artifact.Path), "(unnamed artifact)", Path.GetFileName(artifact.Path)), artifact.Name)
        report.Findings.Add(New VaultHealthFinding With {
            .FindingType = "Incomplete metadata",
            .Subject = subject,
            .Detail = "Catalog entry is missing: " & String.Join(", ", missingFields),
            .ProposedAction = "Review interrupted ingest state; repair safe generated metadata separately and preserve operator-authored context.",
            .RiskLevel = "Medium",
            .MutatesCatalog = False,
            .TouchesRetainedFiles = False
        })
    End Sub

    Private Shared Function HasIncompleteMetadata(artifact As ArtifactModel) As Boolean
        If artifact Is Nothing Then
            Return False
        End If

        Return String.IsNullOrWhiteSpace(artifact.Id) OrElse
            String.IsNullOrWhiteSpace(artifact.Name) OrElse
            String.IsNullOrWhiteSpace(artifact.Type) OrElse
            String.IsNullOrWhiteSpace(artifact.TypeFamily) OrElse
            String.IsNullOrWhiteSpace(artifact.Category) OrElse
            String.IsNullOrWhiteSpace(artifact.Size) OrElse artifact.SizeBytes <= 0 OrElse
            String.IsNullOrWhiteSpace(artifact.DateModified) OrElse
            String.IsNullOrWhiteSpace(artifact.RelativePath) OrElse
            String.IsNullOrWhiteSpace(artifact.Created) OrElse
            String.IsNullOrWhiteSpace(artifact.IngestedAt) OrElse
            String.IsNullOrWhiteSpace(artifact.HashStatus) OrElse
            String.IsNullOrWhiteSpace(artifact.ThumbnailStatus) OrElse
            String.IsNullOrWhiteSpace(artifact.ExtractedTextStatus)
    End Function

    Private Shared Function HasRebindCandidate(artifact As ArtifactModel, vaultRootPath As String) As Boolean
        If artifact Is Nothing OrElse String.IsNullOrWhiteSpace(vaultRootPath) OrElse String.IsNullOrWhiteSpace(artifact.RelativePath) Then
            Return False
        End If

        Dim resolvedPath = ResolveVaultRelativePath(vaultRootPath, artifact.RelativePath)
        Return Not String.IsNullOrWhiteSpace(resolvedPath) AndAlso
            IsPathInsideDirectory(resolvedPath, vaultRootPath) AndAlso
            File.Exists(resolvedPath)
    End Function

    Private Shared Function IsPathInsideDirectory(candidatePath As String, directoryPath As String) As Boolean
        If String.IsNullOrWhiteSpace(candidatePath) OrElse String.IsNullOrWhiteSpace(directoryPath) Then
            Return True
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
            Return True
        End Try
    End Function

    Private Shared Sub AddOrphanGeneratedAssetFindings(report As VaultHealthReport, artifacts As IEnumerable(Of ArtifactModel), vaultRootPath As String, folderName As String, referenceSelector As Func(Of ArtifactModel, String), findingType As String, detail As String, proposedAction As String)
        If report Is Nothing OrElse String.IsNullOrWhiteSpace(vaultRootPath) Then
            Return
        End If

        Dim generatedRoot = Path.Combine(vaultRootPath, folderName)
        If Not Directory.Exists(generatedRoot) Then
            Return
        End If

        Dim referencedPaths = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each artifact In If(artifacts, Enumerable.Empty(Of ArtifactModel)())
            If artifact Is Nothing Then
                Continue For
            End If

            Dim reference = referenceSelector(artifact)
            Dim absolutePath = ResolveVaultRelativePath(vaultRootPath, reference)
            If Not String.IsNullOrWhiteSpace(absolutePath) Then
                referencedPaths.Add(Path.GetFullPath(absolutePath))
            End If
        Next

        For Each generatedPath In Directory.EnumerateFiles(generatedRoot, "*", SearchOption.AllDirectories)
            Dim fullPath = Path.GetFullPath(generatedPath)
            If referencedPaths.Contains(fullPath) Then
                Continue For
            End If

            report.Findings.Add(New VaultHealthFinding With {
                .FindingType = findingType,
                .Subject = Path.GetRelativePath(vaultRootPath, generatedPath),
                .Detail = detail,
                .ProposedAction = proposedAction,
                .RiskLevel = "Low",
                .MutatesCatalog = False,
                .TouchesRetainedFiles = False
            })
        Next
    End Sub

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

    Private Shared Function BuildRepairStatus(report As VaultRepairReport) As String
        Dim status = report.Summary
        Dim detail = report.Detail

        If Not String.IsNullOrWhiteSpace(detail) Then
            status &= $"  |  {detail}"
        End If

        Return status
    End Function

    Private Shared Function FindOrphanStoredFiles(artifacts As IEnumerable(Of ArtifactModel), vaultRootPath As String) As IEnumerable(Of String)
        Dim itemsRoot = Path.Combine(vaultRootPath, "items")
        If Not Directory.Exists(itemsRoot) Then
            Return Enumerable.Empty(Of String)()
        End If

        Dim knownPaths = New HashSet(Of String)(
            If(artifacts, Enumerable.Empty(Of ArtifactModel)()).
                Where(Function(a) Not String.IsNullOrWhiteSpace(a.Path)).
                Select(Function(a) Path.GetFullPath(a.Path)),
            StringComparer.OrdinalIgnoreCase)

        Return Directory.
            EnumerateFiles(itemsRoot, "*", SearchOption.AllDirectories).
            Where(Function(candidatePath) Not knownPaths.Contains(Path.GetFullPath(candidatePath))).
            ToList()
    End Function

    Private Shared Function BuildUniquePath(directoryPath As String, fileName As String) As String
        Dim baseName = Path.GetFileNameWithoutExtension(fileName)
        Dim extension = Path.GetExtension(fileName)
        Dim candidate = Path.Combine(directoryPath, fileName)
        Dim index = 1

        While File.Exists(candidate)
            candidate = Path.Combine(directoryPath, $"{baseName}-{index}{extension}")
            index += 1
        End While

        Return candidate
    End Function

    Private Class VaultMaintenanceResult
        Public Property HealthReport As VaultHealthReport
        Public Property RepairReport As VaultRepairReport
        Public Property ThumbnailUpdates As New List(Of ThumbnailRepairUpdate)
        Public Property AdoptedArtifacts As New List(Of ArtifactModel)
    End Class

    Private Class ThumbnailRepairUpdate
        Public Property Artifact As ArtifactModel
        Public Property RelativePath As String = ""
        Public Property Status As String = ThumbnailService.NotApplicableStatus
    End Class

    Private Class RepairApplicationWorkItem
        Public Property Candidate As RepairCandidate
        Public Property Artifact As ArtifactModel
    End Class

    Private Class RepairApplicationResult
        Public Property Candidate As RepairCandidate
        Public Property Artifact As ArtifactModel
        Public Property Succeeded As Boolean
        Public Property Skipped As Boolean
        Public Property Detail As String = ""
        Public Property ThumbnailRelativePath As String = ""
        Public Property ThumbnailStatus As String = ThumbnailService.NotApplicableStatus
        Public Property Hashes As FileHashes
        Public Property HashStatus As String = ""
        Public Property ExtractedTextRelativePath As String = ""
        Public Property ExtractedTextStatus As String = ""
        Public Property ResolvedPath As String = ""
    End Class

    Private Class SameSourceBatchCandidate
        Public Property Artifact As ArtifactModel
        Public Property DirectoryKey As String = ""
        Public Property IngestedAt As DateTime
    End Class

    Private Shared Function FormatSize(bytes As Long) As String
        Dim units = {"B", "KB", "MB", "GB", "TB"}
        Dim value = CDbl(Math.Max(0, bytes))
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

    Private Shared Sub RunOnUiThread(action As Action)
        Dim dispatcher = Application.Current?.Dispatcher

        If dispatcher IsNot Nothing AndAlso Not dispatcher.CheckAccess() Then
            dispatcher.Invoke(action)
            Return
        End If

        action()
    End Sub

    Private Shared Sub ReplaceCollection(Of T)(target As ObservableCollection(Of T), source As IEnumerable(Of T))
        target.Clear()

        If source Is Nothing Then
            Return
        End If

        For Each item In source
            target.Add(item)
        Next
    End Sub

    Private Sub OnPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class

