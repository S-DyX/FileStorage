using Autofac;
using FileStorage.Api.Host.Services;
using FileStorage.Contracts;
using FileStorage.Contracts.Impl;
using FileStorage.Contracts.Impl.Connections;
using FileStorage.Core;
using FileStorage.Core.Contracts;
using FileStorage.Core.Interfaces;
using FileStorage.Core.Interfaces.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace FileStorage.Api.Host
{
	public class Startup
	{
		private Thread _thread;
		public static IServiceProvider Factory;
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddControllers();
		}

		public void ConfigureContainer(ContainerBuilder builder)
		{

			InitTcpServer(builder);


			builder
				.RegisterType<FolderStorageLocalService>()
				.As<IFolderStorageLocalService>()
				.SingleInstance();

			builder
				.RegisterType<FolderStorageService>()
				.As<IFolderStorageService>()
				.SingleInstance();

			builder
				.RegisterType<FolderStorageFactory>()
				.As<IFolderStorageFactory>()
				.SingleInstance();

			builder
				   .RegisterType<FileStorageLocalService>()
				   .As<IFileStorageLocalService>()
				   .SingleInstance();

			builder
				.RegisterType<FileStorageService>()
				.As<IFileStorageService>()
				.SingleInstance();

			builder
				.RegisterType<FileStorageFactory>()
				.As<IFileStorageFactory<string>>()
				.SingleInstance();
			builder
				.RegisterType<Cache<byte[]>>()
				.As<ICache<byte[]>>()
				.SingleInstance();
			builder
				.RegisterType<FileStorageSettings>()
				.As<IFileStorageSettings>()
				.SingleInstance();
			builder
				.RegisterType<FileStorageVirtual>()
				.As<IFileStorageVirtual>()
				.SingleInstance();

			builder
				.RegisterType<LoggerFactory>()
				.As<ILoggerFactory>()
				.SingleInstance();
			builder
				.RegisterType<LocalLogger>()
				.As<ILocalLogger>()
				.SingleInstance();
		}

		private static void InitTcpServer(ContainerBuilder builder)
		{
			builder
				.RegisterType<MessageProcessingService>()
				.As<IMessageProcessingService>()
				.SingleInstance();

			builder
				.RegisterType<TcpClientRegistry>()
				.As<ITcpClientRegistry>()
				.SingleInstance();


			builder
				.RegisterType<TcpCommandServer>()
				.As<TcpCommandServer>()
				.SingleInstance();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseMiddleware(typeof(RequestLoggingMiddleware));
			app.UseRouting();
			app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
			app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

			var commandChat = Factory?.GetService(typeof(TcpCommandServer)) as TcpCommandServer;
			if (commandChat != null)
			{
				_thread = new Thread(() => { commandChat?.Start(); });
				_thread.Start();
			}
		}
	}
}
