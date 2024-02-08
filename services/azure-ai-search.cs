using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;


public class AzureAISearchService
{
    public string SearchIndexName { get; set; }
    public Uri SearchEndPoint { get; set; }
    private readonly string _SearchAdminKey;
    private readonly SearchClient _SearchClient;
    private readonly SearchIndexClient _IndexClient;

    public AzureAISearchService(string AzureAISearchServiceName, string AzureAISearchIndexName, string AzureAISearchAdminKey)
    {
        SearchIndexName = AzureAISearchIndexName;
        SearchEndPoint = new Uri($"https://{AzureAISearchServiceName}.search.windows.net/");
        _SearchAdminKey = AzureAISearchAdminKey;
        _SearchClient = new(SearchEndPoint, SearchIndexName, new AzureKeyCredential(_SearchAdminKey));
        _IndexClient = new SearchIndexClient(SearchEndPoint, new AzureKeyCredential(_SearchAdminKey));
    }

    public async Task DeleteFromSearchIndexStoreAsync(string filter)
    {
        try
        {
            List<DocumentIndex> documentIndices = new();
            // to be replaced with filter
            //SearchResults<DocumentIndex> searchResults = await client.SearchAsync<DocumentIndex>("*", new SearchOptions() { Filter = "url2 eq 'Sample URL3'" });
            SearchResults<DocumentIndex> searchResults = await _SearchClient.SearchAsync<DocumentIndex>("*");

            List<string> Ids = new List<string>();
            var results = searchResults.GetResults();
            foreach (SearchResult<DocumentIndex> result in results)
            {
                Ids.Add(result.Document.id);
            }
            if (Ids.Count == 0)
            {
                Console.WriteLine($"No documents found to delete from Azure Cognitive Search Index: {SearchIndexName}");
                return;
            }
            Response<IndexDocumentsResult> res = await _SearchClient.DeleteDocumentsAsync("id", (IEnumerable<string>)Ids);
            if (res.GetRawResponse().Status == 200)
            {
                Console.WriteLine($"Successfully deleted documents from Azure Cognitive Search Index: {SearchIndexName}");
            }
            else
            {
                Console.WriteLine($"Failed to delete documents from Azure Cognitive Search Index: {SearchIndexName}");

            }
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to upload documents: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }
    }

    public async Task InsertToSearchIndexStoreAsync(List<DocumentIndex> results)
    {
        try
        {
            // Create the index if it doesn't exist
            if (!_SearchClient.IndexName.Equals(SearchIndexName))
            {
                Console.WriteLine($"Creating index {SearchIndexName}...");
                //await CreateIndexAsync();
            }

            Response<IndexDocumentsResult> res = await _SearchClient.MergeOrUploadDocumentsAsync(results);
            if (res.GetRawResponse().Status == 200)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Successfully uploaded documents to Azure AI Search Index: {SearchIndexName}");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to upload documents to Azure AI Search Index: {SearchIndexName}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to upload documents: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }

    }

    public async Task<List<DocumentIndex>> GenerateDocumentIndexDateAsync(List<string> paragraphs, CustomGraphResponseValue itemValue, List<string> groupIds)
    {
        List<DocumentIndex> documentIndexes = new();

        int c = 1;
        foreach (var paragraph in paragraphs)
        {
            documentIndexes.Add(new DocumentIndex
            {
                id = $"{itemValue.id}-{c}" ?? new Guid().ToString(),
                content = paragraph,
                title = itemValue.name ?? string.Empty,
                filepath = itemValue.name ?? string.Empty,
                url = itemValue.webUrl ?? string.Empty,
                chunk_id = c.ToString(),
                // sample AAD groups GUID for security trimming
                group_ids = groupIds.ToArray(),
            });
            c++;
        }

        return documentIndexes;
    }

    public void CreateOrUpdateIndex()
    {
        try
        {
            Response<SearchIndex> index = _IndexClient.CreateOrUpdateIndex(GetSearchIndex());
            if ((index.GetRawResponse().Status >= 200) || (index.GetRawResponse().Status < 210))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Successfully created/updated Azure AI Search Index: {SearchIndexName} in {SearchEndPoint}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    private SearchIndex GetSearchIndex()
    {
        SearchIndex searchIndex = new(SearchIndexName)
        {
            Fields =
            {
                // simple fields
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchableField("title") { IsFilterable = true, IsSortable = true },
                new SearchableField("filepath") { IsFilterable = true},
                new SearchableField("url") { IsFilterable = false},
                new SearchableField("last_updated") { IsFilterable = false},
                new SimpleField("chunk_id", SearchFieldDataType.String) { IsFilterable = false, IsSortable = true},
                new SearchableField("content") { IsFilterable = true },
                new SearchField("group_ids", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                // Add SearchField with contentvector profile
                new SearchField("contentvector", SearchFieldDataType.Collection(SearchFieldDataType.Single)) { IsSearchable = true, VectorSearchDimensions = 1536, VectorSearchProfileName = "vector-profile" }
            }
        };

        // Add HNSW Parameters
        var algorithmParameters = new HnswParameters();
        algorithmParameters.M = 4;
        algorithmParameters.EfConstruction = 400;
        algorithmParameters.EfSearch = 500;
        algorithmParameters.Metric = VectorSearchAlgorithmMetric.Cosine;

        // Add HNSW Configuration
        var algorithm = new HnswAlgorithmConfiguration("vector-config");
        algorithm.Parameters = algorithmParameters;

        // Add Vector Search Configuration with algorithm
        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(algorithm);
        searchIndex.VectorSearch = vectorSearch;

        // Add the vector search profile
        var vectorProfile = new VectorSearchProfile("vector-profile", "vector-config");
        searchIndex.VectorSearch.Profiles.Add(vectorProfile);

        return searchIndex;
    }
}



