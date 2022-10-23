Chapter 30- Filters
=================================


```C#
public class Startup 
{
   // ...

   public void ConfigureServices(IServiceCollection services)
   {
      // ...
      services.AddScoped<FilterXXXAttribute>();
      services.Configure<MvcOptions>(opts => opts.Filters.Add(new MessageFilterAttribute("This is the globally-scoped filter")));   // global filters, default Order is 0
   }
}

[MessageFilter("This is the controller-scoped filter", Order = 10)]
public class HomeController : Controller
{
   [MessageFilter("This is the first action-scoped filter", Order = 1)]
   [MessageFilter("This is the second action-scoped filter", Order = -1)]
   public IActionResult Index()
   {
      return View("Message", $"{DateTime.Now.ToLongTimeString()}: This is the Message Razor Page");
   }
}
/*
Without Order:

   global-scoped filter -> controller-scoped filter -> first action-scoped filter -> second action-scoped filter

After Order:

   second action-scoped filter -> global-scoped filter ->  first action-scoped filter -> controller-scoped filter
*/


//------------------------------------------------------------------------------------------------------------------------------
public class SimpleCacheAttribute : Attribute, IResourceFilter
{
   private Dictionary<PathString, IActionResult> CachedResponses = new Dictionary<PathString, IActionResult>();

   public void OnResourceExecuting(ResourceExecutingContext context)
   {
      PathString path = context.HttpContext.Request.Path;
      if (CachedResponses.ContainsKey(path))
      {
         context.Result = CachedResponses[path];
         CachedResponses.Remove(path);
      }
   }

   public void OnResourceExecuted(ResourceExecutedContext context)
   {
      CachedResponses.Add(context.HttpContext.Request.Path, context.Result);
   }
}

public class SimpleCacheAsyncAttribute : Attribute, IAsyncResourceFilter
{
   private Dictionary<PathString, IActionResult> CachedResponses = new Dictionary<PathString, IActionResult>();

   public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
   {
      PathString path = context.HttpContext.Request.Path;
      if (CachedResponses.ContainsKey(path))
      {
         context.Result = CachedResponses[path];
         CachedResponses.Remove(path);
      }
      else
      {
         ResourceExecutedContext execContext = await next();   // next delegate must point to the action method somehow

         // it is like statements after `await next()` runs after OnResourceExecuted
         CachedResponses.Add(context.HttpContext.Request.Path, execContext.Result);
      }
   }
}

public class ChangeArgAdvanceAttribute : ActionFilterAttribute
{
   
   /*  even thought you uncommented them, they still won't run, those two will only run if you didn't override OnActionExecutionAsync
   public override void OnActionExecuting(ActionExecutingContext context) { }

   public override void OnActionExecuted(ActionExecutedContext context) { }
   */

   public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
   {
      if (context.ActionArguments.ContainsKey("message1"))
      {
         context.ActionArguments["message1"] = "New message";
      }

      await next();   
   }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class GuidResponseAttribute : Attribute, IAsyncAlwaysRunResultFilter, IFilterFactory
{
   private int counter = 0;
   private string guid = Guid.NewGuid().ToString();

   public bool IsReusable => false;

   public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
   {
      return ActivatorUtilities.GetServiceOrCreateInstance<GuidResponseAttribute>(serviceProvider);   // note it is GetServiceOrCreateInstance, not CreateInstance
   }

   public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
   {
      Dictionary<string, string> resultData;

      if (context.Result is ViewResult vr && vr.ViewData.Model is Dictionary<string, string> data)
      {
         resultData = data;
      }
      else
      {
         resultData = new Dictionary<string, string>();
         context.Result = new ViewResult()
         {
            ViewName = "/Views/Shared/Message.cshtml",
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
               Model = resultData
            }
         };
      }

      while (resultData.ContainsKey($"Counter_{counter}"))
      {
         counter++;
      }

      resultData[$"Counter_{counter}"] = guid;
      await next();
   }
}
```



## Source Code

```C#
public interface IFilterMetadata { } // marker interface for filters handled in the MVC request pipeline

public interface IOrderedFilter : IFilterMetadata 
{
   int Order { get; }
}

public abstract class FilterContext : ActionContext
{
   public FilterContext(ActionContext actionContext, IList<IFilterMetadata> filters) : base(actionContext)
   {
      Filters = filters;
   }

   public virtual IList<IFilterMetadata> Filters { get; }

   public bool IsEffectivePolicy<TMetadata>(TMetadata policy) where TMetadata : IFilterMetadata
   {
      var effective = FindEffectivePolicy<TMetadata>();
      return ReferenceEquals(policy, effective);
   }

   public TMetadata FindEffectivePolicy<TMetadata>() where TMetadata : IFilterMetadata
   {
      for (var i = Filters.Count - 1; i >= 0; i--)
      {
         var filter = Filters[i];
         if (filter is TMetadata match)
         {
            return match;
         }
      }

      return default;
   }
}


// AuthorizationFilter
//----------------------------------------V
public interface IAuthorizationFilter : IFilterMetadata
{
   void OnAuthorizationAsync(AuthorizationFilterContext context);
}

public interface IAsyncAuthorizationFilter : IFilterMetadata
{
   Task OnAuthorizationAsync(AuthorizationFilterContext context);
}

public class AuthorizationFilterContext : FilterContext
{
   public AuthorizationFilterContext(ActionContext actionContext, IList<IFilterMetadata> filters) : base(actionContext, filters) { }

   public virtual IActionResult Result { get; set; }  // set this property to a non-null value will short-circuit the remainder of the filter pipeline
}                                                     // because ASP.NET Core executes this IActionResult instead of invoking the endpoint
//----------------------------------------Ʌ

// ResourceFilter
//----------------------------------------V
public interface IResourceFilter : IFilterMetadata
{
   void OnResourceExecuting(ResourceExecutingContext context);   // if you set context.Result = newIActionResultInstance

   void OnResourceExecuted(ResourceExecutedContext context);     // OnResourceExecuted won't execute, and other filters like Result Filters won't run neither
}

public interface IAsyncResourceFilter : IFilterMetadata
{
   Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next);
}

// public delegate Task<ResourceExecutedContext> ResourceExecutionDelegate();

public class ResourceExecutingContext : FilterContext
{
   public ResourceExecutingContext(ActionContext actionContext, IList<IFilterMetadata> filters, IList<IValueProviderFactory> valueProviderFactories) : base(actionContext, filters) {
      ValueProviderFactories = valueProviderFactories;
   }

   public virtual IActionResult Result { get; set; }
   public IList<IValueProviderFactory> ValueProviderFactories { get; }
}

public class ResourceExecutedContext : FilterContext
{
   private Exception _exception;
   private ExceptionDispatchInfo _exceptionDispatchInfo;

   public ResourceExecutedContext(ActionContext actionContext, IList<IFilterMetadata> filters) : base(actionContext, filters) { }

   public virtual bool Canceled { get; set; }

   public virtual bool ExceptionHandled { get; set; }

   public virtual IActionResult Result { get; set; }

   public virtual Exception Exception { get; set; }

   public virtual ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

}
//----------------------------------------Ʌ

// ActionFilter
//----------------------------------------V
public interface IActionFilter : IFilterMetadata 
{
   void OnActionExecuting(ActionExecutingContext context);   // if you set context.Result = newIActionResultInstance

   void OnActionExecuted(ActionExecutedContext context);     // OnActionExecuted won't execute, but other filters like ResultFilters still run
}                                                            

public interface IAsyncActionFilter : IFilterMetadata
{
   Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next);
}

//-----------------------------------------VV
public abstract class ActionFilterAttribute :Attribute, IActionFilter, IAsyncActionFilter, IResultFilter, IAsyncResultFilter, IOrderedFilter
{
   public int Order { get; set; }

   public virtual void OnActionExecuting(ActionExecutingContext context) { }

   public virtual void OnActionExecuted(ActionExecutedContext context) { }

   public virtual async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
   {   
      OnActionExecuting(context);
      if (context.Result == null)
      {
         OnActionExecuted(await next());
      }
   }

   public virtual void OnResultExecuting(ResultExecutingContext context) { }
 
   public virtual void OnResultExecuted(ResultExecutedContext context) { }

   public virtual async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
   {    
      OnResultExecuting(context);
      if (!context.Cancel)
      {
         OnResultExecuted(await next());
      }
   }
}
//-----------------------------------------ɅɅ

public class ActionExecutingContext : FilterContext
{
   public ActionExecutingContext(ActionContext actionContext, IList<IFilterMetadata> filters, IDictionary<string, object?> actionArguments, object controller) : base(actionContext, filters)
   {
      ActionArguments = actionArguments;
      Controller = controller;
   }

   public virtual IActionResult Result { get; set; }
   public virtual IDictionary<string, object> ActionArguments { get; }
   public virtual object Controller { get; }
}

public class ActionExecutedContext : FilterContext
{
    private Exception _exception;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;
 
    public ActionExecutedContext(ActionContext actionContext, IList<IFilterMetadata> filters, object controller) : base(actionContext, filters)
    {
        Controller = controller;
    }

    public virtual bool Canceled { get; set; }
    public virtual object Controller { get;  }
    public virtual Exception Exception { get; set; }  
    public virtual ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }   
    public virtual bool ExceptionHandled { get; set; }
    public virtual IActionResult Result { get; set; }
}
//----------------------------------------Ʌ


// ResultFilter, executed when the action method/ction filters complete successfully.
//               not executed when: * authorization filter or resource filter short-circuits the request (because it prevents execution of the action)
//                                  * an exception filter handles an exception by producing an action result
//----------------------------V
public interface IResultFilter : IFilterMetadata
{
   void OnResultExecuting(ResultExecutingContext context);  // if you set context.Result = newIActionResultInstance

   void OnResultExecuted(ResultExecutedContext context);    // OnResultExecuted still execute, which make senses because Result Execution still needs to be run
}

public interface IAsyncResultFilter : IFilterMetadata
{
   Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next);
}

public class ResultExecutingContext : FilterContext
{
   public ResultExecutingContext(ActionContext actionContext, IList<IFilterMetadata> filters, IActionResult result, object controller) : base(actionContext, filters)
   {
      Result = result;
      Controller = controller;
   }

   public virtual object Controller { get; }
   public virtual IActionResult Result { get; set; }
   public virtual bool Cancel { get; set; }
}

public class ResultExecutedContext : FilterContext
{
   private Exception _exception;
   private ExceptionDispatchInfo _exceptionDispatchInfo;

   public ResultExecutedContext(ActionContext actionContext, IList<IFilterMetadata> filters, IActionResult result, object controller) : base(actionContext, filters) 
   {
      Result = result;
      Controller = controller;
   }

   public virtual object Controller { get; }

   public virtual bool Canceled { get; set; }

   public virtual bool ExceptionHandled { get; set; }

   public virtual IActionResult Result { get; set; }

   public virtual Exception Exception { get; set; }

   public virtual ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

}

// always runs regardless of the situation mentioned above
public interface IAsyncAlwaysRunResultFilter : IAsyncResultFilter { }

public interface IAlwaysRunResultFilter : IResultFilter { }
//----------------------------Ʌ


// ExceptionFilter
//----------------------------V
public interface IExceptionFilter : IFilterMetadata
{
    void OnException(ExceptionContext context);
}

public interface IAsyncExceptionFilter : IFilterMetadata
{
   Task OnExceptionAsync(ExceptionContext context);
}

public abstract class ExceptionFilterAttribute : Attribute, IAsyncExceptionFilter, IExceptionFilter, IOrderedFilter
{
   public int Order { get; set; }

   public virtual Task OnExceptionAsync(ExceptionContext context)
   {
      OnException(context);
      return Task.CompletedTask;
   }

   public virtual void OnException(ExceptionContext context)
   {

   }
}

public class ExceptionContext : FilterContext
{
    private Exception _exception;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;
 
    public ActionExecutedContext(ActionContext actionContext, IList<IFilterMetadata> filters, object controller) : base(actionContext, filters)
    {  
    }

    public virtual Exception Exception { get; set; }  
    public virtual ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }   
    public virtual bool ExceptionHandled { get; set; }
    public virtual IActionResult Result { get; set; }
}
//----------------------------Ʌ
```

```C#
public class FilterCollection : Collection<IFilterMetadata>
{

}
```

```C#
public interface IFilterFactory : IFilterMetadata
{
   
   bool IsReusable { get; }   // indicates if the result of CreateInstance(System.IServiceProvider) can be reused across requests
                              // note that this has nothing to do with DI, it means if the filter instance can be reused in the next http request
                              
   IFilterMetadata CreateInstance(IServiceProvider serviceProvider);  // always executes when IsReusable set to false, won't execute for next request when IsReusable set to true
}


public class TypeFilterAttribute : Attribute, IFilterFactory, IOrderedFilter   // <------ don't need to register the type
{
   private ObjectFactory _factory;

   public TypeFilterAttribute(Type type)
   {
      ImplementationType = type;
   }

   public object[] Arguments { get; set; }

   public Type ImplementationType { get; }

   public int Order { get; set; }

   public bool IsReusable { get; set; }

   public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
   {
      var argumentTypes = Arguments.Select(a => a.GetType()).ToArray();

      _factory = ActivatorUtilities.CreateFactory(ImplementationType, argumentTypes ?? Type.EmptyTypes);  

      var filter = (IFilterMetadata)_factory(serviceProvider, Arguments);

      if (filter is IFilterFactory filterFactory)
      {
         filter = filterFactory.CreateInstance(serviceProvider);
      }
 
      return filter;
   }
}

public class ServiceFilterAttribute : Attribute, IFilterFactory, IOrderedFilter   // <------need to register the type, see the example below
{
   public ServiceFilterAttribute(Type type)
   {
      ServiceType = type ?? throw new ArgumentNullException(nameof(type));
   }

   public int Order { get; set; }

   public Type ServiceType { get; }

   public bool IsReusable { get; set; }

   public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
   {
      var filter = (IFilterMetadata)serviceProvider.GetRequiredService(ServiceType);
      
      if (filter is IFilterFactory filterFactory)
      {
         filter = filterFactory.CreateInstance(serviceProvider);
      }

      return filter;
   }
}
```

Below is an demo shows the difference between using `TypeFilterAttribute` and `ServiceFilterAttribute`

```C#
public class MySampleActionFilter : Attribute, IActionFilter
{
    private readonly IPersonService _personService;
    private readonly ILogger<MySampleActionFilter> _logger;

    public MySampleActionFilter(IPersonService personService, ILogger<MySampleActionFilter> logger)
    {
        _personService = personService;
        _logger = logger;
        _logger.LogInformation($"MySampleActionFilter.Ctor {DateTime.Now:yyyyMMddHHmmssffff}");
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        Person personService = _personService.GetPerson(1);
        _logger.LogInformation($"TraceId=[{context.HttpContext.TraceIdentifier}] MySampleActionFilter.OnActionExecuted ");
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        _logger.LogInformation($"TraceId=[{context.HttpContext.TraceIdentifier}] MySampleActionFilter.OnActionExecuting ");
    }
}

[Route("api/[controller]/[action]")]
[ApiController]
public class PersonController : ControllerBase
{
    private readonly List<Person> _persons;
    public PersonController()
    {
        _persons = new List<Person>
        {
            new Person{ Id=1,Name="Tom" },
            new Person{ Id=2,Name="Jerry" },
            new Person{ Id=3,Name="Spike" }
        };
    }

    [HttpGet]
    [TypeFilter(typeof(MySampleActionFilter))]
    public List<Person> GetPersons()    // <-----------------OK, TypeFilter doesn't require MySampleActionFilter to be registered
    {
        return _persons;
    }

    [HttpGet]
    [ServiceFilter(typeof(MySampleActionFilter))]
    public List<Person> GetPersons2()   // System.InvalidOperationException: No service for type 'MySampleActionFilter' has been registered
    {                                   // to make it work, add `services.AddScoped<MySampleActionFilter>()` below
        return _persons;
    }
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IPersonService,PersonService>();
    services.AddControllers();
}
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