using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Silky.Core.Exceptions;
using Silky.Core.Extensions;
using Silky.Core.Serialization;
using Silky.RegistryCenter.Consul;
using Silky.RegistryCenter.Consul.Configuration;
using Silky.Swagger.Abstraction;

namespace Silky.Swagger.Gen.Provider.Consul;

public class ConsulSwaggerInfoProvider : SwaggerInfoProviderBase, IRegisterCenterSwaggerInfoProvider
{
    private readonly IConsulClientFactory _consulClientFactory;
    private readonly ConsulRegistryCenterOptions _consulRegistryCenterOptions;
    private readonly ISerializer _serializer;
    public ILogger<ConsulSwaggerInfoProvider> Logger { get; set; }

    public ConsulSwaggerInfoProvider(IConsulClientFactory consulClientFactory,
        IOptions<ConsulRegistryCenterOptions> consulRegistryCenterOptions,
        ISerializer serializer)
    {
        _consulClientFactory = consulClientFactory;
        _consulRegistryCenterOptions = consulRegistryCenterOptions.Value;
        _serializer = serializer;
        Logger = NullLogger<ConsulSwaggerInfoProvider>.Instance;
    }


    public override async Task<string[]> GetGroups()
    {
        using var consulClient = _consulClientFactory.CreateClient();
        return await GetGroups(consulClient);
    }

    public async Task<OpenApiDocument> GetSwagger(string documentName)
    {
        using var consulClient = _consulClientFactory.CreateClient();
        return await GetSwagger(documentName, consulClient);
    }

    public async Task<IEnumerable<OpenApiDocument>> GetSwaggers()
    {
        var openApiDocuments = new List<OpenApiDocument>();
        using var consulClient = _consulClientFactory.CreateClient();
        var documents = await GetGroups(consulClient);
        foreach (var document in documents)
        {
            try
            {
                var openApiDocument = await GetSwagger(document, consulClient);
                if (openApiDocument != null)
                {
                    openApiDocuments.Add(openApiDocument);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Failed to fetch {document} openApiDocument from service registry");
            }
        }

        return openApiDocuments;
    }

    private async Task<string[]> GetGroups(IConsulClient consulClient)
    {
        var getKvResult =
            await consulClient.KV.Keys(_consulRegistryCenterOptions.SwaggerDocPath);
        if (getKvResult.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<string>();
        }

        if (getKvResult.StatusCode != HttpStatusCode.OK)
        {
            throw new SilkyException("Get Swagger Group from consul error");
        }

        var groups = getKvResult.Response.Select(p => p.Split("/").Last()).ToArray();
        return groups;
    }

    private async Task<OpenApiDocument> GetSwagger(string documentName, IConsulClient consulClient)
    {
        var getKvResult = await consulClient.KV.Get(CreateSwaggerDocPath(documentName));
        if (getKvResult.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (getKvResult.StatusCode != HttpStatusCode.OK)
        {
            throw new SilkyException("Get services from consul error");
        }

        try
        {
            var openApiDocumentJsonString = getKvResult.Response.Value.GetString();
            var openApiDocument = _serializer.Deserialize<OpenApiDocument>(openApiDocumentJsonString, camelCase: false,
                typeNameHandling: TypeNameHandling.Auto);
            return openApiDocument;
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private string CreateSwaggerDocPath(string child)
    {
        var routePath = _consulRegistryCenterOptions.SwaggerDocPath;
        if (!routePath.EndsWith("/"))
        {
            routePath += "/";
        }

        routePath += child;
        return routePath;
    }
}