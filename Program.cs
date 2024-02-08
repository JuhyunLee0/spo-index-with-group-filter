using System;
using Microsoft.Extensions.Configuration;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text;
using Microsoft.SemanticKernel.Text;


public class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            //.AddJsonFile("appsettings.json", false)
            .AddJsonFile("appsettings.dev.json", false)
            .Build();

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
        Console.Write("Would you like to create or update index data (y/n)? ");
        string indexDataChoice = Console.ReadLine()?.ToLower() ?? string.Empty;
        
        if (indexDataChoice.ToLower() == "y")
        {
            var apiSerivce = new ApiService();
            var accessToken = graphClient.AccessToken;

            var jsonContent = await apiSerivce.GetSharePointFileList(accessToken, selectedDrive.Id);
            if (jsonContent == null)
            {
                Console.WriteLine("No files found in the SPO Document Library");
                return;
            }

            foreach (var item in jsonContent)
            {
                CustomGraphResponseValue itemValue = (CustomGraphResponseValue)item;
                HttpContent fileContent = await apiSerivce.DownloadSharePointFileAsync(accessToken, itemValue.MicrosoftGraphdownloadUrl);

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
                List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(TextChunker.SplitPlainTextLines(textBuilder.ToString(), 128), 1024, 50);
#pragma warning restore SKEXP0055 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                List<DocumentIndex> documentIndexes = await azureAISearchClient!.GenerateDocumentIndexDateAsync(paragraphs, itemValue, listOfPermissionedGroup);
                await azureAISearchClient.InsertToSearchIndexStoreAsync(documentIndexes);
            }
        }

        Console.WriteLine("---- succesfully created index and index data, please check azure portal (please note that it may take couple of minutes for index to populate with data.) ----");
    }
}
