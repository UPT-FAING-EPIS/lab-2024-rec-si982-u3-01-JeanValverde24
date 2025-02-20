[![Review Assignment Due Date](https://classroom.github.com/assets/deadline-readme-button-22041afd0340ce965d47ae6ef1cefeee28c7c493a6346c4f15d667ab976d596c.svg)](https://classroom.github.com/a/xjwIPJ48)
[![Open in Codespaces](https://classroom.github.com/assets/launch-codespace-2972f46106e565e64193e422d61a12cf1da4916b45550586e14ef0a7c637dd04.svg)](https://classroom.github.com/open-in-codespaces?assignment_repo_id=18298492)
# SESION DE LABORATORIO N° 04: Construyendo una Aplicación WebApi y un Cliente Web Estático

## OBJETIVOS
  * Comprender el desarrollo una Aplicación Web API con .Net Functions y un Cliente Web Estático con Blazor WebAssembly

## REQUERIMIENTOS
  * Conocimientos: 
    - Conocimientos básicos de SQL.
    - Conocimientos shell y comandos en modo terminal.
  * Hardware:
    - Virtualization activada en el BIOS.
    - CPU SLAT-capable feature.
    - Al menos 4GB de RAM.
  * Software:
    - Windows 10 64bit: Pro, Enterprise o Education (1607 Anniversary Update, Build 14393 o Superior)
    - Docker Desktop 
    - Powershell versión 7.x
    - .Net 8
    - Azure CLI
    - Azure Functions Core Tools (winget install Microsoft.Azure.FunctionsCoreTools)
    - Azure Static WebApps CLI (npm install -g @azure/static-web-apps-cli)

## CONSIDERACIONES INICIALES
  * Tener una cuenta en Infracost (https://www.infracost.io/), sino utilizar su cuenta de github para generar su cuenta y generar un token.
  * Tener una cuenta en SonarCloud (https://sonarcloud.io/), sino utilizar su cuenta de github para generar su cuenta y generar un token. El token debera estar registrado en su repositorio de Github con el nombre de SONAR_TOKEN. 
  * Tener una cuenta con suscripción en Azure (https://portal.azure.com/). Tener el ID de la Suscripción, que se utilizará en el laboratorio
  * Clonar el repositorio mediante git para tener los recursos necesarios en una ubicación que no sea restringida del sistema.

## DESARROLLO

### PREPARACION DE LA INFRAESTRUCTURA

1. Iniciar la aplicación Powershell o Windows Terminal en modo administrador, ubicarse en ua ruta donde se ha realizado la clonación del repositorio
```Powershell
md infra
```
2. Abrir Visual Studio Code, seguidamente abrir la carpeta del repositorio clonado del laboratorio, en el folder Infra, crear el archivo main.tf con el siguiente contenido
```Terraform
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0.0"
    }
  }
  required_version = ">= 0.14.9"
}

variable "suscription_id" {
    type = string
    description = "Azure subscription id"
}

variable "sqladmin_username" {
    type = string
    description = "Administrator username for server"
}

variable "sqladmin_password" {
    type = string
    description = "Administrator password for server"
}

provider "azurerm" {
  features {}
  subscription_id = var.suscription_id
}

# Generate a random integer to create a globally unique name
resource "random_integer" "ri" {
  min = 100
  max = 999
}

# Create the resource group
resource "azurerm_resource_group" "rg" {
  name     = "upt-arg-${random_integer.ri.result}"
  location = "eastus2"
}

resource "azurerm_storage_account" "storageaccount" {
  name                     = "uptasa${random_integer.ri.result}"
  location                 = azurerm_resource_group.rg.location
  resource_group_name      = azurerm_resource_group.rg.name
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

# Create the Linux App Service Plan
resource "azurerm_service_plan" "appserviceplan" {
  name                = "upt-asp-${random_integer.ri.result}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  os_type             = "Linux"
  sku_name            = "Y1"
}

resource "azurerm_linux_function_app" "azurefunction" {
  name                = "upt-afn-${random_integer.ri.result}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  storage_account_name       = azurerm_storage_account.storageaccount.name
  storage_account_access_key = azurerm_storage_account.storageaccount.primary_access_key
  service_plan_id            = azurerm_service_plan.appserviceplan.id
  site_config {
    minimum_tls_version = "1.2"
    always_on = false
    application_stack {
      dotnet_version = "8.0"
      }
  }
}

resource "azurerm_static_web_app" "example" {
  name                = "upt-swa-${random_integer.ri.result}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_mssql_server" "sqlsrv" {
  name                         = "upt-dbs-${random_integer.ri.result}"
  resource_group_name          = azurerm_resource_group.rg.name
  location                     = azurerm_resource_group.rg.location
  version                      = "12.0"
  administrator_login          = var.sqladmin_username
  administrator_login_password = var.sqladmin_password
}

resource "azurerm_mssql_firewall_rule" "sqlaccessrule" {
  name             = "PublicAccess"
  server_id        = azurerm_mssql_server.sqlsrv.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "255.255.255.255"
}

resource "azurerm_mssql_database" "sqldb" {
  name      = "shorten"
  server_id = azurerm_mssql_server.sqlsrv.id
  sku_name = "Free"
}
```
![alt text](imagenes/image.png)

3. Abrir un navegador de internet y dirigirse a su repositorio en Github, en la sección *Settings*, buscar la opción *Secrets and Variables* y seleccionar la opción *Actions*. Dentro de esta crear los siguientes secretos
> AZURE_USERNAME: Correo o usuario de cuenta de Azure
> 
> AZURE_PASSWORD: Password de cuenta de Azure
> 
> SUSCRIPTION_ID: ID de la Suscripción de cuenta de Azure
> 
> SQL_USER: Usuario administrador de la base de datos, ejm: adminsql
> 
> SQL_PASS: Password del usuario administrador de la base de datos, ejm: upt.2025

![alt text](imagenes/image-1.png)

5. En el Visual Studio Code, crear la carpeta .github/workflows en la raiz del proyecto, seguidamente crear el archivo deploy.yml con el siguiente contenido
<details><summary>Click to expand: deploy.yml</summary>

```Yaml
name: Construcción infrastructura en Azure

on:
  push:
    branches: [ "main" ]
    paths:
      - 'infra/**'
      - '.github/workflows/infra.yml'
  workflow_dispatch:

jobs:
  Deploy-infra:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: login azure
        run: | 
          az login -u ${{ secrets.AZURE_USERNAME }} -p ${{ secrets.AZURE_PASSWORD }}

      - name: Create terraform.tfvars
        run: |
          cd infra
          echo "suscription_id=\"${{ secrets.SUSCRIPTION_ID }}\"" > terraform.tfvars
          echo "sqladmin_username=\"${{ secrets.SQL_USER }}\"" >> terraform.tfvars
          echo "sqladmin_password=\"${{ secrets.SQL_PASS }}\"" >> terraform.tfvars

      # - name: Setup tfsec
      #   run: |
      #       curl -L -o /tmp/tfsec_1.28.13_linux_amd64.tar.gz "https://github.com/aquasecurity/tfsec/releases/download/v1.28.13/tfsec_1.28.13_linux_amd64.tar.gz"
      #       tar -xzvf /tmp/tfsec_1.28.13_linux_amd64.tar.gz -C /tmp
      #       mv -v /tmp/tfsec /usr/local/bin/tfsec
      #       chmod +x /usr/local/bin/tfsec
      # - name: tfsec
      #   run: |
      #     cd infra
      #     /usr/local/bin/tfsec --format=markdown --tfvars-file=terraform.tfvars --out=tfsec.md .
      #     echo "## TFSec Output" >> $GITHUB_STEP_SUMMARY
      #     cat tfsec.md >> $GITHUB_STEP_SUMMARY
  
      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3
      - name: Terraform Init
        id: init
        run: cd infra && terraform init 
    #   - name: Terraform Fmt
    #     id: fmt
    #     run: cd infra && terraform fmt -check
      - name: Terraform Validate
        id: validate
        run: cd infra && terraform validate -no-color
      - name: Terraform Plan
        run: cd infra && terraform plan -var="suscription_id=${{ secrets.SUSCRIPTION_ID }}" -var="sqladmin_username=${{ secrets.SQL_USER }}" -var="sqladmin_password=${{ secrets.SQL_PASS }}" -no-color -out main.tfplan

      - name: Create String Output
        id: tf-plan-string
        run: |
            TERRAFORM_PLAN=$(cd infra && terraform show -no-color main.tfplan)

            delimiter="$(openssl rand -hex 8)"
            echo "summary<<${delimiter}" >> $GITHUB_OUTPUT
            echo "## Terraform Plan Output" >> $GITHUB_OUTPUT
            echo "<details><summary>Click to expand</summary>" >> $GITHUB_OUTPUT
            echo "" >> $GITHUB_OUTPUT
            echo '```terraform' >> $GITHUB_OUTPUT
            echo "$TERRAFORM_PLAN" >> $GITHUB_OUTPUT
            echo '```' >> $GITHUB_OUTPUT
            echo "</details>" >> $GITHUB_OUTPUT
            echo "${delimiter}" >> $GITHUB_OUTPUT

      - name: Publish Terraform Plan to Task Summary
        env:
          SUMMARY: ${{ steps.tf-plan-string.outputs.summary }}
        run: |
          echo "$SUMMARY" >> $GITHUB_STEP_SUMMARY

      - name: Outputs
        id: vars
        run: |
            echo "terramaid_version=$(curl -s https://api.github.com/repos/RoseSecurity/Terramaid/releases/latest | grep tag_name | cut -d '"' -f 4)" >> $GITHUB_OUTPUT
            case "${{ runner.arch }}" in
            "X64" )
                echo "arch=x86_64" >> $GITHUB_OUTPUT
                ;;
            "ARM64" )
                echo "arch=arm64" >> $GITHUB_OUTPUT
                ;;
            esac

      - name: Setup Go
        uses: actions/setup-go@v5
        with:
          go-version: 'stable'

      - name: Setup Terramaid
        run: |
            curl -L -o /tmp/terramaid.tar.gz "https://github.com/RoseSecurity/Terramaid/releases/download/${{ steps.vars.outputs.terramaid_version }}/Terramaid_Linux_${{ steps.vars.outputs.arch }}.tar.gz"
            tar -xzvf /tmp/terramaid.tar.gz -C /tmp
            mv -v /tmp/Terramaid /usr/local/bin/terramaid
            chmod +x /usr/local/bin/terramaid

      - name: Terramaid
        id: terramaid
        run: |
            cd infra
            /usr/local/bin/terramaid run

      - name: Publish graph in step comment
        run: |
            echo "## Terramaid Graph" >> $GITHUB_STEP_SUMMARY
            cat infra/Terramaid.md >> $GITHUB_STEP_SUMMARY 

      - name: Setup Graphviz
        uses: ts-graphviz/setup-graphviz@v2        

      - name: Setup inframap
        run: |
            curl -L -o /tmp/inframap.tar.gz "https://github.com/cycloidio/inframap/releases/download/v0.7.0/inframap-linux-amd64.tar.gz"
            tar -xzvf /tmp/inframap.tar.gz -C /tmp
            mv -v /tmp/inframap-linux-amd64 /usr/local/bin/inframap
            chmod +x /usr/local/bin/inframap
      - name: inframap
        run: |
            cd infra
            /usr/local/bin/inframap generate main.tf --raw | dot -Tsvg > inframap_azure.svg
      - name: Upload inframap
        id: inframap-upload-step
        uses: actions/upload-artifact@v4
        with:
          name: inframap_azure.svg
          path: infra/inframap_azure.svg

      - name: Setup infracost
        uses: infracost/actions/setup@v3
        with:
            api-key: ${{ secrets.INFRACOST_API_KEY }}
      - name: infracost
        run: |
            cd infra
            infracost breakdown --path . --format html --out-file infracost-report.html
            sed -i '19,137d' infracost-report.html
            sed -i 's/$0/$ 0/g' infracost-report.html

      - name: Convert HTML to Markdown
        id: html2markdown
        uses: rknj/html2markdown@v1.1.0
        with:
            html-file: "infra/infracost-report.html"

      - name: Upload infracost report
        run: |
            echo "## infracost Report" >> $GITHUB_STEP_SUMMARY
            echo "${{ steps.html2markdown.outputs.markdown-content }}" >> infracost.md
            cat infracost.md >> $GITHUB_STEP_SUMMARY

      - name: Terraform Apply
        run: |
            cd infra
            terraform apply -var="suscription_id=${{ secrets.SUSCRIPTION_ID }}" -var="sqladmin_username=${{ secrets.SQL_USER }}" -var="sqladmin_password=${{ secrets.SQL_PASS }}" -auto-approve main.tfplan
```
</details>

![alt text](imagenes/image-2.png)

6. En el Visual Studio Code, guardar los cambios y subir los cambios al repositorio. Revisar los logs de la ejeuciòn de automatizaciòn y anotar el numero de identificaciòn de Grupo de Recursos y Aplicación Web creados
```Bash
azurerm_linux_web_app.webapp: Creation complete after 53s [id=/subscriptions/1f57de72-50fd-4271-8ab9-3fc129f02bc0/resourceGroups/upt-arg-XXX/providers/Microsoft.Web/sites/upt-awa-XXX]
```
![alt text](imagenes/image-3.png)

### CONSTRUCCION DE LA APLICACION - BACKEND

1. En el terminal, ubicarse en un ruta que no sea del sistema y ejecutar los siguientes comandos.
```Bash
md src
cd src
func init ShortenFunction --worker-runtime dotnet --target-framework net8.0
cd ShortenFunction
func new --name ShortenHttp --template "HTTP trigger" --authlevel "anonymous"
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version=8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version=8.0.0
```
<details><summary>dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version=8.0.0</summary>

```bash
info : El paquete "Microsoft.EntityFrameworkCore.SqlServer" es compatible con todos los marcos de trabajo especificados del proyecto "C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj".
info : Se agregó PackageReference para la versión "8.0.0" del paquete "Microsoft.EntityFrameworkCore.SqlServer" al archivo "C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj".
info : Generación de archivo MSBuild C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\obj\ShortenFunction.csproj.nuget.g.props.
info : Generación de archivo MSBuild C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\obj\ShortenFunction.csproj.nuget.g.targets.
info : Escribiendo el archivo de recursos en el disco. Ruta de acceso: C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\obj\project.assets.json
log  : Se ha restaurado C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj (en 6,26 s).
```
</details>


<details><summary>dotnet add package Microsoft.EntityFrameworkCore.Tools --version=8.0.0</summary>

```bash
Compilación realizado correctamente en 0,4s
info : La validación de la cadena de certificados X.509 utilizará el almacén de confianza predeterminado seleccionado por .NET para la firma de código.
info : La validación de la cadena de certificados X.509 utilizará el almacén de confianza predeterminado seleccionado por .NET para la marca de tiempo.
info : Agregando PackageReference para el paquete "Microsoft.EntityFrameworkCore.Tools" al proyecto "C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj".
info : Restaurando paquetes para C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj...
info :   CACHE https://api.nuget.org/v3/vulnerabilities/index.json
info :   CACHE https://api.nuget.org/v3-vulnerabilities/2025.02.14.05.20.53/vulnerability.base.json
info :   CACHE https://api.nuget.org/v3-vulnerabilities/2025.02.14.05.20.53/2025.02.19.17.21.08/vulnerability.update.json
info : El paquete "Microsoft.EntityFrameworkCore.Tools" es compatible con todos los marcos de trabajo especificados del proyecto "C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj".
info : Se agregó PackageReference para la versión "8.0.0" del paquete "Microsoft.EntityFrameworkCore.Tools" al archivo "C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj".
info : Generación de archivo MSBuild C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\obj\ShortenFunction.csproj.nuget.g.props.
info : Generación de archivo MSBuild C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\obj\ShortenFunction.csproj.nuget.g.targets.
info : Escribiendo el archivo de recursos en el disco. Ruta de acceso: C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\obj\project.assets.json
log  : Se ha restaurado C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction\ShortenFunction.csproj (en 334 ms).
PS C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24\src\ShortenFunction>  
```
</details>

2. En el VS Code, buscar el proyecto ShortenFunction modificar el archivo ShortenHttp.cs, con el siguiente contenido:
```CSharp
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
        static string conexion = new ConfigurationBuilder().AddEnvironmentVariables().AddJsonFile("app.settings.json", optional:  true, reloadOnChange: true).Build().GetConnectionString("ShortenDB");
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

```
![alt text](imagenes/image-4.png)

3. En el VS Code, buscar el proyecto ShortenFunction modificar el archivo local.settings.json, con el siguiente contenido:
```JSon
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_INPROC_NET8_ENABLED": "1",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet"
  },
  "ConnectionStrings": {
    "ShortenDB": "Data Source=upt-dbs-XXX.database.windows.net;User ID=YYY;Password=ZZZ;Database=shorten"
  }
}
```
>Donde: XXX, id de su servidor de base de datos
>       YYY, usuario administrador de base de datos
>       ZZZ, password del usuario de base de datos

![alt text](imagenes/image-5.png)

4. En el Terminal, ejecutar el siguiente comando para crear las tablas de base de datos de identidad.
```Bash
dotnet ef migrations add CreateIdentitySchema
dotnet ef database update
```
![alt text](imagenes/image-6.png)

5. En el Terminal, ejecutar el siguiente comando para ejecutar la aplicación.
```Bash
func start
```

![alt text](imagenes/image-8.png)

![alt text](imagenes/image-7.png)

6. En el Terminal, ejecutar el siguiente comando para configurar, compilar y desplegar la aplicación.
```Bash
az functionapp config appsettings set -g upt-arg-373 -n upt-afn-373 --settings FUNCTIONS_INPROC_NET8_ENABLED=1
az webapp config connection-string set -g upt-arg-373 -n upt-afn-373 -t sqlazure --settings ShortenDB='Data Source=upt-dbs-XXX.database.windows.net;User ID=YYY;Password=ZZZ;Database=shorten'
dotnet publish -c Release -o publish
cd publish
zip -r functionapp.zip .
az functionapp deployment source config-zip -g upt-arg-373 -n upt-afn-373 --src .\functionapp.zip --verbose
```
>Donde: XXX, id de su servidor de base de datos
>       YYY, usuario administrador de base de datos
>       ZZZ, password del usuario de base de datos


![alt text](imagenes/image-9.png)

![alt text](imagenes/image-10.png)

7. En el Navegador, abrir una nueva pestaña e ingresar a la url https://upt-afn-XXX.azurewebsites.net/api/shorturl
>Donde: XXX, id de su azure function

![alt text](imagenes/image-11.png)

### CONSTRUCCION DE LA APLICACION - FRONTEND

8. En el Terminal, volver a la carpeta src, y ejecutar los siguientes comandos para crear la aplicación web estatica
```Powershell
dotnet new blazorwasm -o ShortenApp -p
cd ShortenApp
dotnet new razorcomponent -n UrlMapping -o Pages
code .
```
![alt text](imagenes/image-12.png)

9. En Visual Studio Code, dentro del proyecto ClienteApp, editar el archivo UrlMapping.razor con el siguiente contenido:
```CSharp
@page "/urlmapping"
@using System.Text.Json
@inject HttpClient Http

<PageTitle>Mapeo de Urls</PageTitle>

<h1>Urls Acortadas</h1>

<p>Listado de lasurls acortadas</p>

@if (urls == null)
{
    <p><em>Loading...</em></p>
}
else
{
    <table class="table">
        <thead>
            <tr>
                <th>ID</th>
                <th>Url Original</th>
                <th>Url Acortada</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var url in urls)
            {
                <tr>
                    <td>@url.Id</td>
                    <td>@url.OriginalUrl</td>
                    <td>@url.ShortenedUrl</td>
                </tr>
            }
        </tbody>
    </table>
}
@code {
    private UrlMapeada[]? urls;
    protected override async Task OnInitializedAsync()
    {
        //tipos = await Http.GetFromJsonAsync<TipoDocumento[]>("sample-data/weather.json");
        urls = await Http.GetFromJsonAsync<UrlMapeada[]>("api/shorturl", new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
    }
    public class UrlMapeada
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
}
```
10. En Visual Studio Code, en el proyecto ClienteApp en la ruta Layout modificar el archivo NavMenu.razor
> dice
```Razor
            <NavLink class="nav-link" href="weather">
                <span class="bi bi-list-nested-nav-menu" aria-hidden="true"></span> Weather
            </NavLink>
```
> debe decir
```Razor
            <NavLink class="nav-link" href="urlmapping">
                <span class="bi bi-list-nested-nav-menu" aria-hidden="true"></span> Urls Acortadas
            </NavLink>
```
11. En Visual Studio Code, modificar el archivo program.cs, reemplazar la linea
> dice
```CSharp
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
```
> debe decir (reemplazar las XXXX por el puerto del servicio del Backend)
```CSharp
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://upt-afn-XXX.azurewebsites.net") });
```
>Donde: XXX, id del azure function

![alt text](imagenes/image-13.png)

12. (Opcional) en el terminal, ubicarse en la carpeta ClienteAPI, ejecutar el comando `dotnet run` para iniciar la aplicación. Anotar el numero de puerto que aparecera: Now listening on: http://localhost:XXXX. Abrir un navegador de internet e ingresar la url: http://localhost:XXXX

![alt text](imagenes/image-14.png)

13. (Opcional) en el navegador de internet, hacer click en la opción de la barra de navegación para generar una Aplicación Web Progresiva (PWA), lo cual creará una aplicación de escritorio utilizando la aplicación web desarrollada.

![alt text](imagenes/image-15.png)

14. En el Terminal, ubicarse en el directorio ShortenApp y ejecutar el siguiente comando para realizar el despliegue de la aplicación web estatica.
```Powershell
dotnet publish -c Release -o publish
swa deploy ./publish/wwwroot -n upt-swa-549 --env production
```
>Donde: XXX, id del azure static webapp

![alt text](imagenes/image-16.png)

15. En el Terminal, se visualizara el link de la Webapp Estatica, hacer click en el para verificar los resultados.

![image](https://github.com/user-attachments/assets/463ed443-3843-44a1-95bf-c7a9aa999666)

![alt text](imagenes/image-17.png)

```Bash
https://thankful-pond-0f80ab50f.4.azurestaticapps.net 
```

## ACTIVIDADES ENCARGADAS

1. Generar y subir el diagrama de infraestructura al repositorio como lab_02.png y el reporte de metricas. (2ptos)

![alt text](Lab_02.png)

<details><summary>Reporte de metricas de la aplicacion</summary>

```Bash
PS C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24> az monitor metrics list --resource "/subscriptions/83fff94f-ea6d-44c3-a294-e78a99bee2f9/resourceGroups/upt-arg-549/providers/Microsoft.Web/sites/upt-afn-549" --metric "Requests" --start-time 2025-02-19T20:00:00Z --end-time 2025-02-19T20:42:00Z --output table
tput table;9268d4a9-e5d4-4836-bbf1-0a0361b23634Timestamp             Name      Total  
--------------------  --------  -------
2025-02-19T20:00:00Z  Requests  0.0    
2025-02-19T20:01:00Z  Requests  0.0    
2025-02-19T20:02:00Z  Requests  0.0    
2025-02-19T20:03:00Z  Requests  0.0    
2025-02-19T20:04:00Z  Requests  0.0    
2025-02-19T20:05:00Z  Requests  0.0    
2025-02-19T20:06:00Z  Requests  0.0    
2025-02-19T20:07:00Z  Requests  0.0    
2025-02-19T20:08:00Z  Requests  0.0    
2025-02-19T20:09:00Z  Requests  0.0    
2025-02-19T20:10:00Z  Requests  0.0    
2025-02-19T20:11:00Z  Requests  0.0
2025-02-19T20:12:00Z  Requests  0.0
2025-02-19T20:13:00Z  Requests  0.0
2025-02-19T20:14:00Z  Requests  0.0
2025-02-19T20:15:00Z  Requests  0.0
2025-02-19T20:16:00Z  Requests  0.0
2025-02-19T20:17:00Z  Requests  0.0
2025-02-19T20:18:00Z  Requests  0.0
2025-02-19T20:19:00Z  Requests  0.0
2025-02-19T20:20:00Z  Requests  0.0
2025-02-19T20:21:00Z  Requests  0.0
2025-02-19T20:22:00Z  Requests  0.0
2025-02-19T20:23:00Z  Requests  0.0
2025-02-19T20:24:00Z  Requests  0.0
2025-02-19T20:25:00Z  Requests  0.0
2025-02-19T20:26:00Z  Requests  0.0
2025-02-19T20:27:00Z  Requests  0.0
2025-02-19T20:28:00Z  Requests  0.0
2025-02-19T20:29:00Z  Requests  0.0
2025-02-19T20:30:00Z  Requests  0.0
2025-02-19T20:31:00Z  Requests  0.0
2025-02-19T20:32:00Z  Requests  0.0
2025-02-19T20:33:00Z  Requests  0.0
2025-02-19T20:34:00Z  Requests  0.0
2025-02-19T20:35:00Z  Requests  0.0
2025-02-19T20:36:00Z  Requests  0.0
2025-02-19T20:37:00Z  Requests  0.0
2025-02-19T20:38:00Z  Requests  0.0
2025-02-19T20:39:00Z  Requests  0.0
2025-02-19T20:40:00Z  Requests  0.0
2025-02-19T20:41:00Z  Requests  0.0
PS C:\Users\HP\lab-2024-rec-si982-u3-01-JeanValverde24
```
</details>

2. Realizar el escaneo del codigo de terraform utilizando TfSec o Trivy dentro del Github Action. (2ptos)
3. En la aplicación completar con las demas funcionalidades, de crear, actualizar y eliminar (4ptos)
4. Realizar el escaneo de vulnerabilidad con SonarCloud y Semgrep dentro del Github Action correspondiente. (2ptos)
5. Generar un Action para el despliegue de las dos aplicaciones, backend y frntend, incluyendo todo lo anterior. (4ptos)
