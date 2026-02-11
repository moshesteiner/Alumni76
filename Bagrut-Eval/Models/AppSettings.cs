namespace Bagrut_Eval.Models
{
    public class AppSettings
    {
        public string? Version { get; set; }
        public string? Database { get; set; }
        public bool Release { get; set; }
        public string? BuildNumber { get; set; }
        public string? GitCommitHash { get; set; }
    }
}
