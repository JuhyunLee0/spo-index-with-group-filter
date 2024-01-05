using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;


public class AzureAISearchService
{

    private readonly string _SearchIndexName;
    private readonly Uri _SearchEndPoint;
    private readonly string _SearchAdminKey;
    private readonly SearchClient _SearchClient;
    private readonly SearchIndexClient _IndexClient;

    public AzureAISearchService(string AzureAISearchServiceName, string AzureAISearchIndexName, string AzureAISearchAdminKey)
    {
        _SearchIndexName = AzureAISearchIndexName;
        _SearchEndPoint = new Uri($"https://{AzureAISearchServiceName}.search.windows.net/");
        _SearchAdminKey = AzureAISearchAdminKey;
        Console.WriteLine($"Azure Cognitive Search Index: {_SearchIndexName} in {_SearchEndPoint}");
        _SearchClient = new(_SearchEndPoint, _SearchIndexName, new AzureKeyCredential(_SearchAdminKey));
        _IndexClient = new SearchIndexClient(_SearchEndPoint, new AzureKeyCredential(_SearchAdminKey));
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
                Console.WriteLine($"No documents found to delete from Azure Cognitive Search Index: {_SearchIndexName}");
                return;
            }
            Response<IndexDocumentsResult> res = await _SearchClient.DeleteDocumentsAsync("id", (IEnumerable<string>)Ids);
            if (res.GetRawResponse().Status == 200)
            {
                Console.WriteLine($"Successfully deleted documents from Azure Cognitive Search Index: {_SearchIndexName}");
            }
            else
            {
                Console.WriteLine($"Failed to delete documents from Azure Cognitive Search Index: {_SearchIndexName}");

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
            if (!_SearchClient.IndexName.Equals(_SearchIndexName))
            {
                Console.WriteLine($"Creating index {_SearchIndexName}...");
                //await CreateIndexAsync();
            }

            Response<IndexDocumentsResult> res = await _SearchClient.MergeOrUploadDocumentsAsync(results);
            if (res.GetRawResponse().Status == 200)
            {
                Console.WriteLine($"Successfully uploaded documents to Azure Cognitive Search Index: {_SearchIndexName}");
            }
            else
            {
                Console.WriteLine($"Failed to upload documents to Azure Cognitive Search Index: {_SearchIndexName}");
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
                Console.WriteLine($"Successfully created/updated Azure Cognitive Search Index: {_SearchIndexName} in {_SearchEndPoint}");
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
        SearchIndex searchIndex = new(_SearchIndexName)
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
            }
        };

        return searchIndex;
    }
}



