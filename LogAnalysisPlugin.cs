using Azure;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

public sealed class LogAnalysisPlugin
{
    private readonly LogsQueryClient _logsClient;
    private readonly string _workspaceId;

    public LogAnalysisPlugin()
    {
        var workspaceId = "xxx";
        _workspaceId = workspaceId
            ?? throw new ArgumentNullException(nameof(workspaceId));

        _logsClient = new LogsQueryClient(new DefaultAzureCredential());
    }

    // =====================================================
    // KERNEL FUNCTIONS (THIS IS THE IMPORTANT PART)
    // =====================================================

    [KernelFunction]
    [Description("Analyze logs for a single project and return aggregated evidence for root cause analysis.")]
    public async Task<string> AnalyzeProjectAsync(
        [Description("Project ID to analyze")] string projectId,
        [Description("Lookback window in days")] int lookbackDays)
    {
        var kql = BuildProjectAnalysisKql(projectId);

        Response<LogsQueryResult> response =
            await _logsClient.QueryWorkspaceAsync(
                _workspaceId,
                kql,
                new QueryTimeRange(TimeSpan.FromDays(lookbackDays)));

        return FormatProjectResult(projectId, response.Value);
    }

    [KernelFunction]
    [Description("Analyze overall system health across all projects.")]
    public async Task<string> AnalyzeSystemHealthAsync(
        [Description("Lookback window in days")] int lookbackDays)
    {
        var kql = BuildSystemHealthKql();

        Response<LogsQueryResult> response =
            await _logsClient.QueryWorkspaceAsync(
                _workspaceId,
                kql,
                new QueryTimeRange(TimeSpan.FromDays(lookbackDays)));

        return FormatSystemHealthResult(response.Value);
    }

    // =====================================================
    // FORMATTERS (LLM-READY)
    // =====================================================

    private static string FormatProjectResult(
        string projectId,
        LogsQueryResult result)
    {
        var table = result.Table;

        if (table.Rows.Count == 0)
        {
            return $"ProjectId: {projectId}\nNo log data found.";
        }

        var row = table.Rows[0];
        var sb = new StringBuilder();

        sb.AppendLine($"ProjectId: {projectId}");
        sb.AppendLine($"AssetTypes: {row["AssetTypes"]}");
        sb.AppendLine($"Parsers: {row["Parsers"]}");
        sb.AppendLine($"ParserVersions: {row["ParserVersions"]}");
        sb.AppendLine($"TraceCount: {row["TraceCount"]}");
        sb.AppendLine($"WarningCount: {row["WarningCount"]}");
        sb.AppendLine($"ErrorCount: {row["ErrorCount"]}");
        sb.AppendLine($"ExceptionCount: {row["ExceptionCount"]}");
        sb.AppendLine($"ExceptionTypes: {row["ExceptionTypes"]}");
        sb.AppendLine($"StartTime: {row["StartTime"]}");
        sb.AppendLine($"EndTime: {row["EndTime"]}");

        return sb.ToString();
    }

    private static string FormatSystemHealthResult(
        LogsQueryResult result)
    {
        var table = result.Table;

        if (table.Rows.Count == 0)
        {
            return "No system health data found.";
        }

        var row = table.Rows[0];
        var sb = new StringBuilder();

        sb.AppendLine("SYSTEM HEALTH SUMMARY");
        sb.AppendLine($"TotalProjects: {row["TotalProjects"]}");
        sb.AppendLine($"SucceededProjects: {row["SucceededProjects"]}");
        sb.AppendLine($"FailedProjects: {row["FailedProjects"]}");

        return sb.ToString();
    }

    // =====================================================
    // KQL
    // =====================================================

    private static string BuildProjectAnalysisKql(string projectId)
    {
        return $@"AppTraces

";
    }

    private static string BuildSystemHealthKql()
    {
        return @"
AppTraces
| extend ProjectId = tostring(Properties.ProjectId)
| where isnotempty(ProjectId)
| summarize
    ErrorCount = countif(severityLevel in ('Error','Critical'))
by ProjectId
| extend Status = iff(ErrorCount > 0, 'Failed', 'Succeeded')
| summarize
    TotalProjects = count(),
    FailedProjects = countif(Status == 'Failed'),
    SucceededProjects = countif(Status == 'Succeeded')
";
    }
}
