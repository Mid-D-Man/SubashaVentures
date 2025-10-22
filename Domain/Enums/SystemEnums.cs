// Domain/Enums/SystemEnums.cs
namespace SubashaVentures.Domain.Enums
{
    public enum NotificationType
    {
        Success,
        Error,
        Info,
        Warning
    }

    public enum NotificationPosition
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft
    }

    public enum InfoType
    {
        OfflineSync,
        TemporalKeyRefresh,
        RefreshInterval,
        SecurityFeatures,
        DeviceGuidCheck,
        OfflineStorage,
        SyncInterval
    }

    public enum AdvancedSecurityFeatures
    {
        Default = 0,
        DeviceGuidCheck = 1
    }

    public enum TransitionType
    {
        SessionStart,
        SessionEnd,
        SemesterStart,
        SemesterEnd
    }

    public enum OverlapResolutionAction
    {
        ExtendOldSession,
        TruncateOldSession,
        DelayNewSession,
        ManualReview
    }

    public enum SystemHealthStatus
    {
        Healthy,
        Warning,
        Error,
        Critical
    }

    public enum IssueType
    {
        MissingSession,
        OverlappingSessions,
        InvalidDates,
        OrphanedSemester,
        InconsistentData,
        MissingData
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public enum SessionEndReason
    {
        NaturalExpiry,
        ManualTermination,
        SystemCleanup,
        ErrorCorrection
    }

    public enum SemesterEndReason
    {
        NaturalExpiry,
        ManualTermination,
        SystemCleanup,
        ErrorCorrection
    }

    public enum SessionStartReason
    {
        ScheduledStart,
        ManualActivation,
        SystemTransition,
        ErrorCorrection
    }

    public enum SemesterStartReason
    {
        ScheduledStart,
        ManualActivation,
        SystemTransition,
        ErrorCorrection
    }

    public enum ModalType
    {
        CreateSession,
        CreateSemester
    }

    public enum LoadingOperation
    {
        LoadingCourses,
        SavingCourse,
        UpdatingCourse,
        DeletingCourse
    }
}