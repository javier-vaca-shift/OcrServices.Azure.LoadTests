// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Shift.Ocr.Azure.Clients;
using Shift.Ocr.Client.Model;
using Shift.Ocr.Model.OcrClientApi;
using Shift.OcrServices.Azure.Benchmark;

const string url = "http://eaus-azure-ocr-service-preprod.corp.shift-technology.com/";
const string directory = "\\\\SH-AZ-EAUS-PST1\\EAUS-CA\\Historical\\Documents\\ob-extract1-repackage\\";
int[] numThreads = {8};
int[] numDocuments = {1000};
var paths = ReadPaths("/Data/files.json");

var results = new List<OcrPerformanceResult>();
var errorMessages = new List<string>();

foreach (var numThread in numThreads)
{
    foreach (var numDocument in numDocuments)
    {
        var executionResults = 
            await ExecutePerformanceTest(numThread, paths.GetRange(0, numDocument), url, directory);
        results.Add(BuildResults(executionResults.results, numThread, numDocument));
        errorMessages.AddRange(executionResults.errorMessages);
        var innerErrorMessages = executionResults.results.Select(p => p.ErrorMessage)
            .Where(s => !string.IsNullOrEmpty(s));
        Console.WriteLine($"There were: {innerErrorMessages.Count()} errors.");
        errorMessages.AddRange(innerErrorMessages);
    }
}


WriteOcrPerformanceResultToCsv(results);
WriteErrorsToCsv(errorMessages);

List<string>? ReadPaths(string directory, [CallerFilePath] string path = null)
{
    var pathToJsonFile = Path.GetDirectoryName(path) + directory;
    using var reader = new StreamReader(pathToJsonFile);
    var jsonString = reader.ReadToEnd();
    var list = JsonConvert.DeserializeObject<List<string>>(jsonString);

    return list;
}

async Task<(List<OcrDocumentPerformanceResult> results, List<string> errorMessages)> ExecutePerformanceTest(int numThreads, List<string>? paths, string url, string directory)
{
    var tasks = new List<Task>();
    var semaphore = new SemaphoreSlim(numThreads);
    var results = new List<OcrDocumentPerformanceResult>();
    var errorMessages = new List<string>();
    var ocrRequestParameters = new OcrRequestParameters() { ForceProcessing = true };
    
    for (var i = 0; i < numThreads; i++)
    {
        tasks.Add(Task.Run(async () =>
        {
            while (paths.Count > 0)
            {
                await semaphore.WaitAsync();

                var azureClient = new AzureSyncClient(new HttpClient() {BaseAddress = new Uri(url)},
                    new PollingOcrClientConfiguration() {RetryTime = TimeSpan.FromSeconds(5)});
                string filePath = null;
                lock (paths)
                {
                    if (paths.Count > 0)
                    {
                        filePath = paths[0];
                        paths.RemoveAt(0);
                    }
                }

                if (filePath == null) continue;
                try
                {
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();
                    var response =
                        await azureClient.ReadDocumentByPath(directory + filePath, ocrRequestParameters);
                    stopWatch.Stop();
                        
                    results.Add(new OcrDocumentPerformanceResult(TimeSpan.FromMilliseconds(stopWatch.ElapsedMilliseconds),
                        response.Status, response.ErrorMessage, response.Result.Count));
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Error reading {filePath}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }));
    }
    await Task.WhenAll(tasks);
    return (results, errorMessages);
}

void WriteOcrPerformanceResultToCsv(IEnumerable<OcrPerformanceResult> results, [CallerFilePath] string path = null)
{
    using var writer = new StreamWriter(Path.GetDirectoryName(path) + $"/result-{DateTime.Now.ToString("yyyyMMdd-HHmmss")}.csv");
    writer.WriteLine("Requests,SuccessfulRequests,Threads,ElapsedTime,TotalSeconds,AverageSecondsPerRequest,PagesPerHour,TotalPages,AverageSecondsPerPage");

    foreach (var result in results)
    {
        var elapsedTime = result.ElapsedTime.ToString(@"hh\:mm\:ss");
        writer.WriteLine($"{result.Requests},{result.SuccessfulRequests},{result.Threads},{elapsedTime},{result.TotalSeconds}," + 
                         $"{result.AverageSecondsPerRequest},{result.PagesPerHour}," +
                         $"{result.TotalPages},{result.AverageSecondsPerPage}");
    }
}

void WriteErrorsToCsv(List<string> errors, [CallerFilePath] string path = null)
{
    using var writer = new StreamWriter(Path.GetDirectoryName(path) + $"/errors-{DateTime.Now.ToString("yyyyMMdd-HHmmss")}.csv");
    foreach (var error in errors)        
    {
        writer.WriteLine(error);
    }
}

OcrPerformanceResult BuildResults(List<OcrDocumentPerformanceResult> ocrDocumentPerformanceResults, int numThreads, int requests)
{
    var totalElapsedSeconds = ocrDocumentPerformanceResults.Select(p => p.ExecutionTime.TotalSeconds).Sum();
    var totalPages = ocrDocumentPerformanceResults.Select(p => p.PageCount).Sum();
    var ocrPerformanceResult = new OcrPerformanceResult()
    {
        Threads = numThreads,
        TotalSeconds = totalElapsedSeconds,
        ElapsedTime = TimeSpan.FromSeconds(totalElapsedSeconds),
        Requests = requests,
        SuccessfulRequests = ocrDocumentPerformanceResults.Count,
        AverageSecondsPerRequest = totalElapsedSeconds / ocrDocumentPerformanceResults.Count,
        TotalPages = totalPages,
        AverageSecondsPerPage = totalElapsedSeconds/totalPages,
        PagesPerHour = (int) (totalPages / totalElapsedSeconds * 3600),
    };
    return ocrPerformanceResult;
}