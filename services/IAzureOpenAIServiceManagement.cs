using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public interface IOpenAIServiceManagement
{
    Task<List<float>> GetEmbeddings(string textToEncode);

    string APIKey { get; set; }
    string AzureOpenAIResource { get; set; }
    string AzureOpenAIModelDeploymentName { get; set; }
    string AzureOpenAIEmbeddingDeploymentName { get; set; }
}
