Chapter 23-Using Razor Pages
=================================

```C#
public class Startup
{
   // ...
   public void ConfigureServices(IServiceCollection services)
   {
      // ...
      services.AddRazorPages().AddRazorRuntimeCompilation();
   }
   public void Configure(IApplicationBuilder app, DataContext context)
   {
      // ...
      app.UseRouting();
      app.UseEndpoints(endpoints => {
         endpoints.MapRazorPages();
      });
   }
}
```

```C#
@page  "{id:long}"            // @page is used to indicate this is not a Controller related view file
@model IndexModel
@using Microsoft.AspNetCore.Mvc.RazorPages
@using WebApp.Models;
//@inject DataContext context;  // IndexModel is not needed if we just want to inject sth and use it

<!DOCTYPE html>
<html>
<body>
    <div class="bg-primary text-white text-center m-2 p-2">@Model.Product.Name</div>
    <a href="/handlerselector?handler=related" class="btn btn-primary">Related</a>
</body>
</html>

@functions {    //  @functions add the class to the view compiled C# class                           
    public class IndexModel : PageModel
    {
        private DataContext context;
        public Product Product { get; set; }
        
        public IndexModel(DataContext ctx)
        {
            context = ctx;
        }

        /*
        public void Task OnGet(long id = 1)
        {
            Product = context.Products.Find(id);
        }
        */

        public async Task OnGetAsync(long id = 1)
        {
            Product = await context.Products.FindAsync(id);
        }

        public async Task<IActionResult> OnPostAsync(long id, decimal price)
        {
           Product p = await context.Products.FindAsync(id);
           p.Price = price;
           await context.SaveChangesAsync();
           return RedirectToPage();   // for post redirection
        }

        public async Task OnGetRelatedAsync(long id = 1)   // can be chosed by "handler" route value
        {
           Product = await context.Products.Include(p => p.Supplier).Include(p => p.Category).FirstOrDefaultAsync(p => p.ProductId == id);
           Product.Supplier.Products = null;
           Product.Category.Products = null;
        }
    }
}
```

gets compiled into:

```C#
namespace AspNetCore   // default namespace which can be changed by @namespace
{
   public class Pages_Index : Page
   {
      public ViewDataDictionary<IndexModel> ViewData => (ViewDataDictionary<IndexModel>)PageContext?.ViewData;  // <------ A 
      public IndexModel Model => ViewData.Model;

      public async override Task ExecuteAsync()
      {
         // ...
         WriteLiteral("<body>");
         WriteLiteral("<div class=\"bg-primary text-white text-center m-2 p-2\">")
         Write(Model.Product.Name);
         // ...
      }

      public class IndexModel : PageModel  // added by @functions
      {
         // ...
      }

      public IUrlHelper Url { get; private set; }
      public IViewComponentHelper Component { get; private set; }
      public IJsonHelper Json { get; private set; }
      public IHtmlHelper<IndexModel> Html { get; private set; }
      public IModelExpressionProvider ModelExpressionProvider { get; private set; }
   }
}
```

Note that `IndexModel` "code behind file" can be sepearated from the cshtml file and have its own namespace different than the view generated C# class whose default namespace can be changed by using e.g. `@namespace XXX` where "XXX" is the same namespace you use in the sepearate Model class. If you use a sepearate model class (you won't use AspNetCore, will you?) and don't use `@namespace XXX` in the ViewImports.cshtml file, A's `<IndexModel>` part will have an error, once you use `@namespace XXX`, the view generated class and the separate model class will be under the same namespace
`

## Source Code

```C#

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