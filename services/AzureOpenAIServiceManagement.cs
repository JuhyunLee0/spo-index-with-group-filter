using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


public class OpenAIServiceManagement : IOpenAIServiceManagement
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly HttpClient _httpClient;

    public OpenAIServiceManagement(HttpClient client)
    {
        _httpClient = client;
    }

    private string _apiKey;
    public string APIKey
    {
        get => _apiKey;
        set => _apiKey = value;
    }

    private string _azureOpenAIResource;
    public string AzureOpenAIResource
    {
        get => _azureOpenAIResource;
        set => _azureOpenAIResource = value;
    }

    private string _azureOpenAIModelDeploymentName;
    public string AzureOpenAIModelDeploymentName
    {
        get => _azureOpenAIModelDeploymentName;
        set => _azureOpenAIModelDeploymentName = value;
    }

    public async Task<List<float>> GetEmbeddings(string textToEncode)
    {
        var embeddings = new List<float>(1536);

        var httpClient = _httpClient;
        var requestBody = new { input = textToEncode /*, model = "text-embedding-ada-002" */};
        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); //ACCEPT header
        httpClient.DefaultRequestHeaders.Add("api-key", this.APIKey);
        var responseService = await httpClient.PostAsync($"https://{AzureOpenAIResource}.openai.azure.com/openai/deployments/{AzureOpenAIModelDeploymentName}/embeddings?api-version=2023-05-15", content);

        // Check the Response
        if (responseService.IsSuccessStatusCode)
        {
            var responseJsonString = await responseService.Content.ReadAsStringAsync();
            var openAIEmbeddings = JsonConvert.DeserializeObject<OpenAIEmbeddings>(responseJsonString);
            embeddings = openAIEmbeddings!.data[0].embedding.ToList();
            //embeddingsString = System.Text.Json.JsonSerializer.Serialize(embeddingsVectorList);
        }

        return embeddings;
    }
}
