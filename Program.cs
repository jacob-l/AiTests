using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;

Console.WriteLine("Starting the application!");

var builder = Kernel.CreateBuilder();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");
}

builder.AddOpenAIChatCompletion(
    modelId: "gpt-4.1-mini",
    apiKey: apiKey
);

Kernel kernel = builder.Build();

static async Task<IMcpClient> GetMCPClientForPlaywright()
{
    McpClientOptions options = new()
    {
        ClientInfo = new() { Name = "Playwright", Version = "1.0.0" }
    };

    var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "Playwright",
        Command = "npx",
        Arguments = ["-y", "@playwright/mcp@latest"],
    });

    var mcpClient = await McpClientFactory.CreateAsync(
        clientTransport,
        options
    );

    return mcpClient;
}

var mcpClient = await GetMCPClientForPlaywright();

var tools = await mcpClient.ListToolsAsync();

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
kernel.Plugins.AddFromFunctions("Browser", tools.Select(aiFunction => aiFunction.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Enable automatic function calling
var executionSettings = new OpenAIPromptExecutionSettings
{
    Temperature = 0,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var folderPath = "TestCases";

if (!Directory.Exists(folderPath))
{
    Console.WriteLine($"Folder '{folderPath}' does not exist.");
    return;
}

string[] files = Directory.GetFiles(folderPath).Where(n => n.EndsWith(".md")).ToArray();

if (files.Length == 0)
{
    Console.WriteLine($"No test cases found in the '{folderPath}' folder.");
    Console.WriteLine("Please put your test case files into this folder and try again.");
    return;
}

foreach (string file in files)
{
    try
    {
        string content = File.ReadAllText(file);
        var prompt = $"You are a QA expert. You should perform the test cases and provide the report with result. Test Cases: {Environment.NewLine}{content}";

        var result = await kernel.InvokePromptAsync(prompt, new(executionSettings)).ConfigureAwait(false);
        Console.WriteLine($"{result}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Could not read file '{file}': {ex.Message}");
    }
    Console.WriteLine();
}

Console.WriteLine("Done");