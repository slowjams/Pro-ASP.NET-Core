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

       // random infrastructure
       services.TryAddSingleton<MvcMarkerService, MvcMarkerService>();
       services.TryAddSingleton<ITypeActivatorCache, TypeActivatorCache>();
       services.TryAddSingleton<IUrlHelperFactory, UrlHelperFactory>();
       services.TryAddSingleton<IHttpRequestStreamReaderFactory, MemoryPoolHttpRequestStreamReaderFactory>();
       services.TryAddSingleton<IHttpResponseStreamWriterFactory, MemoryPoolHttpResponseStreamWriterFactory>();
       services.TryAddSingleton(ArrayPool<byte>.Shared);
       services.TryAddSingleton(ArrayPool<char>.Shared);
       services.TryAddSingleton<OutputFormatterSelector, DefaultOutputFormatterSelector>();
       services.TryAddSingleton<IActionResultExecutor<ObjectResult>, ObjectResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<PhysicalFileResult>, PhysicalFileResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<VirtualFileResult>, VirtualFileResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<FileStreamResult>, FileStreamResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<FileContentResult>, FileContentResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<RedirectResult>, RedirectResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<LocalRedirectResult>, LocalRedirectResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<RedirectToActionResult>, RedirectToActionResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<RedirectToRouteResult>, RedirectToRouteResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<RedirectToPageResult>, RedirectToPageResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<ContentResult>, ContentResultExecutor>();
       services.TryAddSingleton<IActionResultExecutor<JsonResult>, SystemTextJsonResultExecutor>();
       services.TryAddSingleton<IClientErrorFactory, ProblemDetailsClientErrorFactory>();      
       
       // middleware pipeline filter related
       services.TryAddSingleton<MiddlewareFilterConfigurationProvider>();
       services.TryAddSingleton<MiddlewareFilterBuilder>();
       services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, MiddlewareFilterBuilderStartupFilter>());
 
       // ProblemDetails
       services.TryAddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>();
       services.TryAddEnumerable(ServiceDescriptor.Singleton<IProblemDetailsWriter, DefaultApiProblemDetailsWriter>());  
    }
}
```

**Part A** - (denoted as `o`) Overall

**Part B** - (denoted as `c`) How controller instances are created

**Part C** - (denoted as `b` and `a`) How Model Binding Works + How Action method Executes

**Part D** - (denoted as `d`) How Filters information discovered; d5.1 shows how Filter instance are created by compiler and wrapped into `ControllerModel`, which is further wrapped in `ApplicationModel`, then `ControllerActionDescriptorBuilder.Build(applicationModelInstance)` (d5.6)

**Part E** - (denoted as `f`) How Filters get executed in pipeline


`ActionEndpointFactory`-

```C#
public static class ControllerEndpointRouteBuilderExtensions 
{
   public static ControllerActionEndpointConventionBuilder MapControllers(this IEndpointRouteBuilder endpointsBuilder)  
   {                                                                                                             
      EnsureControllerServices(endpointsBuilder);
      return GetOrCreateDataSource(endpointsBuilder).DefaultBuilder;
   }

   public static ControllerActionEndpointConventionBuilder MapDefaultControllerRoute(this IEndpointRouteBuilder endpointsBuilder)
   {
      EnsureControllerServices(endpointsBuilder);

      var dataSource = GetOrCreateDataSource(endpointsBuilder);
      
      return dataSource.AddRoute("default", "{controller=Home}/{action=Index}/{id?}", defaults: null, constraints: null, dataTokens: null);
   }

   private static ControllerActionEndpointDataSource GetOrCreateDataSource(IEndpointRouteBuilder endpointsBuilder)
   {
      var dataSource = endpointsBuilder.DataSources.OfType<ControllerActionEndpointDataSource>().FirstOrDefault();
      if (dataSource == null) 
      {
         var orderProvider = endpointsBuilder.ServiceProvider.GetRequiredService<OrderedEndpointsSequenceProviderCache>();
         var factory = endpointsBuilder.ServiceProvider.GetRequiredService<ControllerActionEndpointDataSourceFactory>();    // <--------------------------- o1
         dataSource = factory.Create(orderProvider.GetOrCreateOrderedEndpointsSequenceProvider(endpointsBuilder));          // <--------------------------- o2.a
         endpointsBuilder.DataSources.Add(dataSource);      // <--------------------------- o4
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
internal sealed class ControllerActionEndpointDataSourceFactory   // <--------------------------- o1
{
   private readonly ControllerActionEndpointDataSourceIdProvider _dataSourceIdProvider;
   private readonly IActionDescriptorCollectionProvider _actions;
   private readonly ActionEndpointFactory _factory;
 
   public ControllerActionEndpointDataSourceFactory(
      ControllerActionEndpointDataSourceIdProvider dataSourceIdProvider,
      IActionDescriptorCollectionProvider actions,     
      ActionEndpointFactory factory)                   // <--------------------------- c1
   {
      _dataSourceIdProvider = dataSourceIdProvider;
      _actions = actions;
      _factory = factory;
   }
 
   public ControllerActionEndpointDataSource Create(OrderedEndpointsSequenceProvider orderProvider)
   {
       return new ControllerActionEndpointDataSource(_dataSourceIdProvider, _actions, _factory, orderProvider);   // <--------------------------- o2.b_
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

   public IList<FilterDescriptor> FilterDescriptors { get; set; } = Array.Empty<FilterDescriptor>();   // <-----------------------------f4.2

   public virtual string? DisplayName { get; set; }

   public IDictionary<object, object?> Properties { get; set; } = default!;

   internal IFilterMetadata[]? CachedReusableFilters { get; set; }   
}
//---------------------------Ʌ

public interface IActionDescriptorCollectionProvider
{
   ActionDescriptorCollection ActionDescriptors { get; }
}

public abstract class ActionDescriptorCollectionProvider : IActionDescriptorCollectionProvider   // <--------------------------------------d2.1
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
      
      var oldCancellationTokenSource = _cancellationTokenSource;

      _collection = new ActionDescriptorCollection(new ReadOnlyCollection<ActionDescriptor>(context.Results), _version++);

      // ...
   }
}
//---------------------------------------------------------------------Ʌ

public class ActionDescriptorProviderContext
{
   public IList<ActionDescriptor> Results { get; } = new List<ActionDescriptor>();
}

public interface IActionDescriptorProvider
{
   int Order { get; }
   void OnProvidersExecuting(ActionDescriptorProviderContext context);
   void OnProvidersExecuted(ActionDescriptorProviderContext context);
}

//------------------------------------------------------V
internal sealed class ControllerActionDescriptorProvider : IActionDescriptorProvider   // <---------------------------------d3.1
{
   private readonly ApplicationPartManager _partManager;
   private readonly ApplicationModelFactory _applicationModelFactory;    // <------------------------------d3.3+

   public ControllerActionDescriptorProvider(ApplicationPartManager partManager, ApplicationModelFactory applicationModelFactory) { ... }

   public int Order => -1000;

   public void OnProvidersExecuting(ActionDescriptorProviderContext context)
   {
      foreach (ControllerActionDescriptor descriptor in GetDescriptors())
      {
         context.Results.Add(descriptor);    // <---------------------------------------
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

   internal IEnumerable<ControllerActionDescriptor> GetDescriptors()  // <---------------d3.2, use IApplicationModelProvider(DefaultApplicationModelProvider) to create filter
   {
      IEnumerable<TypeInfo> controllerTypes = GetControllerTypes();
      var application = _applicationModelFactory.CreateApplicationModel(controllerTypes);   // <------------------------------d3.3+, d5.5
      return ControllerActionDescriptorBuilder.Build(application);    // <---------------------------d5.6+
   }
 
   private IEnumerable<TypeInfo> GetControllerTypes()
   {
      var feature = new ControllerFeature();
      _partManager.PopulateFeature(feature);
 
      return feature.Controllers;
   }
}
//------------------------------------------------------Ʌ

//---------------------------V
public class FilterDescriptor
{
   public FilterDescriptor(IFilterMetadata filter, int filterScope)
   {
      Filter = filter;
      Scope = filterScope;

      if (Filter is IOrderedFilter orderedFilter)
      {
         Order = orderedFilter.Order;   // <----------------------assign value to Order property
      }
   }

   public IFilterMetadata Filter { get; }

   public int Order { get; set; }

   public int Scope { get; }
}
//---------------------------Ʌ

//---------------------V
public class FilterItem   // associate executable filters with IFilterMetadata instances
{
   public FilterItem(FilterDescriptor descriptor)
   {
      Descriptor = descriptor;
   }

   public FilterItem(FilterDescriptor descriptor, IFilterMetadata filter) : this(descriptor)
   {
      Filter = filter;
   }

   public FilterDescriptor Descriptor { get; } = default!;

   public IFilterMetadata? Filter { get; set; }

   public bool IsReusable { get; set; }
}
//---------------------Ʌ

//-------------------------------------V
public class ControllerActionDescriptor : ActionDescriptor
{
   public string ControllerName { get; set; } = default!;
   public virtual string ActionName { get; set; } = default!;
   public MethodInfo MethodInfo { get; set; } = default!;
   public TypeInfo ControllerTypeInfo { get; set; } = default!;
   internal EndpointFilterDelegate? FilterDelegate { get; set; }
   internal ControllerActionInvokerCacheEntry? CacheEntry { get; set; }  // <---------------------
   public override string? DisplayName { get; set; }
}
//-------------------------------------Ʌ

//-----------------------------------------------------V
internal static class ControllerActionDescriptorBuilder      // <---------------------------------d6
{
   public static IList<ControllerActionDescriptor> Build(ApplicationModel application)  // ApplicationModel contains IList<ControllerModel>, which is like call 
   {                                                                                    // CreateActionDescriptor for each ControllerModel
      return ApplicationModelFactory.Flatten(application, CreateActionDescriptor);
   }

   private static ControllerActionDescriptor CreateActionDescriptor(ApplicationModel application, ControllerModel controller, ActionModel action, SelectorModel selector)
   {
      var actionDescriptor = new ControllerActionDescriptor
      {
         ActionName = action.ActionName,
         MethodInfo = action.ActionMethod,
      };

      actionDescriptor.ControllerName = controller.ControllerName;
      actionDescriptor.ControllerTypeInfo = controller.ControllerType;
      AddControllerPropertyDescriptors(actionDescriptor, controller);
 
      AddActionConstraints(actionDescriptor, selector);
      AddEndpointMetadata(actionDescriptor, selector);
      AddAttributeRoute(actionDescriptor, selector);
      AddParameterDescriptors(actionDescriptor, action);
      AddActionFilters(actionDescriptor, action.Filters, controller.Filters, application.Filters);    // <---------------------------------d6.1
      AddApiExplorerInfo(actionDescriptor, application, controller, action);
      AddRouteValues(actionDescriptor, controller, action);
      AddProperties(actionDescriptor, action, controller, application);
 
      return actionDescriptor;
   }

   private static void AddActionFilters(ControllerActionDescriptor actionDescriptor,                  
      IEnumerable<IFilterMetadata> actionFilters,
      IEnumerable<IFilterMetadata> controllerFilters,
      IEnumerable<IFilterMetadata> globalFilters)
   {
      actionDescriptor.FilterDescriptors =                                                            // <---------------------------------d6.2
         actionFilters.Select(f => new FilterDescriptor(f, FilterScope.Action))   // f is IFilterMetadata, note that those Filters instances are created by the compiler
         .Concat(controllerFilters.Select(f => new FilterDescriptor(f, FilterScope.Controller)))
         .Concat(globalFilters.Select(f => new FilterDescriptor(f, FilterScope.Global)))
         .OrderBy(d => d, FilterDescriptorOrderComparer.Comparer)
         .ToList();
   }

   public static void AddRouteValues(ControllerActionDescriptor actionDescriptor, ControllerModel controller, ActionModel action)
   {
      foreach (var kvp in action.RouteValues)
      {
         if (!actionDescriptor.RouteValues.ContainsKey(kvp.Key))
         {
            actionDescriptor.RouteValues.Add(kvp.Key, kvp.Value);
         }
      }
 
      foreach (var kvp in controller.RouteValues)
      {
         if (!actionDescriptor.RouteValues.ContainsKey(kvp.Key))
         {
            actionDescriptor.RouteValues.Add(kvp.Key, kvp.Value);
         }
      }

      // ...
   }

   // ...
}
//-----------------------------------------------------Ʌ
/*
public static class FilterScope
{
   public static readonly int First;
   public static readonly int Global = 10;
   public static readonly int Controller = 20;
   public static readonly int Action = 30;
   public static readonly int Last = 100;
}
*/
//-----------------------------------------------------Ʌ

//-------------------------------------------V
internal sealed class ApplicationModelFactory      // <-------------------------------d4
{
   private readonly IApplicationModelProvider[] _applicationModelProviders;  // // <-------------------------------d4.1+, contain DefaultApplicationModelProvider
   private readonly IList<IApplicationModelConvention> _conventions;

   public ApplicationModelFactory(IEnumerable<IApplicationModelProvider> applicationModelProviders, IOptions<MvcOptions> options)
   {
      _applicationModelProviders = applicationModelProviders.OrderBy(p => p.Order).ToArray();
      _conventions = options.Value.Conventions;
   }

   public ApplicationModel CreateApplicationModel(IEnumerable<TypeInfo> controllerTypes)
   {
      var context = new ApplicationModelProviderContext(controllerTypes);

      for (var i = 0; i < _applicationModelProviders.Length; i++)
      {
         _applicationModelProviders[i].OnProvidersExecuting(context);   // <-------------------------------d4.1+
      }

      for (var i = _applicationModelProviders.Length - 1; i >= 0; i--)
      {
         _applicationModelProviders[i].OnProvidersExecuted(context);
      }

      ApplicationModelConventions.ApplyConventions(context.Result, _conventions);

      return context.Result;
   }

   // ...
}
//-------------------------------------------Ʌ

//---------------------------V
public class ApplicationModel : IPropertyModel, IFilterModel, IApiExplorerModel   // a model for configuring controllers in an MVC application
{
   public ApplicationModel()
   {
      ApiExplorer = new ApiExplorerModel();
      Controllers = new List<ControllerModel>();
      Filters = new List<IFilterMetadata>();
      Properties = new Dictionary<object, object?>();
   }

   public ApiExplorerModel ApiExplorer { get; set; }
   public IList<ControllerModel> Controllers { get; }      // <--------------------------------------------d5.4*, contains all Controllers' info
   public IList<IFilterMetadata> Filters { get; }
   public IDictionary<object, object?> Properties { get; }
}
//---------------------------Ʌ

//------------------------------------------V
public class ApplicationModelProviderContext
{
   public ApplicationModelProviderContext(IEnumerable<TypeInfo> controllerTypes)
   {
      ControllerTypes = controllerTypes;
   }

   public IEnumerable<TypeInfo> ControllerTypes { get; }

   public ApplicationModel Result { get; } = new ApplicationModel();   // <--------------------------------------------d5.4*
}
//------------------------------------------Ʌ


//----------------------------------------------------------------------V
public class ActionModel : ICommonModel, IFilterModel, IApiExplorerModel
{
   public ActionModel(MethodInfo actionMethod, IReadOnlyList<object> attributes)
   {
       ActionMethod = actionMethod;
 
       ApiExplorer = new ApiExplorerModel();
       Attributes = new List<object>(attributes);
       Filters = new List<IFilterMetadata>();
       Parameters = new List<ParameterModel>();
       RouteValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
       Properties = new Dictionary<object, object?>();
       Selectors = new List<SelectorModel>();
   }

   public ActionModel(ActionModel other) { ... }

   public MethodInfo ActionMethod { get; }

   public string ActionName { get; set; } = default!;

   public IReadOnlyList<object> Attributes { get; }

   public ControllerModel Controller { get; set; };

   public IList<IFilterMetadata> Filters { get; }   // <--------------------contains Filters of current Action

   public IList<ParameterModel> Parameters { get; }

   public IDictionary<string, string?> RouteValues { get; }  

   // ...
}
//----------------------------------------------------------------------Ʌ

//--------------------------------------------------------------------------V
public class ControllerModel : ICommonModel, IFilterModel, IApiExplorerModel
{
   public ControllerModel(TypeInfo controllerType, IReadOnlyList<object> attributes)
   {
       ControllerType = controllerType;
 
       Actions = new List<ActionModel>();
       ApiExplorer = new ApiExplorerModel();
       Attributes = new List<object>(attributes);
       ControllerProperties = new List<PropertyModel>();
       Filters = new List<IFilterMetadata>();
       Properties = new Dictionary<object, object?>();
       RouteValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
       Selectors = new List<SelectorModel>();
   }

   public ControllerModel(ControllerModel other) { ... }

   public IList<ActionModel> Actions { get; }  // contains a list of ActionModel

   public ApiExplorerModel ApiExplorer { get; set; }

   public ApplicationModel? Application { get; set; }

   public IReadOnlyList<object> Attributes { get; }

   public string ControllerName { get; set; }

   public TypeInfo ControllerType { get; }

   public IList<IFilterMetadata> Filters { get; }  // <--------------------contains Filters of current Controller, note that thoese Filter instances are created by the compiler

   public IDictionary<string, string?> RouteValues { get; }

   // ...
}
//--------------------------------------------------------------------------Ʌ

public interface IApplicationModelProvider
{
   int Order { get; }
   void OnProvidersExecuting(ApplicationModelProviderContext context);
   void OnProvidersExecuted(ApplicationModelProviderContext context);
}

//------------------------------------------------------------------------V
internal class DefaultApplicationModelProvider : IApplicationModelProvider    // <---------------------------------d5
{
   private readonly MvcOptions _mvcOptions;   //<----------------
   private readonly IModelMetadataProvider _modelMetadataProvider;
   private readonly Func<ActionContext, bool> _supportsAllRequests;
   private readonly Func<ActionContext, bool> _supportsNonGetRequests;

   public DefaultApplicationModelProvider(IOptions<MvcOptions> mvcOptionsAccessor, IModelMetadataProvider modelMetadataProvider)
   {
      _mvcOptions = mvcOptionsAccessor.Value;
      _modelMetadataProvider = modelMetadataProvider;
 
      _supportsAllRequests = _ => true;
      _supportsNonGetRequests = context => !HttpMethods.IsGet(context.HttpContext.Request.Method);
   }

   public int Order => -1000;

   public void OnProvidersExecuting(ApplicationModelProviderContext context)
   {
      foreach (var filter in _mvcOptions.Filters)
      {
         context.Result.Filters.Add(filter);
      }

      foreach (var controllerType in context.ControllerTypes)
      {
         var controllerModel = CreateControllerModel(controllerType);   // <--------------------------------------------d5.3
         if (controllerModel == null)
            continue;
         
         // context.Result is ApplicationModel, its Controllers property is IList<ControllerModel>
         context.Result.Controllers.Add(controllerModel);               // <--------------------------------------------d5.4
         controllerModel.Application = context.Result;

         foreach (var propertyHelper in PropertyHelper.GetProperties(controllerType.AsType()))
         {
            var propertyInfo = propertyHelper.Property;
            var propertyModel = CreatePropertyModel(propertyInfo);
            if (propertyModel != null)
            {
               propertyModel.Controller = controllerModel;
               controllerModel.ControllerProperties.Add(propertyModel);
            }
         }

         foreach (var methodInfo in controllerType.AsType().GetMethods())
         {
            var actionModel = CreateActionModel(controllerType, methodInfo);
            if (actionModel == null)
               continue;
 
            actionModel.Controller = controllerModel;
            controllerModel.Actions.Add(actionModel);
 
            foreach (var parameterInfo in actionModel.ActionMethod.GetParameters())
            {
               var parameterModel = CreateParameterModel(parameterInfo);
               if (parameterModel != null)
               {
                  parameterModel.Action = actionModel;
                  actionModel.Parameters.Add(parameterModel);
               }
            }
         }
      }
   }

   public void OnProvidersExecuted(ApplicationModelProviderContext context)
   {
      // Intentionally empty.
   }

   private static void AddRange<T>(IList<T> list, IEnumerable<T> items)
   {
      foreach (var item in items)
      {
         list.Add(item);
      }
   }

   internal static ControllerModel CreateControllerModel(TypeInfo typeInfo)
   {
      var currentTypeInfo = typeInfo;
      var objectTypeInfo = typeof(object).GetTypeInfo();

      IRouteTemplateProvider[] routeAttributes;

      do
      {
         routeAttributes = currentTypeInfo
            .GetCustomAttributes(inherit: false)
            .OfType<IRouteTemplateProvider>()
            .ToArray();

         if (routeAttributes.Length > 0)
            break;
         
         currentTypeInfo = currentTypeInfo.BaseType!.GetTypeInfo();
      }
      while (currentTypeInfo != objectTypeInfo);

      var attributes = typeInfo.GetCustomAttributes(inherit: true);

      var filteredAttributes = new List<object>();
      foreach (var attribute in attributes)
      {
         if (attribute is IRouteTemplateProvider)
         {
            // This attribute is a route-attribute, leave it out.
         }
         else
         {
            filteredAttributes.Add(attribute);
         }
      }

      filteredAttributes.AddRange(routeAttributes);

      attributes = filteredAttributes.ToArray();

      var controllerModel = new ControllerModel(typeInfo, attributes);

      AddRange(controllerModel.Selectors, CreateSelectors(attributes));

      controllerModel.ControllerName = typeInfo.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ? 
                                       typeInfo.Name.Substring(0, typeInfo.Name.Length - "Controller".Length) : typeInfo.Name;

      AddRange(controllerModel.Filters, attributes.OfType<IFilterMetadata>());    // <--------------------------------------------d5.1,  get all Filters of this Controller

      foreach (var routeValueProvider in attributes.OfType<IRouteValueProvider>())   
      {
         controllerModel.RouteValues.Add(routeValueProvider.RouteKey, routeValueProvider.RouteValue);
      }

      // ...

      if (typeof(IAsyncActionFilter).GetTypeInfo().IsAssignableFrom(typeInfo) || typeof(IActionFilter).GetTypeInfo().IsAssignableFrom(typeInfo))
      {
         controllerModel.Filters.Add(new ControllerActionFilter());
      }

      if (typeof(IAsyncResultFilter).GetTypeInfo().IsAssignableFrom(typeInfo) || typeof(IResultFilter).GetTypeInfo().IsAssignableFrom(typeInfo))
      {
         controllerModel.Filters.Add(new ControllerResultFilter());
      }

      return controllerModel;    // <--------------------------------------------d5.2, return ControllerModel
   }

   internal PropertyModel CreatePropertyModel(PropertyInfo propertyInfo)
   {
      var attributes = propertyInfo.GetCustomAttributes(inherit: true);

      var declaringType = propertyInfo.DeclaringType!;
      var modelMetadata = _modelMetadataProvider.GetMetadataForProperty(declaringType, propertyInfo.Name);
      var bindingInfo = BindingInfo.GetBindingInfo(attributes, modelMetadata);

      if (bindingInfo == null)
      {
         // Look for BindPropertiesAttribute on the handler type if no BindingInfo was inferred for the property.
         // This allows a user to enable model binding on properties by decorating the controller type with BindPropertiesAttribute.
         var bindPropertiesAttribute = declaringType.GetCustomAttribute<BindPropertiesAttribute>(inherit: true);
         if (bindPropertiesAttribute != null)
         {
            var requestPredicate = bindPropertiesAttribute.SupportsGet ? _supportsAllRequests : _supportsNonGetRequests;
            bindingInfo = new BindingInfo { RequestPredicate = requestPredicate };
         }
      }
 
      var propertyModel = new PropertyModel(propertyInfo, attributes) 
      {
         PropertyName = propertyInfo.Name,
         BindingInfo = bindingInfo,
      };
 
      return propertyModel;
   }

   internal ActionModel CreateActionModel(TypeInfo typeInfo, MethodInfo methodInfo)
   {
      if (!IsAction(typeInfo, methodInfo))
      {
         return null;
      }

      var attributes = methodInfo.GetCustomAttributes(inherit: true);
      var actionModel = new ActionModel(methodInfo, attributes);
      AddRange(actionModel.Filters, attributes.OfType<IFilterMetadata>());   // <--------------------------------------------d5.1,  get all Filters of this Action

      var actionName = attributes.OfType<ActionNameAttribute>().FirstOrDefault();
      if (actionName?.Name != null)
      {
         actionModel.ActionName = actionName.Name;
      }
      else
      {
         actionModel.ActionName = CanonicalizeActionName(methodInfo.Name);
      }

      // ...

      foreach (var routeValueProvider in attributes.OfType<IRouteValueProvider>())
      {
         actionModel.RouteValues.Add(routeValueProvider.RouteKey, routeValueProvider.RouteValue);
      }

      var currentMethodInfo = methodInfo;
 
      IRouteTemplateProvider[] routeAttributes;

      while (true)
      {
         routeAttributes = currentMethodInfo
            .GetCustomAttributes(inherit: false)
            .OfType<IRouteTemplateProvider>()
            .ToArray();
 
         if (routeAttributes.Length > 0)
         {
            // Found 1 or more route attributes.
            break;
         }
 
         // GetBaseDefinition returns 'this' when it gets to the bottom of the chain.
         var nextMethodInfo = currentMethodInfo.GetBaseDefinition();
         if (currentMethodInfo == nextMethodInfo)
         {
            break;
         }
 
         currentMethodInfo = nextMethodInfo;
      }

      var applicableAttributes = new List<object>(routeAttributes.Length);
      foreach (var attribute in attributes)
      {
         if (attribute is IRouteTemplateProvider)
         {
               // This attribute is a route-attribute, leave it out.
         }
         else
         {
            applicableAttributes.Add(attribute);
         }
      }
 
      applicableAttributes.AddRange(routeAttributes);
      AddRange(actionModel.Selectors, CreateSelectors(applicableAttributes));
 
      return actionModel;
   }

   // ...
}
//------------------------------------------------------------------------Ʌ

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

//-------------------V
public class Endpoint  // represents a logical endpoint in an application
{
   public Endpoint(RequestDelegate? requestDelegate, EndpointMetadataCollection? metadata, string? displayName)
   {
      RequestDelegate = requestDelegate;
      Metadata = metadata ?? EndpointMetadataCollection.Empty;
      DisplayName = displayName;
   }

   public string? DisplayName { get; }

   public EndpointMetadataCollection Metadata { get; }  // <--------contains ActionDescriptor, refer to r1

   public RequestDelegate? RequestDelegate { get; }

   public override string? ToString() => DisplayName ?? base.ToString();
}
//-------------------Ʌ

//--------------------------------------V
public abstract class EndpointDataSource   // provides a collection of Endpoint instances
{
   public abstract IChangeToken GetChangeToken();

   public abstract IReadOnlyList<Endpoint> Endpoints { get; }   // <--------!important,  eventually get called via DataSourceDependentMatcher -> DataSourceDependentCache<Matcher>
                                                                // (DataSourceDependentMatcher.cs,23), Endpoint/ActionDescription info will be generated first request  
                                                                //  via EndpointRoutingMiddleware, then all will be cached


   public virtual IReadOnlyList<Endpoint> GetGroupedEndpoints(RouteGroupContext context)
   {
      var endpoints = Endpoints;
      var wrappedEndpoints = new RouteEndpoint[endpoints.Count];

      for (int i = 0; i < endpoints.Count; i++)
      {
         var endpoint = endpoints[i];

         if (endpoint is not RouteEndpoint routeEndpoint)
         {
            throw new NotSupportedException(Resources.FormatMapGroup_CustomEndpointUnsupported(endpoint.GetType()));
         }

         var fullRoutePattern = RoutePatternFactory.Combine(context.Prefix, routeEndpoint.RoutePattern);
         var routeEndpointBuilder = new RouteEndpointBuilder(routeEndpoint.RequestDelegate, fullRoutePattern, routeEndpoint.Order)
         {
            DisplayName = routeEndpoint.DisplayName,
            ApplicationServices = context.ApplicationServices
         }

         foreach (var convention in context.Conventions)
         {
            convention(routeEndpointBuilder);
         }

         foreach (var metadata in routeEndpoint.Metadata)
         {
            routeEndpointBuilder.Metadata.Add(metadata);
         }

         foreach (var finallyConvention in context.FinallyConventions)
         {
            finallyConvention(routeEndpointBuilder);
         }

         wrappedEndpoints[i] = (RouteEndpoint)routeEndpointBuilder.Build();
      }

      return wrappedEndpoints;
   }

   // ...
}
//--------------------------------------Ʌ

//--------------------------------------------------V
internal abstract class ActionEndpointDataSourceBase : EndpointDataSource, IDisposable
{
   private readonly IActionDescriptorCollectionProvider _actions;    // <------------------------------------d1+

   protected readonly object Lock = new object();

   protected readonly List<Action<EndpointBuilder>> Conventions;
   protected readonly List<Action<EndpointBuilder>> FinallyConventions;

   private List<Endpoint>? _endpoints;      // <-----------------------------------------------that's why it is EndpointDataSource
   private CancellationTokenSource? _cancellationTokenSource;
   private IChangeToken? _changeToken;
   private IDisposable? _disposable;

   public ActionEndpointDataSourceBase(IActionDescriptorCollectionProvider actions)
   {
      _actions = actions;
 
      Conventions = new List<Action<EndpointBuilder>>();
      FinallyConventions = new List<Action<EndpointBuilder>>();
   }

   public override IReadOnlyList<Endpoint> Endpoints 
   {
      get {
         Initialize();
         return _endpoints;
      }
   }

   public override IReadOnlyList<Endpoint> GetGroupedEndpoints(RouteGroupContext context)  
   {
      return CreateEndpoints(
         context.Prefix,
         _actions.ActionDescriptors.Items,
         Conventions,
         context.Conventions,
         FinallyConventions,
         context.FinallyConventions);
   }

   protected abstract List<Endpoint> CreateEndpoints(
      RoutePattern? groupPrefix,
      IReadOnlyList<ActionDescriptor> actions,
      IReadOnlyList<Action<EndpointBuilder>> conventions,
      IReadOnlyList<Action<EndpointBuilder>> groupConventions,
      IReadOnlyList<Action<EndpointBuilder>> finallyConventions,
      IReadOnlyList<Action<EndpointBuilder>> groupFinallyConventions);

   protected void Subscribe()
   {
      if (_actions is ActionDescriptorCollectionProvider collectionProviderWithChangeToken)
      {
         _disposable = ChangeToken.OnChange(
            () => collectionProviderWithChangeToken.GetChangeToken(),
            UpdateEndpoints);
      }
   }

   private void Initialize()
   {
      // ...
      UpdateEndpoints();
   }

   private void UpdateEndpoints()
   {
      lock (Lock)
      {
         var endpoints = CreateEndpoints(
            groupPrefix: null,
            _actions.ActionDescriptors.Items,
            conventions: Conventions,
            groupConventions: Array.Empty<Action<EndpointBuilder>>(),
            finallyConventions: FinallyConventions,
            groupFinallyConventions: Array.Empty<Action<EndpointBuilder>>());

         // step 1 - capture old token
         var oldCancellationTokenSource = _cancellationTokenSource;
 
         // step 2 - update endpoints
         _endpoints = endpoints;      // <--------------------------------at this point EndpointDataSource has all the Endpoint
 
         // step 3 - create new change token
         _cancellationTokenSource = new CancellationTokenSource();
         _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);
 
         // step 4 - trigger old token
         oldCancellationTokenSource?.Cancel();
      }
   }
}
//--------------------------------------------------Ʌ

//-------------------------------------------------------------------------------------V
internal sealed class ControllerActionEndpointDataSource : ActionEndpointDataSourceBase     // <--------------------------------o3
{                                                                                           // <----d1, ActionEndpointDataSourceBase contains IActionDescriptorCollectionProvider
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

   public void AddEndpoints(       // <-----------------!important, to add requestDelegate to endpoints
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

         AddActionDataToBuilder(builder, routeNames, action, routeName: null, dataTokens: null, suppressLinkGeneration: false, suppressPathMatching: false,
            groupConventions: groupConventions,
            conventions: conventions,
            perRouteConventions: Array.Empty<Action<EndpointBuilder>>(),
            groupFinallyConventions: groupFinallyConventions,
            finallyConventions: finallyConventions,
            perRouteFinallyConventions: Array.Empty<Action<EndpointBuilder>>());

         endpoints.Add(builder.Build());    // <---------------------------------
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

            var builder = new RouteEndpointBuilder(requestDelegate, updatedRoutePattern, route.Order)   // requestDelegate is the same for every Endpoint
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

   private static RequestDelegate CreateRequestDelegate()  // <-----------------
   {
      IActionInvokerFactory invokerFactory = null;

      return (context) =>    // <----this is the same RequestDelegate that attached to every Endpoint, so IActionInvoker etc will only invoke during the runtime of each request
      {
         var endpoint = context.GetEndpoint()!;   // the purpose is to get ActionDescriptor that is associated with the Endpoint
         var dataTokens = endpoint.Metadata.GetMetadata<IDataTokensMetadata>();
 
         var routeData = new RouteData();
         routeData.PushState(router: null, context.Request.RouteValues, new RouteValueDictionary(dataTokens?.DataTokens));
 
         var action = endpoint.Metadata.GetMetadata<ActionDescriptor>()!;     // r1

         var actionContext = new ActionContext(context, routeData, action);   // <-----------------------------------------------------create an instance of ActionContext
 
         if (invokerFactory == null)
         {
            invokerFactory = context.RequestServices.GetRequiredService<IActionInvokerFactory>();  // <-------------------------
         }                                                                                        
 
         var invoker = invokerFactory.CreateInvoker(actionContext);   // <----------------------------------------------------f1
         
         // invoker is ControllerActionInvoker, but InvokeAsync() is from ResourceInvoker
         return invoker.InvokeAsync();                                // <----------------------------------------------------f1.4, f2.4c_
      };                                                              
   }
   //
   private sealed class InertEndpointBuilder : EndpointBuilder
   {
      public override Endpoint Build()
      {
         return new Endpoint(RequestDelegate, new EndpointMetadataCollection(Metadata), DisplayName);
      }
   }
   //
}
//-----------------------------------------Ʌ


//-------------------------------------------------filter related-----------------------------------------------------------------------------------------------------------

public class ActionInvokerProviderContext
{
   public ActionInvokerProviderContext(ActionContext actionContext)
   {
      ActionContext = actionContext;
   }

   public ActionContext ActionContext { get; }
   
   public IActionInvoker? Result { get; set; }
}

//----------------------------------------------------------------V
internal sealed class ActionInvokerFactory : IActionInvokerFactory      // <---------------------------------------------f1
{
   private readonly IActionInvokerProvider[] _actionInvokerProviders;   // <----------------------------f1.1
                                                                        // rely on ControllerActionInvokerProvider to set IActionInvoker to ActionInvokerProviderContext
                                                                        // ControllerActionInvokerProvider creates an instance of ControllerContext

   public ActionInvokerFactory(IEnumerable<IActionInvokerProvider> actionInvokerProviders)
   {
      _actionInvokerProviders = actionInvokerProviders.OrderBy(item => item.Order).ToArray();
   }
 
   public IActionInvoker CreateInvoker(ActionContext actionContext)    // creates ControllerActionInvoker
   {
      var context = new ActionInvokerProviderContext(actionContext);   
 
      foreach (var provider in _actionInvokerProviders)
      {
         provider.OnProvidersExecuting(context);                       // <----------------------------f1.2+                
      }
 
      for (var i = _actionInvokerProviders.Length - 1; i >= 0; i--)
      {
          _actionInvokerProviders[i].OnProvidersExecuted(context);
      }
 
      return context.Result;    // <----------------------------f1.3, f2.4b
   }
}
//----------------------------------------------------------------Ʌ
```


```C#
//---------------------------------------------------------------------------------------- 
internal interface ITypeActivatorCache
{
   TInstance CreateInstance<TInstance>(IServiceProvider serviceProvider, Type optionType);
}

internal sealed class TypeActivatorCache : ITypeActivatorCache
{
   private readonly Func<Type, ObjectFactory> _createFactory = (type) => ActivatorUtilities.CreateFactory(type, Type.EmptyTypes);

   private readonly ConcurrentDictionary<Type, ObjectFactory> _typeActivatorCache = new ConcurrentDictionary<Type, ObjectFactory>();

   public TInstance CreateInstance<TInstance>(IServiceProvider serviceProvider, Type implementationType)
   {
      // public delegate object ObjectFactory(IServiceProvider serviceProvider, object?[]? arguments);
      ObjectFactory createFactory = _typeActivatorCache.GetOrAdd(implementationType, _createFactory);   // <---------------------------c7<
      return (TInstance)createFactory(serviceProvider, arguments: null);
   }
}
//----------------------------------------------------------------------------------------Ʌ

//-----------------------------------------------------------------------------------------------------------------V
public interface IControllerActivator
{
   object Create(ControllerContext context);  // creates a controller
   
   void Release(ControllerContext context, object controller);

   ValueTask ReleaseAsync(ControllerContext context, object controller)
   {
      Release(context, controller);
      return default;
   }

}

//----------------------------------------------VV
internal sealed class DefaultControllerActivator : IControllerActivator
{
   private readonly ITypeActivatorCache _typeActivatorCache;

   public DefaultControllerActivator(ITypeActivatorCache typeActivatorCache)
   {
      _typeActivatorCache = typeActivatorCache;
   }

   public object Create(ControllerContext controllerContext)
   {
      var controllerTypeInfo = controllerContext.ActionDescriptor.ControllerTypeInfo;

      var serviceProvider = controllerContext.HttpContext.RequestServices;

      return _typeActivatorCache.CreateInstance<object>(serviceProvider, controllerTypeInfo.AsType());   // <---------------------c6<
   }

   public void Release(ControllerContext context, object controller)
   {
      if (controller is IDisposable disposable)
      {
         disposable.Dispose();
      }
   }

   public ValueTask ReleaseAsync(ControllerContext context, object controller)
   {
      if (controller is IAsyncDisposable asyncDisposable)
      {
         return asyncDisposable.DisposeAsync();
      }
 
      Release(context, controller);
      return default;
   }
}
//----------------------------------------------ɅɅ

//----------------------------------------------VV
public interface IControllerFactory
{
   object CreateController(ControllerContext context);

   void ReleaseController(ControllerContext context, object controller);

   ValueTask ReleaseControllerAsync(ControllerContext context, object controller)
   {
      ReleaseController(context, controller);
      return default;
   }
}

internal sealed class DefaultControllerFactory : IControllerFactory
{
   private readonly IControllerActivator _controllerActivator;
   private readonly IControllerPropertyActivator[] _propertyActivators;

   public DefaultControllerFactory(IControllerActivator controllerActivator, IEnumerable<IControllerPropertyActivator> propertyActivators)
   {
      _controllerActivator = controllerActivator;
      _propertyActivators = propertyActivators.ToArray();
   }

   public object CreateController(ControllerContext context)
   {
      var controller = _controllerActivator.Create(context);   // <-------------------------
      foreach (var propertyActivator in _propertyActivators)
      {
         propertyActivator.Activate(context, controller);
      }

      return controller;
   }

   public void ReleaseController(ControllerContext context, object controller)
   {
      _controllerActivator.Release(context, controller);
   }

   public ValueTask ReleaseControllerAsync(ControllerContext context, object controller)
   {
      return _controllerActivator.ReleaseAsync(context, controller);
   }
}
//----------------------------------------------ɅɅ

public interface IControllerFactoryProvider
{
   // creates a factory for producing controllers for the specified descriptor
   Func<ControllerContext, object> CreateControllerFactory(ControllerActionDescriptor descriptor);

   Action<ControllerContext, object>? CreateControllerReleaser(ControllerActionDescriptor descriptor);

   Func<ControllerContext, object, ValueTask>? CreateAsyncControllerReleaser(ControllerActionDescriptor descriptor);
}

//---------------------------------------------V
internal sealed class ControllerFactoryProvider : IControllerFactoryProvider
{
   private readonly IControllerActivatorProvider _activatorProvider;
   private readonly Func<ControllerContext, object>? _factoryCreateController;
   private readonly Action<ControllerContext, object>? _factoryReleaseController;
   private readonly Func<ControllerContext, object, ValueTask>? _factoryReleaseControllerAsync;
   private readonly IControllerPropertyActivator[] _propertyActivators;

   public ControllerFactoryProvider(
      IControllerActivatorProvider activatorProvider,
      IControllerFactory controllerFactory,
      IEnumerable<IControllerPropertyActivator> propertyActivators)
   {
      _activatorProvider = activatorProvider;
      if (controllerFactory.GetType() != typeof(DefaultControllerFactory))
      {
         _factoryCreateController = controllerFactory.CreateController;
         _factoryReleaseController = controllerFactory.ReleaseController;
         _factoryReleaseControllerAsync = controllerFactory.ReleaseControllerAsync;
      }
 
      _propertyActivators = propertyActivators.ToArray();
   }

   public Func<ControllerContext, object> CreateControllerFactory(ControllerActionDescriptor descriptor)   // <--------------------c4<
   {
      var controllerType = descriptor.ControllerTypeInfo?.AsType();
      if (_factoryCreateController != null)
      {
         return _factoryCreateController;
      }

      Func<ControllerContext, object> controllerActivator = _activatorProvider.CreateActivator(descriptor);   // <--------------------c4<
      Action<ControllerContext, object>[] propertyActivators = GetPropertiesToActivate(descriptor);

      // this is like a delegate consists of two delegates (inner and outter)
      // inner one (controllerActivator) is the real one that create an instance of the controller 
      object CreateController(ControllerContext controllerContext)   // <------------- this delegate is called in ControllerInvoker
      {
         var controller = controllerActivator(controllerContext);    // <------------- this is when an instance of controller is created
         for (var i = 0; i < propertyActivators.Length; i++)
         {
            var propertyActivator = propertyActivators[i];
            propertyActivator(controllerContext, controller);
         }
 
         return controller;
      }

      return CreateController;
   }

   public Action<ControllerContext, object>? CreateControllerReleaser(ControllerActionDescriptor descriptor)
   {
      var controllerType = descriptor.ControllerTypeInfo?.AsType();

      if (_factoryReleaseController != null)
      {
         return _factoryReleaseController;
      }
 
      return _activatorProvider.CreateReleaser(descriptor);
   }

   public Func<ControllerContext, object, ValueTask>? CreateAsyncControllerReleaser(ControllerActionDescriptor descriptor)
   {
      var controllerType = descriptor.ControllerTypeInfo?.AsType();

      if (_factoryReleaseControllerAsync != null)
      {
         return _factoryReleaseControllerAsync;
      }
 
      return _activatorProvider.CreateAsyncReleaser(descriptor);
   }

   private Action<ControllerContext, object>[] GetPropertiesToActivate(ControllerActionDescriptor actionDescriptor)
   {
      var propertyActivators = new Action<ControllerContext, object>[_propertyActivators.Length];
      for (var i = 0; i < _propertyActivators.Length; i++)
      {
         var activatorProvider = _propertyActivators[i];
         propertyActivators[i] = activatorProvider.GetActivatorDelegate(actionDescriptor);
      }
 
      return propertyActivators;
   }
}
//---------------------------------------------Ʌ

//-------------------------------------------V
public interface IControllerActivatorProvider
{
   Func<ControllerContext, object> CreateActivator(ControllerActionDescriptor descriptor);

   Action<ControllerContext, object>? CreateReleaser(ControllerActionDescriptor descriptor);

   Func<ControllerContext, object, ValueTask>? CreateAsyncReleaser(ControllerActionDescriptor descriptor)
   {
      var releaser = CreateReleaser(descriptor);
      if (releaser is null)
      {
         return static (_, _) => default;
      }
 
      return (context, controller) =>
      {
         releaser.Invoke(context, controller);
         return default;
      };
   }
}
//-------------------------------------------Ʌ

//--------------------------------------V
public class ControllerActivatorProvider : IControllerActivatorProvider
{
   private static readonly Action<ControllerContext, object> _dispose = Dispose;
   private static readonly Func<ControllerContext, object, ValueTask> _disposeAsync = DisposeAsync;
   private static readonly Func<ControllerContext, object, ValueTask> _syncDisposeAsync = SyncDisposeAsync;
   private readonly Func<ControllerContext, object>? _controllerActivatorCreate;   // <---------------------
   private readonly Action<ControllerContext, object>? _controllerActivatorRelease;
   private readonly Func<ControllerContext, object, ValueTask>? _controllerActivatorReleaseAsync;

   public ControllerActivatorProvider(IControllerActivator controllerActivator)   // <----------------------c5<
   {
      if (controllerActivator.GetType() != typeof(DefaultControllerActivator))
      {
         _controllerActivatorCreate = controllerActivator.Create;                 // <----------------------c5<
         _controllerActivatorRelease = controllerActivator.Release;
         _controllerActivatorReleaseAsync = controllerActivator.ReleaseAsync;
      }
   }

   public Func<ControllerContext, object> CreateActivator(ControllerActionDescriptor descriptor)
   {
      var controllerType = descriptor.ControllerTypeInfo?.AsType();

      if (_controllerActivatorCreate != null)
      {
         return _controllerActivatorCreate;   
      }

      var typeActivator = ActivatorUtilities.CreateFactory(controllerType, Type.EmptyTypes);
      return controllerContext => typeActivator(controllerContext.HttpContext.RequestServices, arguments: null);
   }

   public Action<ControllerContext, object>? CreateReleaser(ControllerActionDescriptor descriptor)
   {
      if (_controllerActivatorRelease != null)
      {
         return _controllerActivatorRelease;
      }

      if (typeof(IDisposable).GetTypeInfo().IsAssignableFrom(descriptor.ControllerTypeInfo))
      {
         return _dispose;
      }

      return null;
   }

   public Func<ControllerContext, object, ValueTask>? CreateAsyncReleaser(ControllerActionDescriptor descriptor)
   {
      if (_controllerActivatorReleaseAsync != null)
      {
         return _controllerActivatorReleaseAsync;
      }
 
      if (typeof(IAsyncDisposable).GetTypeInfo().IsAssignableFrom(descriptor.ControllerTypeInfo))
      {
         return _disposeAsync;
      }
 
      if (typeof(IDisposable).GetTypeInfo().IsAssignableFrom(descriptor.ControllerTypeInfo))
      {
         return _syncDisposeAsync;
      }
 
      return null;
   }

   private static void Dispose(ControllerContext context, object controller)
   {
      ((IDisposable)controller).Dispose();
   }

   // ...
}
//--------------------------------------Ʌ
//-----------------------------------------------------------------------------------------------------------------Ʌ
```


```C#
//-----------------------------------------------------V
internal sealed class ControllerActionInvokerCacheEntry
{
   internal ControllerActionInvokerCacheEntry(
      FilterItem[] cachedFilters,
      Func<ControllerContext, object> controllerFactory,
      Func<ControllerContext, object, ValueTask>? controllerReleaser,
      ControllerBinderDelegate? controllerBinderDelegate,
      ObjectMethodExecutor objectMethodExecutor,
      ActionMethodExecutor actionMethodExecutor,
      ActionMethodExecutor innerActionMethodExecutor)
   {
      // ...
   }

   public FilterItem[] CachedFilters { get; }

   public Func<ControllerContext, object> ControllerFactory { get; }   // <--------------------------c2

   public Func<ControllerContext, object, ValueTask>? ControllerReleaser { get; }
 
   public ControllerBinderDelegate? ControllerBinderDelegate { get; }
 
   internal ObjectMethodExecutor ObjectMethodExecutor { get; }
 
   internal ActionMethodExecutor ActionMethodExecutor { get; }
 
   internal ActionMethodExecutor InnerActionMethodExecutor { get; }
}
//-----------------------------------------------------Ʌ

//------------------------------------------------V
internal sealed class ControllerActionInvokerCache                // <---------------------------------f3
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
         var filterFactoryResult = FilterFactory.GetAllFilters(_filterProviders, controllerContext);    // <---------------------------------f3.1+
         filters = filterFactoryResult.Filters;                                                         // <---------------------------------f3.2
                                                                                                        // <---------------------------------f4.9
         var parameterDefaultValues = ParameterDefaultValues.GetParameterDefaultValues(actionDescriptor.MethodInfo);

         var objectMethodExecutor = ObjectMethodExecutor.Create(
            actionDescriptor.MethodInfo,
            actionDescriptor.ControllerTypeInfo,
            parameterDefaultValues);

         var controllerFactory = _controllerFactoryProvider.CreateControllerFactory(actionDescriptor);   // <----------------------c3<
         var controllerReleaser = _controllerFactoryProvider.CreateAsyncControllerReleaser(actionDescriptor);   
         var propertyBinderFactory = ControllerBinderDelegateProvider.CreateBinderDelegate(   // <---------------------------------b1.a
            _parameterBinder,
            _modelBinderFactory,
            _modelMetadataProvider,
            actionDescriptor,
            _mvcOptions);
         
         var actionMethodExecutor = ActionMethodExecutor.GetExecutor(objectMethodExecutor);
         var filterExecutor = actionDescriptor.FilterDelegate is not null ? ActionMethodExecutor.GetFilterExecutor(actionDescriptor) : null;

         cacheEntry = new ControllerActionInvokerCacheEntry(
            filterFactoryResult.CacheableFilters,
            controllerFactory,                       // <------------------------------c2<
            controllerReleaser,
            propertyBinderFactory,
            objectMethodExecutor,
            filterExecutor ?? actionMethodExecutor,
            actionMethodExecutor);

         actionDescriptor.CacheEntry = cacheEntry;   // <------------------------------c2<
      }
      else 
      {
         // filter instances from statically defined filter descriptors + from filter providers
         filters = FilterFactory.CreateUncachedFilters(_filterProviders, controllerContext, cacheEntry.CachedFilters);
      }

      return (cacheEntry, filters);    // <---------------------------------f3.3
   }
}
//------------------------------------------------Ʌ

//--------------------------------V
public class FilterProviderContext
{
   public FilterProviderContext(ActionContext actionContext, IList<FilterItem> items)
   {
      ActionContext = actionContext;
      Results = items;
   }

   public ActionContext ActionContext { get; set; }

   public IList<FilterItem> Results { get; set; }
}
//--------------------------------Ʌ

//-----------------------------------------V
internal sealed class DefaultFilterProvider : IFilterProvider   // DefaultFilterProvider's purpose is to set Filter instance to FilterItem
{
   public int Order => -1000;

   public void OnProvidersExecuting(FilterProviderContext context)
   {
      if (context.ActionContext.ActionDescriptor.FilterDescriptors != null)
      {
         var results = context.Results;   // results is IList<FilterItem>
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

   public static void ProvideFilter(FilterProviderContext context, FilterItem filterItem)  // <--------------------f5
   {
      if (filterItem.Filter != null)   // <--------------important!, FilterFactory will set FilterItem.Filter to null if it is not reusable
      {
         return;
      }

      var filter = filterItem.Descriptor.Filter;

      if (filter is not IFilterFactory filterFactory)                       // <--------------------f5.1
      {
         filterItem.Filter = filter;     // <------------need to check carefully. related to caching

         filterItem.IsReusable = true;   // that's why default Filters are resusable
      }
      else
      {                                                                     
         var services = context.ActionContext.HttpContext.RequestServices;
         filterItem.Filter = filterFactory.CreateInstance(services);        // <--------------------!important f5.1
         filterItem.IsReusable = filterFactory.IsReusable;

         if (filterItem.Filter == null)
         {
            throw new InvalidOperationException(Resources.FormatTypeMethodMustReturnNotNullValue("CreateInstance", typeof(IFilterFactory).Name));
         }

         ApplyFilterToContainer(filterItem.Filter, filterFactory);
      }
   }

   /*
   private static void ApplyFilterToContainer(object actualFilter, IFilterMetadata filterMetadata)  // not important
   {
      if (actualFilter is IFilterContainer container)
      {
         container.FilterDefinition = filterMetadata;
      }
   }
   */
}
//-----------------------------------------Ʌ

internal readonly struct FilterFactoryResult
{
   public FilterFactoryResult(FilterItem[] cacheableFilters, IFilterMetadata[] filters)
   {
      CacheableFilters = cacheableFilters;
      Filters = filters;
   }

   public FilterItem[] CacheableFilters { get; }

   public IFilterMetadata[] Filters { get; }
}

//---------------------------------V
internal static class FilterFactory       // <------------------------------------f4
{
   public static FilterFactoryResult GetAllFilters(IFilterProvider[] filterProviders, ActionContext actionContext)   // <--------------------------------f4.1
   {
      var actionDescriptor = actionContext.ActionDescriptor;
      var staticFilterItems = new FilterItem[actionDescriptor.FilterDescriptors.Count];

      // <--------------------------------f4.2, at this stage, ActionContext already contains information about Filters
      var orderedFilters = actionDescriptor.FilterDescriptors.OrderBy(filter => filter, FilterDescriptorOrderComparer.Comparer).ToList();

      for (var i = 0; i < orderedFilters.Count; i++)
      {
         staticFilterItems[i] = new FilterItem(orderedFilters[i]);   
      }

      var allFilterItems = new List<FilterItem>(staticFilterItems);    // <--------------------------------f4.3, wraps FilterDescriptor into FilterItem

      // execute the filter factory to determine which static filters can be cached
      var filters = CreateUncachedFiltersCore(filterProviders, actionContext, allFilterItems);   // <--------------------------------f4.4
                                                                                                 // <--------------------------------f4.8
      // Cache the filter items based on the following criteria
      // 1. Are created statically (ex: via filter attributes, added to global filter list etc.)
      // 2. Are re-usable
      var allFiltersAreReusable = true;
      for (var i = 0; i < staticFilterItems.Length; i++)
      {
         var item = staticFilterItems[i];
         if (!item.IsReusable)
         {
            item.Filter = null;   // <----------important!, it will be used in DefaultFilterProvider (refer to the ProviderFilter method), that is how you can reuse filter
            allFiltersAreReusable = false;
         }
      }

      if (allFiltersAreReusable && filterProviders.Length == 1 && filterProviders[0] is DefaultFilterProvider defaultFilterProvider)
      {
         // if we know we can safely cache all filters and only the default filter provider is registered, we can probably re-use filters between requests.
         actionDescriptor.CachedReusableFilters = filters;   // <----------------------------------
      }

      return new FilterFactoryResult(staticFilterItems, filters);   // staticFilterItems is FilterItem[], filters is IFilterMetadata[]   // <--------------------------------f4.9
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

   private static IFilterMetadata[] CreateUncachedFiltersCore(IFilterProvider[] filterProviders, ActionContext actionContext, List<FilterItem> filterItems) // <-------------f4.4
   {                                                                                                                                  // IFilterProvider is DefaultFilterProvider
      // execute providers
      var context = new FilterProviderContext(actionContext, filterItems);   // wrap List<FilterItem> into FilterProviderContext

      for (var i = 0; i < filterProviders.Length; i++)
      {
         filterProviders[i].OnProvidersExecuting(context);    // <------------------------------f4.5+
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
         var filters = new IFilterMetadata[count];     // <------------------------------f4.6, assign Filter instance from FilterItem to this new created array
         var filterIndex = 0;
         for (int i = 0; i < filterItems.Count; i++)
         {
            var filter = filterItems[i].Filter;
            if (filter != null)
            {
               filters[filterIndex++] = filter;
            }
         }

         return filters;                               // <------------------------------f4.7, return IFilterMetadata[]
      }
   }
}
//---------------------------------Ʌ

//---------------------------------------V
public class ActionInvokerProviderContext
{
   public ActionInvokerProviderContext(ActionContext actionContext)
   {
      ActionContext = actionContext;
   }

   public ActionContext ActionContext { get; }

   public IActionInvoker? Result { get; set; }
}
//---------------------------------------Ʌ

public interface IActionInvokerProvider
{
   int Order { get; }
   void OnProvidersExecuting(ActionInvokerProviderContext context);
   void OnProvidersExecuted(ActionInvokerProviderContext context);
}

//----------------------------------------------------------------------------V
internal sealed class ControllerActionInvokerProvider : IActionInvokerProvider      // <------------------------- f2
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

   public void OnProvidersExecuting(ActionInvokerProviderContext context)       // <------------------------- f2.1
   {
      if (context.ActionContext.ActionDescriptor is ControllerActionDescriptor)
      {
         var controllerContext = new ControllerContext(context.ActionContext)   // <----------------------------------------! create ControllerContext instance 
         {
            ValueProviderFactories = new CopyOnWriteList<IValueProviderFactory>(_valueProviderFactories)
         };
         controllerContext.ModelState.MaxAllowedErrors = _maxModelValidationErrors;
 
         var (cacheEntry, filters) = _controllerActionInvokerCache.GetCachedResult(controllerContext);   // <------------------------- f2.2+
 
          // <--------------f2.3  create ControllerActionInvoker instance and supply filter
         var invoker = new ControllerActionInvoker(_logger, _diagnosticListener, _actionContextAccessor, _mapper, controllerContext, cacheEntry, filters);  // <-------f2.3
                          
         context.Result = invoker;    // <-----------------------f2.4a
      }
   }

   public void OnProvidersExecuted(ActionInvokerProviderContext context)
   {

   }
}
//----------------------------------------------------------------------------Ʌ
```

```C#
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

//----------------------V
internal readonly struct FilterCursorItem<TFilter, TFilterAsync>
{
   public FilterCursorItem(TFilter filter, TFilterAsync filterAsync)
   {
      Filter = filter;
      FilterAsync = filterAsync;
   }

   public TFilter Filter { get; }

   public TFilterAsync FilterAsync { get; }
}
//----------------------Ʌ

//---------------------------------------------V
internal abstract partial class ResourceInvoker                    // <------------------------------ f2.3a
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
   protected object _instance;   // this field stores Controller instance

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
      _cursor = new FilterCursor(filters);    // <-------------
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

   protected abstract Task InvokeInnerFilterAsync();   // override by ControllerActionInvoker

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
 
               state = current.FilterAsync;  // state is IAsyncAuthorizationFilter
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
            if (authorizationContext.Result != null)   // that's how you short-circuit by setting Result to IActionResult
            {
               goto case State.AuthorizationShortCircuit;
            }

            goto case State.AuthorizationNext;    // go up again, look like _index from FilterCursor persists, so it doesn't call sth like InvokeNextXXXilter like others
         
         case State.AuthorizationShortCircuit:
            isCompleted = true;
            _result = _authorizationContext.Result;
            return InvokeAlwaysRunResultFilters();   // invoke IAlwaysRunResultFilter
         
         case State.AuthorizationEnd:
            goto case State.ResourceBegin;

         case State.ResourceBegin:
            _cursor.Reset();
            goto case State.ResourceNext;

         case State.ResourceNext:
            var current = _cursor.GetNextFilter<IResourceFilter, IAsyncResourceFilter>();
            if (current.FilterAsync != null)
            {
               if (_resourceExecutingContext == null)
               {
                  _resourceExecutingContext = new ResourceExecutingContextSealed(_actionContext, _filters, _valueProviderFactories);
               }

               state = current.FilterAsync;
               goto case State.ResourceAsyncBegin;
            }
            else if (current.Filter != null)
            {
               if (_resourceExecutingContext == null)
               {
                  _resourceExecutingContext = new ResourceExecutingContextSealed(_actionContext, _filters, _valueProviderFactories);
               }

               state = current.Filter;
               goto case State.ResourceSyncBegin;
            }
            else
            {
               goto case State.ResourceInside;
            }

         case State.ResourceAsyncBegin:
            var filter = (IAsyncResourceFilter)state;
            var resourceExecutingContext = _resourceExecutingContext;

            var task = filter.OnResourceExecutionAsync(resourceExecutingContext, InvokeNextResourceFilterAwaitedAsync);
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResourceAsyncEnd;
               return task;
            }

            goto case State.ResourceAsyncEnd;
         
         case State.ResourceAsyncEnd:
            var filter = (IAsyncResourceFilter)state;
            if (_resourceExecutedContext == null)
            {
               // if we get here then the filter didn't call 'next' indicating a short circuit
               _resourceExecutedContext = new ResourceExecutedContextSealed(_resourceExecutingContext, _filters)
               {
                  Canceled = true,
                  Result = _resourceExecutingContext.Result
               };

               // a filter could complete a Task without setting a result
               if (_resourceExecutingContext.Result != null)
               {
                  goto case State.ResourceShortCircuit;
               }
            }

            goto case State.ResourceEnd;
         
         case State.ResourceSyncBegin:
            var filter = (IResourceFilter)state;
            var resourceExecutingContext = _resourceExecutingContext;

            filter.OnResourceExecuting(resourceExecutingContext);

            if (resourceExecutingContext.Result != null)
            {
               _resourceExecutedContext = new ResourceExecutedContextSealed(resourceExecutingContext, _filters)
               {
                  Canceled = true,
                  Result = _resourceExecutingContext.Result
               }

               goto case State.ResourceShortCircuit;
            }

            var task = InvokeNextResourceFilter();
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResourceSyncEnd;
               return task;
            }

            goto case State.ResourceSyncEnd;

         case State.ResourceSyncEnd:
            var filter = (IResourceFilter)state;
            var resourceExecutedContext = _resourceExecutedContext;

            filter.OnResourceExecuted(resourceExecutedContext);

            goto case State.ResourceEnd;

         case State.ResourceShortCircuit:
            _result = _resourceExecutingContext.Result;
            var task = InvokeAlwaysRunResultFilters();
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResourceEnd;
               return task;
            }
            goto case State.ResourceEnd;

         case State.ResourceInside:
            goto case State.ExceptionBegin;

         case State.ExceptionBegin:
            _cursor.Reset();
            goto case State.ExceptionNext;

         case State.ExceptionNext:
            var current = _cursor.GetNextFilter<IExceptionFilter, IAsyncExceptionFilter>();
            if (current.FilterAsync != null)
            {
               state = current.FilterAsync;
               goto case State.ExceptionAsyncBegin;
            }
            else if (current.Filter != null)
            {
               state = current.Filter;
               goto case State.ExceptionSyncBegin;
            }
            else
            {
               // there are no exception filters - so jump right to the action
               goto case State.ActionBegin;
            }

         case State.ExceptionAsyncBegin:
            var task = InvokeNextExceptionFilterAsync();
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ExceptionAsyncResume;
               return task;
            }

            goto case State.ExceptionAsyncResume;

         case State.ExceptionAsyncResume:
             var filter = (IAsyncExceptionFilter)state;
             var exceptionContext = _exceptionContext;

             // when we get here we're 'unwinding' the stack of exception filters. If we have an unhandled exception, we'll call the filter, otherwise there's nothing to do
             if (exceptionContext?.Exception != null && !exceptionContext.ExceptionHandled)
             {
                var task = filter.OnExceptionAsync(exceptionContext);
                if (!task.IsCompletedSuccessfully)
                {
                   next = State.ExceptionAsyncEnd;
                   return task;
                }

                goto case State.ExceptionAsyncEnd;
             }

             goto case State.ExceptionEnd;

         case State.ExceptionAsyncEnd:
            var filter = (IAsyncExceptionFilter)state;
            var exceptionContext = _exceptionContext;

            if (exceptionContext.Exception == null || exceptionContext.ExceptionHandled)
            {
               // don't need to do anything to short circuit, if there's another exception filter on the stack it will check the same set of conditions and then just skip itself
            }

            goto case State.ExceptionEnd;

         case State.ExceptionSyncBegin:
            var task = InvokeNextExceptionFilterAsync();  // InvokeNextExceptionFilterAsync method has a try catch block to catch Exception and assign it to _exceptionContext
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ExceptionSyncEnd;
               return task;
            }

            goto case State.ExceptionSyncEnd;

         case State.ExceptionSyncEnd:
            var filter = (IExceptionFilter)state;
            var exceptionContext = _exceptionContext;

            // when we get here we're 'unwinding' the stack of exception filters. If we have an unhandled exception, we'll call the filter. Otherwise there's nothing to do
            if (exceptionContext?.Exception != null && !exceptionContext.ExceptionHandled)
            {
               filter.OnException(exceptionContext);

               if (exceptionContext.Exception == null || exceptionContext.ExceptionHandled)
               {
                  // don't need to do anything to short circuit, if there's another exception filter on the stack it will check the same set of conditions and then just skip 
               }
            }

            goto case State.ExceptionEnd;

         case State.ExceptionInside:
            goto case State.ActionBegin;

         case State.ExceptionHandled:
            // we arrive in this state when an exception happened, but was handled by exception filters either by setting ExceptionHandled, or nulling out the Exception or 
            // setting a result on the ExceptionContext. We need to execute the result (if any) and then exit gracefully which unwinding Resource filters.
            if (_exceptionContext.Result == null)
            {
               _exceptionContext.Result = new EmptyResult();
            }

            _result = _exceptionContext.Result;

            var task = InvokeAlwaysRunResultFilters();
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResourceInsideEnd;
               return task;
            }

            goto case State.ResourceInsideEnd;

         case State.ExceptionEnd:
            var exceptionContext = _exceptionContext;

            if (scope == Scope.Exception)   // emmm, must be for InvokeNextExceptionFilterAsync
            {
               isCompleted = true;
               return Task.CompletedTask;
            }

            if (exceptionContext != null)
            {
               if (exceptionContext.Result != null || exceptionContext.Exception == null || exceptionContext.ExceptionHandled)
               {
                  goto case State.ExceptionHandled;
               }

               Rethrow(exceptionContext);
               Debug.Fail("unreachable");
            }

            var task = InvokeResultFilters();
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResourceInsideEnd;
               return task;
            }
            goto case State.ResourceInsideEnd;
         
         case State.ActionBegin:
            // calling ControllerActionInvoker's override InvokeInnerFilterAsync()
            var task = InvokeInnerFilterAsync();  // <------------------------------------a1.a
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ActionEnd;
               return task;
            }

            goto case State.ActionEnd;

         case State.ActionEnd:
            if (scope == Scope.Exception)
            {
               // if we're inside an exception filter, let's allow those filters to 'unwind' before the result
               isCompleted = true;
               return Task.CompletedTask;
            }

            var task = InvokeResultFilters();
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResourceInsideEnd;
               return task;
            }

            goto case State.ResourceInsideEnd;

         case State.ResourceInsideEnd:
            if (scope == Scope.Resource)
            {
               _resourceExecutedContext = new ResourceExecutedContextSealed(_actionContext, _filters)
               {
                  Result = _result,
               };

               goto case State.ResourceEnd;
            }

            goto case State.InvokeEnd;
         
         case State.ResourceEnd:
            if (scope == Scope.Resource)
            {
               isCompleted = true;
               return Task.CompletedTask;
            }

            Rethrow(_resourceExecutedContext!);
            goto case State.InvokeEnd;

         case State.InvokeEnd:
            isCompleted = true;
            return Task.CompletedTask;

         default:
            throw new InvalidOperationException();
      }
   }

   private Task ResultNext<TFilter, TFilterAsync>(ref State next, ref Scope scope, ref object? state, ref bool isCompleted)
   {
      var resultFilterKind = typeof(TFilter) == typeof(IAlwaysRunResultFilter) ? FilterTypeConstants.AlwaysRunResultFilter : FilterTypeConstants.ResultFilter;

      switch (next)
      {
         case State.ResultBegin:
            _cursor.Reset();
            goto case State.ResultNext;

         case State.ResultNext:
            var current = _cursor.GetNextFilter<TFilter, TFilterAsync>();
            if (current.FilterAsync != null)
            {
               if (_resultExecutingContext == null)
               {
                  _resultExecutingContext = new ResultExecutingContextSealed(_actionContext, _filters, _result!, _instance!);
               }

               state = current.FilterAsync;
               goto case State.ResultAsyncBegin;
            }
            else if (current.Filter != null)
            {
               if (_resultExecutingContext == null)
               {
                  _resultExecutingContext = new ResultExecutingContextSealed(_actionContext, _filters, _result!, _instance!);
               }

               state = current.Filter;
               goto case State.ResultSyncBegin;
            }
            else
            {
               goto case State.ResultInside;
            }
         
         /* 
         case State.ResultAsyncBegin: 
            ...
         case State.ResultAsyncEnd:
            ...
         */

         case State.ResultSyncBegin:
            var filter = (TFilter)state;
            var resultExecutingContext = _resultExecutingContext;

            filter.OnResultExecuting(resultExecutingContext);

            if (_resultExecutingContext.Cancel)
            {
               _resultExecutedContext = new ResultExecutedContextSealed(resultExecutingContext, _filters, resultExecutingContext.Result,_instance!) { Canceled = true};

               goto case State.ResultEnd;
            }

            var task = InvokeNextResultFilterAsync<TFilter, TFilterAsync>();
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResultSyncEnd;
               return task;
            }

            goto case State.ResultSyncEnd;
         
         case State.ResultSyncEnd:
            var filter = (TFilter)state;
            var resultExecutedContext = _resultExecutedContext;

            filter.OnResultExecuted(resultExecutedContext);

            goto case State.ResultEnd;

         case State.ResultInside:
            // if we executed result filters then we need to grab the result from there
            if (_resultExecutingContext != null)
            {
               _result = _resultExecutingContext.Result;
            }

            if (_result == null)
            {
               // the empty result is always flowed back as the 'executed' result if we don't have one
               _result = new EmptyResult();
            }

            var task = InvokeResultAsync(_result);
            if (!task.IsCompletedSuccessfully)
            {
               next = State.ResultEnd;
               return task;
            }

            goto case State.ResultEnd;
         
         case State.ResultEnd:
            var result = _result;
            isCompleted = true;

            if (scope == Scope.Result)
            {
               if (_resultExecutedContext == null)
               {
                  _resultExecutedContext = new ResultExecutedContextSealed(_actionContext, _filters, result!, _instance!);
               }

               return Task.CompletedTask;
            }

            Rethrow(_resultExecutedContext!);
            return Task.CompletedTask;

         default:
            throw new InvalidOperationException();  // unreachable
      }
   }

   protected virtual Task InvokeResultAsync(IActionResult result)
   {
      return result.ExecuteResultAsync(_actionContext);    // <-------------------!important, that is how IActionResult get executed; refer to IActionResult flow
   }                                         

   private Task InvokeNextExceptionFilterAsync()
   {
      try
      {
         var next = State.ExceptionNext;
         var state = (object?)null;
         var scope = Scope.Exception;
         var isCompleted = false;

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
         try
         {
            await lastTask;

            while (!isCompleted)
            {
               await invoker.Next(ref next, ref scope, ref state, ref isCompleted);
            }
         }
         catch (Exception exception)
         {
            invoker._exceptionContext = new ExceptionContextSealed(invoker._actionContext, invoker._filters)
            {
               ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
            };
         }
      }
   }

   private Task InvokeAlwaysRunResultFilters()
   {
      try
      {
         var next = State.ResultBegin;
         var scope = Scope.Invoker;
         var state = (object?)null;
         var isCompleted = false;

         while (!isCompleted)
         {
            var lastTask = ResultNext<IAlwaysRunResultFilter, IAsyncAlwaysRunResultFilter>(ref next, ref scope, ref state, ref isCompleted);
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
            await invoker.ResultNext<IAlwaysRunResultFilter, IAsyncAlwaysRunResultFilter>(ref next, ref scope, ref state, ref isCompleted);
         }
      }
   }
}
/*
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
*/
//---------------------------------------------Ʌ

//--------------------------------------------V
internal partial class ControllerActionInvoker : ResourceInvoker, IActionInvoker    // <-----------------------f2.3b
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

   private Task Next(ref State next, ref Scope scope, ref object? state, ref bool isCompleted)    // <---------------------a2.b_
   {
      switch (next)
      {
         case State.ActionBegin:    // <------------------------------ ControllerActionInvoker only starts with ActionBegin while ResourceInvoker starts with AuthorizationBegin
            var controllerContext = _controllerContext;
            _cursor.Reset();

            _instance = _cacheEntry.ControllerFactory(controllerContext);   // <-------------------------------c1<, a3, create an instance of Controller
                                                                            // !important, now you see when an instance of controller is created

            _arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            var task = BindArgumentsAsync();   // <--------------------------a4, a5.b

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
            var task = InvokeActionMethodAsync();   // <-----------------------------important!, invoke action method
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

   protected override Task InvokeInnerFilterAsync()  // override parent ResourceInvoker's abstract InvokeInnerFilterAsync   <------------------a1.b_
   {
      try 
      {
         var next = State.ActionBegin;
         var scope = Scope.Invoker;
         var state = (object?)null;
         var isCompleted = false;

         while (!isCompleted)
         {
            var lastTask = Next(ref next, ref scope, ref state, ref isCompleted);   // <---------------------a2.a
            if (!lastTask.IsCompletedSuccessfully)
            {
               return Awaited(this, lastTask, next, scope, state, isCompleted);
            }
         }

         return Task.CompletedTask;
      }
      catch (Exception ex)
      {
         // wrap non task-wrapped exceptions in a Task, as this isn't done automatically since the method is not async
         return Task.FromException(ex);
      }

      static async Task Awaited(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, object? state, bool isCompleted)
      {
         await lastTask;

         while (!isCompleted)
         {
            await invoker.Next(ref next, ref scope, ref state, ref isCompleted);
         }
      }
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

   private Task BindArgumentsAsync()
   {
      var actionDescriptor = _controllerContext.ActionDescriptor;
      if (actionDescriptor.BoundProperties.Count == 0 && actionDescriptor.Parameters.Count == 0)
      {
         return Task.CompletedTask;
      }

      return _cacheEntry.ControllerBinderDelegate(_controllerContext, _instance!, _arguments!);   // <---------------------a5.a, starting b1
   }

   private static object?[]? PrepareArguments(IDictionary<string, object?>? actionParameters, ObjectMethodExecutor actionMethodExecutor)
   {
      var declaredParameterInfos = actionMethodExecutor.MethodParameters;
      var count = declaredParameterInfos.Length;
      if (count == 0)
      {
         return null;
      }

      var arguments = new object?[count];
      for (var index = 0; index < count; index++)
      {
         var parameterInfo = declaredParameterInfos[index];

         if (!actionParameters.TryGetValue(parameterInfo.Name!, out var value) || value is null)
         {
            value = actionMethodExecutor.GetDefaultValueForParameter(index);
         }

         arguments[index] = value;
      }

      return arguments;
   }
}

/*
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
*/
//---------------------------------------------Ʌ
```

```C#
//------------------------------------------V
internal abstract class ActionMethodExecutor
{
   private static readonly ActionMethodExecutor[] Executors = new ActionMethodExecutor[]
   {
      // executors for sync methods
      new VoidResultExecutor(),
      new SyncActionResultExecutor(),
      new SyncObjectResultExecutor(),
 
      // executors for async methods
      new TaskResultExecutor(),
      new AwaitableResultExecutor(),
      new TaskOfIActionResultExecutor(),
      new TaskOfActionResultExecutor(),
      new AwaitableObjectResultExecutor(),
   }

   public static EmptyResult EmptyResultInstance { get; } = new();

   public abstract ValueTask<IActionResult> Execute(
      ActionContext actionContext,
      IActionResultTypeMapper mapper,
      ObjectMethodExecutor executor,
      object controller,
      object?[]? arguments);

   protected abstract bool CanExecute(ObjectMethodExecutor executor);

   public abstract ValueTask<object?> Execute(ControllerEndpointFilterInvocationContext invocationContext);

   public static ActionMethodExecutor GetExecutor(ObjectMethodExecutor executor)
   {
      for (var i = 0; i < Executors.Length; i++)
      {
         if (Executors[i].CanExecute(executor))
         {
            return Executors[i];
         }
      }
 
      Debug.Fail("Should not get here");
      throw new Exception();
   }

   public static ActionMethodExecutor GetFilterExecutor(ControllerActionDescriptor actionDescriptor) => new FilterActionMethodExecutor(actionDescriptor);

   //-----------------VV
   private sealed class FilterActionMethodExecutor : ActionMethodExecutor
   {
      private readonly ControllerActionDescriptor _controllerActionDescriptor;

      public FilterActionMethodExecutor(ControllerActionDescriptor controllerActionDescriptor)
      {
         _controllerActionDescriptor = controllerActionDescriptor;
      }

      public override async ValueTask<IActionResult> Execute(
         ActionContext actionContext,
         IActionResultTypeMapper mapper,
         ObjectMethodExecutor executor,
         object controller,
         object?[]? arguments)
      {
         var context = new ControllerEndpointFilterInvocationContext(_controllerActionDescriptor, actionContext, executor, mapper, controller, arguments);
         var result = await _controllerActionDescriptor.FilterDelegate!(context);
         return ConvertToActionResult(mapper, result, executor.IsMethodAsync ? executor.AsyncResultType! : executor.MethodReturnType);
      }

      public override ValueTask<object?> Execute(ControllerEndpointFilterInvocationContext invocationContext)
      {
         // this is never called
         throw new NotSupportedException();
      }
 
      protected override bool CanExecute(ObjectMethodExecutor executor)
      {
         // this is never called
         throw new NotSupportedException();
      }
   }
   //-----------------ɅɅ
   
   //-----------------VV
   private sealed class SyncActionResultExecutor : ActionMethodExecutor
   {
      public override ValueTask<IActionResult> Execute(
         ActionContext actionContext,
         IActionResultTypeMapper mapper,
         ObjectMethodExecutor executor,
         object controller,
         object?[]? arguments)
      {
         var actionResult = (IActionResult)executor.Execute(controller, arguments)!;
         EnsureActionResultNotNull(executor, actionResult);
 
         return new(actionResult);
      }

      public override ValueTask<object?> Execute(ControllerEndpointFilterInvocationContext invocationContext)
      {
         var executor = invocationContext.Executor;
         var controller = invocationContext.Controller;
         var arguments = (object[])invocationContext.Arguments;
 
         var actionResult = (IActionResult)executor.Execute(controller, arguments)!;
         EnsureActionResultNotNull(executor, actionResult);
 
         return new(actionResult);
      }

      protected override bool CanExecute(ObjectMethodExecutor executor) => !executor.IsMethodAsync && typeof(IActionResult).IsAssignableFrom(executor.MethodReturnType);
   }
   // ...
   //-----------------ɅɅ
}
//------------------------------------------Ʌ
```



## Model Binding

```C#
//----------------------------------------------V
public abstract class ModelBinderProviderContext
{
   public abstract IModelBinder CreateBinder(ModelMetadata metadata);

   public virtual IModelBinder CreateBinder(ModelMetadata metadata, BindingInfo bindingInfo)
   {
      throw new NotSupportedException();
   }

   public abstract BindingInfo BindingInfo { get; }

   public abstract ModelMetadata Metadata { get; }

   public abstract IModelMetadataProvider MetadataProvider { get; }

   public virtual IServiceProvider Services { get; } = default!;
}
//----------------------------------------------Ʌ

//----------------------------------------------------V
private sealed class DefaultModelBinderProviderContext : ModelBinderProviderContext
{
   private readonly ModelBinderFactory _factory;

   public DefaultModelBinderProviderContext(ModelBinderFactory factory, ModelBinderFactoryContext factoryContext)
   {
      _factory = factory;
      Metadata = factoryContext.Metadata;
      BindingInfo bindingInfo;
      if (factoryContext.BindingInfo != null)
      {
         bindingInfo = new BindingInfo(factoryContext.BindingInfo);
      }
      else
      {
         bindingInfo = new BindingInfo();
      }
 
      bindingInfo.TryApplyBindingInfo(Metadata);
      BindingInfo = bindingInfo;
 
      MetadataProvider = _factory._metadataProvider;
      Visited = new Dictionary<Key, IModelBinder?>();
   }

   private DefaultModelBinderProviderContext(DefaultModelBinderProviderContext parent, ModelMetadata metadata, BindingInfo bindingInfo)
   {
      Metadata = metadata;
      _factory = parent._factory;
      MetadataProvider = parent.MetadataProvider;
      Visited = parent.Visited;
      BindingInfo = bindingInfo;
   }

   public override BindingInfo BindingInfo { get; }

   public override ModelMetadata Metadata { get; }

   public override IModelMetadataProvider MetadataProvider { get; }

   public Dictionary<Key, IModelBinder?> Visited { get; }

   public override IServiceProvider Services => _factory._serviceProvider;

   public override IModelBinder CreateBinder(ModelMetadata metadata)
   {
      var bindingInfo = new BindingInfo();
      bindingInfo.TryApplyBindingInfo(metadata);

      return CreateBinder(metadata, bindingInfo);
   }

   public override IModelBinder CreateBinder(ModelMetadata metadata, BindingInfo bindingInfo)
   {
      var token = metadata;

      var nestedContext = new DefaultModelBinderProviderContext(this, metadata, bindingInfo);
      return _factory.CreateBinderCoreCached(nestedContext, token);
   }
}
//----------------------------------------------------Ʌ

//----------------------------------------------------V
internal static class ControllerBinderDelegateProvider       
{
   public static ControllerBinderDelegate? CreateBinderDelegate(   // <---------------------------------b1.b
      ParameterBinder parameterBinder,
      IModelBinderFactory modelBinderFactory,
      IModelMetadataProvider modelMetadataProvider,
      ControllerActionDescriptor actionDescriptor,
      MvcOptions mvcOptions)
   {
      var parameterBindingInfo = GetParameterBindingInfo(modelBinderFactory, modelMetadataProvider, actionDescriptor);
      var propertyBindingInfo = GetPropertyBindingInfo(modelBinderFactory, modelMetadataProvider, actionDescriptor);
 
      if (parameterBindingInfo == null && propertyBindingInfo == null)
      {
         return null;
      }

      var parameters = actionDescriptor.Parameters switch
      {
         List<ParameterDescriptor> list => list.ToArray(),
         _ => actionDescriptor.Parameters.ToArray()
      };
 
      var properties = actionDescriptor.BoundProperties switch
      {
         List<ParameterDescriptor> list => list.ToArray(),
         _ => actionDescriptor.BoundProperties.ToArray()
      };
 
      return Bind;

      async Task Bind(ControllerContext controllerContext, object controller, Dictionary<string, object?> arguments)
      {
         var (success, valueProvider) = await CompositeValueProvider.TryCreateAsync(controllerContext, controllerContext.ValueProviderFactories);
         if (!success)
         {
            return;
         }

         for (var i = 0; i < parameters.Length; i++)
         {
            var parameter = parameters[i];
            var bindingInfo = parameterBindingInfo![i];
            var modelMetadata = bindingInfo.ModelMetadata;
 
            if (!modelMetadata.IsBindingAllowed)
            {
               continue;
            }
 
            var result = await parameterBinder.BindModelAsync(
               controllerContext,
               bindingInfo.ModelBinder,
               valueProvider,
               parameter,
               modelMetadata,
               value: null,
               container: null); // Parameters do not have containers.
 
            if (result.IsModelSet)
            {
               arguments[parameter.Name] = result.Model;
            }
         }

         for (var i = 0; i < properties.Length; i++)
         {
            var property = properties[i];
            var bindingInfo = propertyBindingInfo![i];
            var modelMetadata = bindingInfo.ModelMetadata;
 
            if (!modelMetadata.IsBindingAllowed)
            {
               continue;
            }
 
            var result = await parameterBinder.BindModelAsync(
               controllerContext,
               bindingInfo.ModelBinder,
               valueProvider,
               property,
               modelMetadata,
               value: null,
               container: controller);
 
            if (result.IsModelSet)
            {
               PropertyValueSetter.SetValue(bindingInfo.ModelMetadata, controller, result.Model);
            }
         }
      }
   }
   
   private static BinderItem[]? GetParameterBindingInfo(
      IModelBinderFactory modelBinderFactory,
      IModelMetadataProvider modelMetadataProvider,
      ControllerActionDescriptor actionDescriptor)
   {
      var parameters = actionDescriptor.Parameters;
      if (parameters.Count == 0)
      {
         return null;
      }

      var parameterBindingInfo = new BinderItem[parameters.Count];
      for (var i = 0; i < parameters.Count; i++)
      {
         var parameter = parameters[i];

         ModelMetadata metadata;
         if (modelMetadataProvider is ModelMetadataProvider modelMetadataProviderBase && parameter is ControllerParameterDescriptor controllerParameterDescriptor)
         {
            // the default model metadata provider derives from ModelMetadataProvider and can therefore supply information about attributes applied to parameters.
            metadata = modelMetadataProviderBase.GetMetadataForParameter(controllerParameterDescriptor.ParameterInfo);
         }
         else
         {
            // for backward compatibility, if there's a custom model metadata provider that only implements the older IModelMetadataProvider interface, access the more
            // limited metadata information it supplies. In this scenario, validation attributes are not supported on parameters.
            metadata = modelMetadataProvider.GetMetadataForType(parameter.ParameterType);
         }

         var binder = modelBinderFactory.CreateBinder(new ModelBinderFactoryContext 
         {
            BindingInfo = parameter.BindingInfo,
            Metadata = metadata,
            CacheToken = parameter,
         });

         parameterBindingInfo[i] = new BinderItem(binder, metadata);
      }

      return parameterBindingInfo;
   }

   private static BinderItem[]? GetPropertyBindingInfo(
      IModelBinderFactory modelBinderFactory,
      IModelMetadataProvider modelMetadataProvider,
      ControllerActionDescriptor actionDescriptor)
   { ... }

   private readonly struct BinderItem
   {
      public BinderItem(IModelBinder modelBinder, ModelMetadata modelMetadata)
      {
         ModelBinder = modelBinder;
         ModelMetadata = modelMetadata;
      }

      public IModelBinder ModelBinder { get; }

      public ModelMetadata ModelMetadata { get; }
   }
}
//----------------------------------------------------Ʌ

//---------------------------------------V
public readonly struct ModelBindingResult : IEquatable<ModelBindingResult>
{
   public static ModelBindingResult Failed()
   {
      return new ModelBindingResult(model: null, isModelSet: false);
   }

   public static ModelBindingResult Success(object? model)
   {
      return new ModelBindingResult(model, isModelSet: true);
   }

   private ModelBindingResult(object? model, bool isModelSet)
   {
      Model = model;
      IsModelSet = isModelSet;
   }

   public object? Model { get; }

   public bool IsModelSet { get; }

   public bool Equals(ModelBindingResult other)
   {
      return IsModelSet == other.IsModelSet && object.Equals(Model, other.Model);
   }
}
//---------------------------------------Ʌ

//---------------------------------V
public abstract class ModelMetadata : IEquatable<ModelMetadata?>, IModelMetadataProvider
{
   public static readonly int DefaultOrder = 10000;

   private static readonly ParameterBindingMethodCache ParameterBindingMethodCache = new(throwOnInvalidMethod: false);

   private int? _hashCode;
   private IReadOnlyList<ModelMetadata>? _boundProperties;
   private IReadOnlyDictionary<ModelMetadata, ModelMetadata>? _parameterMapping;
   private IReadOnlyDictionary<ModelMetadata, ModelMetadata>? _boundConstructorPropertyMapping;
   private Exception? _recordTypeValidatorsOnPropertiesError;
   private bool _recordTypeConstructorDetailsCalculated;

   protected ModelMetadata(ModelMetadataIdentity identity)
   {
      Identity = identity;
      InitializeTypeInformation();
   }

   public Type? ContainerType => Identity.ContainerType;

   public virtual ModelMetadata? ContainerMetadata
   {
      get {
         throw new NotImplementedException();
      }
   }

   public ModelMetadataKind MetadataKind => Identity.MetadataKind;

   public Type ModelType => Identity.ModelType;

   public string? Name => Identity.Name;

   public string? ParameterName => MetadataKind == ModelMetadataKind.Parameter ? Identity.Name : null;

   public string? PropertyName => MetadataKind == ModelMetadataKind.Property ? Identity.Name : null;

   protected internal ModelMetadataIdentity Identity { get; }

   public abstract IReadOnlyDictionary<object, object> AdditionalValues { get; }

   public abstract ModelPropertyCollection Properties { get; }

   internal IReadOnlyList<ModelMetadata> BoundProperties
   {
      get {
         if (BoundConstructor is null)
         {
            return Properties;
         }

         if (_boundProperties is null)
         {
            var boundParameters = BoundConstructor.BoundConstructorParameters!;
            var boundProperties = new List<ModelMetadata>();
 
            foreach (var metadata in Properties)
            {
               if (!boundParameters.Any(p => string.Equals(p.ParameterName, metadata.PropertyName, StringComparison.Ordinal) && p.ModelType == metadata.ModelType))
               {
                  boundProperties.Add(metadata);
               }
            }
 
            _boundProperties = boundProperties;
         }
 
         return _boundProperties;
      }
   }

   internal IReadOnlyDictionary<ModelMetadata, ModelMetadata> BoundConstructorParameterMapping
   {
      get {
         Debug.Assert(BoundConstructor != null, "This API can be only called for types with bound constructors.");
         CalculateRecordTypeConstructorDetails();
 
         return _parameterMapping;
      }
   }

   internal IReadOnlyDictionary<ModelMetadata, ModelMetadata> BoundConstructorPropertyMapping
   {
      get {
         Debug.Assert(BoundConstructor != null, "This API can be only called for types with bound constructors.");
         CalculateRecordTypeConstructorDetails();
 
         return _boundConstructorPropertyMapping;
      }
   }

   public virtual ModelMetadata? BoundConstructor { get; }

   public virtual IReadOnlyList<ModelMetadata>? BoundConstructorParameters { get; }

   public abstract string? BinderModelName { get; }

   public abstract Type? BinderType { get; }

   public abstract BindingSource? BindingSource { get; }

   public abstract bool ConvertEmptyStringToNull { get; }

   public abstract string? DataTypeName { get; }

   public abstract string? DisplayName { get; }

   public abstract bool IsRequired { get; }

   // ...
}
//---------------------------------Ʌ

public interface IModelMetadataProvider
{
   ModelMetadata GetMetadataForType(Type modelType);

   IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType);
}

public abstract class ModelMetadataProvider : IModelMetadataProvider
{
   public abstract IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType);
   public abstract ModelMetadata GetMetadataForType(Type modelType);
   public abstract ModelMetadata GetMetadataForParameter(ParameterInfo parameter);
   public virtual ModelMetadata GetMetadataForParameter(ParameterInfo parameter, Type modelType) => throw new NotSupportedException();
   public virtual ModelMetadata GetMetadataForProperty(PropertyInfo propertyInfo, Type modelType) => throw new NotSupportedException();
   public virtual ModelMetadata GetMetadataForConstructor(ConstructorInfo constructor, Type modelType) => throw new NotSupportedException();
}

//---------------------------------------V
public class DefaultModelMetadataProvider : ModelMetadataProvider
{
   private readonly ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataCacheEntry> _modelMetadataCache = new();
   private readonly Func<ModelMetadataIdentity, ModelMetadataCacheEntry> _cacheEntryFactory;
   private readonly ModelMetadataCacheEntry _metadataCacheEntryForObjectType;

   public DefaultModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider) : this(detailsProvider, new DefaultModelBindingMessageProvider()) { }

   public DefaultModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider, IOptions<MvcOptions> optionsAccessor) 
      : this(detailsProvider, GetMessageProvider(optionsAccessor)) { }

   private DefaultModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider, DefaultModelBindingMessageProvider modelBindingMessageProvider)
   {
      DetailsProvider = detailsProvider;
      ModelBindingMessageProvider = modelBindingMessageProvider;
 
      _cacheEntryFactory = CreateCacheEntry;
      _metadataCacheEntryForObjectType = GetMetadataCacheEntryForObjectType();
   }

   protected ICompositeMetadataDetailsProvider DetailsProvider { get; }

   protected DefaultModelBindingMessageProvider ModelBindingMessageProvider { get; }

   internal void ClearCache() => _modelMetadataCache.Clear();

   public override IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType)
   {
      var cacheEntry = GetCacheEntry(modelType);

      if (cacheEntry.Details.Properties == null)
      {
         var key = ModelMetadataIdentity.ForType(modelType);
         var propertyDetails = CreatePropertyDetails(key);
 
         var properties = new ModelMetadata[propertyDetails.Length];
         for (var i = 0; i < properties.Length; i++)
         {
            propertyDetails[i].ContainerMetadata = cacheEntry.Metadata;
            properties[i] = CreateModelMetadata(propertyDetails[i]);
         }
 
         cacheEntry.Details.Properties = properties;
      }

      return cacheEntry.Details.Properties;
   }

   public override ModelMetadata GetMetadataForParameter(ParameterInfo parameter) => GetMetadataForParameter(parameter, parameter.ParameterType);

   public override ModelMetadata GetMetadataForParameter(ParameterInfo parameter, Type modelType)
   {
      var cacheEntry = GetCacheEntry(parameter, modelType);
      return cacheEntry.Metadata;
   }

   public override ModelMetadata GetMetadataForType(Type modelType)
   {
      var cacheEntry = GetCacheEntry(modelType);
      return cacheEntry.Metadata;
   }

   public override ModelMetadata GetMetadataForProperty(PropertyInfo propertyInfo, Type modelType)
   {
      var cacheEntry = GetCacheEntry(propertyInfo, modelType);
      return cacheEntry.Metadata;
   }

   public override ModelMetadata GetMetadataForConstructor(ConstructorInfo constructorInfo, Type modelType)
   {
      var cacheEntry = GetCacheEntry(constructorInfo, modelType);
      return cacheEntry.Metadata;
   }

   private static DefaultModelBindingMessageProvider GetMessageProvider(IOptions<MvcOptions> optionsAccessor)
   {
      return optionsAccessor.Value.ModelBindingMessageProvider;
   }

   private ModelMetadataCacheEntry GetCacheEntry(Type modelType)
   {
      ModelMetadataCacheEntry cacheEntry;

      if (modelType == typeof(object))
      {
         cacheEntry = _metadataCacheEntryForObjectType;
      }
      else
      {
         var key = ModelMetadataIdentity.ForType(modelType);
 
         cacheEntry = _modelMetadataCache.GetOrAdd(key, _cacheEntryFactory);
      }

      return cacheEntry;
   }

   private ModelMetadataCacheEntry GetCacheEntry(ParameterInfo parameter, Type modelType)
   {
      return _modelMetadataCache.GetOrAdd(ModelMetadataIdentity.ForParameter(parameter, modelType), _cacheEntryFactory);
   }

   // ...

   private readonly struct ModelMetadataCacheEntry
   {
      public ModelMetadataCacheEntry(ModelMetadata metadata, DefaultMetadataDetails details)
      {
         Metadata = metadata;
         Details = details;
      }
 
      public ModelMetadata Metadata { get; }
 
      public DefaultMetadataDetails Details { get; }
   }
}
//---------------------------------------Ʌ


public interface IModelBinderFactory
{
   IModelBinder CreateBinder(ModelBinderFactoryContext context);
}

//-------------------------------------V
public partial class ModelBinderFactory : IModelBinderFactory
{
   private readonly IModelMetadataProvider _metadataProvider;
   private readonly IModelBinderProvider[] _providers;
   private readonly ConcurrentDictionary<Key, IModelBinder> _cache;
   private readonly IServiceProvider _serviceProvider;

   public ModelBinderFactory(IModelMetadataProvider metadataProvider, IOptions<MvcOptions> options, IServiceProvider serviceProvider)
   {
      _metadataProvider = metadataProvider;
      _providers = options.Value.ModelBinderProviders.ToArray();
      _serviceProvider = serviceProvider;
      _cache = new ConcurrentDictionary<Key, IModelBinder>();
   }

   public IModelBinder CreateBinder(ModelBinderFactoryContext context)
   {
      if (TryGetCachedBinder(context.Metadata, context.CacheToken, out var binder))
      {
         return binder;
      }

      var providerContext = new DefaultModelBinderProviderContext(this, context);
      binder = CreateBinderCoreUncached(providerContext, context.CacheToken);

      AddToCache(context.Metadata, context.CacheToken, binder);

      return binder;
   }

   private IModelBinder CreateBinderCoreCached(DefaultModelBinderProviderContext providerContext, object? token)
   {
      if (TryGetCachedBinder(providerContext.Metadata, token, out var binder))
      {
         return binder;
      }

      // we're definitely creating a binder for an non-root node here, so it's OK for binder creation to fail
      binder = CreateBinderCoreUncached(providerContext, token) ?? NoOpBinder.Instance;

      if (!(binder is PlaceholderBinder))
      {
         AddToCache(providerContext.Metadata, token, binder);
      }

      return binder;
   }

   private IModelBinder? CreateBinderCoreUncached(DefaultModelBinderProviderContext providerContext, object? token)
   {
      if (!providerContext.Metadata.IsBindingAllowed)
      {
         return NoOpBinder.Instance;
      }

      var key = new Key(providerContext.Metadata, token);

      var visited = providerContext.Visited;

      if (visited.TryGetValue(key, out var binder))
      {
         if (binder != null)
         {
            return binder;
         }
 
         // if we're currently recursively building a binder for this type, just return a PlaceholderBinder.
         // we'll fix it later to point to the 'real' binder when the stack unwinds.
         binder = new PlaceholderBinder();
         visited[key] = binder;
         return binder;
      }

      // OK this isn't a recursive case (yet) so add an entry and then ask the providers to create the binder
      visited.Add(key, null);

      IModelBinder? result = null;

      for (var i = 0; i < _providers.Length; i++)
      {
         var provider = _providers[i];
         result = provider.GetBinder(providerContext);
         if (result != null)
         {
            break;
         }
      }

      // if the PlaceholderBinder was created, then it means we recursed. Hook it up to the 'real' binder.
      if (visited[key] is PlaceholderBinder placeholderBinder)
      {
         placeholderBinder.Inner = result ?? NoOpBinder.Instance;
      }

      if (result != null)
      {
         visited[key] = result;
      }

      return result;
   }

   private void AddToCache(ModelMetadata metadata, object? cacheToken, IModelBinder binder)
   {
      if (cacheToken == null)
         return;
      
      _cache.TryAdd(new Key(metadata, cacheToken), binder);
   }

   private bool TryGetCachedBinder(ModelMetadata metadata, object? cacheToken, [NotNullWhen(true)] out IModelBinder? binder)
   {
      if (cacheToken == null)
      {
         binder = null;
         return false;
      }
 
      return _cache.TryGetValue(new Key(metadata, cacheToken), out binder);
   }
}
//-------------------------------------Ʌ


//-----------------------------------------------------------------------------------------------V
public interface IValueProvider
{
   bool ContainsPrefix(string prefix);
   ValueProviderResult GetValue(string key);
}

public interface IEnumerableValueProvider : IValueProvider
{
   IDictionary<string, string> GetKeysFromPrefix(string prefix);
}

public interface IBindingSourceValueProvider : IValueProvider
{
   IValueProvider? Filter(BindingSource bindingSource);
}

// >
public readonly struct ValueProviderResult : IEquatable<ValueProviderResult>, IEnumerable<string>
{
   private static readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;
   public static ValueProviderResult None = new ValueProviderResult(Array.Empty<string>());

   public ValueProviderResult(StringValues values) : this(values, _invariantCulture) { }

   public CultureInfo Culture { get; }
   public StringValues Values { get; }

   public string? FirstValue 
   {
      get {
         if (Values.Count == 0)
            return null;
            
         return Values[0];
      }
   }

   public int Length => Values.Count;

   public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)Values).GetEnumerator();
} 
// <

// >
public class ValueProviderFactoryContext 
{  
   public ValueProviderFactoryContext(ActionContext context)
   {
      ActionContext = context;
   }

   public ActionContext ActionContext { get; }
   
   public IList<IValueProvider> ValueProviders { get; } = new List<IValueProvider>();
}  
// <

public interface IValueProviderFactory
{
   Task CreateValueProviderAsync(ValueProviderFactoryContext context);
}
//-----------------------------------------------------------------------------------------------Ʌ

public class QueryStringValueProviderFactory : IValueProviderFactory
{
   public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
   {
      var query = context.ActionContext.HttpContext.Request.Query;
      if (query != null && query.Count > 0)
      {
         var valueProvider = new QueryStringValueProvider(BindingSource.Query, query, CultureInfo.InvariantCulture);
         context.ValueProviders.Add(valueProvider);
      }

      return Task.CompletedTask;
   }
}

public class FormValueProviderFactory 
{
   public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
   {
      var request = context.ActionContext.HttpContext.Request;
      if (request.HasFormContentType)
         return AddValueProviderAsync(context);   // allocating a Task only when the body is form data

      return Task.CompletedTask;
   }

   private static async Task AddValueProviderAsync(ValueProviderFactoryContext context)
   {
      var request = context.ActionContext.HttpContext.Request;
      IFormCollection form;

      try {
         form = await request.ReadFormAsync();
      }
      catch (InvalidDataException ex) {  
         throw new ValueProviderException(Resources.FormatFailedToReadRequestForm(ex.Message), ex);
      }
      catch (IOException ex) {
         throw new ValueProviderException(Resources.FormatFailedToReadRequestForm(ex.Message), ex);
      }

      var valueProvider = new FormValueProvider(BindingSource.Form, form, CultureInfo.CurrentCulture);
 
      context.ValueProviders.Add(valueProvider);
   }
}

//----------------------------------------------------------------------------V
public abstract class BindingSourceValueProvider : IBindingSourceValueProvider
{
   public BindingSourceValueProvider(BindingSource bindingSource)
   {
      if (bindingSource.IsGreedy) {
         var message = Resources.FormatBindingSource_CannotBeGreedy(bindingSource.DisplayName, nameof(BindingSourceValueProvider));
         throw new ArgumentException(message, nameof(bindingSource));
      }

      if (bindingSource is CompositeBindingSource) {
         var message = Resources.FormatBindingSource_CannotBeComposite(bindingSource.DisplayName, nameof(BindingSourceValueProvider));
         throw new ArgumentException(message, nameof(bindingSource));
      }

      BindingSource = bindingSource;
   }

   protected BindingSource BindingSource { get; }
   public abstract bool ContainsPrefix(string prefix);
   public abstract ValueProviderResult GetValue(string key);

   public virtual IValueProvider? Filter(BindingSource bindingSource)
   {
      if (bindingSource.CanAcceptDataFrom(BindingSource))
      {
         return this;
      }     
      else
      {
         return null;
      }
   }
}
//----------------------------------------------------------------------------Ʌ

//------------------------------------------------------------------------------------------V
public class QueryStringValueProvider : BindingSourceValueProvider, IEnumerableValueProvider
{
   private readonly IQueryCollection _values;
   private PrefixContainer? _prefixContainer;

   public QueryStringValueProvider(BindingSource bindingSource, IQueryCollection values, CultureInfo? culture) : base(bindingSource)
   {
      _values = values;
      Culture = culture;
   }

   public CultureInfo? Culture { get; }

   protected PrefixContainer PrefixContainer
   {
      get {
         if (_prefixContainer == null)
            _prefixContainer = new PrefixContainer(_values.Keys);
 
         return _prefixContainer;
      }
   }

   public override bool ContainsPrefix(string prefix) => PrefixContainer.ContainsPrefix(prefix);

   public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix) => PrefixContainer.GetKeysFromPrefix(prefix);

   public override ValueProviderResult GetValue(string key)
   {
      if (key.Length == 0)
         return ValueProviderResult.None;
      
      var values = _values[key];
      if (values.Count == 0)
         return ValueProviderResult.None;
      else
         return new ValueProviderResult(values, Culture);
   }
}
//------------------------------------------------------------------------------------------Ʌ

//-----------------------------------------------------------------------------------V
public class FormValueProvider : BindingSourceValueProvider, IEnumerableValueProvider
{
   private readonly IFormCollection _values;
   private readonly HashSet<string?>? _invariantValueKeys;
   private PrefixContainer? _prefixContainer;

   public FormValueProvider(BindingSource bindingSource, IFormCollection values, CultureInfo? culture) : base(bindingSource)
   {
      _values = values;
 
      if (_values.TryGetValue(FormValueHelper.CultureInvariantFieldName, out var invariantKeys) && invariantKeys.Count > 0)
      {
         _invariantValueKeys = new(invariantKeys, StringComparer.OrdinalIgnoreCase);
      }
 
      Culture = culture;
   }

   public CultureInfo? Culture { get; }

   protected PrefixContainer PrefixContainer {
      get {
         if (_prefixContainer == null)
            _prefixContainer = new PrefixContainer(_values.Keys);
         return _prefixContainer;
        }
   }

   public override bool ContainsPrefix(string prefix) => PrefixContainer.ContainsPrefix(prefix);
   
   public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix) => PrefixContainer.GetKeysFromPrefix(prefix);
   
   public override ValueProviderResult GetValue(string key)
   {
      if (key.Length == 0)
         return ValueProviderResult.None;
         
      var values = _values[key];
      if (values.Count == 0)
      {
         return ValueProviderResult.None;
      }
      else
      {
         var culture = _invariantValueKeys?.Contains(key) == true ? CultureInfo.InvariantCulture : Culture;
         return new ValueProviderResult(values, culture);
      }
   }
}
//-----------------------------------------------------------------------------------Ʌ

// public class RouteValueProvider : BindingSourceValueProvider { ... }

//---------------------------------V
public class CompositeValueProvider :  Collection<IValueProvider>, IEnumerableValueProvider, IBindingSourceValueProvider, IKeyRewriterValueProvider
{
   public CompositeValueProvider() { }

   public CompositeValueProvider(IList<IValueProvider> valueProviders) : base(valueProviders) { }

   public static async Task<CompositeValueProvider> CreateAsync(ControllerContext controllerContext)
   {
      var factories = controllerContext.ValueProviderFactories;
      return await CreateAsync(controllerContext, factories);
   }

   public static async Task<CompositeValueProvider> CreateAsync(ActionContext actionContext, IList<IValueProviderFactory> factories)
   {
      var valueProviderFactoryContext = new ValueProviderFactoryContext(actionContext);

      for (var i = 0; i < factories.Count; i++)
      {
         var factory = factories[i];
         await factory.CreateValueProviderAsync(valueProviderFactoryContext);
      }

      return new CompositeValueProvider(valueProviderFactoryContext.ValueProviders);
   }

   internal static async ValueTask<(bool success, CompositeValueProvider? valueProvider)> TryCreateAsync(ActionContext actionContext, IList<IValueProviderFactory> factories)
   {
      try
      {
         var valueProvider = await CreateAsync(actionContext, factories);
         return (true, valueProvider);
      }
      catch (ValueProviderException exception)
      {
         actionContext.ModelState.TryAddModelException(key: string.Empty, exception);
         return (false, null);
      }
   }

   public virtual bool ContainsPrefix(string prefix)
   {
      for (var i = 0; i < Count; i++)
      {
         if (this[i].ContainsPrefix(prefix))
         {
            return true;
         }
      }
      return false;
   }

   public virtual ValueProviderResult GetValue(string key)
   {
      var itemCount = Items.Count;
      for (var i = 0; i < itemCount; i++)
      {
         var valueProvider = Items[i];
         var result = valueProvider.GetValue(key);
         if (result != ValueProviderResult.None)
         {
            return result;
         }
      }

      return ValueProviderResult.None;
   }

   public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix)
   {
      foreach (var valueProvider in this)
      {
         if (valueProvider is IEnumerableValueProvider enumeratedProvider)
         {
            var result = enumeratedProvider.GetKeysFromPrefix(prefix);
            if (result != null && result.Count > 0)
            {
               return result;
            }
         }
      }
      return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
   }

   protected override void InsertItem(int index, IValueProvider item)
   {
      base.InsertItem(index, item);
   }

   protected override void SetItem(int index, IValueProvider item)
   {
      base.SetItem(index, item);
   }

   public IValueProvider? Filter(BindingSource bindingSource)
   {
      var shouldFilter = false;
      for (var i = 0; i < Count; i++)
      {
         var valueProvider = Items[i];
         if (valueProvider is IBindingSourceValueProvider)
         {
            shouldFilter = true;
            break;
         }
      }

      if (!shouldFilter)
      {
         // no inner IBindingSourceValueProvider implementations. Result will be empty
         return null;
      }

      var filteredValueProviders = new List<IValueProvider>();
      for (var i = 0; i < Count; i++)
      {
         var valueProvider = Items[i];
         if (valueProvider is IBindingSourceValueProvider bindingSourceValueProvider)
         {
            var result = bindingSourceValueProvider.Filter(bindingSource);
            if (result != null)
            {
               filteredValueProviders.Add(result);
            }
         }
      }
 
      if (filteredValueProviders.Count == 0)
      {
         // do not create an empty CompositeValueProvider.
         return null;
      }
 
      return new CompositeValueProvider(filteredValueProviders);
   }

   public IValueProvider? Filter()
   {
      var shouldFilter = false;
      for (var i = 0; i < Count; i++)
      {
         var valueProvider = Items[i];
         if (valueProvider is IKeyRewriterValueProvider)
         {
            shouldFilter = true;
            break;
         }
      }

      if (!shouldFilter)
      {
         // no inner IKeyRewriterValueProvider implementations. Nothing to exclude.
         return this;
      }

      var filteredValueProviders = new List<IValueProvider>();
      for (var i = 0; i < Count; i++)
      {
         var valueProvider = Items[i];
         if (valueProvider is IKeyRewriterValueProvider keyRewriterValueProvider)
         {
            var result = keyRewriterValueProvider.Filter();
            if (result != null)
            {
               filteredValueProviders.Add(result);
            }
         }
         else 
         {
            // assume value providers that aren't rewriter-aware do not rewrite their keys.
            filteredValueProviders.Add(valueProvider);
         }
      }

      if (filteredValueProviders.Count == 0)
      {
         // Do not create an empty CompositeValueProvider.
         return null;
      }
 
      return new CompositeValueProvider(filteredValueProviders);
   }
}
//---------------------------------Ʌ

//------------------------V
public class BindingSource : IEquatable<BindingSource?>
{
   public static readonly BindingSource Body = new BindingSource("Body", Resources.BindingSource_Body, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource Custom = new BindingSource("Custom", Resources.BindingSource_Custom, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource Form = new BindingSource("Form", Resources.BindingSource_Form, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Header = new BindingSource("Header", Resources.BindingSource_Header, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource ModelBinding = new BindingSource("ModelBinding", Resources.BindingSource_ModelBinding, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Path = new BindingSource("Path", Resources.BindingSource_Path, isGreedy: false, isFromRequest: true);     // url request url path
   public static readonly BindingSource Query = new BindingSource("Query", Resources.BindingSource_Query, isGreedy: false, isFromRequest: true);  // query-string
   public static readonly BindingSource Services = new BindingSource("Services", Resources.BindingSource_Services, isGreedy: true, isFromRequest: false);
   public static readonly BindingSource Special = new BindingSource("Special", Resources.BindingSource_Special, isGreedy: true, isFromRequest: false);  // when not user input
   public static readonly BindingSource FormFile = new BindingSource("FormFile", Resources.BindingSource_FormFile, isGreedy: true, isFromRequest: true);

   public BindingSource(string id, string displayName, bool isGreedy, bool isFromRequest)
   {
      Id = id;
      DisplayName = displayName;
      IsGreedy = isGreedy;
      IsFromRequest = isFromRequest;
   }

   public string DisplayName { get; }
   public string Id { get; }
   public bool IsGreedy { get; }
   public bool IsFromRequest { get; }

   public virtual bool CanAcceptDataFrom(BindingSource bindingSource)
   {
      if (bindingSource is CompositeBindingSource)
         throw new ArgumentNullException(nameof(bindingSource));

      if (bindingSource is CompositeBindingSource)
      {
         var message = Resources.FormatBindingSource_CannotBeComposite(bindingSource.DisplayName, nameof(CanAcceptDataFrom));
         throw new ArgumentException(message, nameof(bindingSource));
      }

      if (this == bindingSource)
      {
         return true;
      }

      if (this == ModelBinding)
      {
         return bindingSource == Form || bindingSource == Path || bindingSource == Query;
      }
 
      return false;
   }
}
//------------------------Ʌ

//---------------------------------V
public class CompositeBindingSource : BindingSource
{
   public static CompositeBindingSource Create(IEnumerable<BindingSource> bindingSources, string displayName)
   {
      foreach (var bindingSource in bindingSources)
      {
         if (bindingSource.IsGreedy) {
            var message = Resources.FormatBindingSource_CannotBeGreedy(bindingSource.DisplayName, nameof(CompositeBindingSource));
            throw new ArgumentException(message, nameof(bindingSources));
         }

         if (!bindingSource.IsFromRequest) {
            var message = Resources.FormatBindingSource_MustBeFromRequest(bindingSource.DisplayName, nameof(CompositeBindingSource));
            throw new ArgumentException(message, nameof(bindingSources));
         }

         if (bindingSource is CompositeBindingSource) {
            var message = Resources.FormatBindingSource_CannotBeComposite(bindingSource.DisplayName, nameof(CompositeBindingSource));
            throw new ArgumentException(message, nameof(bindingSources));
         }
      }

      var id = string.Join("&", bindingSources.Select(s => s.Id).OrderBy(s => s, StringComparer.Ordinal));
      return new CompositeBindingSource(id, displayName, bindingSources);
   }

   private CompositeBindingSource(string id, string displayName, IEnumerable<BindingSource> bindingSources) : base(id, displayName, isGreedy: false, isFromRequest: true)
   {
      BindingSources = bindingSources;
   }

   public IEnumerable<BindingSource> BindingSources { get; }

   public override bool CanAcceptDataFrom(BindingSource bindingSource)
   {
      if (bindingSource is CompositeBindingSource)
        throw new ArgumentException(message, nameof(bindingSource));

      foreach (var source in BindingSources)
      {
         if (source.CanAcceptDataFrom(bindingSource))
         {
            return true;
         }
      }

      return false;
   }
}
//---------------------------------Ʌ

//----------------------V
public class BindingInfo
{
   private Type? _binderType;

   public BindingInfo() { }

   public BindingInfo(BindingInfo other)
   {
      BindingSource = other.BindingSource;
      BinderModelName = other.BinderModelName;
      BinderType = other.BinderType;
      PropertyFilterProvider = other.PropertyFilterProvider;
      RequestPredicate = other.RequestPredicate;
      EmptyBodyBehavior = other.EmptyBodyBehavior;
   }

   public BindingSource? BindingSource { get; set; }

   public string? BinderModelName { get; set; }

   public Type? BinderType
   {
      get => _binderType;
      set {
         if (value != null && !typeof(IModelBinder).IsAssignableFrom(value))
            throw new ArgumentException(Resources.FormatBinderType_MustBeIModelBinder(value.FullName, typeof(IModelBinder).FullName), nameof(value));
         
         _binderType = value;
      }
   }

   public IPropertyFilterProvider? PropertyFilterProvider { get; set; }

   public Func<ActionContext, bool>? RequestPredicate { get; set; }  // a predicate determines if the model should be bound based on state from the current request

   public EmptyBodyBehavior EmptyBodyBehavior { get; set; }  // gets or sets the value which decides if empty bodies are treated as valid inputs

   public static BindingInfo? GetBindingInfo(IEnumerable<object> attributes)
   {
      var bindingInfo = new BindingInfo();
      var isBindingInfoPresent = false;

      foreach (var binderModelNameAttribute in attributes.OfType<IModelNameProvider>())  // BinderModelName
      {
         isBindingInfoPresent = true;
         if (binderModelNameAttribute?.Name != null)
         {
            bindingInfo.BinderModelName = binderModelNameAttribute.Name;
            break;
         }
      }

      foreach (var binderTypeAttribute in attributes.OfType<IBinderTypeProviderMetadata>())  // BinderType
      {
         isBindingInfoPresent = true;
         if (binderTypeAttribute.BinderType != null)
         {
            bindingInfo.BinderType = binderTypeAttribute.BinderType;
            break;
         }
      }
    
      foreach (var bindingSourceAttribute in attributes.OfType<IBindingSourceMetadata>())  // BindingSource
      {
         isBindingInfoPresent = true;
         if (bindingSourceAttribute.BindingSource != null)
         {
            bindingInfo.BindingSource = bindingSourceAttribute.BindingSource;
            break;
         }
      }

      var propertyFilterProviders = attributes.OfType<IPropertyFilterProvider>().ToArray();  // PropertyFilterProvider
      if (propertyFilterProviders.Length == 1)
      {
         isBindingInfoPresent = true;
         bindingInfo.PropertyFilterProvider = propertyFilterProviders[0];
      }
      else if (propertyFilterProviders.Length > 1)
      {
         isBindingInfoPresent = true;
         bindingInfo.PropertyFilterProvider = new CompositePropertyFilterProvider(propertyFilterProviders);
      }

      foreach (var requestPredicateProvider in attributes.OfType<IRequestPredicateProvider>())  // RequestPredicate
      {
         isBindingInfoPresent = true;
         if (requestPredicateProvider.RequestPredicate != null)
         {
            bindingInfo.RequestPredicate = requestPredicateProvider.RequestPredicate;
            break;
         }
      }

      foreach (var configureEmptyBodyBehavior in attributes.OfType<IConfigureEmptyBodyBehavior>())
      {
         isBindingInfoPresent = true;
         bindingInfo.EmptyBodyBehavior = configureEmptyBodyBehavior.EmptyBodyBehavior;
         break;
      }
 
      return isBindingInfoPresent ? bindingInfo : null;
   }

   public static BindingInfo? GetBindingInfo(IEnumerable<object> attributes, ModelMetadata modelMetadata)
   {
      var bindingInfo = GetBindingInfo(attributes);
      var isBindingInfoPresent = bindingInfo != null;

      if (bindingInfo == null)
      {
         bindingInfo = new BindingInfo();
      }

      isBindingInfoPresent |= bindingInfo.TryApplyBindingInfo(modelMetadata);
      
      return isBindingInfoPresent ? bindingInfo : null;
   }

   public bool TryApplyBindingInfo(ModelMetadata modelMetadata)
   {
      var isBindingInfoPresent = false;
      if (BinderModelName == null && modelMetadata.BinderModelName != null)
      {
         isBindingInfoPresent = true;
         BinderModelName = modelMetadata.BinderModelName;
      }
 
      if (BinderType == null && modelMetadata.BinderType != null)
      {
         isBindingInfoPresent = true;
         BinderType = modelMetadata.BinderType;
      }
 
      if (BindingSource == null && modelMetadata.BindingSource != null)
      {
         isBindingInfoPresent = true;
         BindingSource = modelMetadata.BindingSource;
      }
 
      if (PropertyFilterProvider == null && modelMetadata.PropertyFilterProvider != null)
      {
         isBindingInfoPresent = true;
         PropertyFilterProvider = modelMetadata.PropertyFilterProvider;
      }

      if (EmptyBodyBehavior == EmptyBodyBehavior.Default && BindingSource == BindingSource.Body &&
         (modelMetadata.NullabilityState == NullabilityState.Nullable || modelMetadata.IsNullableValueType || modelMetadata.HasDefaultValue))
      {
         isBindingInfoPresent = true;
         EmptyBodyBehavior = EmptyBodyBehavior.Allow;
      }
 
      return isBindingInfoPresent;
   }

   private sealed class CompositePropertyFilterProvider : IPropertyFilterProvider
   {
      private readonly IEnumerable<IPropertyFilterProvider> _providers;
 
      public CompositePropertyFilterProvider(IEnumerable<IPropertyFilterProvider> providers)
      {
          _providers = providers;
      }
 
      public Func<ModelMetadata, bool> PropertyFilter => CreatePropertyFilter();

      private Func<ModelMetadata, bool> CreatePropertyFilter()
      {
         var propertyFilters = _providers.Select(p => p.PropertyFilter).Where(p => p != null);

         return (m) => 
         {
            foreach (var propertyFilter in propertyFilters) {
               if (!propertyFilter(m))
                  return false;         
            }
 
            return true;
         };
      }
   }
}
//----------------------Ʌ

//---------------------------------------V
public abstract class ModelBindingContext
{
   public abstract ActionContext ActionContext
   public abstract string? BinderModelName { get; set; }
   public abstract BindingSource? BindingSource { get; set; }
   public abstract string FieldName { get; set; }
   public virtual HttpContext HttpContext => ActionContext?.HttpContext!;
   public abstract bool IsTopLevelObject { get; set; }
   public abstract object? Model { get; set; }
   public abstract ModelMetadata ModelMetadata { get; set; }
   public abstract string ModelName { get; set; }
   public string OriginalModelName { get; protected set; } = default!;
   public abstract ModelStateDictionary ModelState { get; set; }
   public virtual Type ModelType => ModelMetadata.ModelType;
   public abstract Func<ModelMetadata, bool>? PropertyFilter { get; set; }
   public abstract ValidationStateDictionary ValidationState { get; set; }
   public abstract IValueProvider ValueProvider { get; set; }
   public abstract ModelBindingResult Result { get; set; }
   public abstract NestedScope EnterNestedScope(ModelMetadata modelMetadata, string fieldName, string modelName, object? model);
   public abstract NestedScope EnterNestedScope();
   protected abstract void ExitNestedScope();
   
   public readonly struct NestedScope : IDisposable
   {
      private readonly ModelBindingContext _context;

      public NestedScope(ModelBindingContext context)
      {
         _context = context;
      }

      public void Dispose()
      {
         _context.ExitNestedScope();
      }
   }
}
//---------------------------------------Ʌ

//-------------------V
public abstract class ModelBinderProviderContext
{
   public abstract IModelBinder CreateBinder(ModelMetadata metadata);

   public virtual IModelBinder CreateBinder(ModelMetadata metadata, BindingInfo bindingInfo) => throw new NotSupportedException();

   public abstract BindingInfo BindingInfo { get; }

   public abstract ModelMetadata Metadata { get; }

   public abstract IModelMetadataProvider MetadataProvider { get; }

   public virtual IServiceProvider Services { get; } = default!;
}
//-------------------Ʌ

public interface IModelBinder
{
   Task BindModelAsync(ModelBindingContext bindingContext);
}

public interface IModelBinderProvider
{
   IModelBinder? GetBinder(ModelBinderProviderContext context);
}


//----------------------------------------V
public class SimpleTypeModelBinderProvider : IModelBinderProvider
{
   public IModelBinder? GetBinder(ModelBinderProviderContext context)
   {
      if (!context.Metadata.IsComplexType)
      {
         var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();
         return new SimpleTypeModelBinder(context.Metadata.ModelType, loggerFactory);
      }

      return null;
   }
}

public class SimpleTypeModelBinder : IModelBinder
{
   private readonly TypeConverter _typeConverter;
   private readonly ILogger _logger;

   public SimpleTypeModelBinder(Type type, ILoggerFactory loggerFactory)
   {
      _typeConverter = TypeDescriptor.GetConverter(type);
      _logger = loggerFactory.CreateLogger<SimpleTypeModelBinder>();
   }

   public Task BindModelAsync(ModelBindingContext bindingContext)
   {
      var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
      if (valueProviderResult == ValueProviderResult.None) {
         return Task.CompletedTask;
      }

      bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

      try
      {
         var value = valueProviderResult.FirstValue;
         
         object? model;
         if (bindingContext.ModelType == typeof(string))
         {
            // already have a string. No further conversion required but handle ConvertEmptyStringToNull.
            if (bindingContext.ModelMetadata.ConvertEmptyStringToNull && string.IsNullOrWhiteSpace(value))
            {
               model = null;
            }
            else
            {
               model = value;
            }
         }
         else if (string.IsNullOrWhiteSpace(value))
         {
            // other than the StringConverter, converters Trim() the value then throw if the result is empty.
            model = null;
         }
         else
         {
            model = _typeConverter.ConvertFrom(context: null, culture: valueProviderResult.Culture, value: value);
         }
         CheckModel(bindingContext, valueProviderResult, model);

         return Task.CompletedTask;
      }
      catch (Exception exception)
      {
         var isFormatException = exception is FormatException;
         if (!isFormatException && exception.InnerException != null)
         {
            // TypeConverter throws System.Exception wrapping the FormatException, so we capture the inner exception.
            exception = ExceptionDispatchInfo.Capture(exception.InnerException).SourceException;
         }

         bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, exception, bindingContext.ModelMetadata);

         // were able to find a converter for the type but conversion failed.
         return Task.CompletedTask;
      }
   }

   protected virtual void CheckModel(ModelBindingContext bindingContext, ValueProviderResult valueProviderResult, object? model)
   {
      if (model == null && !bindingContext.ModelMetadata.IsReferenceOrNullableType)
      {
         bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(valueProviderResult.ToString())
         );
      }
      else
      {
         bindingContext.Result = ModelBindingResult.Success(model);
      }
   }
}
//----------------------------------------Ʌ

//-------------------------------------------V
public class ComplexObjectModelBinderProvider : IModelBinderProvider
{
   public IModelBinder? GetBinder(ModelBinderProviderContext context)
   {
      var metadata = context.Metadata;
      if (metadata.IsComplexType && !metadata.IsCollectionType)
      {
         var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();
         var logger = loggerFactory.CreateLogger<ComplexObjectModelBinder>();
         var parameterBinders = GetParameterBinders(context);

         var propertyBinders = new Dictionary<ModelMetadata, IModelBinder>();
         for (var i = 0; i < context.Metadata.Properties.Count; i++)
         {
            var property = context.Metadata.Properties[i];
            propertyBinders.Add(property, context.CreateBinder(property));
         }

         return new ComplexObjectModelBinder(propertyBinders, parameterBinders, logger);
      }

      return null;
   }

   private static IReadOnlyList<IModelBinder> GetParameterBinders(ModelBinderProviderContext context)
   {
      var boundConstructor = context.Metadata.BoundConstructor;
      if (boundConstructor is null)
      {
         return Array.Empty<IModelBinder>();
      }

      var parameterBinders = boundConstructor.BoundConstructorParameters!.Count == 0 
         ? Array.Empty<IModelBinder>() : new IModelBinder[boundConstructor.BoundConstructorParameters.Count];
      
      for (var i = 0; i < parameterBinders.Length; i++)
      {
         parameterBinders[i] = context.CreateBinder(boundConstructor.BoundConstructorParameters[i]);
      }

      return parameterBinders;
   }
}
//-------------------------------------------Ʌ

//--------------------------------------------------V
public sealed partial class ComplexObjectModelBinder : IModelBinder
{
   internal const int NoDataAvailable = 0;
   internal const int GreedyPropertiesMayHaveData = 1;
   internal const int ValueProviderDataAvailable = 2;

   private readonly IDictionary<ModelMetadata, IModelBinder> _propertyBinders;
   private readonly IReadOnlyList<IModelBinder> _parameterBinders;
   private readonly ILogger _logger;
   private Func<object>? _modelCreator;

   internal ComplexObjectModelBinder(
      IDictionary<ModelMetadata, IModelBinder> propertyBinders, 
      IReadOnlyList<IModelBinder> parameterBinders, 
      ILogger<ComplexObjectModelBinder> logger)
   {
      _propertyBinders = propertyBinders;
      _parameterBinders = parameterBinders;
      _logger = logger;
   }

   public Task BindModelAsync(ModelBindingContext bindingContext)
   {
      var parameterData = CanCreateModel(bindingContext);
      if (parameterData == NoDataAvailable)
      {
         return Task.CompletedTask;
      }

      return BindModelCoreAsync(bindingContext, parameterData);
   }

   private async Task BindModelCoreAsync(ModelBindingContext bindingContext, int propertyData)
   {
      // create model first (if necessary) to avoid reporting errors about properties when activation fails.
      var attemptedBinding = false;
      var bindingSucceeded = false;

      var modelMetadata = bindingContext.ModelMetadata;
      var boundConstructor = modelMetadata.BoundConstructor;

      if (boundConstructor != null)
      {
         var values = new object[boundConstructor.BoundConstructorParameters!.Count];
         var (attemptedParameterBinding, parameterBindingSucceeded) = await BindParametersAsync(
            bindingContext,
            propertyData,
            boundConstructor.BoundConstructorParameters,
            values
         );
 
         attemptedBinding |= attemptedParameterBinding;
         bindingSucceeded |= parameterBindingSucceeded;
 
         if (!CreateModel(bindingContext, boundConstructor, values))
         {
            return;
         }
      }
      else if (bindingContext.Model == null)
      {
         CreateModel(bindingContext);
      }

      var (attemptedPropertyBinding, propertyBindingSucceeded) = await BindPropertiesAsync(bindingContext, propertyData, modelMetadata.BoundProperties);

      attemptedBinding |= attemptedPropertyBinding;
      bindingSucceeded |= propertyBindingSucceeded;

      if (!attemptedBinding && bindingContext.IsTopLevelObject && modelMetadata.IsBindingRequired)
      {
         var messageProvider = modelMetadata.ModelBindingMessageProvider;
         var message = messageProvider.MissingBindRequiredValueAccessor(bindingContext.FieldName);
         bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, message);
      }

      if (!bindingContext.IsTopLevelObject && !bindingSucceeded && propertyData == GreedyPropertiesMayHaveData)
      {
         bindingContext.Result = ModelBindingResult.Failed();
         return;
      }
 
      bindingContext.Result = ModelBindingResult.Success(bindingContext.Model);
   }

   internal static bool CreateModel(ModelBindingContext bindingContext, ModelMetadata boundConstructor, object[] values)
   {
      try
      {
         bindingContext.Model = boundConstructor.BoundConstructorInvoker!(values);
         return true;
      }
      catch (Exception ex)
      {
         AddModelError(ex, bindingContext.ModelName, bindingContext);
         bindingContext.Result = ModelBindingResult.Failed();
         return false;
      }
   }

   internal void CreateModel(ModelBindingContext bindingContext)
   {
      if (_modelCreator == null)
      {
         var modelType = bindingContext.ModelType;
         if (modelType.IsAbstract || modelType.GetConstructor(Type.EmptyTypes) == null)
         {
            var metadata = bindingContext.ModelMetadata;
            switch (metadata.MetadataKind)
            {
               case ModelMetadataKind.Parameter:
                  throw new InvalidOperationException(...);
               case ModelMetadataKind.Property:
                  throw new InvalidOperationException(...);
               case ModelMetadataKind.Type:
                  throw new InvalidOperationException(...);
            }
         }
 
         _modelCreator = Expression.Lambda<Func<object>>(Expression.New(bindingContext.ModelType)).Compile();
      }

      bindingContext.Model = _modelCreator();
   }

   private async ValueTask<(bool attemptedBinding, bool bindingSucceeded)> BindParametersAsync(
      ModelBindingContext bindingContext,
      int propertyData,
      IReadOnlyList<ModelMetadata> parameters,
      object?[] parameterValues)
   {
      // ...
   }

   private async ValueTask<(bool attemptedBinding, bool bindingSucceeded)> BindPropertiesAsync(
      ModelBindingContext bindingContext,
      int propertyData,
      IReadOnlyList<ModelMetadata> boundProperties)
   {
      // ...
   }

   internal static bool CanBindItem(ModelBindingContext bindingContext, ModelMetadata propertyMetadata)
   {
      var metadataProviderFilter = bindingContext.ModelMetadata.PropertyFilterProvider?.PropertyFilter;
      if (metadataProviderFilter?.Invoke(propertyMetadata) == false)
         return false;
 
      if (bindingContext.PropertyFilter?.Invoke(propertyMetadata) == false)
         return false;
 
      if (!propertyMetadata.IsBindingAllowed)
          return false;
 
      if (propertyMetadata.MetadataKind == ModelMetadataKind.Property && propertyMetadata.IsReadOnly)
      {
         // determine if we can update a readonly property (such as a collection).
         return CanUpdateReadOnlyProperty(propertyMetadata.ModelType);
      }
 
      return true;
   }

   private static async ValueTask<ModelBindingResult> BindParameterAsync(
      ModelBindingContext bindingContext,
      ModelMetadata parameter,
      IModelBinder parameterBinder,
      string fieldName,
      string modelName)
   { 
      ModelBindingResult result;
      using (bindingContext.EnterNestedScope(modelMetadata: parameter, fieldName: fieldName, modelName: modelName, model: null))
      {
         await parameterBinder.BindModelAsync(bindingContext);
         result = bindingContext.Result;
      }
 
      if (!result.IsModelSet && parameter.IsBindingRequired)
      {
         var message = parameter.ModelBindingMessageProvider.MissingBindRequiredValueAccessor(fieldName);
         bindingContext.ModelState.TryAddModelError(modelName, message);
      }
 
      return result;
   }

   internal int CanCreateModel(ModelBindingContext bindingContext)
   {
      var isTopLevelObject = bindingContext.IsTopLevelObject;
      var bindingSource = bindingContext.BindingSource;
      if (!isTopLevelObject && bindingSource != null && bindingSource.IsGreedy)
      {
         return NoDataAvailable;
      }
 
      // Create the object if:
      // 1. It is a top level model.
      if (isTopLevelObject)
      {
         return ValueProviderDataAvailable;
      }
 
      // 2. Any of the model properties can be bound.
      return CanBindAnyModelItem(bindingContext);
   }

   private int CanBindAnyModelItem(ModelBindingContext bindingContext)
   {
      // ...
   }

   private static void AddModelError(Exception exception, string modelName, ModelBindingContext bindingContext)
   {
      var targetInvocationException = exception as TargetInvocationException;
      if (targetInvocationException?.InnerException != null)
      {
         exception = targetInvocationException.InnerException;
      }
 
      // do not add an error message if a binding error has already occurred for this property.
      var modelState = bindingContext.ModelState;
      var validationState = modelState.GetFieldValidationState(modelName);
      if (validationState == ModelValidationState.Unvalidated)
      {
         modelState.AddModelError(modelName, exception, bindingContext.ModelMetadata);
      }
   }

   // ...
}
//--------------------------------------------------Ʌ

//----------------------------------V
public class BodyModelBinderProvider : IModelBinderProvider
{
   private readonly IList<IInputFormatter> _formatters;
   private readonly IHttpRequestStreamReaderFactory _readerFactory;
   private readonly ILoggerFactory _loggerFactory;
   private readonly MvcOptions? _options;

   public BodyModelBinderProvider(IList<IInputFormatter> formatters, IHttpRequestStreamReaderFactory readerFactory) : this(...) { }
   
   public BodyModelBinderProvider(IList<IInputFormatter> formatters, IHttpRequestStreamReaderFactory readerFactory, ILoggerFactory loggerFactory, MvcOptions? options)
   {
      _formatters = formatters;
      _readerFactory = readerFactory;
      _loggerFactory = loggerFactory;
      _options = options;
   }

   public IModelBinder? GetBinder(ModelBinderProviderContext context)
   {
      if (context.BindingInfo.BindingSource != null && context.BindingInfo.BindingSource.CanAcceptDataFrom(BindingSource.Body))
      {
         var treatEmptyInputAsDefaultValue = CalculateAllowEmptyBody(context.BindingInfo.EmptyBodyBehavior, _options);

         return new BodyModelBinder(_formatters, _readerFactory, _loggerFactory, _options)
         {
            AllowEmptyBody = treatEmptyInputAsDefaultValue,
         };
      }

      return null;
   }

   internal static bool CalculateAllowEmptyBody(EmptyBodyBehavior emptyBodyBehavior, MvcOptions? options)
   {
      if (emptyBodyBehavior == EmptyBodyBehavior.Default)
      {
         return options?.AllowEmptyInputInBodyModelBinding ?? false;
      }
 
      return emptyBodyBehavior == EmptyBodyBehavior.Allow;
   }
}
//----------------------------------Ʌ

//----------------------------------V
public partial class BodyModelBinder : IModelBinder
{
   private readonly IList<IInputFormatter> _formatters;
   private readonly Func<Stream, Encoding, TextReader> _readerFactory;
   private readonly ILogger _logger;
   private readonly MvcOptions? _options;

   public BodyModelBinder(IList<IInputFormatter> formatters, IHttpRequestStreamReaderFactory readerFactory) : this(formatters, readerFactory, loggerFactory: null) { }

   public BodyModelBinder(IList<IInputFormatter> formatters, IHttpRequestStreamReaderFactory readerFactory, ILoggerFactory? loggerFactory, MvcOptions? options)
   {
      _formatters = formatters;
      _readerFactory = readerFactory.CreateReader;
      _logger = loggerFactory?.CreateLogger<BodyModelBinder>() ?? NullLogger<BodyModelBinder>.Instance;
      _options = options;
   }

   internal bool AllowEmptyBody { get; set; }

   public async Task BindModelAsync(ModelBindingContext bindingContext)
   {
      string modelBindingKey;
      if (bindingContext.IsTopLevelObject)
      {
         modelBindingKey = bindingContext.BinderModelName ?? string.Empty;
      }
      else
      {
         modelBindingKey = bindingContext.ModelName;
      }

      var httpContext = bindingContext.HttpContext;

      var formatterContext = new InputFormatterContext(httpContext, modelBindingKey, bindingContext.ModelState, bindingContext.ModelMetadata, _readerFactory, AllowEmptyBody);
      var formatter = (IInputFormatter?)null;
      for (var i = 0; i < _formatters.Count; i++)
      {
         if (_formatters[i].CanRead(formatterContext))
         {
            formatter = _formatters[i];
            Log.InputFormatterSelected(_logger, formatter, formatterContext);
            break;
         }
         else
         {
               Log.InputFormatterRejected(_logger, _formatters[i], formatterContext);
         }
      }

      if (formatter == null)
      {
         if (AllowEmptyBody)
         {
            var hasBody = httpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody;
            hasBody ??= httpContext.Request.ContentLength is not null && httpContext.Request.ContentLength == 0;
            if (hasBody == false)
            {
               bindingContext.Result = ModelBindingResult.Success(model: null);
               return;
            }
         }
  
         var message = Resources.FormatUnsupportedContentType(httpContext.Request.ContentType);
         var exception = new UnsupportedContentTypeException(message);
         bindingContext.ModelState.AddModelError(modelBindingKey, exception, bindingContext.ModelMetadata);
         return;
      }

      try
      {
         var result = await formatter.ReadAsync(formatterContext);
 
         if (result.HasError)
         {
            // formatter encountered an error. Do not use the model it returned.
            _logger.DoneAttemptingToBindModel(bindingContext);
            return;
         }
 
         if (result.IsModelSet)
         {
            var model = result.Model;
            bindingContext.Result = ModelBindingResult.Success(model);
         }
         else
         {        
            var message = bindingContext
               .ModelMetadata
               .ModelBindingMessageProvider
               .MissingRequestBodyRequiredValueAccessor();
            bindingContext.ModelState.AddModelError(modelBindingKey, message);
         }
      }
      catch (Exception exception) when (exception is InputFormatterException || ShouldHandleException(formatter))
      {
         bindingContext.ModelState.AddModelError(modelBindingKey, exception, bindingContext.ModelMetadata);
      }
   }

   private static bool ShouldHandleException(IInputFormatter formatter)
   {
      // Any explicit policy on the formatters overrides the default.
      var policy = (formatter as IInputFormatterExceptionPolicy)?.ExceptionPolicy ?? InputFormatterExceptionPolicy.MalformedInputExceptions;
 
      return policy == InputFormatterExceptionPolicy.AllExceptions;
   }
}
//----------------------------------Ʌ

//----------------------------------V
public partial class ParameterBinder
{
   private readonly IModelMetadataProvider _modelMetadataProvider;
   private readonly IModelBinderFactory _modelBinderFactory;
   private readonly IObjectModelValidator _objectModelValidator;

   public ParameterBinder(
      IModelMetadataProvider modelMetadataProvider,
      IModelBinderFactory modelBinderFactory,
      IObjectModelValidator validator,
      IOptions<MvcOptions> mvcOptions,
      ILoggerFactory loggerFactory)
   {
      _modelMetadataProvider = modelMetadataProvider;
      _modelBinderFactory = modelBinderFactory;
      _objectModelValidator = validator;
      Logger = loggerFactory.CreateLogger(GetType());
   }

   public virtual Task<ModelBindingResult> BindModelAsync(
      ActionContext actionContext,
      IModelBinder modelBinder,
      IValueProvider valueProvider,
      ParameterDescriptor parameter,
      ModelMetadata metadata,
      object? value)
   {
      BindModelAsync(actionContext, modelBinder, valueProvider, parameter, metadata, value, container: null).AsTask();
   }

   public virtual async ValueTask<ModelBindingResult> BindModelAsync(
      ActionContext actionContext,
      IModelBinder modelBinder,
      IValueProvider valueProvider,
      ParameterDescriptor parameter,
      ModelMetadata metadata,
      object? value,
      object? container)
   {
      if (parameter.BindingInfo?.RequestPredicate?.Invoke(actionContext) == false)
      {
         Log.ParameterBinderRequestPredicateShortCircuit(Logger, parameter, metadata);
         return ModelBindingResult.Failed();
      }

      var modelBindingContext = DefaultModelBindingContext.CreateBindingContext(actionContext, valueProvider, metadata, parameter.BindingInfo, parameter.Name);
      modelBindingContext.Model = value;

      var parameterModelName = parameter.BindingInfo?.BinderModelName ?? metadata.BinderModelName;
      if (parameterModelName != null)
      {
         // the name was set explicitly, always use that as the prefix.
         modelBindingContext.ModelName = parameterModelName;
      }
      else if (modelBindingContext.ValueProvider.ContainsPrefix(parameter.Name))
      {
         // we have a match for the parameter name, use that as that prefix.
         modelBindingContext.ModelName = parameter.Name;
      }
      else
      {
         // no match, fallback to empty string as the prefix.
         modelBindingContext.ModelName = string.Empty;
      }

      await modelBinder.BindModelAsync(modelBindingContext);

      var modelBindingResult = modelBindingContext.Result;

      if (_objectModelValidator is ObjectModelValidator baseObjectValidator)
      {
         EnforceBindRequiredAndValidate(baseObjectValidator, actionContext, parameter, metadata, modelBindingContext, modelBindingResult, container);
      }
      else
      {
         // for legacy implementations (which directly implemented IObjectModelValidator), fall back to the
         // back-compatibility logic. In this scenario, top-level validation attributes will be ignored like they were historically.
         if (modelBindingResult.IsModelSet)
         {
            _objectModelValidator.Validate(actionContext, modelBindingContext.ValidationState, modelBindingContext.ModelName, modelBindingResult.Model);
         }
      }

      return modelBindingResult;
   }

   private void EnforceBindRequiredAndValidate(
      ObjectModelValidator baseObjectValidator,
      ActionContext actionContext,
      ParameterDescriptor parameter,
      ModelMetadata metadata,
      ModelBindingContext modelBindingContext,
      ModelBindingResult modelBindingResult,
      object? container)
   {
      RecalculateModelMetadata(parameter, modelBindingResult, ref metadata);
 
      if (!modelBindingResult.IsModelSet && metadata.IsBindingRequired)
      {
         // enforce BindingBehavior.Required (e.g., [BindRequired])
         var modelName = modelBindingContext.FieldName;
         var message = metadata.ModelBindingMessageProvider.MissingBindRequiredValueAccessor(modelName);
         actionContext.ModelState.TryAddModelError(modelName, message);
      }
      else if (modelBindingResult.IsModelSet)
      {
         // enforce any other validation rules
         baseObjectValidator.Validate(actionContext, modelBindingContext.ValidationState, modelBindingContext.ModelName, modelBindingResult.Model, metadata, container);
      }
      else if (metadata.IsRequired)
      {
         var modelName = modelBindingContext.ModelName;
 
         if (string.IsNullOrEmpty(modelBindingContext.ModelName) && parameter.BindingInfo?.BinderModelName == null)
         {
            // if we get here then this is a fallback case. The model name wasn't explicitly set and we ended up with an empty prefix.
            modelName = modelBindingContext.FieldName;
         }
 
         // run validation, we expect this to validate [Required].
         baseObjectValidator.Validate(actionContext, modelBindingContext.ValidationState, modelName, modelBindingResult.Model, metadata, container);
      }
   }

   private void RecalculateModelMetadata(ParameterDescriptor parameter, ModelBindingResult modelBindingResult, ref ModelMetadata metadata)
   {
      if (!modelBindingResult.IsModelSet || modelBindingResult.Model == null || _modelMetadataProvider is not ModelMetadataProvider modelMetadataProvider)
      {
         return;
      }
 
      var modelType = modelBindingResult.Model.GetType();
      if (parameter is IParameterInfoParameterDescriptor parameterInfoParameter)
      {
         var parameterInfo = parameterInfoParameter.ParameterInfo;
         if (modelType != parameterInfo.ParameterType)
         {
            metadata = modelMetadataProvider.GetMetadataForParameter(parameterInfo, modelType);
         }
      }
      else if (parameter is IPropertyInfoParameterDescriptor propertyInfoParameter)
      {
         var propertyInfo = propertyInfoParameter.PropertyInfo;
         if (modelType != propertyInfo.PropertyType)
         {
            metadata = modelMetadataProvider.GetMetadataForProperty(propertyInfo, modelType);
         }
      }
   }
}
//----------------------------------Ʌ
```

Goals:

1. read source code of Produces and Consumes attributes
2. Create custom binder




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