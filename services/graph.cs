using Microsoft.Graph;
using Azure.Core;
using Azure.Identity;
using System.Threading.Tasks;

public class GraphService
{
    private GraphServiceClient _graphClient;
    private string _spoSiteId;
    private string accessToken;

    public string AccessToken
    {
        get { return accessToken; }
    }

    public GraphService(string clientId, string clientSecret, string tenantId)
    {
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
        };
        // https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        var requestContext = new TokenRequestContext(scopes);
        var tokenResponse = clientSecretCredential.GetToken(requestContext);
        this.accessToken = tokenResponse.Token;
        _graphClient  = new GraphServiceClient(clientSecretCredential, scopes);
        
    }
    
    public async Task<dynamic?> GetSpoDriveList(string SpoSiteUrl, string SpoSiteName){

        string url = SpoSiteUrl;
        Uri uri = new Uri(url);
        string domain = uri.Host;
        var siteId = $"{domain}:/sites/{SpoSiteName}";

        try
        {
            var site = await _graphClient.Sites[siteId]
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id" };
                });
            Console.WriteLine($"Site ID: {site.Id}");
            _spoSiteId = site.Id;
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error getting site: {ex.Message}");
            return null;
        }
        var drives = await _graphClient.Sites[_spoSiteId].Drives.GetAsync();
        return drives;
    }


    public async Task<dynamic?> GetListOfPermissionedGroupForDrive(string driveId){
        // generate group permission with from drive

        List<string> listOfPermission = new List<string>();

        var permissions = await _graphClient.Drives[driveId].Items["root"].Permissions
            .GetAsync();
        // iterate through the list of permissions

        permissions?.Value?.ForEach(permission => {
            var grantedGroup = permission.GrantedToV2?.Group;
            // checking if grantedGrp is not null
            if (grantedGroup != null)
            {
                Console.WriteLine($"adding group to index GroupFilter: {grantedGroup.DisplayName}");
                listOfPermission.Add(grantedGroup.Id);
            }
        });
        return listOfPermission;
    }
    

    // public async Task<dynamic?> GetSharepointDriveItems(string driveId){
        
    //     List<string> listOfFiles = new List<string>();
        
    //     var items = await _graphClient.Drives[driveId].Items["root"].Children
    //     .GetAsync(requestConfiguration =>
    //     {
    //         requestConfiguration.QueryParameters.Search = "q='.pdf'";
    //     });

    //     items?.Value?.ForEach(item => {
    //         Console.WriteLine($"File Name: {item.Name}");
    //         listOfFiles.Add(item.);
    //     });

    //     return item;
    // }

}