using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Silky.Core;
using Silky.Core.Exceptions;
using Silky.Core.Extensions;
using Silky.Core.Logging;
using Silky.Core.MiniProfiler;
using Silky.Core.Runtime.Rpc;
using Silky.Core.Serialization;
using Silky.Http.Core.Configuration;
using Silky.Http.Core.Executor;
using Silky.Rpc.Auditing;
using Silky.Rpc.Extensions;
using Silky.Rpc.Runtime.Server;
using Silky.Rpc.Security;

namespace Silky.Http.Core.Handlers
{
    internal class DefaultHttpMessageReceivedHandler : MessageReceivedHandlerBase
    {
        private readonly ISerializer _serializer;

        private GatewayOptions _gatewayOptions;
        private readonly IHttpExecutor _executor;
        private readonly IParameterParser _parameterParser;
        private readonly ICurrentRpcToken _currentRpcToken;
        private readonly IHttpHandleDiagnosticListener _httpHandleDiagnosticListener;
        private readonly IAuditSerializer _auditSerializer;

        public DefaultHttpMessageReceivedHandler(
            IOptionsMonitor<GatewayOptions> gatewayOptions,
            IHttpExecutor executor,
            ISerializer serializer,
            IParameterParser parameterParser,
            ICurrentRpcToken currentRpcToken,
            IHttpHandleDiagnosticListener httpHandleDiagnosticListener,
            IAuditSerializer auditSerializer)
        {
            _executor = executor;
            _parameterParser = parameterParser;
            _currentRpcToken = currentRpcToken;
            _httpHandleDiagnosticListener = httpHandleDiagnosticListener;
            _auditSerializer = auditSerializer;

            _serializer = serializer;
            _gatewayOptions = gatewayOptions.CurrentValue;
            gatewayOptions.OnChange((options, s) => _gatewayOptions = options);
            Logger = NullLogger<DefaultHttpMessageReceivedHandler>.Instance;
        }


        protected override async Task HandleCallAsyncCore(HttpContext httpContext,
            HttpContextServerCallContext serverCallContext)
        {
            var sp = Stopwatch.StartNew();
            var parameters = await _parameterParser.Parser(httpContext.Request, serverCallContext.ServiceEntry);
            RpcContext.Context.SetRequestParameters(_auditSerializer.Serialize(parameters));
            var serviceKey = await ResolveServiceKey(httpContext);
            if (!serviceKey.IsNullOrEmpty())
            {
                RpcContext.Context.SetServiceKey(serviceKey);
                Logger.LogWithMiniProfiler(MiniProfileConstant.Route.Name,
                    MiniProfileConstant.Route.State.FindServiceKey,
                    $"serviceKey => {serviceKey}");
            }

            var rpcConnection = RpcContext.Context.Connection;
            var clientRpcEndpoint = rpcConnection.ClientHost;
            var serverHandleMonitor = EngineContext.Current.Resolve<IServerHandleMonitor>();
            var messageId = GetMessageId(httpContext);
            var serverHandleInfo = serverHandleMonitor?.Monitor((serverCallContext.ServiceEntry.Id, clientRpcEndpoint));
            var tracingTimestamp =
                _httpHandleDiagnosticListener.TracingBefore(messageId, serverCallContext.ServiceEntry, httpContext,
                    parameters);
            object executeResult = null;
            var isHandleSuccess = true;
            var isFriendlyStatus = false;
            try
            {
                executeResult = await _executor.Execute(serverCallContext.ServiceEntry, parameters, serviceKey);
                if (executeResult != null)
                {
                    var responseData = _serializer.Serialize(executeResult);
                    await serverCallContext.HttpContext.Response.BodyWriter.WriteMessageAsync(responseData,
                        serverCallContext,
                        (msg, context) =>
                        {
                            byte[] payload = this.serializer(message);
                            context.Complete(payload);
                        }, true);
                }

                _httpHandleDiagnosticListener.TracingAfter(tracingTimestamp, messageId, serverCallContext.ServiceEntry,
                    httpContext,
                    executeResult);
            }
            catch (Exception ex)
            {
                isHandleSuccess = false;
                isFriendlyStatus = ex.IsFriendlyException();
                _httpHandleDiagnosticListener.TracingError(tracingTimestamp, messageId, serverCallContext.ServiceEntry,
                    httpContext, ex,
                    ex.GetExceptionStatusCode());
                throw;
            }
            finally
            {
                sp.Stop();
                if (isHandleSuccess)
                {
                    serverHandleMonitor?.ExecSuccess((serverCallContext.ServiceEntry.Id, clientRpcEndpoint),
                        sp.ElapsedMilliseconds,
                        serverHandleInfo);
                }
                else
                {
                    serverHandleMonitor?.ExecFail((serverCallContext.ServiceEntry?.Id, clientRpcEndpoint),
                        !isFriendlyStatus,
                        sp.ElapsedMilliseconds, serverHandleInfo);
                }
            }
        }

        protected virtual Task<string> ResolveServiceKey(HttpContext httpContext)
        {
            return Task.Factory.StartNew(() =>
            {
                string serviceKey = null;
                if (httpContext.Request.Headers.ContainsKey("serviceKey"))
                {
                    serviceKey = httpContext.Request.Headers["serviceKey"].ToString();
                }

                return serviceKey;
            });
        }


        private string GetMessageId(HttpContext httpContext)
        {
            httpContext.SetHttpMessageId();
            return httpContext.TraceIdentifier;
        }
    }
}