namespace Nabrh.Bootstrapper.Models
{
    public static class InstallerExitCode
    {
        public const int Success = 0;
        public const int UserCancelled = 1602;
        public const int FatalError = 1603;
        public const int PrerequisiteFailed = 1603;
        public const int AnotherInstanceRunning = 55;
        public const int AdministratorApprovalRequired = 5;
        public const int RebootRequired = 3010;
    }
}
