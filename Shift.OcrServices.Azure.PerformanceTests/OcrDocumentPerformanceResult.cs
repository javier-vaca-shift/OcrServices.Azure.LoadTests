using Shift.Ocr.Model.OcrServicesApi;

namespace Shift.OcrServices.Azure.Benchmark;

public class OcrDocumentPerformanceResult
{
    public TimeSpan ExecutionTime { get; init; }
    public DocumentStatus StatusCode { get; init; }
    public int PageCount { get; set; }
    public string ErrorMessage { get; init; }

    public OcrDocumentPerformanceResult(TimeSpan executionTime, DocumentStatus documentStatus, string errorMessage, int pageCount)
    {
        ExecutionTime = executionTime;
        StatusCode = documentStatus;
        ErrorMessage = errorMessage;
        PageCount = pageCount;
    }
}
