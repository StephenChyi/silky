using System.Threading.Tasks;
using Lms.Core;
using Lms.Core.Exceptions;
using Lms.Core.Extensions;
using Lms.Core.Serialization;
using Lms.HttpServer.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lms.HttpServer.Middlewares
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseLmsExceptionHandler(this IApplicationBuilder application)
        {
            var webHostEnvironment = EngineContext.Current.Resolve<IWebHostEnvironment>();
            var gatewayOptions = EngineContext.Current.Resolve<IOptions<GatewayOptions>>().Value;
            var serializer = EngineContext.Current.Resolve<ISerializer>();

            var useDetailedExceptionPage = gatewayOptions.DisplayFullErrorStack;
            if (useDetailedExceptionPage)
            {
                application.UseDeveloperExceptionPage();
            }
            else
            {
                application.UseExceptionHandler(handler =>
                {
                    handler.Run(context =>
                    {
                        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                        if (exception == null)
                            return Task.CompletedTask;
                        context.Response.ContentType = "application/json;charset=utf-8";

                        if (gatewayOptions.WrapResult)
                        {
                            var responseResultDto = new ResponseResultDto()
                            {
                                Status = exception.GetExceptionStatusCode(),
                                ErrorMessage = exception.Message
                            };
                            if (exception is IHasValidationErrors)
                            {
                                responseResultDto.ValidErrors = ((IHasValidationErrors) exception).GetValidateErrors();
                            }
                            var responseResultData = serializer.Serialize(responseResultDto);
                            context.Response.ContentLength = responseResultData.GetBytes().Length;
                            context.Response.StatusCode = ResponseStatusCode.Success;
                            return context.Response.WriteAsync(responseResultData);
                        }
                        else
                        {
                            context.Response.StatusCode = exception.IsBusinessException()
                                ? ResponseStatusCode.BadCode
                                : exception.IsUnauthorized()
                                    ? ResponseStatusCode.Unauthorized
                                    : ResponseStatusCode.InternalServerError;
                        
                            if (exception is IHasValidationErrors)
                            {
                                var validateErrors = exception.GetValidateErrors();
                                var responseResultData = serializer.Serialize(validateErrors);
                                context.Response.ContentLength = responseResultData.GetBytes().Length;
                                return context.Response.WriteAsync(responseResultData);
                            }
                            context.Response.ContentLength = exception.Message.GetBytes().Length;
                            return context.Response.WriteAsync(exception.Message);
                        }
                        
                    });
                });
            }
        }
    }
}