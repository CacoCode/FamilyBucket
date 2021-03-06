﻿using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using Bucket.Config.Extensions;
using Bucket.Config.HostedService;
using Bucket.EventBus.Extensions;
using Bucket.EventBus.RabbitMQ.Extensions;
using Bucket.HostedService.AspNetCore;
using Bucket.Logging.Events;
using Bucket.DbContext;
using Bucket.Config;
using Bucket.Logging;
using Bucket.Caching;
using Bucket.Caching.InMemory;
using Bucket.Caching.StackExchangeRedis;
using Bucket.AspNetCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Bucket.Caching.Abstractions;

namespace Bucket.Console
{
    static class Program
    {
        static void Main(string[] args)
        {
            var hostBuilder = new HostBuilder()
                   .UseContentRoot(Directory.GetCurrentDirectory())
                   .ConfigureHostConfiguration(config =>
                   {
                       if (args != null)
                       {
                           config.AddCommandLine(args);
                       }
                   })
                   .ConfigureAppConfiguration((hostingContext, config) =>
                   {
                       config
                         .SetBasePath(Directory.GetCurrentDirectory())
                         .AddJsonFile("appsettings.json", true, true)
                         .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true, true)
                         .AddEnvironmentVariables();
                       var option = new BucketConfigOptions();
                       config.Build().GetSection("ConfigServer").Bind(option);
                       config.AddBucketConfig(option);
                   })
                   .ConfigureServices((hostContext, services) =>
                   {
                       // 添加基础
                       services.AddBucket();
                       // 添加日志
                       services.AddLogEventTransport();
                       // 添加数据库Orm
                       services.AddSqlSugarDbContext();
                       // 添加配置服务
                       services.AddConfigServer(hostContext.Configuration);
                       // 添加事件驱动
                       services.AddEventBus(option => { option.UseRabbitMQ(); });
                       // 添加HttpClient
                       services.AddHttpClient();
                       // 添加缓存
                       services.AddMemoryCache();
                       // 添加定时任务
                       services.AddBucketHostedService(builder => { builder.AddConfig(); });
                       // 添加缓存组件
                       services.AddCaching(build =>
                       {
                           build.UseInMemory("default");
                           build.UseStackExchangeRedis(new Caching.StackExchangeRedis.Abstractions.StackExchangeRedisOption
                           {
                               Configuration = "10.10.188.136:6379,allowadmin=true",
                               DbProviderName = "redis"
                           });
                       });
                       // 添加数据仓储注册
                       services.AddScoped(typeof(IDbRepository<>), typeof(SqlSugarRepository<>));
                       // 事件注册
                       RegisterEventBus(services);
                   })
                   .ConfigureLogging((hostingContext, logging) =>
                   {
                       logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"))
                              .AddBucketLog("Pinzhi.BackgroundTasks");
                   })
                   .UseConsoleLifetime()
                   .Build();

            hostBuilder.ConfigureEventBus().Run();
        }
        /// <summary>
        /// 注册消息事件
        /// </summary>
        /// <param name="services"></param>
        private static void RegisterEventBus(IServiceCollection services)
        {
            // 事件

        }
        /// <summary>
        /// 配置并启动消息事件订阅
        /// </summary>
        /// <param name="serviceProvider"></param>
        private static IHost ConfigureEventBus(this IHost host)
        {
            var _cachingProviderFactory = host.Services.GetRequiredService<ICachingProviderFactory>();

            var cache1 = _cachingProviderFactory.GetCachingProvider("default");
            cache1.Set("key1", "123456", new TimeSpan(0, 1, 0));
            System.Console.WriteLine($"内存key1:{cache1.Get<string>("key1")}");

            var cache2 = _cachingProviderFactory.GetCachingProvider("redis");
            cache1.Set("key2", "123", new TimeSpan(0, 1, 0));
            System.Console.WriteLine($"redis key1:{cache1.Get<int>("key2")}");

            return host;
        }
    }
}
