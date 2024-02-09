using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel;
using SharpToken;
using Microsoft.Graph.Models;


public class Program
{
    static async Task Main(string[] args)
    {
        // OpenAI Token Settings
        var MAXTOKENSPERLINE = 300;
        var MAXTOKENSPERPARAGRAPH = 4000; // Provide enough context to answer questions

        // Build the config
        // Note: For dev use appsettings.dev.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            //.AddJsonFile("appsettings.json", false)
            .AddJsonFile("appsettings.dev.json", false)
            .Build();

        var builder = new HostBuilder();
        builder
            .ConfigureServices((hostContext, services) =>
            {
                // with AddHttpClient we register the IHttpClientFactory
                services.AddHttpClient();
                // Retrieve Polly retry policy and apply it
                var retryPolicy = HttpPolicies.GetRetryPolicy();
                services.AddHttpClient<IOpenAIServiceManagement, OpenAIServiceManagement>().AddPolicyHandler(retryPolicy);
            });
        var host = builder.Build();

        // Setting up SharePoint application settings
        var clientId = configuration["AadApplicationClientId"];
        var clientSecret = configuration["AadApplicationClientSecret"];
        var tenantId = configuration["AadApplicationTenantId"];
        var SpoSiteUrl = configuration["SpoSiteUrl"];
        var SpoSiteName = configuration["SpoSiteName"];

        // Setting up Azure AI Search Index
        var AzureAISearchServiceName = configuration["AzureAISearchServiceName"];
        var AzureAISearchAdminKey = configuration["AzureAISearchAdminKey"];
        var AzureAISearchIndexName = configuration["AzureAISearchIndexName"];

        // Setting up the Azure OpenAI Embeddings Endpoint
        var azureOpenAPIKey = configuration["AzureOpenAIAPIKey"];
        var azureOpenAIResource = configuration["AzureOpenAIResource"];
        var azureOpenAIModelDeploymentName = configuration["AzureOpenAIModelDeploymentName"];

        // Catch error if any of the key SharePoint connecion settings are empty
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
        {
            throw new ArgumentNullException("clientId, clientSecret, tenantId");
        }

        // Catch error if any of the SharePoint variables is empty
        if (string.IsNullOrEmpty(SpoSiteUrl) || string.IsNullOrEmpty(SpoSiteName))
        {
            throw new ArgumentNullException("SpoSiteUrl, SpoSiteName");
        }

        // Checking if any of the variable is empty
        if (string.IsNullOrEmpty(AzureAISearchServiceName) || string.IsNullOrEmpty(AzureAISearchAdminKey) || string.IsNullOrEmpty(AzureAISearchIndexName))
        {
            throw new ArgumentNullException("AzureAISearchServiceName, AzureAISearchAdminApiKey, AzureAISearchIndexName");
        }

        // Checking if any of the Azure OpenAPI Settings are empty
        if (string.IsNullOrEmpty(azureOpenAPIKey) || string.IsNullOrEmpty(azureOpenAIResource) || string.IsNullOrEmpty(azureOpenAIModelDeploymentName))
        {
            throw new ArgumentNullException("azureOpenAPIKey, azureOpenAIResource, azureOpenAIModelDeploymentName");
        }


        // Initialize Microsoft Graph client
        var graphClient = new GraphService(clientId, clientSecret, tenantId);
        if (graphClient != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully Connected to SharePoint Site");
            Console.ForegroundColor = ConsoleColor.White;
        }

        // Get SharePoint SiteId from Graph
        var listOfDrives = await graphClient!.GetSpoDriveList(SpoSiteUrl, SpoSiteName);
        // Checking if drives are empty
        if (listOfDrives!.Value.Count == 0)
        {
            throw new Exception("No drives found in the site");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("Successfully found SharePoint document libraries:");
            Console.ForegroundColor = ConsoleColor.White;
        }

        // Show list of SharePoint Document Libraries (Drives)
        Console.WriteLine("List of Graph Drives (SharePoint document libraries):");
        for (int i = 0; i < listOfDrives.Value.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {listOfDrives.Value[i].Name}");
        }

        // selecting drive
        int selectedDriveIndex = -1;
        while (selectedDriveIndex < 0 || selectedDriveIndex >= listOfDrives.Value.Count)
        {
            Console.WriteLine("Enter the number of the drive you want to select:");
            string input = Console.ReadLine() ?? string.Empty;
            selectedDriveIndex = int.Parse(input) - 1;

            if (selectedDriveIndex < 0 || selectedDriveIndex >= listOfDrives.Value.Count)
            {
                Console.WriteLine("Invalid selection. Please try again.");
            }
        }

        var selectedDrive = listOfDrives.Value[selectedDriveIndex];
        Console.WriteLine($"You selected SharePoint Drive Id: {selectedDrive.Id}, Drive Name: {selectedDrive.Name}");

        // Get list of GroupId for the drive via permission(s)
        var listOfPermissionedGroup = await graphClient.GetListOfPermissionedGroupForDrive(selectedDrive.Id);

        // Initialize the Azure OpenAI Client
        // Create the OpenAI Service
        var azureOpenAIService = host.Services.GetRequiredService<IOpenAIServiceManagement>();
        azureOpenAIService.APIKey = azureOpenAPIKey;
        azureOpenAIService.AzureOpenAIResource = azureOpenAIResource;
        azureOpenAIService.AzureOpenAIModelDeploymentName = azureOpenAIModelDeploymentName;

        // create azure ai search client
        var azureAISearchClient = new AzureAISearchService(AzureAISearchServiceName, AzureAISearchIndexName, AzureAISearchAdminKey);
        if (azureAISearchClient != null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Successfully connected to Azure AI Search Index: {azureAISearchClient.SearchIndexName} in {azureAISearchClient.SearchEndPoint}");
            Console.ForegroundColor = ConsoleColor.White;
        }

        Console.WriteLine($"Would you like to create or update index with name {AzureAISearchIndexName}(y/n)? ");
        string indexChoice = Console.ReadLine()?.ToLower() ?? string.Empty;
        if (indexChoice.ToLower() == "y")
        {
            // Create the search index  
            azureAISearchClient!.CreateOrUpdateIndex();
        }


        Console.WriteLine("Would you like to create or update index data (y/n)? ");
        string indexDataChoice = Console.ReadLine()?.ToLower() ?? string.Empty;
        if (indexDataChoice.ToLower() == "y")
        {
            var apiService = new ApiService();
            var accessToken = graphClient.AccessToken;

            var jsonContent = await apiService.GetSharePointFileList(accessToken, selectedDrive.Id);
            if (jsonContent == null)
            {
                Console.WriteLine("No files found in the SPO Document Library");
                return;
            }

            foreach (var item in jsonContent)
            {
                CustomGraphResponseValue itemValue = (CustomGraphResponseValue) item;
                Console.WriteLine($"- Processing...{itemValue.name}");

                // TODO: Use HttpClientFactory
                var apiServiceItem = new ApiService();
                HttpContent fileContent = await apiServiceItem.DownloadSharePointFileAsync(accessToken, itemValue.MicrosoftGraphdownloadUrl);

                Stream streamContent = await fileContent.ReadAsStreamAsync();
                PdfReader reader = new PdfReader(streamContent);
                StringBuilder textBuilder = new StringBuilder();

                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    var pageText = PdfTextExtractor.GetTextFromPage(reader, i);
                    textBuilder.Append(pageText);
                }
                reader.Close();

#pragma warning disable SKEXP0055 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                var documentText = textBuilder.ToString();
                var documentLines = Microsoft.SemanticKernel.Text.TextChunker.SplitPlainTextLines(documentText, MAXTOKENSPERLINE);
                List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(documentLines, MAXTOKENSPERPARAGRAPH, 0);
#pragma warning restore SKEXP0055 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                List<DocumentIndex> documentIndexes = await azureAISearchClient!.GenerateDocumentIndexDateAsync(paragraphs, itemValue, listOfPermissionedGroup, azureOpenAIService);
                // Send the document to the Azure AI Search Index
                await azureAISearchClient.InsertToSearchIndexStoreAsync(documentIndexes);
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("---- Successfully created index and index data, please check azure portal (please note that it may take couple of minutes for index to populate with data.) ----");
        }

        Console.WriteLine("Would you like to search the index (y/n)? ");
        string searchIndexChoice = Console.ReadLine()?.ToLower() ?? string.Empty;
        if (searchIndexChoice.ToLower() == "y")
        {
            // Perform Vector search in Azure AI
            var searchString = "What is covered by Perks Plus?";
            Console.WriteLine($"Searching: {searchString}");

            var topMatchingDocument = await azureAISearchClient!.SearchIndex(searchString, azureOpenAIService);
            var documentText = topMatchingDocument.Document.content;
            Console.WriteLine($"Top matching document: {topMatchingDocument.Document.title}");

            var semanticKernelBuilder = Kernel.CreateBuilder();
            semanticKernelBuilder.Services.AddAzureOpenAIChatCompletion(
                "gpt-4-preview-1106",
                "https://bartopenaiswedencentral.openai.azure.com/",
                azureOpenAPIKey,
                "gpt-4-preview-1106"
                );
            var semanticKernel = semanticKernelBuilder.Build();

            var summarizeFunction = semanticKernel.CreateFunctionFromPrompt(
                @"Summarize the text from a PDF document.
                Text: {{$documentText}}"
            );

            var summary = await semanticKernel.InvokeAsync(summarizeFunction,
                new()
                {
                    {"documentText", documentText}
                });

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Summary of {topMatchingDocument.Document.title}: {summary.ToString()}");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
