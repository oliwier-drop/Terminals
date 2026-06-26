// Fork display names — keep in sync with Product.wxs and Common.AssemblyInfo.cs
namespace Terminals
{
    internal static class ForkBranding
    {
        public const string Name = "Terminals (SSH.NET fork)";
        public const string Version = "1.0.5";
        public const string DisplayName = Name + " " + Version;

        public const string MaintainerName = "Oliwier Drop";
        public const string MaintainerProfileUrl = "https://github.com/oliwier-drop";

        public const string RepositoryUrl = "https://github.com/oliwier-drop/Terminals";
        public const string ReleasesPageUrl = RepositoryUrl + "/releases";
        public const string ReleasesApiUrl = "https://api.github.com/repos/oliwier-drop/Terminals/releases";
        public const string IssuesPageUrl = RepositoryUrl + "/issues";
        public const string LicensePageUrl = RepositoryUrl + "/blob/master/LICENSE.md";
        public const string IconRawUrl = "https://raw.githubusercontent.com/oliwier-drop/Terminals/master/Source/Terminals/Resources/terminalsicon.png";
    }
}
