using AsyncTcpServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddHostedService<Server>();
        })
        .ConfigureLogging((context, logs) => logs.AddSimpleConsole())
        .Build()
        .Run();
}
catch (Exception exc)
{
    Console.Error.WriteLine(exc);
}