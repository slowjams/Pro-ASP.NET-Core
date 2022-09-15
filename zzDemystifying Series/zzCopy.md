Demystifying ControllerActionInvoker
==============================

```C#
public class Startup {
   public void ConfigureServices(IServiceCollection services) 
   {  
      public void ConfigureServices(IServiceCollection services) {
         // ...
         services.AddControllers();
      }
   }

   public void Configure(IApplicationBuilder app, IWebHostEnvironment env) 
   {
      // ...
      app.UseRouting();

      app.UseEndpoints(endpoints => {
         endpoints.MapControllers();   // <--------------------------
      });
   }
}
```

DI related code:

```C#
public static class MvcServiceCollectionExtensions
{
    public static IMvcBuilder AddControllers(this IServiceCollection services)
    {
       var builder = AddControllersCore(services);
       return new MvcBuilder(builder.Services, builder.PartManager);
    }

    private static IMvcCoreBuilder AddControllersCore(IServiceCollection services)
    {
       // This method excludes all of the view-related services by default.
       var builder = services
          .AddMvcCore()  // <------------------------------
          .AddApiExplorer()
          .AddAuthorization()
          .AddCors()
          .AddDataAnnotations()
          .AddFormatterMappings();
 
       if (MetadataUpdater.IsSupported)
       {
          services.TryAddEnumerable(
          ServiceDescriptor.Singleton<IActionDescriptorChangeProvider, HotReloadService>());
       }
 
       return builder;
    }

    public static IMvcCoreBuilder AddMvcCore(this IServiceCollection services)  // <------------------------------
    {
       if (services == null)
       {
          throw new ArgumentNullException(nameof(services));
       }
 
       var environment = GetServiceFromCollection<IWebHostEnvironment>(services);
       var partManager = GetApplicationPartManager(services, environment);
       services.TryAddSingleton(partManager);
 
       ConfigureDefaultFeatureProviders(partManager);
       ConfigureDefaultServices(services);  // call services.AddRouting();
       AddMvcCoreServices(services);      // <------------------------------------------------
 
       var builder = new MvcCoreBuilder(services, partManager);
 
       return builder;
    }

    internal static void AddMvcCoreServices(IServiceCollection services)
    {
       // options
       services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, MvcCoreMvcOptionsSetup>());
       services.TryAddEnumerable(ServiceDescriptor.Transient<IPostConfigureOptions<MvcOptions>, MvcCoreMvcOptionsSetup>());
       services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<ApiBehaviorOptions>, ApiBehaviorOptionsSetup>());
       services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<RouteOptions>, MvcCoreRouteOptionsSetup>());

       // action selection
       services.TryAddSingleton<IActionSelector, ActionSelector>();
       services.TryAddSingleton<ActionConstraintCache>();     
       services.TryAddEnumerable(ServiceDescriptor.Transient<IActionConstraintProvider, DefaultActionConstraintProvider>());  // cached by the DefaultActionSelector
       services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, ActionConstraintMatcherPolicy>());  // policies for Endpoints

       // controller Factory
       services.TryAddSingleton<IControllerFactory, DefaultControllerFactory>();      // cache, so it needs to be a singleton
       services.TryAddTransient<IControllerActivator, DefaultControllerActivator>();  // will be cached by the DefaultControllerFactory
       services.TryAddSingleton<IControllerFactoryProvider, ControllerFactoryProvider>();
       services.TryAddSingleton<IControllerActivatorProvider, ControllerActivatorProvider>();
       services.TryAddEnumerable(ServiceDescriptor.Transient<IControllerPropertyActivator, DefaultControllerPropertyActivator>());

       // action invoker
       services.TryAddSingleton<IActionInvokerFactory, ActionInvokerFactory>();  // IActionInvokerFactory is cachable
       services.TryAddEnumerable(ServiceDescriptor.Transient<IActionInvokerProvider, ControllerActionInvokerProvider>());
       services.TryAddSingleton<ControllerActionInvokerCache>();
       services.TryAddEnumerable(ServiceDescriptor.Singleton<IFilterProvider, DefaultFilterProvider>());
       services.TryAddSingleton<IActionResultTypeMapper, ActionResultTypeMapper>();

       // model binding, validation
       services.TryAddSingleton<IModelMetadataProvider, DefaultModelMetadataProvider>();
       services.TryAdd(ServiceDescriptor.Transient<ICompositeMetadataDetailsProvider>(s =>
       {
          var options = s.GetRequiredService<IOptions<MvcOptions>>().Value;
          return new DefaultCompositeMetadataDetailsProvider(options.ModelMetadataDetailsProviders);
       }));
       services.TryAddSingleton<IModelBinderFactory, ModelBinderFactory>();
       services.TryAddSingleton<IObjectModelValidator>(s =>
       {
          var options = s.GetRequiredService<IOptions<MvcOptions>>().Value;
          var metadataProvider = s.GetRequiredService<IModelMetadataProvider>();
          return new DefaultObjectValidator(metadataProvider, options.ModelValidatorProviders, options);
       });
       services.TryAddSingleton<ClientValidatorCache>();
       services.TryAddSingleton<ParameterBinder>();
       
       // route handlers
       services.TryAddSingleton<MvcRouteHandler>();           // only one per app
       services.TryAddTransient<MvcAttributeRouteHandler>();  // many per app

       // endpoint routing / endpoints
       services.TryAddSingleton<ControllerActionEndpointDataSourceFactory>();
       services.TryAddSingleton<OrderedEndpointsSequenceProviderCache>();
       services.TryAddSingleton<ControllerActionEndpointDataSourceIdProvider>();
       services.TryAddSingleton<ActionEndpointFactory>();
       services.TryAddSingleton<DynamicControllerEndpointSelectorCache>();
       services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, DynamicControllerEndpointMatcherPolicy>());
       services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestDelegateFactory, ControllerRequestDelegateFactory>());

       // ...    
    }
}
```

Everything sits inside `MapControllers`

```C#
public static class ControllerEndpointRouteBuilderExtensions 
{
   public static ControllerActionEndpointConventionBuilder MapControllers(this IEndpointRouteBuilder endpoints)  
   {                                                                                                             
      EnsureControllerServices(endpoints);
      return GetOrCreateDataSource(endpoints).DefaultBuilder;
   }

   private static ControllerActionEndpointDataSource GetOrCreateDataSource(IEndpointRouteBuilder endpoints)
   {
      var dataSource = endpoints.DataSources.OfType<ControllerActionEndpointDataSource>().FirstOrDefault();
      if (dataSource == null) 
      {
         var orderProvider = endpoints.ServiceProvider.GetRequiredService<OrderedEndpointsSequenceProviderCache>();
         var factory = endpoints.ServiceProvider.GetRequiredService<ControllerActionEndpointDataSourceFactory>();    // <--------------------------- a1
         dataSource = factory.Create(orderProvider.GetOrCreateOrderedEndpointsSequenceProvider(endpoints));
         endpoints.DataSources.Add(dataSource);
      }

      return dataSource;
   }

   private static void RegisterInCache(IServiceProvider serviceProvider, ControllerActionEndpointDataSource dataSource)
   {
      var cache = serviceProvider.GetRequiredService<DynamicControllerEndpointSelectorCache>();
       cache.AddDataSource(dataSource);
   }
}
```

```C#
//-------------------------------------------------------------V
internal sealed class ControllerActionEndpointDataSourceFactory   // <--------------------------- a1
{
   private readonly ControllerActionEndpointDataSourceIdProvider _dataSourceIdProvider;
   private readonly IActionDescriptorCollectionProvider _actions;
   private readonly ActionEndpointFactory _factory;
 
   public ControllerActionEndpointDataSourceFactory(
      ControllerActionEndpointDataSourceIdProvider dataSourceIdProvider,
      IActionDescriptorCollectionProvider actions,     // <--------------------------- a1.1
      ActionEndpointFactory factory)                   // <--------------------------- a1.2
   {
      _dataSourceIdProvider = dataSourceIdProvider;
      _actions = actions;
      _factory = factory;
   }
 
   public ControllerActionEndpointDataSource Create(OrderedEndpointsSequenceProvider orderProvider)
   {
       return new ControllerActionEndpointDataSource(_dataSourceIdProvider, _actions, _factory, orderProvider);   // <--------------------------- a1.3.
   }
}
//-------------------------------------------------------------Ʌ

//---------------------------V
public class ActionDescriptor
{
   public ActionDescriptor()
   {
      Id = Guid.NewGuid().ToString();
      Properties = new Dictionary<object, object>();
      RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   
   }

   public string Id { get; }

   public IDictionary<string, string> RouteValues { get; set; }

   public AttributeRouteInfo? AttributeRouteInfo { get; set; }

   public IList<IActionConstraintMetadata>? ActionConstraints { get; set; }

   public IList<object> EndpointMetadata { get; set; } = Array.Empty<ParameterDescriptor>();

   public IList<ParameterDescriptor> Parameters { get; set; } = Array.Empty<ParameterDescriptor>();

   public IList<ParameterDescriptor> BoundProperties { get; set; } = Array.Empty<ParameterDescriptor>();

   public IList<FilterDescriptor> FilterDescriptors { get; set; } = Array.Empty<FilterDescriptor>();

   public virtual string? DisplayName { get; set; }

   public IDictionary<object, object?> Properties { get; set; } = default!;

   internal IFilterMetadata[]? CachedReusableFilters { get; set; }   // <-----------------------------
}
//---------------------------Ʌ

public interface IActionDescriptorCollectionProvider
{
   ActionDescriptorCollection ActionDescriptors { get; }
}

public abstract class ActionDescriptorCollectionProvider : IActionDescriptorCollectionProvider
{
   public abstract ActionDescriptorCollection ActionDescriptors { get; }
   public abstract IChangeToken GetChangeToken();
}

public interface IActionDescriptorProvider
{
   int Order { get; }
   void OnProvidersExecuting(ActionDescriptorProviderContext context);
   void OnProvidersExecuted(ActionDescriptorProviderContext context);
}

//------------------------------------------------------V
internal sealed class ControllerActionDescriptorProvider : IActionDescriptorProvider
{
   private readonly ApplicationPartManager _partManager;
   private readonly ApplicationModelFactory _applicationModelFactory;

   public ControllerActionDescriptorProvider(ApplicationPartManager partManager, ApplicationModelFactory applicationModelFactory) { ... }

   public int Order => -1000;

   public void OnProvidersExecuting(ActionDescriptorProviderContext context)
   {
      foreach (var descriptor in GetDescriptors())
      {
         context.Results.Add(descriptor);
      }
   }

   public void OnProvidersExecuted(ActionDescriptorProviderContext context)
   {
      var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (var i = 0; i < context.Results.Count; i++)
      {
         var action = context.Results[i];
         foreach (var key in action.RouteValues.Keys)
         {
            keys.Add(key);
         }
      }

      for (var i = 0; i < context.Results.Count; i++)
      {
         var action = context.Results[i];
         foreach (var key in keys)
         {
            if (!action.RouteValues.ContainsKey(key))
            {
               action.RouteValues.Add(key, null);
            }
         }
      }
   }

   internal IEnumerable<ControllerActionDescriptor> GetDescriptors()
   {
      var controllerTypes = GetControllerTypes();
      var application = _applicationModelFactory.CreateApplicationModel(controllerTypes);
      return ControllerActionDescriptorBuilder.Build(application);
   }
 
   private IEnumerable<TypeInfo> GetControllerTypes()
   {
      var feature = new ControllerFeature();
      _partManager.PopulateFeature(feature);
 
      return feature.Controllers;
   }
}
//------------------------------------------------------Ʌ

//---------------------------------------------------------------------V
internal sealed partial class DefaultActionDescriptorCollectionProvider : ActionDescriptorCollectionProvider
{
   private readonly IActionDescriptorProvider[] _actionDescriptorProviders;
   private readonly IActionDescriptorChangeProvider[] _actionDescriptorChangeProviders;
   private ActionDescriptorCollection? _collection;
   private readonly ILogger _logger;
   // ...

   public DefaultActionDescriptorCollectionProvider(
      IEnumerable<IActionDescriptorProvider> actionDescriptorProviders,
      IEnumerable<IActionDescriptorChangeProvider> actionDescriptorChangeProviders,
      ILogger<DefaultActionDescriptorCollectionProvider> logger)
   {
      _actionDescriptorProviders = actionDescriptorProviders
         .OrderBy(p => p.Order)
         .ToArray();

      _actionDescriptorChangeProviders = actionDescriptorChangeProviders.ToArray();

      ChangeToken.OnChange(GetCompositeChangeToken, UpdateCollection);
   }

   public override ActionDescriptorCollection ActionDescriptors
   {
      get {
         Initialize();
         return _collection;
      }
   }

   private void Initialize()
   {
      if (_collection == null)
      {
         // ... call UpdateCollection();
      }
   }

   private void UpdateCollection()
   {
      // ...
      var context = new ActionDescriptorProviderContext();
      for (var i = 0; i < _actionDescriptorProviders.Length; i++)
      {
         _actionDescriptorProviders[i].OnProvidersExecuting(context);
      }

      for (var i = _actionDescriptorProviders.Length - 1; i >= 0; i--)
      {
         _actionDescriptorProviders[i].OnProvidersExecuted(context);
      }
      // ...
   }
}
//---------------------------------------------------------------------Ʌ

//-------------------------------------V
public class ActionDescriptorCollection
{
   public ActionDescriptorCollection(IReadOnlyList<ActionDescriptor> items, int version)
   {
      Items = items;
      Version = version;
   }

   public IReadOnlyList<ActionDescriptor> Items { get; }
   public int Version { get; }
}
//-------------------------------------Ʌ

//--------------------------------------------------V
public interface IActionDescriptorCollectionProvider
{
   ActionDescriptorCollection ActionDescriptors { get; }
}
//--------------------------------------------------Ʌ

//-------------------------------------------------------------------------------------V
internal sealed class ControllerActionEndpointDataSource : ActionEndpointDataSourceBase     // <--------------------------------a2
{
   private readonly ActionEndpointFactory _endpointFactory;
   private readonly OrderedEndpointsSequenceProvider _orderSequence;
   private readonly List<ConventionalRouteEntry> _routes;

   public ControllerActionEndpointDataSource(
      IActionDescriptorCollectionProvider actions,
      ActionEndpointFactory endpointFactory,
      OrderedEndpointsSequenceProvider orderSequence)
      : base(actions)
   {
      _endpointFactory = endpointFactory;

      _orderSequence = orderSequence;
      _routes = new List<ConventionalRouteEntry>();
      DefaultBuilder = new ControllerActionEndpointConventionBuilder(Lock, Conventions, FinallyConventions);     
   }
 
   public ControllerActionEndpointConventionBuilder DefaultBuilder { get; }

   public ControllerActionEndpointConventionBuilder AddRoute(
      string routeName,
      string pattern,
      RouteValueDictionary? defaults,
      IDictionary<string, object?>? constraints,
      RouteValueDictionary? dataTokens)
   {
      lock (Lock)
      {
         var conventions = new List<Action<EndpointBuilder>>();
         var finallyConventions = new List<Action<EndpointBuilder>>();
         _routes.Add(new ConventionalRouteEntry(routeName, pattern, defaults, constraints, dataTokens, _orderSequence.GetNext(), conventions, finallyConventions));
         return new ControllerActionEndpointConventionBuilder(Lock, conventions, finallyConventions);
      }
   }

   protected override List<Endpoint> CreateEndpoints(
      RoutePattern? groupPrefix,
      IReadOnlyList<ActionDescriptor> actions,
      IReadOnlyList<Action<EndpointBuilder>> conventions,
      IReadOnlyList<Action<EndpointBuilder>> groupConventions,
      IReadOnlyList<Action<EndpointBuilder>> finallyConventions,
      IReadOnlyList<Action<EndpointBuilder>> groupFinallyConventions)
   {
      var endpoints = new List<Endpoint>();
      // ...

      // For each controller action - add the relevant endpoints.
      //
      // 1. If the action is attribute routed, we use that information verbatim
      // 2. If the action is conventional routed
      //      a. Create a *matching only* endpoint for each action X route (if possible)
      //      b. Ignore link generation for now
      for (var i = 0; i < actions.Count; i++)
      {
         if (actions[i] is ControllerActionDescriptor action)
         {
            _endpointFactory.AddEndpoints(endpoints,
                                          routeNames,
                                          action,
                                          _routes,
                                          conventions: conventions,
                                          groupConventions: groupConventions,
                                          finallyConventions: finallyConventions,
                                          groupFinallyConventions: groupFinallyConventions,
                                          CreateInertEndpoints,
                                          groupPrefix: groupPrefix);
 
            if (_routes.Count > 0)
            {
               // If we have conventional routes, keep track of the keys so we can create
               // the link generation routes later.
               foreach (var kvp in action.RouteValues)
               {
                  keys.Add(kvp.Key);
               }
            }
         }
      }
 
      // Now create a *link generation only* endpoint for each route. This gives us a very
      // compatible experience to previous versions.
      for (var i = 0; i < _routes.Count; i++)
      {
         var route = _routes[i];
         _endpointFactory.AddConventionalLinkGenerationRoute(
             endpoints,
             routeNames,
             keys,
             route,
             groupConventions: groupConventions,
             conventions: conventions,
             finallyConventions: finallyConventions,
             groupFinallyConventions: groupFinallyConventions,
             groupPrefix: groupPrefix);
      }
 
      return endpoints;
   }
}
//-------------------------------------------------------------------------------------Ʌ

//-----------------------------------------V
internal sealed class ActionEndpointFactory
{
   private readonly RoutePatternTransformer _routePatternTransformer;
   private readonly RequestDelegate _requestDelegate;
   private readonly IRequestDelegateFactory[] _requestDelegateFactories;
   private readonly IServiceProvider _serviceProvider;

   public ActionEndpointFactory(RoutePatternTransformer routePatternTransformer, IEnumerable<IRequestDelegateFactory> requestDelegateFactories, IServiceProvider serviceProvider);

   public void AddEndpoints(
      List<Endpoint> endpoints,
      HashSet<string> routeNames,
      ActionDescriptor action,
      IReadOnlyList<ConventionalRouteEntry> routes,
      IReadOnlyList<Action<EndpointBuilder>> conventions,
      IReadOnlyList<Action<EndpointBuilder>> groupConventions,
      IReadOnlyList<Action<EndpointBuilder>> finallyConventions,
      IReadOnlyList<Action<EndpointBuilder>> groupFinallyConventions,
      bool createInertEndpoints,
      RoutePattern? groupPrefix = null)
   {
      if (createInertEndpoints)
      {
         var builder = new InertEndpointBuilder()
         {
            DisplayName = action.DisplayName,
            RequestDelegate = _requestDelegate,
         };

         AddActionDataToBuilder(
            builder,
            routeNames,
            action,
            routeName: null,
            dataTokens: null,
            suppressLinkGeneration: false,
            suppressPathMatching: false,
            groupConventions: groupConventions,
            conventions: conventions,
            perRouteConventions: Array.Empty<Action<EndpointBuilder>>(),
            groupFinallyConventions: groupFinallyConventions,
            finallyConventions: finallyConventions,
            perRouteFinallyConventions: Array.Empty<Action<EndpointBuilder>>());
            endpoints.Add(builder.Build());
      }

      if (action.AttributeRouteInfo?.Template == null)
      {
         foreach (var route in routes)
         {
            // a route is applicable if:
            // 1. it has a parameter (or default value) for 'required' non-null route value
            // 2. it does not have a parameter (or default value) for 'required' null route value
            var updatedRoutePattern = _routePatternTransformer.SubstituteRequiredValues(route.Pattern, action.RouteValues);
            if (updatedRoutePattern == null)
            {
               continue;
            }

            updatedRoutePattern = RoutePatternFactory.Combine(groupPrefix, updatedRoutePattern);

            var requestDelegate = CreateRequestDelegate(action, route.DataTokens) ?? _requestDelegate;  // <-------------------------------

            var builder = new RouteEndpointBuilder(requestDelegate, updatedRoutePattern, route.Order)
            {
               DisplayName = action.DisplayName,
            };
            
            AddActionDataToBuilder(
               builder,
               routeNames,
               action,
               route.RouteName,
               route.DataTokens,
               suppressLinkGeneration: true,
               suppressPathMatching: false,
               groupConventions: groupConventions,
               conventions: conventions,
               perRouteConventions: route.Conventions,
               groupFinallyConventions: groupFinallyConventions,
               finallyConventions: finallyConventions,
               perRouteFinallyConventions: route.FinallyConventions);
               endpoints.Add(builder.Build());
         }
      }
      else
      {
         var requestDelegate = CreateRequestDelegate(action) ?? _requestDelegate;
         var attributeRoutePattern = RoutePatternFactory.Parse(action.AttributeRouteInfo.Template);
         var (resolvedRoutePattern, resolvedRouteValues) = ResolveDefaultsAndRequiredValues(action, attributeRoutePattern);
         var updatedRoutePattern = _routePatternTransformer.SubstituteRequiredValues(resolvedRoutePattern, resolvedRouteValues);
         // ...
         updatedRoutePattern = RoutePatternFactory.Combine(groupPrefix, updatedRoutePattern);
         var builder = new RouteEndpointBuilder(requestDelegate, updatedRoutePattern, action.AttributeRouteInfo.Order)
         {
            DisplayName = action.DisplayName,
         };

         AddActionDataToBuilder(
            builder,
            routeNames,
            action,
            action.AttributeRouteInfo.Name,
            dataTokens: null,
            action.AttributeRouteInfo.SuppressLinkGeneration,
            action.AttributeRouteInfo.SuppressPathMatching,
            groupConventions: groupConventions,
            conventions: conventions,
            perRouteConventions: Array.Empty<Action<EndpointBuilder>>(),
            groupFinallyConventions: groupFinallyConventions,
            finallyConventions: finallyConventions,
            perRouteFinallyConventions: Array.Empty<Action<EndpointBuilder>>());

         endpoints.Add(builder.Build());
      }
   }

   // ...

   private RequestDelegate? CreateRequestDelegate(ActionDescriptor action, RouteValueDictionary? dataTokens = null)
   {
      foreach (var factory in _requestDelegateFactories)
      {
         var requestDelegate = factory.CreateRequestDelegate(action, dataTokens);
         if (requestDelegate != null)
         {
            return requestDelegate;
         }
      }  
      return null;
   }

   private static RequestDelegate CreateRequestDelegate()  // <---------------------------------------
   {
      IActionInvokerFactory invokerFactory = null;

      return (context) =>
      {
         var endpoint = context.GetEndpoint()!;
         var dataTokens = endpoint.Metadata.GetMetadata<IDataTokensMetadata>();
 
         var routeData = new RouteData();
         routeData.PushState(router: null, context.Request.RouteValues, new RouteValueDictionary(dataTokens?.DataTokens));
 
         var action = endpoint.Metadata.GetMetadata<ActionDescriptor>()!;
         var actionContext = new ActionContext(context, routeData, action);
 
         if (invokerFactory == null)
         {
            invokerFactory = context.RequestServices.GetRequiredService<IActionInvokerFactory>();  // <----------------------
         }
 
         var invoker = invokerFactory.CreateInvoker(actionContext);
         return invoker.InvokeAsync();
      };
   }
}
//-----------------------------------------Ʌ

//----------------------------------------------------------------V
internal sealed class ActionInvokerFactory : IActionInvokerFactory
{
   private readonly IActionInvokerProvider[] _actionInvokerProviders;

   public ActionInvokerFactory(IEnumerable<IActionInvokerProvider> actionInvokerProviders)
   {
      _actionInvokerProviders = actionInvokerProviders.OrderBy(item => item.Order).ToArray();
   }
 
   public IActionInvoker CreateInvoker(ActionContext actionContext)
   {
      var context = new ActionInvokerProviderContext(actionContext);
 
      foreach (var provider in _actionInvokerProviders)
      {
         provider.OnProvidersExecuting(context);
      }
 
      for (var i = _actionInvokerProviders.Length - 1; i >= 0; i--)
      {
          _actionInvokerProviders[i].OnProvidersExecuted(context);
      }
 
      return context.Result;
   }
}
//----------------------------------------------------------------Ʌ
```

```C#
public interface IActionInvoker  // defines an interface for invoking an MVC action
{
   Task InvokeAsync();
}

//--------------------------V
internal struct FilterCursor
{
   private readonly IFilterMetadata[] _filters;
   private int _index;

   public FilterCursor(IFilterMetadata[] filters)
   {
      _filters = filters;
      _index = 0;
   }
 
   public void Reset()
   {
      _index = 0;
   }

   public FilterCursorItem<TFilter?, TFilterAsync?> GetNextFilter<TFilter, TFilterAsync>()
   {
      while (_index < _filters.Length)
      {
         var filter = _filters[_index] as TFilter;
         var filterAsync = _filters[_index] as TFilterAsync;
 
         _index += 1;
 
         if (filter != null || filterAsync != null)
         {
            return new FilterCursorItem<TFilter?, TFilterAsync?>(filter, filterAsync);
         }
      }
 
      return default(FilterCursorItem<TFilter?, TFilterAsync?>);
   }
}
//--------------------------Ʌ

//---------------------------------------------V
internal abstract partial class ResourceInvoker
{
   protected readonly DiagnosticListener _diagnosticListener;
   protected readonly ILogger _logger;
   protected readonly IActionContextAccessor _actionContextAccessor;
   protected readonly IActionResultTypeMapper _mapper;
   protected readonly ActionContext _actionContext;
   protected readonly IFilterMetadata[] _filters;
   protected readonly IList<IValueProviderFactory> _valueProviderFactories;

   private AuthorizationFilterContextSealed? _authorizationContext;
   private ResourceExecutingContextSealed? _resourceExecutingContext;
   private ResourceExecutedContextSealed? _resourceExecutedContext;
   private ExceptionContextSealed? _exceptionContext;
   private ResultExecutingContextSealed? _resultExecutingContext;
   private ResultExecutedContextSealed? _resultExecutedContext;

   protected FilterCursor _cursor;
   protected IActionResult _result;
   protected object _instance;

   public ResourceInvoker(DiagnosticListener diagnosticListener, ILogger logger, IActionContextAccessor actionContextAccessor, IActionResultTypeMapper mapper,
                          ActionContext actionContext, IFilterMetadata[] filters, IList<IValueProviderFactory> valueProviderFactories);
   {
      _diagnosticListener = diagnosticListener ?? throw new ArgumentNullException(nameof(diagnosticListener));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _actionContextAccessor = actionContextAccessor ?? throw new ArgumentNullException(nameof(actionContextAccessor));
      _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
      _actionContext = actionContext ?? throw new ArgumentNullException(nameof(actionContext));
 
      _filters = filters ?? throw new ArgumentNullException(nameof(filters));
      _valueProviderFactories = valueProviderFactories ?? throw new ArgumentNullException(nameof(valueProviderFactories));
      _cursor = new FilterCursor(filters);
   }

   public virtual Task InvokeAsync()
   {
      _actionContextAccessor.ActionContext = _actionContext;
      var scope = _logger.ActionScope(_actionContext.ActionDescriptor);

      Task task;
      try
      {
         task = InvokeFilterPipelineAsync();
      }
      catch (Exception exception)
      {
         return Awaited(this, Task.FromException(exception), scope);
      }
 
      if (!task.IsCompletedSuccessfully)
      {
         return Awaited(this, task, scope);
      }

      return ReleaseResourcesCore(scope).AsTask();

      static async Task Awaited(ResourceInvoker invoker, Task task, IDisposable? scope)
      {
         try
         {
            await task;
         }
         finally
         {
            await invoker.ReleaseResourcesCore(scope);
         }
      }
      // ...
   }

   // ...

   private Task InvokeFilterPipelineAsync()
   {
      var next = State.InvokeBegin;
      var scope = Scope.Invoker;
      var state = (object)null;
      var isCompleted = false;
      try
      {
         while (!isCompleted)
         {
            var lastTask = Next(ref next, ref scope, ref state, ref isCompleted);
            if (!lastTask.IsCompletedSuccessfully)
            {
               return Awaited(this, lastTask, next, scope, state, isCompleted);
            }
         }
 
         return Task.CompletedTask;
      }
      catch (Exception ex)
      {
         return Task.FromException(ex);
      }
 
      static async Task Awaited(ResourceInvoker invoker, Task lastTask, State next, Scope scope, object? state, bool isCompleted)
      {
         await lastTask;
 
         while (!isCompleted)
         {
            await invoker.Next(ref next, ref scope, ref state, ref isCompleted);
         }
      }
   }

   protected virtual Task InvokeResultAsync(IActionResult result)
   {
      return result.ExecuteResultAsync(_actionContext);
   }

   private Task Next(ref State next, ref Scope scope, ref object? state, ref bool isCompleted)
   {
      switch (next)
      {
         case State.InvokeBegin:
            goto case State.AuthorizationBegin;

         case State.AuthorizationBegin:
            _cursor.Reset();
            goto case State.AuthorizationNext;

         case State.AuthorizationNext:
            var current = _cursor.GetNextFilter<IAuthorizationFilter, IAsyncAuthorizationFilter>();
            if (current.FilterAsync != null)
            {
               if (_authorizationContext == null)
               {
                  _authorizationContext = new AuthorizationFilterContextSealed(_actionContext, _filters);
               }
 
               state = current.FilterAsync;
               goto case State.AuthorizationAsyncBegin;
            }
            else if (current.Filter != null)
            {
               if (_authorizationContext == null)
               {
                  _authorizationContext = new AuthorizationFilterContextSealed(_actionContext, _filters);
               }

               state = current.Filter;
               goto case State.AuthorizationSync;
            }
            else
            {
               goto case State.AuthorizationEnd;
            }

         case State.AuthorizationAsyncBegin:
            var filter = (IAsyncAuthorizationFilter)state;
            var authorizationContext = _authorizationContext;

            var task = filter.OnAuthorizationAsync(authorizationContext);
            if (!task.IsCompletedSuccessfully)
            {
               next = State.AuthorizationAsyncEnd;
               return task;
            }

            goto case State.AuthorizationAsyncEnd;

         case State.AuthorizationAsyncEnd:
            var filter = (IAsyncAuthorizationFilter)state;
            var authorizationContext = _authorizationContext;
            if (authorizationContext.Result != null)
            {
               goto case State.AuthorizationShortCircuit;
            }           
            goto case State.AuthorizationNext;
         
         case State.AuthorizationSync:
            var filter = (IAuthorizationFilter)state;
            var authorizationContext = _authorizationContext;

            filter.OnAuthorization(authorizationContext);
            if (authorizationContext.Result != null)
            {
               goto case State.AuthorizationShortCircuit;
            }

            goto case State.AuthorizationNext;
         
         case State.AuthorizationShortCircuit:
            // this is a short-circuit - execute relevant result filters + result and complete this invocation.
            isCompleted = true;
            _result = _authorizationContext.Result;
            return InvokeAlwaysRunResultFilters();
         
         case State.AuthorizationEnd:
            goto case State.ResourceBegin;

         case State.ResourceBegin:
            _cursor.Reset();
            goto case State.ResourceNext;

         case State.ResourceNext:
           // ...
         
         // ...
      }
   }
}
//---------------------------------------------Ʌ

private enum Scope
{
   Invoker,
   Resource,
   Exception,
   Result,
}

private enum State
{
   InvokeBegin,
   AuthorizationBegin,
   AuthorizationNext,
   AuthorizationAsyncBegin,
   AuthorizationAsyncEnd,
   AuthorizationSync,
   AuthorizationShortCircuit,
   AuthorizationEnd,
   ResourceBegin,
   ResourceNext,
   ResourceAsyncBegin,
   ResourceAsyncEnd,
   ResourceSyncBegin,
   ResourceSyncEnd,
   ResourceShortCircuit,
   ResourceInside,
   ResourceInsideEnd,
   ResourceEnd,
   ExceptionBegin,
   ExceptionNext,
   ExceptionAsyncBegin,
   ExceptionAsyncResume,
   ExceptionAsyncEnd,
   ExceptionSyncBegin,
   ExceptionSyncEnd,
   ExceptionInside,
   ExceptionHandled,
   ExceptionEnd,
   ActionBegin,
   ActionEnd,
   ResultBegin,
   ResultNext,
   ResultAsyncBegin,
   ResultAsyncEnd,
   ResultSyncBegin,
   ResultSyncEnd,
   ResultInside,
   ResultEnd,
   InvokeEnd,
}
//---------------------------------------------Ʌ


//------------------------------------------------V
internal sealed class ControllerActionInvokerCache
{
   private readonly ParameterBinder _parameterBinder;
   private readonly IModelBinderFactory _modelBinderFactory;
   private readonly IModelMetadataProvider _modelMetadataProvider;
   private readonly IFilterProvider[] _filterProviders;
   private readonly IControllerFactoryProvider _controllerFactoryProvider;
   private readonly MvcOptions _mvcOptions;

   public ControllerActionInvokerCache(
      ParameterBinder parameterBinder,
      IModelBinderFactory modelBinderFactory,
      IModelMetadataProvider modelMetadataProvider,
      IEnumerable<IFilterProvider> filterProviders,
      IControllerFactoryProvider factoryProvider,
      IOptions<MvcOptions> mvcOptions)
   {
      _parameterBinder = parameterBinder;
      _modelBinderFactory = modelBinderFactory;
      _modelMetadataProvider = modelMetadataProvider;
      _filterProviders = filterProviders.OrderBy(item => item.Order).ToArray();
       _controllerFactoryProvider = factoryProvider;
      _mvcOptions = mvcOptions.Value;
   }

   public (ControllerActionInvokerCacheEntry cacheEntry, IFilterMetadata[] filters) GetCachedResult(ControllerContext controllerContext)
   {
      var actionDescriptor = controllerContext.ActionDescriptor;
      IFilterMetadata[] filters;
      var cacheEntry = actionDescriptor.CacheEntry;

      if (cacheEntry is null)
      {
         var filterFactoryResult = FilterFactory.GetAllFilters(_filterProviders, controllerContext);
         filters = filterFactoryResult.Filters;

         var parameterDefaultValues = ParameterDefaultValues.GetParameterDefaultValues(actionDescriptor.MethodInfo);

         var objectMethodExecutor = ObjectMethodExecutor.Create(
            actionDescriptor.MethodInfo,
            actionDescriptor.ControllerTypeInfo,
            parameterDefaultValues);

         var controllerFactory = _controllerFactoryProvider.CreateControllerFactory(actionDescriptor);
         var controllerReleaser = _controllerFactoryProvider.CreateAsyncControllerReleaser(actionDescriptor);
         var propertyBinderFactory = ControllerBinderDelegateProvider.CreateBinderDelegate(
            _parameterBinder,
            _modelBinderFactory,
            _modelMetadataProvider,
            actionDescriptor,
            _mvcOptions);
         
         var actionMethodExecutor = ActionMethodExecutor.GetExecutor(objectMethodExecutor);
         var filterExecutor = actionDescriptor.FilterDelegate is not null ? ActionMethodExecutor.GetFilterExecutor(actionDescriptor) : null;

         cacheEntry = new ControllerActionInvokerCacheEntry(
            filterFactoryResult.CacheableFilters,
            controllerFactory,
            controllerReleaser,
            propertyBinderFactory,
            objectMethodExecutor,
            filterExecutor ?? actionMethodExecutor,
            actionMethodExecutor);

         actionDescriptor.CacheEntry = cacheEntry;
      }
      else 
      {
         // filter instances from statically defined filter descriptors + from filter providers
         filters = FilterFactory.CreateUncachedFilters(_filterProviders, controllerContext, cacheEntry.CachedFilters);
      }

      return (cacheEntry, filters);
   }
}
//------------------------------------------------Ʌ

//---------------------------------V
internal static class FilterFactory
{
   public static FilterFactoryResult GetAllFilters(IFilterProvider[] filterProviders, ActionContext actionContext)
   {
      var actionDescriptor = actionContext.ActionDescriptor;
      var staticFilterItems = new FilterItem[actionDescriptor.FilterDescriptors.Count];
      var orderedFilters = actionDescriptor.FilterDescriptors.OrderBy(filter => filter,FilterDescriptorOrderComparer.Comparer).ToList();

      for (var i = 0; i < orderedFilters.Count; i++)
      {
         staticFilterItems[i] = new FilterItem(orderedFilters[i]);
      }

      var allFilterItems = new List<FilterItem>(staticFilterItems);

      // execute the filter factory to determine which static filters can be cached
      var filters = CreateUncachedFiltersCore(filterProviders, actionContext, allFilterItems);

      // Cache the filter items based on the following criteria
      // 1. Are created statically (ex: via filter attributes, added to global filter list etc.)
      // 2. Are re-usable
      var allFiltersAreReusable = true;
      for (var i = 0; i < staticFilterItems.Length; i++)
      {
         var item = staticFilterItems[i];
         if (!item.IsReusable)
         {
            item.Filter = null;
            allFiltersAreReusable = false;
         }
      }

      if (allFiltersAreReusable && filterProviders.Length == 1 && filterProviders[0] is DefaultFilterProvider defaultFilterProvider)
      {
         // if we know we can safely cache all filters and only the default filter provider is registered, we can probably re-use filters between requests.
         actionDescriptor.CachedReusableFilters = filters;
      }

      return new FilterFactoryResult(staticFilterItems, filters);
   }

   public static IFilterMetadata[] CreateUncachedFilters(IFilterProvider[] filterProviders, ActionContext actionContext, FilterItem[] cachedFilterItems)
   {
      if (actionContext.ActionDescriptor.CachedReusableFilters is { } cached)
      {
         return cached;
      }

      // deep copy the cached filter items as filter providers could modify them
      var filterItems = new List<FilterItem>(cachedFilterItems.Length);
      for (var i = 0; i < cachedFilterItems.Length; i++)
      {
         var filterItem = cachedFilterItems[i];
         filterItems.Add(new FilterItem(filterItem.Descriptor) { Filter = filterItem.Filter, IsReusable = filterItem.IsReusable });
      }

      return CreateUncachedFiltersCore(filterProviders, actionContext, filterItems);
   }

   private static IFilterMetadata[] CreateUncachedFiltersCore(IFilterProvider[] filterProviders, ActionContext actionContext, List<FilterItem> filterItems)
   {
      // execute providers
      var context = new FilterProviderContext(actionContext, filterItems);

      for (var i = 0; i < filterProviders.Length; i++)
      {
         filterProviders[i].OnProvidersExecuting(context);
      }

      for (var i = filterProviders.Length - 1; i >= 0; i--)
      {
         filterProviders[i].OnProvidersExecuted(context);
      }

      // extract filter instances from statically defined filters and filter providers
      var count = 0;
      for (var i = 0; i < filterItems.Count; i++)
      {
         if (filterItems[i].Filter != null)
         {
            count++;
         }
      }

      if (count == 0)
      {
         return Array.Empty<IFilterMetadata>();
      }
      else 
      {
         var filters = new IFilterMetadata[count];
         var filterIndex = 0;
         for (int i = 0; i < filterItems.Count; i++)
         {
            var filter = filterItems[i].Filter;
            if (filter != null)
            {
               filters[filterIndex++] = filter;
            }
         }

         return filters;
      }
   }
}
//---------------------------------Ʌ

//-----------------------------------------V
internal sealed class DefaultFilterProvider : IFilterProvider
{
   public int Order => -1000;

   public void OnProvidersExecuting(FilterProviderContext context)
   {
      if (context.ActionContext.ActionDescriptor.FilterDescriptors != null)
      {
         var results = context.Results;
         var resultsCount = results.Count;
         for (var i = 0; i < resultsCount; i++)
         {
            ProvideFilter(context, results[i]);
         }
      }
   }

   public void OnProvidersExecuted(FilterProviderContext context)
   {

   }

   public static void ProvideFilter(FilterProviderContext context, FilterItem filterItem)
   {
      if (filterItem.Filter != null)
      {
         return;
      }

      var filter = filterItem.Descriptor.Filter;

      if (filter is not IFilterFactory filterFactory)
      {
         filterItem.Filter = filter;
         filterItem.IsReusable = true;
      }
      else
      {
         var services = context.ActionContext.HttpContext.RequestServices;
         filterItem.Filter = filterFactory.CreateInstance(services);
         filterItem.IsReusable = filterFactory.IsReusable;

         if (filterItem.Filter == null)
         {
            throw new InvalidOperationException(Resources.FormatTypeMethodMustReturnNotNullValue("CreateInstance", typeof(IFilterFactory).Name));
         }

         ApplyFilterToContainer(filterItem.Filter, filterFactory);
      }
   }

   private static void ApplyFilterToContainer(object actualFilter, IFilterMetadata filterMetadata)
   {
      if (actualFilter is IFilterContainer container)
      {
         container.FilterDefinition = filterMetadata;
      }
   }
}
//-----------------------------------------Ʌ

//----------------------------------------------------------------------------V
internal sealed class ControllerActionInvokerProvider : IActionInvokerProvider
{
   private readonly ControllerActionInvokerCache _controllerActionInvokerCache;
   private readonly IReadOnlyList<IValueProviderFactory> _valueProviderFactories;
   private readonly int _maxModelValidationErrors;
   private readonly ILogger _logger;
   private readonly DiagnosticListener _diagnosticListener;
   private readonly IActionResultTypeMapper _mapper;
   private readonly IActionContextAccessor _actionContextAccessor;

   public ControllerActionInvokerProvider(
        ControllerActionInvokerCache controllerActionInvokerCache,
        IOptions<MvcOptions> optionsAccessor,
        ILoggerFactory loggerFactory,
        DiagnosticListener diagnosticListener,
        IActionResultTypeMapper mapper,
        IActionContextAccessor? actionContextAccessor)
   {
      _controllerActionInvokerCache = controllerActionInvokerCache;
      _valueProviderFactories = optionsAccessor.Value.ValueProviderFactories.ToArray();
      _maxModelValidationErrors = optionsAccessor.Value.MaxModelValidationErrors;
      _logger = loggerFactory.CreateLogger<ControllerActionInvoker>();
      _diagnosticListener = diagnosticListener;
      _mapper = mapper;
      _actionContextAccessor = actionContextAccessor ?? ActionContextAccessor.Null;
   }

   public int Order => -1000;

   public void OnProvidersExecuting(ActionInvokerProviderContext context)
   {
      if (context.ActionContext.ActionDescriptor is ControllerActionDescriptor)
      {
         var controllerContext = new ControllerContext(context.ActionContext)
         {
            ValueProviderFactories = new CopyOnWriteList<IValueProviderFactory>(_valueProviderFactories)
         };
         controllerContext.ModelState.MaxAllowedErrors = _maxModelValidationErrors;
 
         var (cacheEntry, filters) = _controllerActionInvokerCache.GetCachedResult(controllerContext);   // <---------------------------
 
         var invoker = new ControllerActionInvoker(_logger, _diagnosticListener, _actionContextAccessor, _mapper, controllerContext, cacheEntry, filters);
 
         context.Result = invoker;
      }
   }

   public void OnProvidersExecuted(ActionInvokerProviderContext context)
   {

   }
}
//----------------------------------------------------------------------------Ʌ

//--------------------------------------------V
internal partial class ControllerActionInvoker : ResourceInvoker, IActionInvoker 
{
   private readonly ControllerActionInvokerCacheEntry _cacheEntry;
   private readonly ControllerContext _controllerContext;
   private Dictionary<string, object> _arguments;
   private ActionExecutingContextSealed _actionExecutingContext;
   private ActionExecutedContextSealed _actionExecutedContext;

   internal ControllerActionInvoker(
        ILogger logger,
        DiagnosticListener diagnosticListener,
        IActionContextAccessor actionContextAccessor,
        IActionResultTypeMapper mapper,
        ControllerContext controllerContext,
        ControllerActionInvokerCacheEntry cacheEntry,
        IFilterMetadata[] filters)
        : base(diagnosticListener, logger, actionContextAccessor, mapper, controllerContext, filters, controllerContext.ValueProviderFactories)
   {
      _cacheEntry = cacheEntry;
      _controllerContext = controllerContext;
   }

   protected override ValueTask ReleaseResources()
   {
      if (_instance != null && _cacheEntry.ControllerReleaser != null)
      {
         return _cacheEntry.ControllerReleaser(_controllerContext, _instance);
      }
 
      return default;
   }

   private Task Next(ref State next, ref Scope scope, ref object? state, ref bool isCompleted)
   {
      switch (next)
      {
         case State.ActionBegin:
            var controllerContext = _controllerContext;
            _cursor.Reset();

            _instance = _cacheEntry.ControllerFactory(controllerContext);

            _arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            var task = BindArgumentsAsync();
            if (task.Status != TaskStatus.RanToCompletion)
            {
               next = State.ActionNext;
               return task;
            }

            goto case State.ActionNext;

         case State.ActionNext:
            var current = _cursor.GetNextFilter<IActionFilter, IAsyncActionFilter>();
            if (current.FilterAsync != null)
            {
               if (_actionExecutingContext == null)
               {
                  _actionExecutingContext = new ActionExecutingContextSealed(_controllerContext, _filters, _arguments!, _instance!);
               }
 
               state = current.FilterAsync;
               goto case State.ActionAsyncBegin;
            }
            else if (current.Filter != null)
            {
               if (_actionExecutingContext == null)
               {
                  _actionExecutingContext = new ActionExecutingContextSealed(_controllerContext, _filters, _arguments!, _instance!);
               }

               state = current.Filter;
               goto case State.ActionSyncBegin;
            }
            else
            {
               goto case State.ActionInside;
            }
         
         case State.ActionAsyncBegin:
            var filter = (IAsyncActionFilter)state;
            var actionExecutingContext = _actionExecutingContext;

            var task = filter.OnActionExecutionAsync(actionExecutingContext, InvokeNextActionFilterAwaitedAsync);
            if (task.Status != TaskStatus.RanToCompletion)
            {
               next = State.ActionAsyncEnd;
               return task;
            }

            goto case State.ActionAsyncEnd;

         case State.ActionAsyncEnd:
            var filter = (IAsyncActionFilter)state;

            if (_actionExecutedContext == null)
            {
               // if we get here then the filter didn't call 'next' indicating a short circuit.

               actionExecutedContext = new ActionExecutedContextSealed(_controllerContext, _filters, _instance!)
               {
                  Canceled = true,
                  Result = _actionExecutingContext.Result,
               }
            }

            goto case State.ActionEnd;

         case State.ActionSyncBegin:
            var filter = (IActionFilter)state;
            var actionExecutingContext = _actionExecutingContext;

            filter.OnActionExecuting(actionExecutingContext);

            if (actionExecutingContext.Result != null)
            {
               // short-circuited by setting a result

               _actionExecutedContext = new ActionExecutedContextSealed(_actionExecutingContext, _filters, _instance!)
               {
                  Canceled = true,
                  Result = _actionExecutingContext.Result,
               };

               goto case State.ActionEnd;
            }

            var task = InvokeNextActionFilterAsync();
            if (task.Status != TaskStatus.RanToCompletion)
            {
               next = State.ActionSyncEnd;
               return task;
            }
 
            goto case State.ActionSyncEnd;

         case State.ActionSyncEnd:
            var filter = (IActionFilter)state;
            var actionExecutedContext = _actionExecutedContext;

            filter.OnActionExecuted(actionExecutedContext);
            goto case State.ActionEnd;
         
         case State.ActionInside:
            var task = InvokeActionMethodAsync();
            if (task.Status != TaskStatus.RanToCompletion)
            {
               next = State.ActionEnd;
               return task;
            }
 
            goto case State.ActionEnd;

         case State.ActionEnd:
            if (scope == Scope.Action)
            {
               if (_actionExecutedContext == null)
               {
                  _actionExecutedContext = new ActionExecutedContextSealed(_controllerContext, _filters, _instance!)
                  {
                     Result = _result,
                  };
               }

               isCompleted = true;
               return Task.CompletedTask;
            }

            var actionExecutedContext = _actionExecutedContext;
            Rethrow(actionExecutedContext);

            if (actionExecutedContext != null)
            {
               _result = actionExecutedContext.Result;
            }
 
            isCompleted = true;
            return Task.CompletedTask;

         default:
            throw new InvalidOperationException();
      }
   }
   
   private Task InvokeActionMethodAsync()
   {
      var objectMethodExecutor = _cacheEntry.ObjectMethodExecutor;
      var actionMethodExecutor = _cacheEntry.ActionMethodExecutor;
      var orderedArguments = PrepareArguments(_arguments, objectMethodExecutor);
 
      var actionResultValueTask = actionMethodExecutor.Execute(ControllerContext, _mapper, objectMethodExecutor, _instance!, orderedArguments);
      if (actionResultValueTask.IsCompletedSuccessfully)
      {
         _result = actionResultValueTask.Result;
      }
      else
      {
         return Awaited(this, actionResultValueTask);
      }
 
      return Task.CompletedTask;
   }

   private Task InvokeNextActionFilterAsync()
   {
      try 
      {
         var next = State.ActionNext;
         var state = (object)null;
         var scope = Scope.Action;
         var isCompleted = false;
         while (!isCompleted)
         {
            var lastTask = Next(ref next, ref scope, ref state, ref isCompleted);
            if (!lastTask.IsCompletedSuccessfully)
            {
               return Awaited(this, lastTask, next, scope, state, isCompleted);
            }
         }
      }
      catch (Exception exception)
      {
         _actionExecutedContext = new ActionExecutedContextSealed(_controllerContext, _filters, _instance!)
         {
            ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
         };
      }
 
      return Task.CompletedTask;
   }

   private static void Rethrow(ActionExecutedContextSealed? context)
   {
      if (context == null)
         return;
 
      if (context.ExceptionHandled)
         return;
 
      if (context.ExceptionDispatchInfo != null)
         context.ExceptionDispatchInfo.Throw();
 
      if (context.Exception != null)
         throw context.Exception;
   }
}

private enum Scope
{
   Invoker,
   Action,
}
 
private enum State
{
   ActionBegin,
   ActionNext,
   ActionAsyncBegin,
   ActionAsyncEnd,
   ActionSyncBegin,
   ActionSyncEnd,
   ActionInside,
   ActionEnd,
}
//--------------------------------------------Ʌ
```


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