public class UtcTimeProvider : ITimeProvider
{
    // This is the implementation that guarantees UTC time
    public DateTime Now => DateTime.UtcNow;
}