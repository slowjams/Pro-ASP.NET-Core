z19-Creating RESTful Web Services
=================================

Endpoint Approach (tedious):
```C#
public class Startup {
   public void ConfigureServices(IServiceCollection services) {
      services.AddDbContext<DataContext>(opts => {
         opts.UseSqlServer(Configuration["ConnectionStrings:ProductConnection"]);
         opts.EnableSensitiveDataLogging(true);
      });

      services.AddControllers();
   }

   public void Configure(IApplicationBuilder app, DataContext context) {
      // ...
      app.UseRouting();

      app.UseEndpoints(endpoints => {
         endpoints.MapGet("/", async context => {
            await context.Response.WriteAsync("Hello World!");
         });

         endpoints.MapWebService();   // <------------------------------
      });
   }
}

public static class WebServiceEndpoint {
   private static string BASEURL = "api/products";

   public static void MapWebService(this IEndpointRouteBuilder app) {
      app.MapGet($"{BASEURL}/{{id}}", async context => {
         long key = long.Parse(context.Request.RouteValues["id"] as string);
         DataContext data = context.RequestServices.GetService<DataContext>();
         Product p = data.Products.Find(key);
         if (p == null) {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
         } else {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize<Product>(p));
         }
      });

      app.MapGet(BASEURL, async context => {
         DataContext data = context.RequestServices.GetService<DataContext>();
         context.Response.ContentType = "application/json";
         await context.Response.WriteAsync(JsonSerializer.Serialize<IEnumerable<Product>>(data.Products));
      });

      app.MapPost(BASEURL, async context => {
         DataContext data = context.RequestServices.GetService<DataContext>();
         Product p = await JsonSerializer.DeserializeAsync<Product>(context.Request.Body);
         await data.AddAsync(p);
         await data.SaveChangesAsync();
         context.Response.StatusCode = StatusCodes.Status200OK;
      });
   }
}
```

Chapter 13 Source Code:

```C#
public class Startup
{
   public Startup(IConfiguration config)
   {
      Configuration = config;
   }

   public IConfiguration Configuration { get; set; }

   public void ConfigureServices(IServiceCollection services)
   {
      services.AddDbContext<DataContext>(opts => {
         opts.UseSqlServer(Configuration["ConnectionStrings:ProductConnection"]);
         opts.EnableSensitiveDataLogging(true);
      });

      services.AddControllers();
      services.Configure<JsonOptions>(options => {
         options.JsonSerializerOptions.IgnoreNullValues = true;
      });
   }

   public void Configure(IApplicationBuilder app, DataContext context) { // ... }
}
```

```C#
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using WebApp.Models;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace WebApp.Controllers
{
   // [ApiController] if used , then no need to use [FromBody] and if (ModelState.IsValid)
   [Route("api/[controller]")]
   public class ProductsController : ControllerBase 
   {
      private DataContext context;

      public ProductsController(DataContext ctx)
      {
         context = ctx;
      }


      [HttpGet]
      public IAsyncEnumerable<Product> GetProducts()
      {
         return context.Products;
      }

      [HttpGet("{id}")]
      public async Task<IActionResult> GetProduct(long id)
      {
         Product p = await context.Products.FindAsync(id);
         if (p == null)
            return NotFound();
         return Ok(p);
      }

      [HttpPost]
      public async Task<IActionResult> SaveProduct([FromBody] ProductBindingTarget target)
      {
         if (ModelState.IsValid)
         {
            Product p = target.ToProduct();
            await context.Products.AddAsync(p);
            await context.SaveChangesAsync(); 
            return Ok(p);
         }
         return BadRequest(ModelState); 
      }

      [HttpPut]
      public async Task UpdateProduct([FromBody] Product product)
      {
         context.Products.Update(product);
         await context.SaveChangesAsync();
      }

      [HttpDelete("{id}")]
      public async Task DeleteProduct(long id)
      {
         context.Products.Remove(new Product() { ProductId = id });
         await context.SaveChangesAsync();
      }

      [HttpGet("redirect")]
      public IActionResult Redirect()
      {
         //return Redirect("/api/products/1");
         return RedirectToAction(nameof(GetProduct), new { id = 1 });  // case insensitive, can be "getproduct" or "Id"
      }
   }
}

public class DataContext : DbContext
{
   public DataContext(DbContextOptions<DataContext> opts) : base(opts) { }

   public DbSet<Product> Products { get; set; }
   public DbSet<Category> Categories { get; set; }
   public DbSet<Supplier> Suppliers { get; set; }
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