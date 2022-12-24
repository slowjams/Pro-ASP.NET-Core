using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
//using Microsoft.Extensions.Logging;/using Serilog;

namespace DemystifyingLogging.Controllers
{
   public class HomeController : ControllerBase
   {
      //private ILogger<HomeController> _logger;
      private ILogger _logger;

      //public HomeController(ILoggerFactory factory)
      //{
      //   // Unless you’re using heavily customized categories for some reason, favor injecting ILogger<T> over ILoggerFactory 
      //   // _logger = factory.CreateLogger("DemystifyingLogging.DefaultController");  // for heavily customized categories

      //   _logger = factory.CreateLogger<HomeController>(); // same as inject ILogger<HomeController>
      //}

      public HomeController(ILogger<HomeController> logger)
      {
         _logger = logger;
      }

      public string Index()
      {
         _logger.LogWarning("No, I don't have scope");

         using(_logger.BeginScope("Scope value"))
         using(_logger.BeginScope(new Dictionary<string, object> { { "CustomValue1", 12345 } }))
         {
            _logger.LogWarning("Yes, I have the scope!");
            _logger.LogWarning("again, I have the scope!");
         }
         _logger.LogWarning("No, I lost it again");

         //_logger.LogTrace("Loaded Trace");
         //_logger.LogDebug("Loaded Debug");
         //_logger.LogInformation(3, "Loaded Information {suffix}", "Yes");

         //_logger.LogWarning("Loaded Warning");
         //_logger.LogError("Loaded Error");
         //_logger.LogCritical("Loaded Critical");

         //_logger.Verbose("Loaded Verbose");
         //_logger.Debug("Loaded Debug");
         //_logger.Information("Loaded Information {suffix}", "Yes");

         //_logger.Warning("Loaded Warning");
         //_logger.Error("Loaded Error");
         //_logger.Fatal("Loaded Critical");

         return "Hello";
      }

      //public HomeController(ILogger<HomeController> logger)
      //{
      //   _logger = logger;
      //}

      //public string Index()
      //{
      //   _logger.LogTrace("Loaded Trace");
      //   _logger.LogDebug("Loaded Debug");
      //   _logger.LogInformation(3, "Loaded Information {suffix}", "Yes");
      //   _logger.LogWarning("Loaded Warning");
      //   _logger.LogError("Loaded Error");
      //   _logger.LogCritical("Loaded Critical");

      //   return "Hello";
      //}
   }
}
