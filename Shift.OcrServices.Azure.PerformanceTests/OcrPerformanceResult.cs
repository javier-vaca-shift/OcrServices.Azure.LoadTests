namespace Shift.OcrServices.Azure.Benchmark;

public class OcrPerformanceResult
{
    public int Requests { get; init; }
    public int Threads { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public double TotalSeconds { get; init; }
    public double AverageSecondsPerRequest { get; init; }
    public int PagesPerHour { get; init; }
    public int TotalPages { get; init; }
    public double AverageSecondsPerPage { get; init; }
    public int SuccessfulRequests { get; init; }
}