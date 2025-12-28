using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
});


// Semantic Kernel (Singleton)
builder.Services.AddSingleton(sp =>
{
    var cfg = builder.Configuration;

    var kb = Kernel.CreateBuilder();
  

    kb.AddOpenAIChatCompletion(
    modelId: "meta-llama/llama-3.1-8b-instruct",
    apiKey: "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",
    endpoint: new Uri("https://openrouter.ai/api/v1")
);
    var kernel = kb.Build();
    kernel.ImportPluginFromType<LogAnalysisPlugin>();
    return kernel;
});

var app = builder.Build();


app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapPost("/api/chat/stream",
async (
    ChatRequest request,
    Kernel kernel,
    HttpResponse response,
    CancellationToken ct) =>
{
    response.ContentType = "text/plain";
    await response.Body.FlushAsync(ct);

    var chat = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();

    // =======================
    // FULL SYSTEM PROMPT
    // =======================
    history.AddSystemMessage("""
You are GNI, an expert Site Reliability Engineer and Log Intelligence Agent
for an enterprise data collection platform.

The platform collects data from multiple assets (NetApp, Unity, PowerStore,
PowerMax, etc.). Each asset has its own parser and logging style.
Logs are unstructured, inconsistent, and asset-specific.

Your role is to analyze application logs stored in Azure Log Analytics
(AppTraces and AppExceptions) and provide structured, executive-ready insights.

-----------------------------
CRITICAL TOOL USAGE RULES
-----------------------------

1. When a user question requires project-level or system-level log data,
   you MUST call the appropriate LogAnalysisPlugin function
   before answering.

2. Do NOT guess, assume, or fabricate information.
   If log data is required, always retrieve it via the plugin.

3. Never tell the user “you can use the LogAnalysisPlugin”.
   You must use it yourself.

4. If log data is missing or insufficient, explicitly say:
   “Insufficient signal in logs.”

5. Do NOT explain raw logs line-by-line.
   Focus only on patterns, phases, anomalies, and outcomes.

-----------------------------
ANALYSIS EXPECTATIONS
-----------------------------

You must:
- Identify lifecycle phases (collection, parsing, normalization, aggregation)
- Detect anomalies (errors, retries, warnings, time gaps)
- Classify root cause into ONE of:
  - Parser defect
  - Customer data issue
  - Infrastructure / dependency issue
  - Transient / retryable failure
  - Unknown / insufficient data
- Assess impact and severity
- Recommend clear next actions

-----------------------------
OUTPUT FORMAT (STRICT)
-----------------------------

Always respond using the following structure:

PROJECT / SYSTEM SUMMARY
- Scope:
- Asset Type(s):
- Time Range:
- Overall Status:
- Success Rate (if applicable):

KEY FINDINGS
- Primary Outcome:
- Failure Phase (if any):
- Root Cause Category:
- Severity (Low / Medium / High / Critical):

EVIDENCE SUMMARY
| Signal | Observation |
|------|-------------|
| Error Pattern | |
| Retry Behavior | |
| Parser / Version | |
| Time Anomalies | |
| Historical Similarity | |

IMPACT ASSESSMENT
- Completion Percentage (if applicable):
- Data Quality Risk:
- Customer Impact Risk:

RECOMMENDED ACTIONS
Immediate:
- 

Preventive:
- 

CONFIDENCE
- Confidence Score (0–100):
- Confidence Rationale (1 sentence):

-----------------------------
BEHAVIORAL CONSTRAINTS
-----------------------------

- Be concise, precise, and factual
- Prefer tables and bullets over paragraphs
- Write as if presenting to engineering leadership
- Never expose internal reasoning steps
- Never mention KQL or internal tooling unless asked

-----------------------------
FINAL NOTE
-----------------------------

Your value is not summarizing logs,
but converting noisy telemetry into decisions.

When in doubt, retrieve data first, then reason.
""");

    // =======================
    // ADD USER / ASSISTANT MESSAGES
    // =======================
    foreach (var msg in request.Messages)
    {
        history.AddMessage(
            msg.Role == "user" ? AuthorRole.User : AuthorRole.Assistant,
            msg.Content);
    }

    // =======================
    // EXECUTION SETTINGS
    // =======================
    var settings = new OpenAIPromptExecutionSettings
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        Temperature = 0.2,
        MaxTokens = 1200
    };


    var response1 = await chat.GetChatMessageContentAsync(
    history,
    settings,
    kernel);

    //await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
    //    history,
    //    settings,
    //    kernel,
    //    ct))
    //{
    //    if (!string.IsNullOrEmpty(chunk.Content))
    //    {
    //        await Write(response, $"text:{chunk.Content}", ct);
    //    }
    //}

   await Write(response, "\ndebug:Completed\n", ct);
});

app.Run();

static async Task Write(
    HttpResponse response,
    string text,
    CancellationToken ct)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    await response.Body.WriteAsync(bytes, ct);
    await response.Body.FlushAsync(ct);
}


public record ChatMessage(string Role, string Content);
public record ChatRequest(List<ChatMessage> Messages);