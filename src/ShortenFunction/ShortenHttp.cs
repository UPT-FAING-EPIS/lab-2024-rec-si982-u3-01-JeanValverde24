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
        [FunctionName("GetAll")]
        public static async Task<IActionResult> GetShortUrls(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shorturl")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Obteniendo todas las URLs acortadas...");
            try
            {
                using var context = new ShortenContext();
                var urls = await context.UrlMappings.ToListAsync();
                return new OkObjectResult(urls);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error al obtener las URLs.");
                return new BadRequestObjectResult("Error al obtener las URLs.");
            }
        }

        [FunctionName("GetById")]
        public static async Task<IActionResult> GetShortUrlById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shorturl/{id}")] HttpRequest req, ILogger log, int id)
        {
            log.LogInformation($"Obteniendo URL con ID {id}");
            using var context = new ShortenContext();
            var url = await context.UrlMappings.FindAsync(id);
            return url == null ? new NotFoundResult() : new OkObjectResult(url);
        }

        [FunctionName("Create")]
        public static async Task<IActionResult> CreateShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shorturl")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Creando una nueva URL acortada...");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<UrlMappingCreateModel>(requestBody);

            if (string.IsNullOrEmpty(input.OriginalUrl) || string.IsNullOrEmpty(input.ShortenedUrl))
                return new BadRequestObjectResult("Los datos de la URL no pueden estar vacíos.");

            using var context = new ShortenContext();
            var url = new UrlMapping { OriginalUrl = input.OriginalUrl, ShortenedUrl = input.ShortenedUrl };
            await context.UrlMappings.AddAsync(url);
            await context.SaveChangesAsync();

            return new OkObjectResult(url);
        }

        [FunctionName("Update")]
        public static async Task<IActionResult> UpdateShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "shorturl/{id}")] HttpRequest req, ILogger log, int id)
        {
            log.LogInformation($"Actualizando URL con ID {id}");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<UrlMappingCreateModel>(requestBody);

            using var context = new ShortenContext();
            var url = await context.UrlMappings.FindAsync(id);
            if (url == null)
            {
                log.LogWarning($"No se encontró la URL con ID {id}");
                return new NotFoundResult();
            }

            url.OriginalUrl = input.OriginalUrl;
            url.ShortenedUrl = input.ShortenedUrl;
            await context.SaveChangesAsync();

            return new OkObjectResult(url);
        }

        [FunctionName("Delete")]
        public static async Task<IActionResult> DeleteShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "shorturl/{id}")] HttpRequest req, ILogger log, int id)
        {
            log.LogInformation($"Eliminando URL con ID {id}");
            using var context = new ShortenContext();
            var url = await context.UrlMappings.FindAsync(id);
            if (url == null)
            {
                log.LogWarning($"No se encontró la URL con ID {id}");
                return new NotFoundResult();
            }

            context.UrlMappings.Remove(url);
            await context.SaveChangesAsync();

            // ✅ Si la tabla está vacía, reiniciar el autoincremento del IDdsadas
            if (!await context.UrlMappings.AnyAsync())
            {
                log.LogInformation("Tabla vacía, reiniciando ID...");
                await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('UrlMappings', RESEED, 0);");
            }

            return new OkResult();
        }
    }

    public class UrlMappingCreateModel
    {
        public string OriginalUrl { get; set; } = string.Empty;
        public string ShortenedUrl { get; set; } = string.Empty;
    }

    public class UrlMapping
    {
        public int Id { get; set; }
        public string OriginalUrl { get; set; } = string.Empty;
        public string ShortenedUrl { get; set; } = string.Empty;
    }

    public class ShortenContext : DbContext
    {
        static string conexion = new ConfigurationBuilder().AddEnvironmentVariables().AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).Build().GetConnectionString("ShortenDB");

        public ShortenContext() : base(SqlServerDbContextOptionsExtensions.UseSqlServer(new DbContextOptionsBuilder(), conexion, o => o.CommandTimeout(300)).Options) { }

        public DbSet<UrlMapping> UrlMappings { get; set; }
    }
}
