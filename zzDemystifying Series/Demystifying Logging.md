Demystifying Logging
==============================

## Logging Source Code

```C#
public class Program {
   public static void Main(string[] args) {
      CreateHostBuilder(args).Build().Run();
   }

   public static IHostBuilder CreateHostBuilder(string[] args) =>
       Host.CreateDefaultBuilder(args)
           .ConfigureWebHostDefaults(webBuilder => {
              webBuilder.UseStartup<Startup>();
           })
           .ConfigureLogging(logger => {
              logger.AddConsole(options => {
                 options.IncludeScopes = true;
              });
           });
}
```
```C#
//------------------V
public enum LogLevel {
   // Logs that contain the most detailed messages. These messages may contain sensitive application data.
   // These messages are disabled by default and should never be enabled in a production environment.
   Trace = 0,    

   // Logs that are used for interactive investigation during development. 
   //These logs should primarily contain information useful for debugging and have no long-term value
   Debug = 1,

   // Logs that track the general flow of the application. These logs should have long-term value.
   Information = 2,
   
   // Logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the application to stop
   Warning = 3,

   // Logs that highlight when the current flow of execution is stopped due to a failure. 
   // These should indicate a failure in the current activity, not an application-wide failure
   Error = 4,

   // Logs that describe an unrecoverable application or system crash, or a catastrophic failure that requires immediate attention
   Critical =5,

   // Not used for writing log messages. Specifies that a logging category should not write any message
   None = 6
}
//------------------Ʌ

//------------------------------------------------------------------V
public interface ILogger {
   void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
   bool IsEnabled(LogLevel logLevel);
   IDisposable BeginScope<TState>(TState state);
}

internal sealed class Logger : ILogger {
   public LoggerInformation[] Loggers { get; set; }
   public MessageLogger[] MessageLoggers { get; set; }
   public ScopeLogger[] ScopeLoggers { get; set; }

   public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
      MessageLogger[] loggers = MessageLoggers;

      List<Exception> exceptions = null;
      for (int i = 0; i < loggers.Length; i++) {
         ref readonly MessageLogger loggerInfo = ref loggers[i];
         if (!loggerInfo.IsEnabled(logLevel)) {
            continue;
         }
         LoggerLog(logLevel, eventId, loggerInfo.Logger, exception, formatter, ref exceptions, state);
      }

      if (exceptions != null && exceptions.Count > 0) {
         ThrowLoggingError(exceptions);
      }

      static void LoggerLog(LogLevel logLevel, EventId eventId, ILogger logger, Exception exception, Func<TState, Exception, string> formatter, ref List<Exception> exceptions, in TState state) {
         try {
            logger.Log(logLevel, eventId, state, exception, formatter);
         }
         catch (Exception ex) {
            if (exceptions == null)
               exceptions = new List<Exception>();
            exceptions.Add(ex);
         }
      }
   }

   public bool IsEnabled(LogLevel logLevel) {
      MessageLogger[] loggers = MessageLoggers;

      List<Exception> exceptions = null;
      int i = 0;
      for (; i < loggers.Length; i++) {
         ref readonly MessageLogger loggerInfo = ref loggers[i];
         if (!loggerInfo.IsEnabled(logLevel)) {
            continue;
         }
         if (LoggerIsEnabled(logLevel, loggerInfo.Logger, ref exceptions)) {
            break;
         }
      }

      if (exceptions != null && exceptions.Count > 0) {
         ThrowLoggingError(exceptions);
      }

      return i < loggers.Length ? true : false;

      static bool LoggerIsEnabled(LogLevel logLevel, ILogger logger, ref List<Exception> exceptions) {
         try {
            if (logger.IsEnabled(logLevel)) {
               return true;
            }
         }
         catch (Exception ex) {
            if (exceptions == null)
               exceptions = new List<Exception>();
            exceptions.Add(ex);
         }

         return false;
      }
   }

   public IDisposable BeginScope<TState>(TState state) { 
      ScopeLogger[] loggers = ScopeLoggers;
      
      if (loggers.Length == 1) {
         return loggers[0].CreateScope(state);
      }

      var scope = new Scope(loggers.Length);
      List<Exception> exceptions = null;
      for (int i = 0; i < loggers.Length; i++) {
         ref readonly ScopeLogger scopeLogger = ref loggers[i];

         try {
            scope.SetDisposable(i, scopeLogger.CreateScope(state));
         }
         // catch         
      }

      return scope;
   }

   private sealed class Scope : IDisposable {
      private bool _isDisposed;

      private IDisposable _disposable0;
      private IDisposable _disposable1;
      private readonly IDisposable[] _disposable;

      public Scope(int count) {
         if (count > 2) {
            _disposable = new IDisposable[count - 2];
         }
      }

      public void SetDisposable(int index, IDisposable disposable) {
         switch (index) {
            case 0:
               _disposable0 = disposable;
            case 1:
               _disposable1 = disposable;
               break;
            default:
               _disposable[index - 2] = disposable;
               break;
         }
      }

      public void Dispose() {
         if (!_isDisposed) {
            _disposable0?.Dispose();
            _disposable1?.Dispose();

            if (_disposable != null) {
               int count = _disposable.Length;
               for (int index = 0; index != count; ++index) {
                  if (_disposable[index] != null) {
                     _disposable[index].Dispose();
                  }
               }
            }

            _isDisposed = true;
         }
      }
   }
}

public class Logger<T> : ILogger<T> {
   private readonly ILogger _logger;

   public Logger(ILoggerFactory factory) {   // ILoggerFactory is registered via services.AddLogging()
      _logger = factory.CreateLogger(TypeNameHelper.GetTypeDisplayName(typeof(T), includeGenericParameters: false, nestedTypeDelimiter: '.'));
   }

   IDisposable ILogger.BeginScope<TState>(TState state) {
      return _logger.BeginScope(state);
   }

   bool ILogger.IsEnabled(LogLevel logLevel) {
      return _logger.IsEnabled(logLevel);
   }

   void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
      _logger.Log(logLevel, eventId, state, exception, formatter);
   }
}
//------------------------------------------------------------------Ʌ
```
Note that both `ILoggerFactory` and `ILogger<T>` has been registered via `services.AddLogging()` (in `Host.CreateDefaultBuilder()`) as :
```C#
public static class LoggingServiceCollectionExtensions {
   
   public static IServiceCollection AddLogging(this IServiceCollection services) {
      return AddLogging(services, builder => { });
   }

   public static IServiceCollection AddLogging(this IServiceCollection services, Action<ILoggingBuilder> configure) {
      services.AddOptions();

      services.TryAdd(ServiceDescriptor.Singleton<ILoggerFactory, LoggerFactory>());
      services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));
      // ...
      return services;
   }
}
```
That's why you can use `ILogger<T>` on the fly as:
```C#
public class MyService {

   public MyService(ILogger<MyService> logger) {
      // ...
   }
   // ...
}
```

You can see a lot of buillt-in middlewares that uses DI to inject a logger:
```C#
public class CookiePolicyMiddleware {
   private readonly RequestDelegate _next;
   private readonly ILogger _logger;
                                                       // <-------------------------------b0
   public CookiePolicyMiddleware(RequestDelegate next, IOptions<CookiePolicyOptions> options, ILoggerFactory factory) {
      // ...
      _next = next;
      _logger = factory.CreateLogger<CookiePolicyMiddleware>();
   }

   public Task Invoke(HttpContext context) {
      var feature = context.Features.Get<IResponseCookiesFeature>() ?? new ResponseCookiesFeature(context.Features) // <-------------b2
      var wrapper = new ResponseCookiesWrapper(context, Options, feature, _logger);
      context.Features.Set<IResponseCookiesFeature>(new CookiesWrapperFeature(wrapper));
      context.Features.Set<ITrackingConsentFeature>(wrapper);

      return _next(context);
   }
}
```
<!-- <code>&lt;T&gt;<code> -->

<!-- <div class="alert alert-info p-1" role="alert">
    
</div> -->

<!-- <div class="alert alert-info pt-2 pb-0" role="alert">
    <ul class="pl-1">
      <li></li>
      <li></li>
    </ul>  
</div> -->

<!-- <ul>
  <li></li>
  <li></li>
  <li></li>
  <li></li>
</ul>  -->

<!-- <ul>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
</ul>  -->

<!-- ![alt text](./zImages/16-1.png "Title") -->

<!-- <span style="color:red">hurt</span> -->

<style type="text/css">
.markdown-body {
  max-width: 1800px;
  margin-left: auto;
  margin-right: auto;
}
</style>

<link rel="stylesheet" href="./zCSS/bootstrap.min.css">
<script src="./zCSS/jquery-3.3.1.slim.min.js"></script>
<script src="./zCSS/popper.min.js"></script>
<script src="./zCSS/bootstrap.min.js"></script>