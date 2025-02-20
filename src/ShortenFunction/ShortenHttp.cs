using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ShortenFunction
{
    public static class ShortenHttp
    {
        [FunctionName("ShortenHttp")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("GetAll")]
        public static async Task<IActionResult> GetShortUrls(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shorturl")]
            HttpRequest req, ILogger log)
        {
            log.LogInformation("Getting url list items");
            try
            {
                var context = new ShortenContext();
                log.LogInformation("ConectionString: " + context.Database.GetDbConnection().ConnectionString);
                var urls = await context.UrlMappings.ToListAsync();
                return new OkObjectResult(urls);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error al obtener los datos" + ex.Message);
                return new BadRequestObjectResult("Error al obtener los datos");
            }
        }

        [FunctionName("GetById")]
        public static async Task<IActionResult> GetShortUrlById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shorturl/{id}")]
            HttpRequest req, ILogger log, int id)
        {
            log.LogInformation("Getting url list item by id");
            var url = await new ShortenContext().UrlMappings.FindAsync(id);
            return new OkObjectResult(url);
        }

        [FunctionName("Create")]
        public static async Task<IActionResult> CreateShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shorturl")]
            HttpRequest req, ILogger log)
        {
            log.LogInformation("Creating a new todo list item");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<UrlMappingCreateModel>(requestBody);
            var url = new UrlMapping { OriginalUrl = input.OriginalUrl, ShortenedUrl = input.ShortenedUrl };
            var context = new ShortenContext();
            await context.UrlMappings.AddAsync(url);
            await context.SaveChangesAsync();
            return new OkObjectResult(url);
        }

        [FunctionName("Update")]
        public static async Task<IActionResult> UpdateShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "shorturl/{id}")]
            HttpRequest req, ILogger log, int id)
        {
            log.LogInformation("Updating a todo list item");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<UrlMappingCreateModel>(requestBody);
            var context = new ShortenContext();
            var url = await context.UrlMappings.FindAsync(id);
            if (url == null)
            {
                log.LogWarning($"Item {id} not found");
                return new NotFoundResult();
            }
            url.OriginalUrl = input.OriginalUrl;
            url.ShortenedUrl = input.ShortenedUrl;
            await context.SaveChangesAsync();
            return new OkObjectResult(url);
        }

        [FunctionName("Delete")]
        public static async Task<IActionResult> DeleteShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "shorturl/{id}")]
            HttpRequest req, ILogger log, int id)
        {
            log.LogInformation("Deleting a todo list item");
            var context = new ShortenContext();
            var url = await context.UrlMappings.FindAsync(id);
            if (url == null)
            {
                log.LogWarning($"Item {id} not found");
                return new NotFoundResult();
            }
            context.UrlMappings.Remove(url);
            await context.SaveChangesAsync();
            return new OkResult();
        }

    }   

    public class UrlMappingCreateModel
    {
        /// <summary>
        /// Valor original de la url
        /// </summary>
        /// <value>Cadena</value>
        public string OriginalUrl { get; set; } = string.Empty;
        /// <summary>
        /// Valor corto de la url
        /// </summary>
        /// <value>Cadena</value>
        public string ShortenedUrl { get; set; } = string.Empty;
    }
    public class UrlMapping
    {
        /// <summary>
        /// Identificador del mapeo de url
        /// </summary>
        /// <value>Entero</value>
        public int Id { get; set; }
        /// <summary>
        /// Valor original de la url
        /// </summary>
        /// <value>Cadena</value>
        public string OriginalUrl { get; set; } = string.Empty;
        /// <summary>
        /// Valor corto de la url
        /// </summary>
        /// <value>Cadena</value>
        public string ShortenedUrl { get; set; } = string.Empty;
    }

    public class ShortenContext : DbContext
    {
        /// <summary>
        /// Constructor de la clase
        /// </summary>
        static string conexion = new ConfigurationBuilder().AddEnvironmentVariables().AddJsonFile("local.settings.json", optional:  true, reloadOnChange: true).Build().GetConnectionString("ShortenDB");
        public ShortenContext() : base(SqlServerDbContextOptionsExtensions.UseSqlServer(new DbContextOptionsBuilder(), conexion, o => o.CommandTimeout(300)).Options)
        {
        }
        /// <summary>
        /// Propiedad que representa la tabla de mapeo de urls
        /// </summary>
        /// <value>Conjunto de UrlMapping</value>
        public DbSet<UrlMapping> UrlMappings { get; set; }
    }
}
