Demystifying IActionResult
==============================

```C#
// RESTful Approach
//------------------V
public class Startup {
   public void ConfigureServices(IServiceCollection services) 
   {  
      public void ConfigureServices(IServiceCollection services) {
         // ...
         services.AddControllers();   // <-------------------------- R1
      }
   }

   public void Configure(IApplicationBuilder app, IWebHostEnvironment env) 
   {
      // ...
      app.UseRouting();

      app.UseEndpoints(endpoints => {
         endpoints.MapControllers();
      });
   }
}
//------------------Ʌ

// MVC with Views
//------------------V
public class Startup {   // asp.net core 3
   public void ConfigureServices(IServiceCollection services) 
   {  
      public void ConfigureServices(IServiceCollection services) {
         // ...
         services.AddControllersWithViews()      // <-------------------------- V1
                 .AddRazorRuntimeCompilation();  // so that you can save change on view without restart/recompile the app
      }
   }

   public void Configure(IApplicationBuilder app, IWebHostEnvironment env) 
   {
      // ...
      app.UseRouting();

      app.UseEndpoints(endpoints => {
         //endpoints.MapControllers(); 
         endpoints.MapDefaultControllerRoute();  // same as `endpoints.MapControllerRoute("default", "{controller=home}/{action=Index}/{id?}")`
      });
   }
}

public class Startup {   // old asp.net core version
   public void ConfigureServices(IServiceCollection services) 
   {  
      public void ConfigureServices(IServiceCollection services) {
         // ...
         services.AddMvc(option => option.EnableEndpointRouting = false);  
      }
   }

   public void Configure(IApplicationBuilder app, IWebHostEnvironment env) 
   {
      // ...
      app.UseMvc(routes =>
      {
         routes.MapRoute(
            name: "default",
            template: "{controller=Home}/{action=Index}/{id?}");
      });   
      
   }
}
//------------------Ʌ
```

Note that in old asp.net core version 2.2, it only has one middleware `RouterMiddleware` to register routes and execute routes, while in new version asp.net core, two seperate middlewares are used as above shown, which has more benefits. One benefit is, the middleware between `app.UseRouting()` and `app.UseEndpoints` knows that which route is chosesn. 


------------------------------------------------------------------------------------------------------------------------------------------
```C#
//--------------------------V
public interface IMvcBuilder {
   IServiceCollection Services { get; }

   ApplicationPartManager PartManager { get; }
}

internal class MvcBuilder : IMvcBuilder {
   public MvcBuilder(IServiceCollection services, ApplicationPartManager manager) {
      Services = services;
      PartManager = manager;
   }

   public IServiceCollection Services { get; }
   public ApplicationPartManager PartManager { get; }
}
//--------------------------Ʌ

//------------------------------------------------V
public static class MvcServiceCollectionExtensions {

   public static IMvcBuilder AddMvc(this IServiceCollection services) {
      services.AddControllersWithViews();
      return services.AddRazorPages();
   }

   public static IMvcBuilder AddMvc(this IServiceCollection services, Action<MvcOptions> setupAction) {
      var builder = services.AddMvc();
      builder.Services.Configure(setupAction);

      return builder;
   }

   public static IMvcBuilder AddControllers(this IServiceCollection services) {    // <-------------------------- R2
      var builder = AddControllersCore(services);
      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   public static IMvcBuilder AddControllers(this IServiceCollection services, Action<MvcOptions>? configure) {
      var builder = AddControllersCore(services);
      if (configure != null) {
         builder.AddMvcOptions(configure);
      }

      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   private static IMvcCoreBuilder AddControllersCore(IServiceCollection services) {   // <-------------------------- R3
      // this method excludes all of the view-related services by default
      var builder = services
          .AddMvcCore()
          .AddApiExplorer()
          .AddAuthorization()
          .AddCors()
          .AddDataAnnotations()
          .AddFormatterMappings();
      
      if (MetadataUpdater.IsSupported) {
         services.TryAddEnumerable(ServiceDescriptor.Singleton<IActionDescriptorChangeProvider, HotReloadService>());
      }

      return builder;
   }

   public static IMvcBuilder AddControllersWithViews(this IServiceCollection services) {   // <-------------------------- V2
      var builder = AddControllersWithViewsCore(services);
      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   public static IMvcBuilder AddControllersWithViews(this IServiceCollection services, Action<MvcOptions>? configure) {
      // this method excludes all of the view-related services by default
      var builder = AddControllersWithViewsCore(services);
      if (configure != null) {
         builder.AddMvcOptions(configure);
      }

      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   private static IMvcCoreBuilder AddControllersWithViewsCore(IServiceCollection services) {   // <-------------------------- V3
      var builder = AddControllersCore(services)   // <--------------- called for `both AddControllers` and `AddControllersWithViews`
          .AddViews()
          .AddRazorViewEngine()   // services.TryAddSingleton<IRazorViewEngine, RazorViewEngine>();
          .AddCacheTagHelper();
      
      AddTagHelpersFrameworkParts(builder.PartManager);

      return builder;
   }

   public static IMvcBuilder AddRazorPages(this IServiceCollection services) {
      var builder = AddRazorPagesCore(services);
      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   private static IMvcCoreBuilder AddRazorPagesCore(IServiceCollection services) {
      // this method includes the minimal things controllers need. It's not really feasible to exclude the services for controllers
      var builder = services
          .AddMvcCore()
          .AddAuthorization()
          .AddDataAnnotations()
          .AddRazorPages()
          .AddCacheTagHelper();
      
      AddTagHelpersFrameworkParts(builder.PartManager);

      if (MetadataUpdater.IsSupported) {
         services.TryAddEnumerable(ServiceDescriptor.Singleton<IActionDescriptorChangeProvider, HotReloadService>());
      }

      return builder;
   }

   internal static void AddTagHelpersFrameworkParts(ApplicationPartManager partManager) {
      var mvcTagHelpersAssembly = typeof(InputTagHelper).Assembly;
      if (!partManager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == mvcTagHelpersAssembly)) {
         partManager.ApplicationParts.Add(new FrameworkAssemblyPart(mvcTagHelpersAssembly));
      }
 
      var mvcRazorAssembly = typeof(UrlResolutionTagHelper).Assembly;
      if (!partManager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == mvcRazorAssembly)) {
         partManager.ApplicationParts.Add(new FrameworkAssemblyPart(mvcRazorAssembly));
      }
   }

   private class FrameworkAssemblyPart : AssemblyPart, ICompilationReferencesProvider {
      public FrameworkAssemblyPart(Assembly assembly): base(assembly) { }
 
      IEnumerable<string> ICompilationReferencesProvider.GetReferencePaths() => Enumerable.Empty<string>();
   }
}
//------------------------------------------------Ʌ

//----------------------------------------------------------V
public static class ControllerEndpointRouteBuilderExtensions 
{
   public static ControllerActionEndpointConventionBuilder MapControllers(this IEndpointRouteBuilder endpoints)  // rely on users to specify Controller and Action methods with
   {                                                                                                             // attributes like [Route("XXX")], [HttpGet("{id}")] etc
      EnsureControllerServices(endpoints);
      return GetOrCreateDataSource(endpoints).DefaultBuilder;
   }

   public static ControllerActionEndpointConventionBuilder MapDefaultControllerRoute(this IEndpointRouteBuilder endpoints)
   {
      EnsureControllerServices(endpoints);

      var dataSource = GetOrCreateDataSource(endpoints);
      return dataSource.AddRoute("default","{controller=Home}/{action=Index}/{id?}", defaults: null, constraints: null, dataTokens: null));
   }

   public static ControllerActionEndpointConventionBuilder MapControllerRoute(
      this IEndpointRouteBuilder endpoints, 
      string name, string pattern, 
      object defaults = null, 
      object constraints = null,
      object dataTokens = null)
   {
      EnsureControllerServices(endpoints);

      var dataSource = GetOrCreateDataSource(endpoints);
      return dataSource.AddRoute(name, pattern, new RouteValueDictionary(defaults), new RouteValueDictionary(constraints), new RouteValueDictionary(dataTokens));
   }

   public static ControllerActionEndpointConventionBuilder MapAreaControllerRoute(
      this IEndpointRouteBuilder endpoints,
      string name,
      string areaName,
      string pattern,
      object? defaults = null,
      object? constraints = null,
      object? dataTokens = null) 
   {
      var defaultsDictionary = new RouteValueDictionary(defaults);
      defaultsDictionary["area"] = defaultsDictionary["area"] ?? areaName;

      var constraintsDictionary = new RouteValueDictionary(constraints);
      constraintsDictionary["area"] = constraintsDictionary["area"] ?? new StringRouteConstraint(areaName);

      return endpoints.MapControllerRoute(name, pattern, defaultsDictionary, constraintsDictionary, dataTokens);
   }

   public static IEndpointConventionBuilder MapFallbackToController(this IEndpointRouteBuilder endpoints, string action, string controller) {
      EnsureControllerServices(endpoints);

      // called for side-effect to make sure that the data source is registered
      var dataSource = GetOrCreateDataSource(endpoints);
      dataSource.CreateInertEndpoints = true;
      RegisterInCache(endpoints.ServiceProvider, dataSource);

      // maps a fallback endpoint with an empty delegate, this is OK because we don't expect the delegate to run
      var builder = endpoints.MapFallback(context => Task.CompletedTask);
      builder.Add(b =>
      {
         // MVC registers a policy that looks for this metadata.
         b.Metadata.Add(CreateDynamicControllerMetadata(action, controller, area: null));
         b.Metadata.Add(new ControllerEndpointDataSourceIdMetadata(dataSource.DataSourceId));
      });

      return builder;
   }

   public static IEndpointConventionBuilder MapFallbackToAreaController(this IEndpointRouteBuilder endpoints, string pattern, string action, string controller, string area);

   public static void MapDynamicControllerRoute<TTransformer>(this IEndpointRouteBuilder endpoints, string pattern);

   public static void MapDynamicControllerRoute<TTransformer>(this IEndpointRouteBuilder endpoints, string pattern, object? state);

   public static void MapDynamicControllerRoute<TTransformer>(this IEndpointRouteBuilder endpoints, string pattern, object state, int order);

   private static void EnsureControllerServices(IEndpointRouteBuilder endpoints) 
   {
      var marker = endpoints.ServiceProvider.GetService<MvcMarkerService>();
      if (marker == null) {
         throw new InvalidOperationException(...);
      }
   }

   private static ControllerActionEndpointDataSource GetOrCreateDataSource(IEndpointRouteBuilder endpoints)
   {
      var dataSource = endpoints.DataSources.OfType<ControllerActionEndpointDataSource>().FirstOrDefault();
      if (dataSource == null) 
      {
         var orderProvider = endpoints.ServiceProvider.GetRequiredService<OrderedEndpointsSequenceProviderCache>();
         var factory = endpoints.ServiceProvider.GetRequiredService<ControllerActionEndpointDataSourceFactory>();
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
//----------------------------------------------------------Ʌ
```
```C#
//----------------------------------V
[Controller]
public abstract class ControllerBase   // a base class for an MVC controller without view support
{
   private ControllerContext _controllerContext;
   private IModelMetadataProvider _metadataProvider;
   private IModelBinderFactory _modelBinderFactory;
   private IObjectModelValidator _objectValidator;
   private IUrlHelper _url;
   private ProblemDetailsFactory _problemDetailsFactory;

   public HttpContext HttpContext => ControllerContext.HttpContext;

   public HttpResponse Response => HttpContext?.Response!;

   public RouteData RouteData => ControllerContext.RouteData;

   public ModelStateDictionary ModelState => ControllerContext.ModelState;

   [ControllerContext]
   public ControllerContext ControllerContext
   {
      get {
         if (_controllerContext == null) {
            _controllerContext = new ControllerContext();
         }
 
         return _controllerContext;
      }
      set {
         if (value == null) {
            throw new ArgumentNullException(nameof(value));
         }
 
         _controllerContext = value;
      }
   }

   public IModelMetadataProvider MetadataProvider
   {
      get {
         if (_metadataProvider == null)
         {
            _metadataProvider = HttpContext?.RequestServices?.GetRequiredService<IModelMetadataProvider>();
         }
      }

      set {
         _metadataProvider = value;
      }
   }

   public IModelBinderFactory ModelBinderFactory
   {
      get {
         if (_modelBinderFactory == null) {
            _modelBinderFactory = HttpContext?.RequestServices?.GetRequiredService<IModelBinderFactory>();
         }

         return _modelBinderFactory!;
      }
      set
      {
         _modelBinderFactory = value;
      }
   }

   public IUrlHelper Url {
      get {
         if (_url == null) {
            var factory = HttpContext?.RequestServices?.GetRequiredService<IUrlHelperFactory>();
            _url = factory?.GetUrlHelper(ControllerContext);
         }

         return _url!;
      }
      set {
         _url = value;
      }
   }

   public IObjectModelValidator ObjectValidator {
      get {
         if (_objectValidator == null) {
            _objectValidator = HttpContext?.RequestServices?.GetRequiredService<IObjectModelValidator>();
         }

         return _objectValidator!;
      }
      set {
         _objectValidator = value;
      }
   }

   public ProblemDetailsFactory ProblemDetailsFactory {
      get {
         if (_problemDetailsFactory == null) {
            _problemDetailsFactory = HttpContext?.RequestServices?.GetRequiredService<ProblemDetailsFactory>();
         }

         return _problemDetailsFactory!;
      }
      set {
         _problemDetailsFactory = value;
      }
   }

   public ClaimsPrincipal User => HttpContext?.User!;

   [NonAction]
   public virtual StatusCodeResult StatusCode([ActionResultStatusCode] int statusCode) => new StatusCodeResult(statusCode);

   [NonAction]
   public virtual ObjectResult StatusCode([ActionResultStatusCode] int statusCode, [ActionResultObjectValue] object? value) {
      return new ObjectResult(value) {
         StatusCode = statusCode
      };
   }
   
   [NonAction]
   public virtual ContentResult Content(string content, string contentType) => Content(content, MediaTypeHeaderValue.Parse(contentType));

   [NonAction]
   public virtual NoContentResult NoContent() => new NoContentResult();

   [NonAction]
   public virtual OkResult Ok() => new OkResult();

   [NonAction]
   public virtual OkObjectResult Ok([ActionResultObjectValue] object? value) => new OkObjectResult(value);

   [NonAction]
   public virtual RedirectResult Redirect(string url) {
      return new RedirectResult(url);
   }

   [NonAction]
   public virtual RedirectResult RedirectPermanent(string url) {
      return new RedirectResult(url, permanent: true);
   }

   [NonAction]
   public virtual RedirectResult RedirectPreserveMethod(string url) {
      return new RedirectResult(url: url, permanent: false, preserveMethod: true);
   }

   [NonAction]
   public virtual RedirectResult RedirectPermanentPreserveMethod(string url) {
      return new RedirectResult(url: url, permanent: true, preserveMethod: true);
   }

   [NonAction]
   public virtual LocalRedirectResult LocalRedirect(string localUrl) {
      return new LocalRedirectResult(localUrl);
   }

   [NonAction]
   public virtual RedirectToActionResult RedirectToAction(string? actionName, string? controllerName, object? routeValues, string? fragment) {
      return new RedirectToActionResult(actionName, controllerName, routeValues, fragment) { UrlHelper = Url };
   }

   // ...
}
//----------------------------------Ʌ
//------------------------V
public class ActionContext {  // context object for execution of action which has been selected as part of an HTTP request
   
   public ActionContext(ActionContext actionContext) : this(actionContext.HttpContext, actionContext.RouteData, actionContext.ActionDescriptor, actionContext.ModelState) { }
   
   public ActionContext(
      HttpContext httpContext,
      RouteData routeData,
      ActionDescriptor actionDescriptor,
      ModelStateDictionary modelState)
   {
      HttpContext = httpContext;
      RouteData = routeData;
      ActionDescriptor = actionDescriptor;
      ModelState = modelState;
   }

   public ActionDescriptor ActionDescriptor { get; set; } = default!;

   public HttpContext HttpContext { get; set; } = default!;

   public ModelStateDictionary ModelState { get; } = default!;

   public RouteData RouteData { get; set; } = default!;
}
//------------------------Ʌ
//----------------------------V

public class ControllerContext : ActionContext {   // the context associated with the current request for a controller
   
   private IList<IValueProviderFactory>? _valueProviderFactories;

   public ControllerContext() { }

   public ControllerContext(ActionContext context) : base(context) {
      if (!(context.ActionDescriptor is ControllerActionDescriptor)) {
         throw new ArgumentException("XXX");
      }
   }

   internal ControllerContext(HttpContext httpContext, RouteData routeData, ControllerActionDescriptor actionDescriptor) : base(httpContext, routeData, actionDescriptor) { }

   public new ControllerActionDescriptor ActionDescriptor {
      get { return (ControllerActionDescriptor)base.ActionDescriptor; }
      set { base.ActionDescriptor = value; }
   }

   public virtual IList<IValueProviderFactory> ValueProviderFactories {
      get {
         if (_valueProviderFactories == null) {
            _valueProviderFactories = new List<IValueProviderFactory>();
         }

         return _valueProviderFactories;
      }
      set {
         _valueProviderFactories = value;
      }
   }
}
//----------------------------Ʌ
```

Executor:

```C#
//-------------------------------------------------------------------------------------------hfgfgfgfgfgfgfgfgfgfgfgfgf
public interface IActionResultExecutor<in TResult> where TResult : notnull, IActionResult {
   Task ExecuteAsync(ActionContext context, TResult result);
}

public class ObjectResultExecutor : IActionResultExecutor<ObjectResult> 
{

}
//------------------------------------------------------------------------------------------
```

```C#
//---------------------------VVV
public interface IActionResult {
   Task ExecuteResultAsync(ActionContext context);  // called by MVC to process the result of an action method
}

public abstract class ActionResult : IActionResult {
   public virtual Task ExecuteResultAsync(ActionContext context) {
      ExecuteResult(context);
      return Task.CompletedTask;
   }

   public virtual void ExecuteResult(ActionContext context) {
      
   }
}

public interface IStatusCodeActionResult : IActionResult {
   int? StatusCode { get; }
}
//---------------------------ɅɅɅ

//-----------------------------------V
public partial class StatusCodeResult : ActionResult, IClientErrorActionResult {
   public StatusCodeResult([ActionResultStatusCode] int statusCode) {
      StatusCode = statusCode;
   }

   public int StatusCode { get; }

   int? IStatusCodeActionResult.StatusCode => StatusCode;

   public override void ExecuteResult(ActionContext context) {
      var httpContext = context.HttpContext;
      httpContext.Response.StatusCode = StatusCode;
   }
}
//-----------------------------------Ʌ
// StatusCodeResult example
public class NotFoundResult : StatusCodeResult {
   private const int DefaultStatusCode = StatusCodes.Status404NotFound;

   public NotFoundResult() : base(DefaultStatusCode) { }
}

public class BadRequestResult : StatusCodeResult {
   private const int DefaultStatusCode = StatusCodes.Status400BadRequest;

   public BadRequestResult() : base(DefaultStatusCode) { }
}
//-----------------------------------Ʌ

//-----------------------V
public class ObjectResult : ActionResult, IStatusCodeActionResult {
   private MediaTypeCollection _contentTypes;

   public ObjectResult(object? value) {
      Value = value;
      Formatters = new FormatterCollection<IOutputFormatter>();
      _contentTypes = new MediaTypeCollection();
   }

   [ActionResultObjectValue]
   public object? Value { get; set; }

   public FormatterCollection<IOutputFormatter> Formatters { get; set; }

   public MediaTypeCollection ContentTypes {
      get => _contentTypes;
      set => _contentTypes = value ?? throw new ArgumentNullException(nameof(value));
   }

   public Type? DeclaredType { get; set; }

   public int? StatusCode { get; set; }

   public override Task ExecuteResultAsync(ActionContext context) 
   {
      var executor = context.HttpContext.RequestServices.GetRequiredService<IActionResultExecutor<ObjectResult>>();
      return executor.ExecuteAsync(context, this);
   }

   public virtual void OnFormatting(ActionContext context) {
      if (Value is ProblemDetails details) {
         if (details.Status != null && StatusCode == null) {
            StatusCode = details.Status;
         } else if (details.Status == null && StatusCode != null) {
            details.Status = StatusCode;
         }
      }

      if (StatusCode.HasValue) {
         context.HttpContext.Response.StatusCode = StatusCode.Value;
      }
   }
}
//-----------------------Ʌ
// ObjectResult example
public class OkObjectResult : ObjectResult {
   private const int DefaultStatusCode = StatusCodes.Status200OK;

   public OkObjectResult(object? value) : base(value) {
      StatusCode = DefaultStatusCode;
   }
}
//-------------------------Ʌ
```
-------------------------------------------------------------------------------------------------------------------------

```C#
// example
public class HomeController : Controller
{
   private DataContext context;

   public HomeController(DataContext ctx)
   {
      context = ctx;
   }
   public async Task<IActionResult> Index(long id = 1)
   {
      return View(await context.Products.FindAsync(id));
   }
}
```

```C#
//-------------------V
public abstract class Controller : ControllerBase, IActionFilter, IFilterMetadata, IAsyncActionFilter, IDisposable
{
   private ITempDataDictionary _tempData;
   private DynamicViewData _viewBag;
   private ViewDataDictionary _viewData;

   [ViewDataDictionary]
   public ViewDataDictionary ViewData
   {
      get {
         return _viewData!;
      }

      set {
         _viewData = value;
      }
   }

   public ITempDataDictionary TempData
   {
      get {
         if (_tempData = null) 
         {
            var factory = HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();
            _tempData = factory.GetTempData(HttpContext);
         }

         return _tempData;
      }

      set {
         _tempData = value;
      }
   }

   public dynamic ViewBag
   {
      get {
         if (_viewBag == null)
            _viewBag = new DynamicViewData(() => ViewData);
         
         return _viewBag;
      }
   }

   [NonAction]
   public virtual ViewResult View()
   {
      return View(viewName: null);
   }

   [NonAction]
   public virtual ViewResult View(string viewName)
   {
      return View(viewName, model: ViewData.Model);
   }

   [NonAction]
   public virtual ViewResult View(object model)
   {
      return View(viewName: null, model: model);
   }

   [NonAction]
   public virtual ViewResult View(string viewName, object model)
   {
      ViewData.Model = model;

      return new ViewResult()
      {
         ViewName = viewName,
         ViewData = ViewData,
         TempData = TempData
      };
   }

   [NonAction]
   public virtual PartialViewResult PartialView()
   {
      return PartialView(viewName: null);
   }

   [NonAction]
   public virtual PartialViewResult PartialView(string viewName)
   {
      return PartialView(viewName, model: ViewData.Model);
   }

   [NonAction]
   public virtual PartialViewResult PartialView(object model)
   {
      return PartialView(viewName: null, model: model);
   }

   [NonAction]
   public virtual PartialViewResult PartialView(string viewName, object model)
   {
      ViewData.Model = model;

      return new PartialViewResult()
      {
         ViewName = viewName,
         ViewData = ViewData,
         TempData = TempData
      };
   }

   [NonAction]
   public virtual ViewComponentResult ViewComponent(string componentName, object arguments)
   {
      return new ViewComponentResult
      {
         ViewComponentName = componentName,
         Arguments = arguments,
         ViewData = ViewData,
         TempData = TempData
      };
   }

   [NonAction]
   public virtual JsonResult Json(object data)
   {
      return new JsonResult(data);
   }

   [NonAction]
   public virtual void OnActionExecuting(ActionExecutingContext context)
   {
   }

   [NonAction]
   public virtual void OnActionExecuted(ActionExecutedContext context)
   {   
   }

   [NonAction]
   public virtual Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
   {
      OnActionExecuting(context);
      if (context.Result == null)
      {
         var task = next();
         if (!task.IsCompletedSuccessfully)
         {
            return Awaited(this, task);
         }

         OnActionExecuted(task.Result);
      }

      return Task.CompletedTask;

      static async Task Awaited(Controller controller, Task<ActionExecutedContext> task)
      {
         controller.OnActionExecuted(await task);
      }
   }
}
//-------------------Ʌ

//---------------------V
public class ViewResult : ActionResult, IStatusCodeActionResult
{
   public int StatusCode { get; set; }

   public string ViewName { get; set; }

   public object Model => ViewData.Model;

   public ViewDataDictionary ViewData { get; set; } = default;

   public ITempDataDictionary TempData { get; set; } = default;

   public IViewEngine ViewEngine { get; set; }

   public string ContentType { get; set; }

   public override async Task ExecuteResultAsync(ActionContext context)
   {
      var executor = context.HttpContext.RequestServices.GetService<IActionResultExecutor<ViewResult>>();

      if (executor == null)
      {
         throw new InvalidOperationException(Mvc.Core.Resources.FormatUnableToFindServices(
            nameof(IServiceCollection),
            "AddControllersWithViews()",
            "ConfigureServices(...)"));
      }

      await executor.ExecuteAsync(context, this);
   }
}
//---------------------Ʌ
```

Razor views are converted into C# classes that inherit from the `RazorPage` class:

```html
<!DOCTYPE html>
<html>
<head>
    <link href="/lib/twitter-bootstrap/css/bootstrap.min.css" rel="stylesheet" />
</head>
<body>
    <h6 class="bg-primary text-white text-center m-2 p-2">Product Table</h6>
    <div class="m-2">
        <table class="table table-sm table-striped table-bordered">
            <tbody>
                <tr><th>Name</th><td>@Model.Name</td></tr>
                <tr><th>Price</th><td>@Model.Price.ToString("c")</td></tr>
                <tr><th>Category ID</th><td>@Model.CategoryId</td></tr>
            </tbody>
        </table>
    </div>
</body>
</html>
```

```C#
using Microsoft.AspNetCore.Mvc.Razor;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace AspNetCore
{
   public class Views_Home_Watersports : RazorPage<dynamic>
   {
      public async override Task ExecuteAsync()
      {
         WriteLiteral("<!DOCTYPE html>\r\n<html>\r\n");
         WriteLiteral("<head>");
         WriteLiteral(@"<link href=""/lib/twitter-bootstrap/css/bootstrap.min.css"" rel=""stylesheet"" />");
         WriteLiteral("</head>");
         WriteLiteral("<body>");
         WriteLiteral(@"<h6 class=""bg-secondary text-white text-center m-2 p-2"">Watersports</h6>\r\n<div class=""m-2"">\r\n<table class=""table table-sm table-striped table-bordered"">\r\n<tbody>\r\n>");
         WriteLiteral("<th>Name</th><td>");
         Write(Model.Name);
         WriteLiteral("</td></tr>");
         WriteLiteral("<tr><th>Price</th><td>");
         Write(Model.Price.ToString("c"));
         WriteLiteral("</td></tr>\r\n<tr><th>Category ID</th><td>");
         Write(Model.CategoryId);
         WriteLiteral("</td></tr>\r\n</tbody>\r\n</table>\r\n</div>");
         WriteLiteral("</body></html>");
      }
      public IUrlHelper Url { get; private set; }
      public IViewComponentHelper Component { get; private set; }
      public IJsonHelper Json { get; private set; }
      public IHtmlHelper<dynamic> Html { get; private set; }
      public IModelExpressionProvider ModelExpressionProvider { get; private set; }
   }
}

public interface IRazorPage
{
   // jhjhjhjhjh
}

public abstract class RazorPageBase : IRazorPage
{
   public string Layout { get; set; }
   
   protected virtual IHtmlContent RenderBody();
}

public abstract class RazorPage<TModel> : RazorPage
{
   public TModel Model => ViewData.Model;

   [RazorInject]
   public ViewDataDictionary<TModel> ViewData { get; set; } = default;
}

public abstract class RazorPage : RazorPageBase
{
   // dfdgfjdfjdfhdjfhjhh
}
```

```C#
//--------------V
public interface IActionResultExecutor<TResult> where TResult : IActionResult 
{
   Task ExecuteAsync(ActionContext context, TResult result);
}

public class ViewExecutor
{
   public static readonly string DefaultContentType = "text/html; charset=utf-8";

   public ViewExecutor(
      IOptions<MvcViewOptions> viewOptions,
      IHttpResponseStreamWriterFactory writerFactory,
      ICompositeViewEngine viewEngine,
      ITempDataDictionaryFactory tempDataFactory,
      DiagnosticListener diagnosticListener,
      IModelMetadataProvider modelMetadataProvider)
      : this(writerFactory, viewEngine, diagnosticListener)
   {
      ViewOptions = viewOptions.Value;
      TempDataFactory = tempDataFactory;
      ModelMetadataProvider = modelMetadataProvider;
   }

   protected DiagnosticListener DiagnosticListener { get; }
   protected ITempDataDictionaryFactory TempDataFactory { get; }
   protected IViewEngine ViewEngine { get; }
   protected MvcViewOptions ViewOptions { get; }
   protected IModelMetadataProvider ModelMetadataProvider { get; }
   protected IHttpResponseStreamWriterFactory WriterFactory { get; }

   public virtual async Task ExecuteAsync(ActionContext actionContext, IView view, ViewDataDictionary viewData, ITempDataDictionary tempData, string contentType, int statusCode) 
   {
      var viewContext = new ViewContext(actionContext, view, viewData, tempData, TextWriter.Null, ViewOptions.HtmlHelperOptions);
 
      await ExecuteAsync(viewContext, contentType, statusCode);
   }

   protected async Task ExecuteAsync(ViewContext viewContext, string contentType, int statusCode)
   {
      var response = viewContext.HttpContext.Response;
 
      ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
         contentType,
         response.ContentType,
         (DefaultContentType, Encoding.UTF8),
         MediaType.GetEncoding,
         out var resolvedContentType,
         out var resolvedContentTypeEncoding);
 
      response.ContentType = resolvedContentType;
 
      if (statusCode != null)
      {
         response.StatusCode = statusCode.Value;
      }
 
      OnExecuting(viewContext);

      using (var writer = WriterFactory.CreateWriter(response.Body, resolvedContentTypeEncoding))
      {
         var view = viewContext.View;
 
         var oldWriter = viewContext.Writer;
         try
         {
            viewContext.Writer = writer;
 
            DiagnosticListener.BeforeView(view, viewContext);
 
            await view.RenderAsync(viewContext);
 
            DiagnosticListener.AfterView(view, viewContext);
         }
         finally
         {
            viewContext.Writer = oldWriter;
         }
 
         // perf: Invoke FlushAsync to ensure any buffered content is asynchronously written to the underlying
         // response asynchronously. In the absence of this line, the buffer gets synchronously written to the
         // response as part of the Dispose which has a perf impact.
         await writer.FlushAsync();
      }
   }

   private static void OnExecuting(ViewContext viewContext)
   {
      var viewDataValuesProvider = viewContext.HttpContext.Features.Get<IViewDataValuesProviderFeature>();
      if (viewDataValuesProvider != null)
      {
         viewDataValuesProvider.ProvideViewDataValues(viewContext.ViewData);
      }
   }
}

public partial class ViewResultExecutor : ViewExecutor, IActionResultExecutor<ViewResult>
{
   private const string ActionNameKey = "action";

   public ViewResultExecutor(
      IOptions<MvcViewOptions> viewOptions,
      IHttpResponseStreamWriterFactory writerFactory,
      ICompositeViewEngine viewEngine,
      ITempDataDictionaryFactory tempDataFactory,
      DiagnosticListener diagnosticListener,
      ILoggerFactory loggerFactory,
      IModelMetadataProvider modelMetadataProvider)
      : base(viewOptions, writerFactory, viewEngine, tempDataFactory, diagnosticListener, modelMetadataProvider)
   {
      if (loggerFactory == null)
      {
         throw new ArgumentNullException(nameof(loggerFactory));
      }
 
      Logger = loggerFactory.CreateLogger<ViewResultExecutor>();
   }

   protected ILogger Logger { get; }

   public virtual ViewEngineResult FindView(ActionContext actionContext, ViewResult viewResult)
   {
      var viewEngine = viewResult.ViewEngine ?? ViewEngine;

      var viewName = viewResult.ViewName ?? GetActionName(actionContext) ?? string.Empty;

      var stopwatch = ValueStopwatch.StartNew();

      var result = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: true);
      var originalResult = result;
      if (!result.Success)
      {
         result = viewEngine.FindView(actionContext, viewName, isMainPage: true);
      }

      Log.ViewResultExecuting(Logger, result.ViewName);
      if (!result.Success)
      {
         if (originalResult.SearchedLocations.Any())
         {
            if (result.SearchedLocations.Any())
            {
               // return a new ViewEngineResult listing all searched locations.
               var locations = new List<string>(originalResult.SearchedLocations);
               locations.AddRange(result.SearchedLocations);
               result = ViewEngineResult.NotFound(viewName, locations);
            }
            else
            {
               // GetView() searched locations but FindView() did not. Use first ViewEngineResult.
               result = originalResult;
            }
         }
      }

      // ...
      return result;
   }

   // ...

   public async Task ExecuteAsync(ActionContext context, ViewResult result)
   {
      var stopwatch = ValueStopwatch.StartNew();

      var viewEngineResult = FindView(context, result);
      viewEngineResult.EnsureSuccessful(originalLocations: null);

      var view = viewEngineResult.View;
      using (view as IDisposable)
      {
         await ExecuteAsync(context, view, result.ViewData, result.TempData, result.ContentType, result.StatusCode);
      }
   }

   private static string GetActionName(ActionContext context)
   {
      if (!context.RouteData.Values.TryGetValue(ActionNameKey, out var routeValue))
         return null;

      var actionDescriptor = context.ActionDescriptor;
      string normalizedValue = null;
      if (actionDescriptor.RouteValues.TryGetValue(ActionNameKey, out var value) && string.IsNullOrEmpty(value))
      {
         normalizedValue = value;
      }
 
      var stringRouteValue = Convert.ToString(routeValue, CultureInfo.InvariantCulture);
      if (string.Equals(normalizedValue, stringRouteValue, StringComparison.OrdinalIgnoreCase))
      {
         return normalizedValue;
      }
 
      return stringRouteValue;
   }
}
//--------------Ʌ


//--------------V
public interface IViewEngine
{
   ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage);
   ViewEngineResult GetView(string? executingFilePath, string viewPath, bool isMainPage);
}

public interface IRazorViewEngine : IViewEngine
{
   RazorPageResult FindPage(ActionContext context, string pageName);
   RazorPageResult GetPage(string executingFilePath, string pagePath);
   string GetAbsolutePath(string? executingFilePath, string? pagePath);
}
//--------------Ʌ

//------------------V
public partial class RazorViewEngine : IRazorViewEngine
{
   public static readonly string ViewExtension = ".cshtml";
   private const string AreaKey = "area";
   private const string ControllerKey = "controller";
   private const string PageKey = "page";

   private static readonly TimeSpan _cacheExpirationDuration = TimeSpan.FromMinutes(20);

   private readonly IRazorPageFactoryProvider _pageFactory;
   private readonly IRazorPageActivator _pageActivator;
   private readonly HtmlEncoder _htmlEncoder;
   private readonly ILogger _logger;
   private readonly RazorViewEngineOptions _options;
   private readonly DiagnosticListener _diagnosticListener;

   public RazorViewEngine(IRazorPageFactoryProvider pageFactory, 
        IRazorPageActivator pageActivator, 
        HtmlEncoder htmlEncoder, 
        IOptions<RazorViewEngineOptions> optionsAccessor,
        ILoggerFactory loggerFactory,
        DiagnosticListener diagnosticListener)
   {
      _options = optionsAccessor.Value;
       _pageFactory = pageFactory;
      _pageActivator = pageActivator;
      _htmlEncoder = htmlEncoder;
      _logger = loggerFactory.CreateLogger<RazorViewEngine>();
      _diagnosticListener = diagnosticListener;
      ViewLookupCache = new MemoryCache(new MemoryCacheOptions());
   }

   internal void ClearCache()
   {
      ViewLookupCache = new MemoryCache(new MemoryCacheOptions());
   }

   protected internal IMemoryCache ViewLookupCache { get; private set; }

   public static string GetNormalizedRouteValue(ActionContext context, string key)
        => NormalizedRouteValue.GetNormalizedRouteValue(context, key);

   public RazorPageResult FindPage(ActionContext context, string pageName)
   {
      if (IsApplicationRelativePath(pageName) || IsRelativePath(pageName))
      {
         // a path; not a name this method can handle.
         return new RazorPageResult(pageName, Enumerable.Empty<string>());
      }

      var cacheResult = LocatePageFromViewLocations(context, pageName, isMainPage: false);
      if (cacheResult.Success)
      {
         var razorPage = cacheResult.ViewEntry.PageFactory();
         return new RazorPageResult(pageName, razorPage);
      }
      else
      {
         return new RazorPageResult(pageName, cacheResult.SearchedLocations!);
      }
   }

   public RazorPageResult GetPage(string executingFilePath, string pagePath)
   {
      if (!(IsApplicationRelativePath(pagePath) || IsRelativePath(pagePath)))
      {
         // Not a path this method can handle.
         return new RazorPageResult(pagePath, Enumerable.Empty<string>());
      }
 
      var cacheResult = LocatePageFromPath(executingFilePath, pagePath, isMainPage: false);
      if (cacheResult.Success)
      {
         var razorPage = cacheResult.ViewEntry.PageFactory();
         return new RazorPageResult(pagePath, razorPage);
      }
      else
      {
         return new RazorPageResult(pagePath, cacheResult.SearchedLocations!);
      }
   }

   public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
   {
      if (IsApplicationRelativePath(viewName) || IsRelativePath(viewName))
      {
         // a path; not a name this method can handle.
         return ViewEngineResult.NotFound(viewName, Enumerable.Empty<string>());
      }
 
      var cacheResult = LocatePageFromViewLocations(context, viewName, isMainPage);
      return CreateViewEngineResult(cacheResult, viewName);
   }

   public ViewEngineResult GetView(string executingFilePath, string viewPath, bool isMainPage)
   {
      if (string.IsNullOrEmpty(viewPath))
      {
         throw new ArgumentException(Resources.ArgumentCannotBeNullOrEmpty, nameof(viewPath));
      }
 
      if (!(IsApplicationRelativePath(viewPath) || IsRelativePath(viewPath)))
      {
         // Not a path this method can handle.
         return ViewEngineResult.NotFound(viewPath, Enumerable.Empty<string>());
      }
 
      var cacheResult = LocatePageFromPath(executingFilePath, viewPath, isMainPage);
      return CreateViewEngineResult(cacheResult, viewPath);
    }

    // ...
}
//------------------Ʌ

//----------V
public class ViewEngineResult
{
   private ViewEngineResult(string viewName)
   {
      ViewName = viewName;
   }

   public IEnumerable<string> SearchedLocations { get; private init; } = Enumerable.Empty<string>();

   public IView View { get; private init; }

   public string ViewName { get; private set; }

   public bool Success => View != null;

   public static ViewEngineResult NotFound(string viewName, IEnumerable<string> searchedLocations)
   {
      return new ViewEngineResult(viewName)
      {
         SearchedLocations = searchedLocations
      };
   }

   public static ViewEngineResult Found(string viewName, IView view)
   {
      return new ViewEngineResult(viewName) { View = view };
   }

   public ViewEngineResult EnsureSuccessful(IEnumerable<string>? originalLocations)
   {
      if (!Success)
      {
         var locations = string.Empty;
         if (originalLocations != null && originalLocations.Any())
         {
            locations = Environment.NewLine + string.Join(Environment.NewLine, originalLocations);
         }
 
         if (SearchedLocations.Any())
         {
            locations += Environment.NewLine + string.Join(Environment.NewLine, SearchedLocations);
         }
 
         throw new InvalidOperationException(Resources.FormatViewEngine_ViewNotFound(ViewName, locations));
      }
 
      return this;
   }
}
//----------Ʌ
```

------------------------------------------------------------------------------------------------------------------------

**Razor View Engine**

```C#
public interface IViewEngine
{
   ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage);
   ViewEngineResult GetView(string executingFilePath, string viewPath, bool isMainPage);
}

public interface IRazorViewEngine : IViewEngine
{
   RazorPageResult FindPage(ActionContext context, string pageName);
   RazorPageResult GetPage(string executingFilePath, string pagePath);
   string GetAbsolutePath(string? executingFilePath, string? pagePath);
}

//----------------------------------V
public partial class RazorViewEngine : IRazorViewEngine
{
   public static readonly string ViewExtension = ".cshtml";
   private const string AreaKey = "area";
   private const string ControllerKey = "controller";
   private const string PageKey = "page";

   private static readonly TimeSpan _cacheExpirationDuration = TimeSpan.FromMinutes(20);

   private readonly IRazorPageFactoryProvider _pageFactory;
   private readonly IRazorPageActivator _pageActivator;
   private readonly HtmlEncoder _htmlEncoder;
   private readonly ILogger _logger;
   private readonly RazorViewEngineOptions _options;
   private readonly DiagnosticListener _diagnosticListener;

   public RazorViewEngine(
        IRazorPageFactoryProvider pageFactory,
        IRazorPageActivator pageActivator,
        HtmlEncoder htmlEncoder,
        IOptions<RazorViewEngineOptions> optionsAccessor,
        ILoggerFactory loggerFactory,
        DiagnosticListener diagnosticListener)
   {
      _options = optionsAccessor.Value;
       _pageFactory = pageFactory;
      _pageActivator = pageActivator;
      _htmlEncoder = htmlEncoder;
      _logger = loggerFactory.CreateLogger<RazorViewEngine>();
      _diagnosticListener = diagnosticListener;
      ViewLookupCache = new MemoryCache(new MemoryCacheOptions());
   }


   // jkhgdfhgfkjldshfsdhfnsdjkhfjkldshfldsjhflksdjhflkjsdhfljkdsh
}
//----------------------------------Ʌ
```

## Demystifying ExecuteResultAsync Process


```C#
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
   private DataContext context;

   public ProductsController(DataContext ctx)
   {
      context = ctx;
   }

   [HttpGet("{id}")]
   public async Task<IActionResult> GetProduct(long id)
   {
      Product p = await context.Products.FindAsync(id);
      if (p == null)
         return NotFound();
      return Ok(p);
   }

   [HttpPut]
   public async Task UpdateProduct([FromBody] Product product)
   {
      context.Products.Update(product);
      await context.SaveChangesAsync();
   }

   [HttpGet("redirect")]
   public IActionResult Redirect()
   {
      //return Redirect("/api/products/1");
      return RedirectToAction(nameof(GetProduct), new { id = 1 }); 
   }
}
//------------------------------------------------------------------
public abstract class ControllerBase  
{
   [NonAction]
   public virtual OkObjectResult Ok([ActionResultObjectValue] object? value) => new OkObjectResult(value);

   //...
}

//----------------------------------------------------------------V
public interface IActionResult
{
   Task ExecuteResultAsync(ActionContext context);
}

public abstract class ActionResult : IActionResult
{
   public virtual Task ExecuteResultAsync(ActionContext context)
   {
      ExecuteResult(context);
      return Task.CompletedTask;
   }

   public virtual void ExecuteResult(ActionContext context)
   {
      // empty
   }
}

public class ObjectResult : ActionResult, IStatusCodeActionResult
{
   private MediaTypeCollection _contentTypes;

   public ObjectResult(object? value)
   {
      Value = value;
      Formatters = new FormatterCollection<IOutputFormatter>();
      _contentTypes = new MediaTypeCollection();
   }

   public object Value { get; set; }

   public FormatterCollection<IOutputFormatter> Formatters { get; set; }

   public MediaTypeCollection ContentTypes
   {
      get => _contentTypes;
      set => _contentTypes = value ?? throw new ArgumentNullException(nameof(value));
   }

   public Type DeclaredType { get; set; }

   public int StatusCode { get; set; }

   public override Task ExecuteResultAsync(ActionContext context)
   {
      var executor = context.HttpContext.RequestServices.GetRequiredService<IActionResultExecutor<ObjectResult>>();
      return executor.ExecuteAsync(context, this);
   }

   public virtual void OnFormatting(ActionContext context)
   {
      if (Value is ProblemDetails details)
      {
         if (details.Status != null && StatusCode == null)
         {
            StatusCode = details.Status;
         }
         else if (details.Status == null && StatusCode != null)
         {
            details.Status = StatusCode;
         }
      }

      if (StatusCode.HasValue)
      {
         context.HttpContext.Response.StatusCode = StatusCode.Value;
      }
   }
}

public class OkObjectResult : ObjectResult
{
   private const int DefaultStatusCode = StatusCodes.Status200OK;
   
   public OkObjectResult(object value) : base(value)
   {
      StatusCode = DefaultStatusCode;
   }
}
//----------------------------------------------------------------Ʌ
```























compare endpoints.MapControllers() and endpoints.MapDefaultControllerRoute() to see if MapControllers() is still needed when using MapDefaultControllerRoute()




Now whenever you run your application and try to access a view then what the Razor View Engine does is, it converts the View code into a C# class file. It will not convert the view to a C# class until and unless you try to access the view.



-Q
how does output get generated
how does ActionContext get created
how IActionResult get rendered, combined with previous restful web api chapter

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