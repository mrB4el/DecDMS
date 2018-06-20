using System;

namespace DecDMS
{
    class Program
    {
        static async System.Threading.Tasks.Task MainAsync(string[] args)
        {
            Console.WriteLine("Hello World!");

            var host = new HostBuilder().Build();

            await host.RunAsync();

        }
    }
}
