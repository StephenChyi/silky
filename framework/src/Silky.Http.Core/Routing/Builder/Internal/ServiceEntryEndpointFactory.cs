using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Silky.Core;
using Silky.Core.Runtime.Rpc;
using Silky.Http.Core.Configuration;
using Silky.Http.Core.Handlers;
using Silky.Rpc.Runtime.Server;
using Silky.Rpc.Security;

namespace Silky.Http.Core.Routing.Builder.Internal
{
    internal class ServiceEntryEndpointFactory
    {
        private RequestDelegate CreateRequestDelegate(ServiceEntry serviceEntry)
        {
            return async httpContext =>
            {
                var rpcContextAccessor = httpContext.RequestServices.GetRequiredService<IRpcContextAccessor>();
                RpcContext.Context.RpcServices = httpContext.RequestServices;
                rpcContextAccessor.RpcContext = RpcContext.Context;
                var currentRpcToken = EngineContext.Current.Resolve<ICurrentRpcToken>();
                currentRpcToken.SetRpcToken();
                var messageReceivedHandler = EngineContext.Current.Resolve<IMessageReceivedHandler>();
                await messageReceivedHandler.Handle(serviceEntry, httpContext);
            };
        }

        public void AddEndpoints(List<Endpoint> endpoints,
            HashSet<string> routeNames,
            ServiceEntry serviceEntry,
            IReadOnlyList<Action<EndpointBuilder>> conventions)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            if (routeNames == null)
            {
                throw new ArgumentNullException(nameof(routeNames));
            }

            if (serviceEntry == null)
            {
                throw new ArgumentNullException(nameof(serviceEntry));
            }

            if (conventions == null)
            {
                throw new ArgumentNullException(nameof(conventions));
            }

            if (serviceEntry.GovernanceOptions.ProhibitExtranet)
            {
                return;
            }

            var requestDelegate = CreateRequestDelegate(serviceEntry);
            var builder =
                new RouteEndpointBuilder(requestDelegate, RoutePatternFactory.Parse(serviceEntry.Router.RoutePath),
                    serviceEntry.Router.RouteTemplate.Order)
                {
                    DisplayName = serviceEntry.Id,
                };
            builder.Metadata.Add(serviceEntry);
            builder.Metadata.Add(serviceEntry.Id);
            builder.Metadata.Add(serviceEntry.ServiceEntryDescriptor);
            builder.Metadata.Add(new HttpMethodMetadata(new[] { serviceEntry.Router.HttpMethod.ToString() }));
            if (!serviceEntry.ServiceEntryDescriptor.IsAllowAnonymous && serviceEntry.AuthorizeData != null)
            {
                if (serviceEntry.AuthorizeData.Any())
                {
                    foreach (var data in serviceEntry.AuthorizeData)
                    {
                        builder.Metadata.Add(data);
                    }
                }
                else if (EngineContext.Current.ApplicationOptions.GlobalAuthorize)
                {
                    builder.Metadata.Add(new AuthorizeAttribute());
                }
            }

            endpoints.Add(builder.Build());
        }
    }
}