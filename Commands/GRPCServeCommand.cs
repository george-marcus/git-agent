using GitAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Hosting;


namespace GitAgent.Commands
{
    internal class GRPCServeCommand
    {
        public static Command BuildServeCommand()
        {
            var serveCmd = new Command("serve", "Start JSON-RPC server for IDE integration");

            var portOption = new Option<int>(["--port", "-p"], () => 9123, "Port to listen on");
            serveCmd.AddOption(portOption);

            serveCmd.SetHandler(async context =>
            {
                var port = context.ParseResult.GetValueForOption(portOption);
                var host = context.GetHost();
                var server = host.Services.GetRequiredService<IJsonRpcServer>();

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                try
                {
                    await server.StartAsync(port, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    await Console.Out.WriteLineAsync("\nServer stopped.");
                }
            });

            return serveCmd;
        }
    }
}
