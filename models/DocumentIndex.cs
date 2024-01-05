using Microsoft.Graph.Models.Security;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class DocumentIndex
{
    // create class properties based on the structure of the JSON response
    public string id { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
    public string filepath { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string url { get; set; } = string.Empty;
    public string chunk_id { get; set; } = string.Empty;
    public string last_updated { get; set; } = string.Empty;
    public string[] group_ids { get; set; } = new string[] { };
}