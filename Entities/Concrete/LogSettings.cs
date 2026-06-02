namespace Entities.Concrete
{
    public class LogSettings
    {
        public bool LogReadActions { get; set; } = false;
        public int RetentionDays { get; set; } = 90;
        public LogLevelSettings LogLevel { get; set; } = new();
    }

    public class LogLevelSettings
    {
        public bool Create { get; set; } = true;
        public bool Update { get; set; } = true;
        public bool Delete { get; set; } = true;
        public bool Read { get; set; } = false;
        public bool Login { get; set; } = true;
        public bool Logout { get; set; } = true;
    }
}