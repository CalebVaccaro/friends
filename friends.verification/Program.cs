using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using friends;
using Microsoft.Extensions.Configuration;

Console.WriteLine("friends.verification");

var configuration = ConfigurationLoader.LoadConfiguration();

var apiURL = configuration.GetConnectionString("IdAnalyzerApiUrl");
var apiKey = configuration.GetConnectionString("IdAnalyzerApiKey");
var profileId = configuration.GetConnectionString("IdAnalyzerProfileId");

var facePhoto = Convert.ToBase64String(DownladImageAsBytes("https://www.idanalyzer.com/assets/testsample_face.jpg").Result);
var documentLicense = Convert.ToBase64String(DownladImageAsBytes("https://www.idanalyzer.com/assets/testsample_id.jpg").Result);

Console.WriteLine(facePhoto);
Console.WriteLine(documentLicense); 

var payload = new
{
    profile = profileId,
    document = documentLicense,
    face = facePhoto
};

try{
    using (var client = new HttpClient()){
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8);
        var respone = await client.PostAsync(apiURL, content);

        Console.WriteLine(await respone.Content.ReadAsStringAsync());
    }
}
catch (Exception e){
    Console.WriteLine(e);
    throw;
}

static async Task<byte[]> DownladImageAsBytes(string url)
{
    using (var client = new HttpClient())
    {
        return await client.GetByteArrayAsync(url);
    }
}