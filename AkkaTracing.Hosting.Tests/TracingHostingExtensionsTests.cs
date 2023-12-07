using Akka.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace AkkaTracing.Hosting.Tests;

public class TracingHostingExtensionsTests : IDisposable
{
    public TracingHostingExtensionsTests()
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services =>
        {
            services.AddAkka("TestSystem", builder =>
            {
                
            });
        });
    }
    
    public void Dispose()
    {
    }
}