using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemystifyingLogging
{
   public class Program
   {
      //public static void Main(string[] args)
      //{
      //   Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

      //   try
      //   {
      //      CreateHostBuilder(args).Build().Run();
      //   }
      //   catch (Exception ex)
      //   {
      //      Log.Fatal(ex, "Host terminated unexpectedly");
      //   }
      //   finally
      //   {
      //      Log.CloseAndFlush();
      //   }
      //}

      //public static IHostBuilder CreateHostBuilder(string[] args) =>
      //    Host.CreateDefaultBuilder(args)
      //        .UseSerilog()
      //        .ConfigureWebHostDefaults(webBuilder =>
      //        {
      //           webBuilder.UseStartup<Startup>();
      //        });

      public static void Main(string[] args)
      {
         CreateHostBuilder(args).Build().Run();
      }

      //public static IHostBuilder CreateHostBuilder(string[] args) =>
      //    Host.CreateDefaultBuilder(args)
      //        .ConfigureWebHostDefaults(webBuilder =>
      //        {
      //           webBuilder.UseStartup<Startup>();
      //        });

      //public static IHostBuilder CreateHostBuilder(string[] args) =>
      // Host.CreateDefaultBuilder(args)
      //     .ConfigureWebHostDefaults(webBuilder =>
      //     {
      //        webBuilder.UseStartup<Startup>();
      //     })
      //     .ConfigureLogging(logger =>
      //     {
      //        logger.AddConsole(options =>
      //        {
      //           options.IncludeScopes = true;
      //        });
      //     });

      public static IHostBuilder CreateHostBuilder(string[] args) =>
       Host.CreateDefaultBuilder(args)
           .ConfigureWebHostDefaults(webBuilder =>
           {
              webBuilder.UseStartup<Startup>();
           })
           .ConfigureLogging(logger =>
           {
              logger.AddConsole(options =>
              {
                 options.IncludeScopes = true;
              });
           });

      //public static IHostBuilder CreateHostBuilder(string[] args) =>
      // Host.CreateDefaultBuilder(args)
      //     .ConfigureWebHostDefaults(webBuilder =>
      //     {
      //        webBuilder.UseStartup<Startup>();
      //     })
      //     .ConfigureLogging(logger =>
      //     {
      //        //logger.AddFile();
      //        //logger.AddSeq();
      //     });
   }


}
