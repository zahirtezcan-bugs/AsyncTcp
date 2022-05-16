using AsyncTcpClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddHostedService<Client>();
        })
        .ConfigureLogging((context, logs) => logs.AddSimpleConsole())
        .Build()
        .Run();
}
catch (Exception exc)
{
    Console.Error.WriteLine(exc);
}