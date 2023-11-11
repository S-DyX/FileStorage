using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace FileStorage.Api.Host
{
	public class Program
	{
		public static void Main(string[] args)
        {
            var build = CreateHostBuilder(args).Build();
            Startup.Factory = build.Services;
			build.Run();
        }

		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			var jsonConfigurationRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
															  .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
															  .AddEnvironmentVariables()
                                                              .Build();
			return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
						 .UseServiceProviderFactory(new AutofacServiceProviderFactory())
						 .ConfigureLogging(logging =>
						 {
							 logging.ClearProviders();
							 logging.SetMinimumLevel(LogLevel.Trace);
							 logging.AddNLog();

						 })
						 .ConfigureWebHostDefaults(webBuilder =>
						 {
							 webBuilder.UseUrls(jsonConfigurationRoot.GetSection("FileStorage:Host").Value)
							 .UseStartup<Startup>();
						 });
		}
	}

}
