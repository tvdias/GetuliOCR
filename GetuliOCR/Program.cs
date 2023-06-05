using System.Net.Http.Json;
using Google.Cloud.DocumentAI.V1;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Xceed.Words.NET;

var configurationBuilder = new ConfigurationBuilder().AddJsonFile($"appsettings.json");
var config = configurationBuilder.Build();

#pragma warning disable CA2208 // Instantiate argument exceptions correctly
string inputDirectoryName = config["inputDirectoryName"] ?? throw new ArgumentNullException("inputDirectoryName");
string outputDirectoryName = config["outputDirectoryName"] ?? throw new ArgumentNullException("outputDirectoryName");
string projectId = config["projectId"] ?? throw new ArgumentNullException("projectId");
string locationId = config["locationId"] ?? throw new ArgumentNullException("locationId");
string processorId = config["processorId"] ?? throw new ArgumentNullException("processorId");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

const string mimeType = "application/pdf";

try
{
    HttpClient httpClient = new();
    var quotes = await httpClient.GetFromJsonAsync<PensadorResponse>("https://pensador-api.vercel.app/?term=Getulio+Vargas&max=50");

    if (quotes != null && quotes.Frases.Length > 0)
    {
        Console.WriteLine("Getúlio, és tu?");
        Random random = new();
        var randomIndex = random.Next(0, quotes.Frases.Length);
        Console.WriteLine(quotes.Frases[randomIndex].Texto);
    }
}
catch
{
}

Console.WriteLine("");
Console.WriteLine("");

string inputDirectory = Path.IsPathRooted(inputDirectoryName)
    ? inputDirectoryName
    : Path.Combine(Directory.GetCurrentDirectory(), inputDirectoryName);

string outputDirectory = Path.IsPathRooted(outputDirectoryName)
    ? outputDirectoryName
    : Path.Combine(Directory.GetCurrentDirectory(), outputDirectoryName);

if (!Directory.Exists(inputDirectory))
{
    Console.WriteLine($"Diretório de entrada não encontrado ({inputDirectory}).");
    return;
}

var files = Directory.GetFiles(inputDirectory, "*.pdf").ToList();

if (files.Count == 0)
{
    Console.WriteLine("Nenhum PDF encontrado.");
    return;
}

if (!Directory.Exists(outputDirectoryName))
{
    Directory.CreateDirectory(outputDirectoryName);
}

DocX.Create("test.docx");

var client = new DocumentProcessorServiceClientBuilder
{
    Endpoint = $"{locationId}-documentai.googleapis.com",
}.Build();

foreach (var file in files)
{
    var filename = Path.GetFileName(file);

    var outputFilename = filename + ".ocr.docx";
    var outputFile = Path.Combine(outputDirectory, outputFilename);

    var outputInfoFilename = filename + ".info.txt";
    var outputInfoFile = Path.Combine(outputDirectory, outputInfoFilename);

    if (File.Exists(outputFile))
    {
        Console.WriteLine($"{filename} já foi processado anteriormente.");
        continue;
    }

    Console.WriteLine($"Processando {filename}.");

    using var fileStream = File.OpenRead(file);
    var rawDocument = new RawDocument
    {
        Content = ByteString.FromStream(fileStream),
        MimeType = mimeType
    };

    var request = new ProcessRequest
    {
        Name = ProcessorName.FromProjectLocationProcessor(projectId, locationId, processorId).ToString(),
        RawDocument = rawDocument
    };

    var response = client.ProcessDocument(request);

    var doc = DocX.Create(outputFile);
    doc.InsertParagraph(response.Document.Text);
    doc.Save();

    var fileInfoStream = File.Create(outputInfoFile);
    fileInfoStream.Close();
    await File.AppendAllLinesAsync(outputInfoFile, response.Document.Pages.Select(p => { p.Image = null; return p.ToString(); }));

    Console.WriteLine($"{filename} foi processado.");
}