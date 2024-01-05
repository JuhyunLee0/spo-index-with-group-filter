using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Graph;
using Newtonsoft.Json;

/// <summary>
/// API helper interface.
/// </summary>
public class ApiService
{
    private HttpClient client;
    const string requestHeaderAuthorizationType = "Bearer";
    const string jsonContentType = "application/json";
    const double httpClientTimeout = 5;

    /// <summary>
    /// The Constructor
    /// </summary>
    /// <param name="httpClientFactory"></param>
    public ApiService()
    {

    }

    public async Task<object> ExecuteGetAsync(string requestUrl, string accessToken)
    {
        
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var authHeader = new AuthenticationHeaderValue(requestHeaderAuthorizationType, accessToken);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Authorization", "Bearer " + accessToken);
        using (HttpClient httpClient = new HttpClient()){
            using (HttpResponseMessage response = await httpClient.SendAsync(request))
            {
                if(response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    return responseContent;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                }
            }
        }
        
        
        return "";
    }

    public async Task<dynamic?> GetSharePointFileList (string authToken, string driveId)
    {
        try
        {
            string driveGraphUrl = $"https://graph.microsoft.com/v1.0/drives/{driveId}/root/children?$search(q='.pdf')";
            dynamic responseContent = await this.ExecuteGetAsync(driveGraphUrl, authToken);
            var fileList = JsonConvert.DeserializeObject<CustomGraphResponse>(responseContent);
            if (fileList != null)
            {
                if(fileList.value != null)
                {
                    return fileList.value;
                }
            }
            return null;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<HttpContent> DownloadSharePointFileAsync(string accessToken, string requestUrl)
    {
        using (HttpClient httpClient = this.client ?? new HttpClient())
        {
            if (httpClient.Timeout < TimeSpan.FromMinutes(httpClientTimeout))
            {
                httpClient.Timeout = TimeSpan.FromMinutes(httpClientTimeout);
            }
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            // Make the HTTP GET request to download the file content
            HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return response.Content;
        }
    }
}

