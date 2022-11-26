Demystifying Logging
==============================

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
           .ConfigureLogging(logger => {       // logger is ILoggingBuilder
              logger.AddConsole(options => {   // options is ConsoleLoggerOptions
                 options.IncludeScopes = true;
              });
           });
}
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

## Source Code

```C#
//-----------------------------------------V
public static class ConsoleLoggerExtensions
{
   public static ILoggingBuilder AddConsole(this ILoggingBuilder builder)
   {
      builder.AddConfiguration();  // <--------------------------------------------------------------------------1
                                   // register LoggerProviderConfigurationFactory and LoggerProviderConfiguration

      builder.AddConsoleFormatter<JsonConsoleFormatter, JsonConsoleFormatterOptions>();   // <-------------------2
      builder.AddConsoleFormatter<SystemdConsoleFormatter, ConsoleFormatterOptions>();
      builder.AddConsoleFormatter<SimpleConsoleFormatter, SimpleConsoleFormatterOptions>();

      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ConsoleLoggerProvider>());
      LoggerProviderOptions.RegisterProviderOptions<ConsoleLoggerOptions, ConsoleLoggerProvider>(builder.Services);
 
      return builder;
   }

   public static ILoggingBuilder AddConsole(this ILoggingBuilder builder, Action<ConsoleLoggerOptions> configure)
   {
      builder.AddConsole();
      builder.Services.Configure(configure);
 
      return builder;
   }

   public static ILoggingBuilder AddSimpleConsole(this ILoggingBuilder builder)
   {
      return builder.AddFormatterWithName(ConsoleFormatterNames.Simple);
   }

   public static ILoggingBuilder AddJsonConsole(this ILoggingBuilder builder)
   {
      return builder.AddFormatterWithName(ConsoleFormatterNames.Json);
   }
            
   public static ILoggingBuilder AddJsonConsole(this ILoggingBuilder builder, Action<JsonConsoleFormatterOptions> configure)
   {
      return builder.AddConsoleWithFormatter<JsonConsoleFormatterOptions>(ConsoleFormatterNames.Json, configure);
   } 

   public static ILoggingBuilder AddSystemdConsole(this ILoggingBuilder builder, Action<ConsoleFormatterOptions> configure)
   {
      return builder.AddConsoleWithFormatter<ConsoleFormatterOptions>(ConsoleFormatterNames.Systemd, configure);
   }    

   internal static ILoggingBuilder AddConsoleWithFormatter<TOptions>(this ILoggingBuilder builder, string name, Action<TOptions> configure)
   {
      builder.AddFormatterWithName(name);
      builder.Services.Configure(configure);
 
      return builder;
   }

   private static ILoggingBuilder AddFormatterWithName(this ILoggingBuilder builder, string name)
   {
      return builder.AddConsole((ConsoleLoggerOptions options) => options.FormatterName = name);
   }

   public static ILoggingBuilder AddConsoleFormatter<TFormatter, TOptions>(this ILoggingBuilder builder)  // <-----------------------3
   {
      builder.AddConfiguration();
 
      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ConsoleFormatter, TFormatter>());
      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<TOptions>, ConsoleLoggerFormatterConfigureOptions<TFormatter, TOptions>>());
      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IOptionsChangeTokenSource<TOptions>, ConsoleLoggerFormatterOptionsChangeTokenSource<TFormatter, TOptions>>());
 
      return builder;
   }

   public static ILoggingBuilder AddConsoleFormatter<TFormatter, TOptions>(this ILoggingBuilder builder, Action<TOptions> configure)
   { 
      builder.AddConsoleFormatter<TFormatter, TOptions>();
      builder.Services.Configure(configure);
      return builder;
   }
}
//-----------------------------------------Ʌ

//----------------------------------------------V
public static class EventLoggerFactoryExtensions
{
   public static ILoggingBuilder AddEventLog(this ILoggingBuilder builder)
   { 
      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, EventLogLoggerProvider>());
 
      return builder;
   }

   public static ILoggingBuilder AddEventLog(this ILoggingBuilder builder, EventLogSettings settings)
   {
      builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider>(new EventLogLoggerProvider(settings)));
 
      return builder;
   }

   public static ILoggingBuilder AddEventLog(this ILoggingBuilder builder, Action<EventLogSettings> configure)
   {
      builder.AddEventLog();
      builder.Services.Configure(configure);
 
      return builder;
   }
}

//--------------------------->>>
public class EventLogSettings
{
   private IEventLog? _eventLog;
   public string? LogName { get; set; }
   public string? SourceName { get; set; }
   public string? MachineName { get; set; }
   public Func<string, LogLevel, bool>? Filter { get; set; }

   internal IEventLog EventLog
   {
      get => _eventLog ??= CreateDefaultEventLog();
 
      // for unit testing purposes only.
      set => _eventLog = value;
   }

   private IEventLog CreateDefaultEventLog()
   {
      string logName = string.IsNullOrEmpty(LogName) ? "Application" : LogName;
      string machineName = string.IsNullOrEmpty(MachineName) ? "." : MachineName;
      string sourceName = string.IsNullOrEmpty(SourceName) ? ".NET Runtime" : SourceName;
      int? defaultEventId = null;
 
      if (string.IsNullOrEmpty(SourceName))
      {
         sourceName = ".NET Runtime";
         defaultEventId = 1000;
      }
 
      return new WindowsEventLog(logName, machineName, sourceName) { DefaultEventId = defaultEventId };
   }
}
//---------------------------<<<
//----------------------------------------------Ʌ

//---------------------------------->>
public class ConsoleFormatterOptions
{
   public ConsoleFormatterOptions() { }
   public bool IncludeScopes { get; set; }
   public string? TimestampFormat { get; set; }
   public bool UseUtcTimestamp { get; set; }
}
//----------------------------------<<

//-------------------------------------->>
public class JsonConsoleFormatterOptions : ConsoleFormatterOptions
{
   public JsonConsoleFormatterOptions() { }
   public JsonWriterOptions JsonWriterOptions { get; set; }
}
//--------------------------------------<<

//----------------------------->>
public struct JsonWriterOptions
{
   internal const int DefaultMaxDepth = 1000;
 
   private int _maxDepth;
   private int _optionsMask;
   public JavaScriptEncoder? Encoder { get; set; }

   public bool Indented
   {
      get
      {
         return (_optionsMask & IndentBit) != 0;
      }
      set
      {
         if (value)
            _optionsMask |= IndentBit;
         else
            _optionsMask &= ~IndentBit;
      }
   }

   public int MaxDepth { get; set; }

   public bool SkipValidation
   {
      get
      {
         return (_optionsMask & SkipValidationBit) != 0;
      }
      set
      {
         if (value)
            _optionsMask |= SkipValidationBit;
         else
            _optionsMask &= ~SkipValidationBit;
      }
   }

   internal bool IndentedOrNotSkipValidation => _optionsMask != SkipValidationBit; // Equivalent to: Indented || !SkipValidation;
 
   private const int IndentBit = 1;
   private const int SkipValidationBit = 2;
}
//-----------------------------<<

//------------------------------------------------------V
public static class LoggingBuilderConfigurationExtensions
{
   public static void AddConfiguration(this ILoggingBuilder builder)   // <-----------------------2
   {
      builder.Services.TryAddSingleton<ILoggerProviderConfigurationFactory, LoggerProviderConfigurationFactory>();
      builder.Services.TryAddSingleton(typeof(ILoggerProviderConfiguration<>), typeof(LoggerProviderConfiguration<>));
   }
}
//------------------------------------------------------Ʌ

//-------------------------------------------------->>
public interface ILoggerProviderConfigurationFactory 
{
   IConfiguration GetConfiguration(Type providerType);
}
//--------------------------------------------------<<

//------------------------------------------------------V
internal sealed class LoggerProviderConfigurationFactory : ILoggerProviderConfigurationFactory  
{
   private readonly IEnumerable<LoggingConfiguration> _configurations;
 
   public LoggerProviderConfigurationFactory(IEnumerable<LoggingConfiguration> configurations)
   {
      _configurations = configurations;
   }

   public IConfiguration GetConfiguration(Type providerType)
   {
      string fullName = providerType.FullName!;
      string? alias = ProviderAliasUtilities.GetAlias(providerType);
      ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();

      foreach (LoggingConfiguration configuration in _configurations)
      {
         IConfigurationSection sectionFromFullName = configuration.Configuration.GetSection(fullName);
         configurationBuilder.AddConfiguration(sectionFromFullName);
 
         if (!string.IsNullOrWhiteSpace(alias))
         {
            IConfigurationSection sectionFromAlias = configuration.Configuration.GetSection(alias);
            configurationBuilder.AddConfiguration(sectionFromAlias);
         }
      }
      return configurationBuilder.Build();
   }
}
//------------------------------------------------------Ʌ

//---------------------------------------------->>
public interface ILoggerProviderConfiguration<T>
{
   IConfiguration Configuration { get; }
}
//----------------------------------------------<<

//--------------------------------------------------V
internal sealed class LoggerProviderConfiguration<T> : ILoggerProviderConfiguration<T>
{
   public LoggerProviderConfiguration(ILoggerProviderConfigurationFactory providerConfigurationFactory)
   {
      Configuration = providerConfigurationFactory.GetConfiguration(typeof(T));
   }
 
   public IConfiguration Configuration { get; }
}
//--------------------------------------------------Ʌ

//------------------------------>>
public interface ILoggerProvider : IDisposable
{
   ILogger CreateLogger(string categoryName);
}
//------------------------------<<

//------------------------------------>>
public interface ISupportExternalScope
{
   void SetScopeProvider(IExternalScopeProvider scopeProvider);
}
//------------------------------------<<

//------------------------------------->>
public interface IExternalScopeProvider
{
   void ForEachScope<TState>(Action<object?, TState> callback, TState state);
   IDisposable Push(object? state);
}
//-------------------------------------<<

//--------------------------------V
public class ConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope   // ConsoleLoggerProvider implements ISupportExternalScope
{
   private readonly IOptionsMonitor<ConsoleLoggerOptions> _options;
   private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers;
   private ConcurrentDictionary<string, ConsoleFormatter> _formatters;
   private readonly ConsoleLoggerProcessor _messageQueue;

   private IDisposable? _optionsReloadToken;
   private IExternalScopeProvider _scopeProvider = NullExternalScopeProvider.Instance;

   public ConsoleLoggerProvider(IOptionsMonitor<ConsoleLoggerOptions> options) : this(options, Array.Empty<ConsoleFormatter>()) { }

   public ConsoleLoggerProvider(IOptionsMonitor<ConsoleLoggerOptions> options, IEnumerable<ConsoleFormatter>? formatters)
   {
       _options = options;
      _loggers = new ConcurrentDictionary<string, ConsoleLogger>();
      SetFormatters(formatters);
      IConsole? console;
      IConsole? errorConsole;
      if (DoesConsoleSupportAnsi())
      {
         console = new AnsiLogConsole();
         errorConsole = new AnsiLogConsole(stdErr: true);
      }
      else
      {
         console = new AnsiParsingLogConsole();
         errorConsole = new AnsiParsingLogConsole(stdErr: true);
      }
      _messageQueue = new ConsoleLoggerProcessor(console, errorConsole, options.CurrentValue.QueueFullMode, options.CurrentValue.MaxQueueLength);
 
      ReloadLoggerOptions(options.CurrentValue);
      _optionsReloadToken = _options.OnChange(ReloadLoggerOptions);
   }

   private static bool DoesConsoleSupportAnsi() { ... }

   private void SetFormatters(IEnumerable<ConsoleFormatter>? formatters = null)
   {
      var cd = new ConcurrentDictionary<string, ConsoleFormatter>(StringComparer.OrdinalIgnoreCase);
 
      bool added = false;
      if (formatters != null)
      {
         foreach (ConsoleFormatter formatter in formatters)
         {
            cd.TryAdd(formatter.Name, formatter);
            added = true;
         }
      }
 
      if (!added)
      {
         cd.TryAdd(ConsoleFormatterNames.Simple, new SimpleConsoleFormatter(new FormatterOptionsMonitor<SimpleConsoleFormatterOptions>(new SimpleConsoleFormatterOptions())));
         cd.TryAdd(ConsoleFormatterNames.Systemd, new SystemdConsoleFormatter(new FormatterOptionsMonitor<ConsoleFormatterOptions>(new ConsoleFormatterOptions())));
         cd.TryAdd(ConsoleFormatterNames.Json, new JsonConsoleFormatter(new FormatterOptionsMonitor<JsonConsoleFormatterOptions>(new JsonConsoleFormatterOptions())));
      }
 
      _formatters = cd;
   }

   private void ReloadLoggerOptions(ConsoleLoggerOptions options)
   {
      if (options.FormatterName == null || !_formatters.TryGetValue(options.FormatterName, out ConsoleFormatter? logFormatter))
      {
         logFormatter = options.Format switch
         {
            ConsoleLoggerFormat.Systemd => _formatters[ConsoleFormatterNames.Systemd],
            _ => _formatters[ConsoleFormatterNames.Simple],
         };
         if (options.FormatterName == null)
         {
            UpdateFormatterOptions(logFormatter, options);
         }
      }

      _messageQueue.FullMode = options.QueueFullMode;
      _messageQueue.MaxQueueLength = options.MaxQueueLength;
 
      foreach (KeyValuePair<string, ConsoleLogger> logger in _loggers)
      {
         logger.Value.Options = options;
         logger.Value.Formatter = logFormatter;
      }
   }

   public ILogger CreateLogger(string name)
   {
      if (_options.CurrentValue.FormatterName == null || !_formatters.TryGetValue(_options.CurrentValue.FormatterName, out ConsoleFormatter? logFormatter))
      {
         logFormatter = _options.CurrentValue.Format switch
         {
            ConsoleLoggerFormat.Systemd => _formatters[ConsoleFormatterNames.Systemd],
            _ => _formatters[ConsoleFormatterNames.Simple],
         };

         if (_options.CurrentValue.FormatterName == null)
         {
            UpdateFormatterOptions(logFormatter, _options.CurrentValue);
         }
      }

      return _loggers.TryGetValue(name, out ConsoleLogger? logger) ? logger : 
         _loggers.GetOrAdd(name, new ConsoleLogger(name, _messageQueue, logFormatter, _scopeProvider, _options.CurrentValue));
   }

   private static void UpdateFormatterOptions(ConsoleFormatter formatter, ConsoleLoggerOptions deprecatedFromOptions)
   {
      // kept for deprecated apis:
      if (formatter is SimpleConsoleFormatter defaultFormatter)
      {
         defaultFormatter.FormatterOptions = new SimpleConsoleFormatterOptions()
         {
            ColorBehavior = deprecatedFromOptions.DisableColors ? LoggerColorBehavior.Disabled : LoggerColorBehavior.Default,
            IncludeScopes = deprecatedFromOptions.IncludeScopes,
            TimestampFormat = deprecatedFromOptions.TimestampFormat,
            UseUtcTimestamp = deprecatedFromOptions.UseUtcTimestamp,
         };
      }
      else if (formatter is SystemdConsoleFormatter systemdFormatter)
      {
         systemdFormatter.FormatterOptions = new ConsoleFormatterOptions()
         {
            IncludeScopes = deprecatedFromOptions.IncludeScopes,
            TimestampFormat = deprecatedFromOptions.TimestampFormat,
            UseUtcTimestamp = deprecatedFromOptions.UseUtcTimestamp,
         };
      }
   }

   public void Dispose()
   {
      _optionsReloadToken?.Dispose();
      _messageQueue.Dispose();
   }

   public void SetScopeProvider(IExternalScopeProvider scopeProvider)
   {
      _scopeProvider = scopeProvider;
      foreach (System.Collections.Generic.KeyValuePair<string, ConsoleLogger> logger in _loggers)
      {
         logger.Value.ScopeProvider = _scopeProvider;
      }
   }
}
//--------------------------------Ʌ

//-------------------------------------V
public readonly struct LogEntry<TState>
{
   public LogEntry(LogLevel logLevel, string category, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
   {
      LogLevel = logLevel;
      Category = category;
      EventId = eventId;
      State = state;
      Exception = exception;
      Formatter = formatter;
   }

   public LogLevel LogLevel { get; }
   public string Category { get; }
   public EventId EventId { get; }
   public TState State { get; }
   public Exception? Exception { get; }
   public Func<TState, Exception?, string> Formatter { get; }
}
//-------------------------------------Ʌ

//------------------------------------V
public abstract class ConsoleFormatter
{
   protected ConsoleFormatter(string name)
   {
      Name = name;
   }

   public string Name { get; }

   public abstract void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter);
}
//------------------------------------Ʌ

//----------------------------------------V
internal sealed class JsonConsoleFormatter : ConsoleFormatter, IDisposable
{
   private IDisposable? _optionsReloadToken;
 
   public JsonConsoleFormatter(IOptionsMonitor<JsonConsoleFormatterOptions> options) : base(ConsoleFormatterNames.Json)
   {
      ReloadLoggerOptions(options.CurrentValue);
      _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
   }

   public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
   {
      string message = logEntry.Formatter(logEntry.State, logEntry.Exception);

      if (logEntry.Exception == null && message == null)
         return;

      LogLevel logLevel = logEntry.LogLevel;
      string category = logEntry.Category;
      int eventId = logEntry.EventId.Id;
      Exception? exception = logEntry.Exception;
      const int DefaultBufferSize = 1024;

      using (var output = new PooledByteBufferWriter(DefaultBufferSize))
      {
         using (var writer = new Utf8JsonWriter(output, FormatterOptions.JsonWriterOptions))
         {
            writer.WriteStartObject();
            var timestampFormat = FormatterOptions.TimestampFormat;
            if (timestampFormat != null)
            {
               DateTimeOffset dateTimeOffset = FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
               writer.WriteString("Timestamp", dateTimeOffset.ToString(timestampFormat));
            }
            writer.WriteNumber(nameof(logEntry.EventId), eventId);
            writer.WriteString(nameof(logEntry.LogLevel), GetLogLevelString(logLevel));
            writer.WriteString(nameof(logEntry.Category), category);
            writer.WriteString("Message", message);
 
            if (exception != null)
            {
               string exceptionMessage = exception.ToString();
               if (!FormatterOptions.JsonWriterOptions.Indented)
               {
                  exceptionMessage = exceptionMessage.Replace(Environment.NewLine, " ");
               }
               writer.WriteString(nameof(Exception), exceptionMessage);
            }

            if (logEntry.State != null)
            {
               writer.WriteStartObject(nameof(logEntry.State));
               writer.WriteString("Message", logEntry.State.ToString());
               if (logEntry.State is IReadOnlyCollection<KeyValuePair<string, object>> stateProperties)
               {
                  foreach (KeyValuePair<string, object> item in stateProperties)
                  {
                     WriteItem(writer, item);
                  }
               }
               writer.WriteEndObject();
            }
            WriteScopeInformation(writer, scopeProvider);
            writer.WriteEndObject();
            writer.Flush();
         }

         textWriter.Write(Encoding.UTF8.GetString(output.WrittenMemory.Span));
      }

      textWriter.Write(Environment.NewLine);
   }

   private static string GetLogLevelString(LogLevel logLevel)
   {
      return logLevel switch
      {
         LogLevel.Trace => "Trace",
         LogLevel.Debug => "Debug",
         LogLevel.Information => "Information",
         LogLevel.Warning => "Warning",
         LogLevel.Error => "Error",
         LogLevel.Critical => "Critical",
         _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
      };
   }

   private void WriteScopeInformation(Utf8JsonWriter writer, IExternalScopeProvider? scopeProvider)
   {
      if (FormatterOptions.IncludeScopes && scopeProvider != null)
      {
         writer.WriteStartArray("Scopes");
         scopeProvider.ForEachScope((scope, state) =>
         {
            if (scope is IEnumerable<KeyValuePair<string, object>> scopeItems)
            {
               state.WriteStartObject();
               state.WriteString("Message", scope.ToString());
               foreach (KeyValuePair<string, object> item in scopeItems)
               {
                  WriteItem(state, item);
               }
               state.WriteEndObject();
            }
            else
            {
               state.WriteStringValue(ToInvariantString(scope));
            }
         }, writer);
         writer.WriteEndArray();
      }
   }

   private static void WriteItem(Utf8JsonWriter writer, KeyValuePair<string, object> item)
   {
      var key = item.Key;
      switch (item.Value)
      {
         case bool boolValue:
            writer.WriteBoolean(key, boolValue);
            break;
         case byte byteValue:
            writer.WriteNumber(key, byteValue);
            break;
         case sbyte sbyteValue:
            writer.WriteNumber(key, sbyteValue);
            break;
         case char charValue:
            writer.WriteString(key, charValue.ToString());
            break;
         case int intValue:
            writer.WriteNumber(key, intValue);
            break;
         ...
         case null:
            writer.WriteNull(key);
            break;
         default:
            writer.WriteString(key, ToInvariantString(item.Value));
            break;
      }
   }

   private static string? ToInvariantString(object? obj) => Convert.ToString(obj, CultureInfo.InvariantCulture);
 
   internal JsonConsoleFormatterOptions FormatterOptions { get; set; }
 
   private void ReloadLoggerOptions(JsonConsoleFormatterOptions options)
   {
      FormatterOptions = options;
   }
 
   public void Dispose()
   {
      _optionsReloadToken?.Dispose();
   }
}
//----------------------------------------Ʌ
```


```C#
//------------------V
public enum LogLevel 
{

   Trace = 0,        // logs that contain the most detailed messages. These messages may contain sensitive application data
                     // these messages are disabled by default and should never be enabled in a production environment

   Debug = 1,        // logs that are used for interactive investigation during development
                     // These logs should primarily contain information useful for debugging and have no long-term value

   Information = 2,  // logs that track the general flow of the application. These logs should have long-term value
   
   Warning = 3,      // logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the application to stop

   Error = 4,        // logs that highlight when the current flow of execution is stopped due to a failure 
                     // these should indicate a failure in the current activity, not an application-wide failure

   Critical =5,      // logs that describe an unrecoverable application or system crash, or a catastrophic failure that requires immediate attention

   None = 6          // not used for writing log messages. Specifies that a logging category should not write any message
}
//------------------Ʌ

//------------------------------>>
public interface ILoggingBuilder   // an interface for configuring logging providers
{
   IServiceCollection Services { get; }
}
//------------------------------<<

//----------------------------------V
internal sealed class LoggingBuilder : ILoggingBuilder
{
   public LoggingBuilder(IServiceCollection services)
   {
      Services = services;
   }
 
   public IServiceCollection Services { get; }
}
//----------------------------------Ʌ

//-------------------------------V
public class ConsoleLoggerOptions
{
   internal const int DefaultMaxQueueLengthValue = 2500;
   private int _maxQueuedMessages = DefaultMaxQueueLengthValue;

   public bool DisableColors { get; set; }
   
   private ConsoleLoggerFormat _format = ConsoleLoggerFormat.Default;
   
   public ConsoleLoggerFormat Format
   {
      get => _format;
      set
      {
         if (value < ConsoleLoggerFormat.Default || value > ConsoleLoggerFormat.Systemd)
         {
            throw new ArgumentOutOfRangeException(nameof(value));
         }
         _format = value;
      }
   }

   public string? FormatterName { get; set; }
   
   public bool IncludeScopes { get; set; }

   public LogLevel LogToStandardErrorThreshold { get; set; } = LogLevel.None;

   public string? TimestampFormat { get; set; }

   public bool UseUtcTimestamp { get; set; }

   private ConsoleLoggerQueueFullMode _queueFullMode = ConsoleLoggerQueueFullMode.Wait;

   public ConsoleLoggerQueueFullMode QueueFullMode
   {
      get => _queueFullMode;
      set
      {
         if (value != ConsoleLoggerQueueFullMode.Wait && value != ConsoleLoggerQueueFullMode.DropWrite)
         {
            throw new ArgumentOutOfRangeException(SR.Format(SR.QueueModeNotSupported, nameof(value)));
         }
         _queueFullMode = value;
      }
   }

   public int MaxQueueLength
   {
      get => _maxQueuedMessages;
      set
      {
         if (value <= 0)
         {
            throw new ArgumentOutOfRangeException(SR.Format(SR.MaxQueueLengthBadValue, nameof(value)));
         }
 
         _maxQueuedMessages = value;
      }
   }
}
//--------------------------------Ʌ

//----------------------V
public interface ILogger 
{
   void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

   bool IsEnabled(LogLevel logLevel);

   IDisposable BeginScope<TState>(TState state);
}
//----------------------Ʌ

//----------------------------V
public readonly struct EventId : IEquatable<EventId>
{
   public EventId(int id, string? name = null)
   {
      Id = id;
      Name = name;
   }

   public int Id { get; }

   public string? Name { get; }

   public static implicit operator EventId(int i)
   {
      return new EventId(i);
   }

   public static bool operator ==(EventId left, EventId right)
   {
      return left.Equals(right);
   }

   // ...
}
//----------------------------Ʌ

//------------------------------>>
public class LoggerFilterOptions
{
   public LoggerFilterOptions() { }
   public bool CaptureScopes { get; set; } = true;
   public LogLevel MinLevel { get; set; }
   public IList<LoggerFilterRule> Rules => RulesInternal;
   internal List<LoggerFilterRule> RulesInternal { get; } = new List<LoggerFilterRule>();
}
//------------------------------<<

//--------------------------->>
public class LoggerFilterRule
{
   public LoggerFilterRule(string? providerName, string? categoryName, LogLevel? logLevel, Func<string?, string?, LogLevel, bool>? filter)
   {
      ProviderName = providerName;
      CategoryName = categoryName;
      LogLevel = logLevel;
      Filter = filter;
   }

   public string? ProviderName { get; }
   public string? CategoryName { get; }
   public LogLevel? LogLevel { get; }
   public Func<string?, string?, LogLevel, bool>? Filter { get; }

   public override string ToString()
   {
      return $"{nameof(ProviderName)}: '{ProviderName}', {nameof(CategoryName)}: '{CategoryName}', {nameof(LogLevel)}: '{LogLevel}', {nameof(Filter)}: '{Filter}'";
   }
}
//---------------------------<<

//----------------------------------------->>
public static class LoggerFactoryExtensions
{
   public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory)
   {
      return new Logger<T>(factory);
   }

   public static ILogger CreateLogger(this ILoggerFactory factory, Type type)
   {
      return factory.CreateLogger(TypeNameHelper.GetTypeDisplayName(type, includeGenericParameters: false, nestedTypeDelimiter: '.'));
   }
}
//-----------------------------------------<<

//-----------------------------V
public interface ILoggerFactory : IDisposable
{
   ILogger CreateLogger(string categoryName);
   void AddProvider(ILoggerProvider provider);
}
//-----------------------------Ʌ

//------------------------V
public class LoggerFactory : ILoggerFactory
{
   private readonly Dictionary<string, Logger> _loggers = new Dictionary<string, Logger>(StringComparer.Ordinal);
   private readonly List<ProviderRegistration> _providerRegistrations = new List<ProviderRegistration>();
   private readonly object _sync = new object();
   private volatile bool _disposed;
   private IDisposable? _changeTokenRegistration;
   private LoggerFilterOptions _filterOptions;
   private IExternalScopeProvider? _scopeProvider;
   private LoggerFactoryOptions _factoryOptions;

   public LoggerFactory() : this(Array.Empty<ILoggerProvider>()) { }
   public LoggerFactory(IEnumerable<ILoggerProvider> providers) : this(providers, new StaticFilterOptionsMonitor(new LoggerFilterOptions())) { }
   public LoggerFactory(IEnumerable<ILoggerProvider> providers, LoggerFilterOptions filterOptions) : this(providers, new StaticFilterOptionsMonitor(filterOptions)) { }
   // ...

   public LoggerFactory(IEnumerable<ILoggerProvider> providers, IOptionsMonitor<LoggerFilterOptions> filterOption, IOptions<LoggerFactoryOptions>? options = null, IExternalScopeProvider? scopeProvider = null)
   {
      _scopeProvider = scopeProvider;
 
      _factoryOptions = options == null || options.Value == null ? new LoggerFactoryOptions() : options.Value;
 
      const ActivityTrackingOptions ActivityTrackingOptionsMask = ~(ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId | ActivityTrackingOptions.ParentId |
                                                                          ActivityTrackingOptions.TraceFlags | ActivityTrackingOptions.TraceState | ActivityTrackingOptions.Tags
                                                                          | ActivityTrackingOptions.Baggage);
 
 
      if ((_factoryOptions.ActivityTrackingOptions & ActivityTrackingOptionsMask) != 0)
      {
         throw new ArgumentException(SR.Format(SR.InvalidActivityTrackingOptions, _factoryOptions.ActivityTrackingOptions), nameof(options));
      }
 
      foreach (ILoggerProvider provider in providers)
      {
         AddProviderRegistration(provider, dispose: false);
      }
 
      _changeTokenRegistration = filterOption.OnChange(RefreshFilters);
      RefreshFilters(filterOption.CurrentValue);
   }

   public static ILoggerFactory Create(Action<ILoggingBuilder> configure)
   {
      var serviceCollection = new ServiceCollection();
      serviceCollection.AddLogging(configure);
      ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
      ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
      return new DisposingLoggerFactory(loggerFactory, serviceProvider);
   }

   public ILogger CreateLogger(string categoryName)
   {
      if (CheckDisposed())
      {
         throw new ObjectDisposedException(nameof(LoggerFactory));
      }
 
      lock (_sync)
      {
         if (!_loggers.TryGetValue(categoryName, out Logger? logger))
         {
            logger = new Logger(CreateLoggers(categoryName));
 
            (logger.MessageLoggers, logger.ScopeLoggers) = ApplyFilters(logger.Loggers);
 
            _loggers[categoryName] = logger;
         }
 
         return logger;
      }
   }

   private LoggerInformation[] CreateLoggers(string categoryName)
   {
      var loggers = new LoggerInformation[_providerRegistrations.Count];
      for (int i = 0; i < _providerRegistrations.Count; i++)
      {
         loggers[i] = new LoggerInformation(_providerRegistrations[i].Provider, categoryName);
      }
      return loggers;
   }

   private (MessageLogger[] MessageLoggers, ScopeLogger[]? ScopeLoggers) ApplyFilters(LoggerInformation[] loggers)
   {
      var messageLoggers = new List<MessageLogger>();
      List<ScopeLogger>? scopeLoggers = _filterOptions.CaptureScopes ? new List<ScopeLogger>() : null;
 
      foreach (LoggerInformation loggerInformation in loggers)
      {
         LoggerRuleSelector.Select(_filterOptions,
                    loggerInformation.ProviderType,
                    loggerInformation.Category,
                    out LogLevel? minLevel,
                    out Func<string?, string?, LogLevel, bool>? filter);
 
         if (minLevel is not null and > LogLevel.Critical)
            continue;
 
         messageLoggers.Add(new MessageLogger(loggerInformation.Logger, loggerInformation.Category, loggerInformation.ProviderType.FullName, minLevel, filter));
 
         if (!loggerInformation.ExternalScope)
            scopeLoggers?.Add(new ScopeLogger(logger: loggerInformation.Logger, externalScopeProvider: null));
      }
 
      if (_scopeProvider != null)
      {
         scopeLoggers?.Add(new ScopeLogger(logger: null, externalScopeProvider: _scopeProvider));
      }
 
      return (messageLoggers.ToArray(), scopeLoggers?.ToArray());
   }

   public void AddProvider(ILoggerProvider provider)
   {
      if (CheckDisposed())
         throw new ObjectDisposedException(nameof(LoggerFactory));
  
      lock (_sync)
      {
         AddProviderRegistration(provider, dispose: true);
 
         foreach (KeyValuePair<string, Logger> existingLogger in _loggers)
         {
            Logger logger = existingLogger.Value;
            LoggerInformation[] loggerInformation = logger.Loggers;
 
            int newLoggerIndex = loggerInformation.Length;
            Array.Resize(ref loggerInformation, loggerInformation.Length + 1);
            loggerInformation[newLoggerIndex] = new LoggerInformation(provider, existingLogger.Key);
 
            logger.Loggers = loggerInformation;
            (logger.MessageLoggers, logger.ScopeLoggers) = ApplyFilters(logger.Loggers);
         }
      }
   }

   private void AddProviderRegistration(ILoggerProvider provider, bool dispose)
   {
      _providerRegistrations.Add(new ProviderRegistration
      {
         Provider = provider,
         ShouldDispose = dispose
      });
 
      if (provider is ISupportExternalScope supportsExternalScope)
      {
         _scopeProvider ??= new LoggerFactoryScopeProvider(_factoryOptions.ActivityTrackingOptions);
 
         supportsExternalScope.SetScopeProvider(_scopeProvider);
      }
   }

   private void RefreshFilters(LoggerFilterOptions filterOptions)
   {
      lock (_sync)
      {
         _filterOptions = filterOptions;
         foreach (KeyValuePair<string, Logger> registeredLogger in _loggers)
         {
            Logger logger = registeredLogger.Value;
            (logger.MessageLoggers, logger.ScopeLoggers) = ApplyFilters(logger.Loggers);
         }
      }
   }

   protected virtual bool CheckDisposed() => _disposed;

   public void Dispose()
   {
      if (!_disposed)
      {
         _disposed = true;
 
         _changeTokenRegistration?.Dispose();
 
         foreach (ProviderRegistration registration in _providerRegistrations)
         {
            try
            {
               if (registration.ShouldDispose)
                  registration.Provider.Dispose();
                        
            }
            catch
            {
               // Swallow exceptions on dispose
            }
         }
      }
   }

   private struct ProviderRegistration
   {
      public ILoggerProvider Provider;
      public bool ShouldDispose;
   }

   private sealed class DisposingLoggerFactory : ILoggerFactory
   {
      private readonly ILoggerFactory _loggerFactory;
 
      private readonly ServiceProvider _serviceProvider;
 
      public DisposingLoggerFactory(ILoggerFactory loggerFactory, ServiceProvider serviceProvider)
      {
         _loggerFactory = loggerFactory;
         _serviceProvider = serviceProvider;
      }
 
      public void Dispose()
      {
         _serviceProvider.Dispose();
      }
 
      public ILogger CreateLogger(string categoryName)
      {
         return _loggerFactory.CreateLogger(categoryName);
      }
 
      public void AddProvider(ILoggerProvider provider)
      {
         _loggerFactory.AddProvider(provider);
      }
   }
}
//------------------------Ʌ

//--------------------------V
internal sealed class Logger : ILogger 
{
   public LoggerInformation[] Loggers { get; set; }
   public MessageLogger[] MessageLoggers { get; set; }
   public ScopeLogger[] ScopeLoggers { get; set; }

   public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
      MessageLogger[] loggers = MessageLoggers;

      List<Exception> exceptions = null;
      for (int i = 0; i < loggers.Length; i++) 
      {
         ref readonly MessageLogger loggerInfo = ref loggers[i];
         if (!loggerInfo.IsEnabled(logLevel))
            continue;

         LoggerLog(logLevel, eventId, loggerInfo.Logger, exception, formatter, ref exceptions, state);
      }

      if (exceptions != null && exceptions.Count > 0) 
      {
         ThrowLoggingError(exceptions);
      }

      static void LoggerLog(LogLevel logLevel, EventId eventId, ILogger logger, Exception exception, Func<TState, Exception, string> formatter, ref List<Exception> exceptions, in TState state) {
         try 
         {
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
      for (; i < loggers.Length; i++) 
      {
         ref readonly MessageLogger loggerInfo = ref loggers[i];
         if (!loggerInfo.IsEnabled(logLevel)) {
            continue;
         }
         if (LoggerIsEnabled(logLevel, loggerInfo.Logger, ref exceptions)) {
            break;
         }
      }

      if (exceptions != null && exceptions.Count > 0) 
         ThrowLoggingError(exceptions);
      

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

   public IDisposable BeginScope<TState>(TState state) 
   { 
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

   private sealed class Scope : IDisposable 
   {
      private bool _isDisposed;

      private IDisposable _disposable0;
      private IDisposable _disposable1;
      private readonly IDisposable[] _disposable;

      public Scope(int count) {
         if (count > 2) {
            _disposable = new IDisposable[count - 2];
         }
      }

      public void SetDisposable(int index, IDisposable disposable) 
      {
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

      public void Dispose() 
      {
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
//--------------------------Ʌ

//--------------------V
public class Logger<T> : ILogger<T> 
{
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
//--------------------Ʌ

//----------------------------------V
internal readonly struct ScopeLogger
{
   public ScopeLogger(ILogger? logger, IExternalScopeProvider? externalScopeProvider)
   {
      Debug.Assert(logger != null || externalScopeProvider != null, "Logger can't be null when there isn't an ExternalScopeProvider");
 
      Logger = logger;
      ExternalScopeProvider = externalScopeProvider;
   }

   public ILogger? Logger { get; }

   public IExternalScopeProvider? ExternalScopeProvider { get; }

   public IDisposable? CreateScope<TState>(TState state) where TState : notnull
   {
      if (ExternalScopeProvider != null)
         return ExternalScopeProvider.Push(state);
         
 
      Debug.Assert(Logger != null);
      return Logger.BeginScope<TState>(state);
   }
}
//----------------------------------Ʌ

//----------------------------------------V
internal readonly struct LoggerInformation
{
   public LoggerInformation(ILoggerProvider provider, string category) : this()
   {
      ProviderType = provider.GetType();
      Logger = provider.CreateLogger(category);
      Category = category;
      ExternalScope = provider is ISupportExternalScope;
   }
 
   public ILogger Logger { get; }
 
   public string Category { get; }
 
   public Type ProviderType { get; }
 
   public bool ExternalScope { get; }
}
//----------------------------------------Ʌ
```