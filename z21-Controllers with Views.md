Chapter 21-Using Controllers with Views
=================================

```C#
public class Startup
{
   // ...
   public void ConfigureServices(IServiceCollection services)
   {
      services.AddDbContext<DataContext>(opts => {
         opts.UseSqlServer(Configuration[
         "ConnectionStrings:ProductConnection"]);
         opts.EnableSensitiveDataLogging(true);
      });
      services.AddControllersWithViews().AddRazorRuntimeCompilation();
   }
   public void Configure(IApplicationBuilder app, DataContext context)
   {
      app.UseDeveloperExceptionPage();
      app.UseStaticFiles();
      app.UseRouting();
      app.UseEndpoints(endpoints => {
         //endpoints.MapControllers();
         endpoints.MapControllerRoute("Default",
         "{controller=Home}/{action=Index}/{id?}");
      });
   }
}

/*
MapControllers only supports attribute routing
MapControllerRoute() is MapControllers() + ControllerActionEndpointDataSource.AddRoute(name, pattern...)
MapDefaultControllerRoute() is just a wrapper of MapControllerRoute() with default name ("Default), and default pattern ("{controller=Home}/{action=Index}/{id?}")
*/  
```

```C#
public class HomeController : Controller
{
   private DataContext context;
   public HomeController(DataContext ctx)
   {
      context = ctx;
   }

   public async Task<IActionResult> Index(long id = 1)
   {
      ViewBag.AveragePrice = await context.Products.AverageAsync(p => p.Price);
      return View(await context.Products.FindAsync(id));   
   }

   public IActionResult Common()
   {
      return View();
   }

   public IActionResult Html()
   {
      return View((object)"This is a <h3><i>string</i></h3>");
   }

   public IActionResult Cube(double num) 
   {
      TempData["value"] = num.ToString();
      TempData["result"] = Math.Pow(num, 3).ToString();
      return RedirectToAction(nameof(Index));
   }

   public IActionResult CubeByAttribute(double num) 
   {
      Value = num.ToString();
      Result = Math.Pow(num, 3).ToString();
      return RedirectToAction(nameof(Index));
   }

   [TempData]
   public string Value { get; set; }

   [TempData]
   public string Result { get; set; }
}
```


`_ViewImports.cshtml`  in `\Views\_ViewImports.cshtml`

```C#
@using WebApp.Models
```


 `_Layout.cshtml` File in the `Views/Shared` Folder

 ```HTML
 <!DOCTYPE html>
<html>
   <head>
      <title>@ViewBag.Title</title>   <!--the ViewBag property set in a specified view can be passed to the generic Layout-->
      <link href="/lib/twitter-bootstrap/css/bootstrap.min.css" rel="stylesheet" />
   </head>
   <body>
      <h6 class="bg-primary text-white text-center m-2 p-2">Shared View</h6>
      @RenderBody()
   </body>
   <h6 class="bg-primary text-white text-center m-2 p-2">
      @RenderSection("Footer", false)   <!--the second argument false means it is optional-->
   </h6>
   @if (IsSectionDefined("Summary"))   <!--<------------------>
   {
      @RenderSection("Summary", false)  
   } 
   else 
   {
      <div class="bg-info text-center text-white m-2 p-2">This is the default summary</div>
   }
</html>
 ```


 `_ViewStart.cshtml` File in the Views Folder

```C#
@{
    Layout = "_Layout";
}
```


`Views/Home/Index.cshtml`

```HTML
@Product     <!--it should have been @WebApp.Models.Product but it can be simplied because of the _ViewImports.cshtml-->
@{
    Layout = "_Layout";   <!--doesn't really need this one because of the  _ViewStart.cshtml-->
    ViewBag.Title = ViewBag.Title ?? "Product Table";  <!--use null collapse operator in case the action method might have already defined Title in the ViewBag-->
}
<!DOCTYPE html>
<html>
   <head>
      <link href="/lib/twitter-bootstrap/css/bootstrap.min.css" rel="stylesheet" />
   </head>
   <body>
      <table class="table table-sm table-striped table-bordered">
         <tbody>
            <tr>
               <th>Name</th>
               <td>@Model.Name</td>
            </tr>
            <tr>
               <th>Price</th>
               <td>
                  @Model.Price.ToString("c")
                  (@(((Model.Price / ViewBag.AveragePrice) * 100).ToString("F2")) % of average price)
               </td>
            </tr>
         </tbody>
      </table>
      <form method="get" action="/cubed/cube" class="m-2">
         <div class="form-group">
            <label>Value</label>
            <input name="num" class="form-control" value="@(TempData["value"])" />
         </div>
         <button class="btn btn-primary" type="submit">Submit</button>
      </form>
      @if (TempData["result"] != null) {
      <div class="bg-info text-white m-2 p-2">
         The cube of @TempData["value"] is @TempData["result"]
      </div>
      }
   </body>
</html>
```

get translated into a C# class file (in `obj/Debug/netcoreapp3.0/Razor/Views` folder):

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
         WriteLiteral(@"<h6 class=""bg-secondary text-white text-center m-2 p-2"">Watersports</h6>\r\n<div class=""m-2"">\r\n<table class=""table table-sm table-striped");
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
```


## Demystifying Razor Engine

View Engine comprises three components: 
* `ViewEngine` class which is responsible for locating view templates
* `IView` class which is responsible for combining the template with data and then converting it to HTML markup to be rendered in the web browser
* Template Parser which is to compile the cshtml view into executable code.


1. `Controller`'s `View()` method is called, which create a new instance of `ViewResult`

```C#
public class HomeController : Controller
{ 
   public async Task<IActionResult> Index(long id = 1)
   {
      ViewBag.AveragePrice = await context.Products.AverageAsync(p => p.Price);
      return View(await context.Products.FindAsync(id));   
   }
}
```

```C#
public abstract class Controller : ControllerBase, IActionFilter, IFilterMetadata, IAsyncActionFilter, IDisposable
{
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
}
```

2. `ViewResult.ExecuteResultAsync(ActionContext context)` called (implements `IActionResult.ExecuteResultAsync(ActionContext context)`)

```C#
public class ViewResult : ActionResult, IStatusCodeActionResult
{
   public int StatusCode { get; set; }

   public string ViewName { get; set; }

   public object Model => ViewData.Model;

   public ViewDataDictionary ViewData { get; set; } = default;

   public ITempDataDictionary TempData { get; set; } = default;

   public IViewEngine ViewEngine { get; set; }

   public string ContentType { get; set; }

   public override async Task ExecuteResultAsync(ActionContext context)    // implements IActionResult.ExecuteResultAsync(ActionContext context)
   {
       // get ViewResultExecutor/ViewExecutor to call ExecuteAsync
      var executor = context.HttpContext.RequestServices.GetService<IActionResultExecutor<ViewResult>>();   //  <---------------------------------------------2.1 
      await executor.ExecuteAsync(context, this);                                                           //  <---------------------------------------------2.2                   
   }
}

public class ViewExecutor
{
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

   public virtual async Task ExecuteAsync(ActionContext actionContext, IView view, ViewDataDictionary viewData, ITempDataDictionary tempData, string contentType, int statusCode) 
   {
      // ViewEngineResult.View is passed to ViewContext instance
      var viewContext = new ViewContext(actionContext, view, viewData, tempData, TextWriter.Null, ViewOptions.HtmlHelperOptions);  // <----------------- ViewContext created
 
      await view.RenderAsync(viewContext);  // <------------------------------- 2.6.
   }
}

public partial class ViewResultExecutor : ViewExecutor, IActionResultExecutor<ViewResult>
{
   public virtual ViewEngineResult FindView(ActionContext actionContext, ViewResult viewResult)
   {
      var viewEngine = viewResult.ViewEngine ?? ViewEngine;
 
      var viewName = viewResult.ViewName ?? GetActionName(actionContext) ?? string.Empty;

      // use ViewEngine Property from ViewResult to find a View which represented as ViewEngineResult
      ViewEngineResult result = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: true);  //  <---------------------------------------------2.4

      return result;
   }

   private static string GetActionName(ActionContext context);

   public async Task ExecuteAsync(ActionContext context, ViewResult result)     //  <---------------------------------------------2.3
   {
      var viewEngineResult = FindView(context, result); // use ViewEngine Property from ViewResult to find a View which represented as ViewEngineResult   

      RazorView view = viewEngineResult.View;  // <------------------------------- 2.5, this view contain the complied cshtml file represneted as RazorView.RazorPage

      await ExecuteAsync(context, view, result.ViewData, result.TempData, result.ContentType, result.StatusCode);   // <--------- calling base class ViewExecutor.ExecuteAsync()

   }
}
```

3.  `RazorView.RenderAsync(ViewContext context)` executed

```C#
public class RazorView : IView     // RazorView is like a wrapper of RazorPage
{
   private readonly IRazorViewEngine _viewEngine;
   // ... 

   // simplified constructor
   public RazorView(IRazorViewEngine viewEngine, 
                    IReadOnlyList<IRazorPage> viewStartPages, 
                    IRazorPage razorPage) 
   { 
      _viewEngine = viewEngine;
      ViewStartPages = viewStartPages;
      RazorPage = razorPage;
   }

   public string Path => RazorPage.Path;

   public IRazorPage RazorPage { get; }  // <--------------------- this is the compiled cshtml file
   
   public IReadOnlyList<IRazorPage> ViewStartPages { get; }

   // ...

   public virtual async Task RenderAsync(ViewContext context)  
   {
      // ...
      var bodyWriter = await RenderPageAsync(RazorPage, context, invokeViewStarts: true);    // <------------------------------------ 3.1
      await RenderLayoutAsync(context, bodyWriter);
   }

   private async Task<ViewBufferTextWriter> RenderPageAsync(IRazorPage page, ViewContext context, bool invokeViewStarts)
   {
      // ...
      await RenderViewStartsAsync(context);

      await RenderPageCoreAsync(page, context);
      // ...
   }

   private async Task RenderPageCoreAsync(IRazorPage page, ViewContext context)    // <------------------------------------ 3.2
   {
      page.ViewContext = context;   // <----------------------------
      // ...
      await page.ExecuteAsync();    // <--------------------------------------- 3.3
   }

   private async Task RenderViewStartsAsync(ViewContext context)
   {
      // ...
      RazorPage.Layout = xxx;  // modify RazorPage instance
   }
   // ...
}
//--------------------Ʌ
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