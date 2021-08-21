using System;
using System.Threading.Tasks;
using Silky.Rpc.Address;
using Silky.Rpc.Configuration;
using Silky.Rpc.Runtime.Server;

namespace Silky.Rpc.Routing
{
    public interface IServiceRouteManager
    {
        // Task SetRoutesAsync(IReadOnlyList<ServiceRouteDescriptor> serviceRouteDescriptors);

        Task CreateSubscribeServiceRouteDataChanges();
        
        Task CreateWsSubscribeDataChanges(Type[] wsAppType);

        void UpdateRegistryCenterOptions(RegistryCenterOptions options);
        
        Task RegisterRpcRoutes(double processorTime, ServiceProtocol serviceProtocol);

        Task RegisterWsRoutes(double processorTime, Type[] wsAppServiceTypes, int wsPort);

        Task EnterRoutes();
        
        Task RemoveServiceRoute(string serviceId, IAddressModel selectedAddress);
        
    }
}