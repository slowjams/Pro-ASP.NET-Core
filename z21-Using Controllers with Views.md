Chapter 21-Using Controllers with Views
=================================

Source Code:

```C#
//-----------------V
public static class MvcServiceCollectionExtensions
{
   public static IMvcBuilder AddMvc(this IServiceCollection services)
   {
      services.AddControllersWithViews();
      return services.AddRazorPages();
   }

   public static IMvcBuilder AddMvc(this IServiceCollection services, Action<MvcOptions> setupAction)
   {
      var builder = services.AddMvc();
      builder.Services.Configure(setupAction);

      return builder;
   }

   public static IMvcBuilder AddControllersWithViews(this IServiceCollection services)
   {
      var builder = AddControllersWithViewsCore(services);
      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   public static IMvcBuilder AddRazorPages(this IServiceCollection services)
   {
      var builder = AddRazorPagesCore(services);
      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   private static IMvcCoreBuilder AddRazorPagesCore(IServiceCollection services)
   {
       var builder = services
           .AddMvcCore()
           .AddAuthorization()
           .AddDataAnnotations()
           .AddRazorPages()
           .AddCacheTagHelper();
   }

   public static IMvcBuilder AddControllers(this IServiceCollection services)
   {
      var builder = AddControllersCore(services);
      return new MvcBuilder(builder.Services, builder.PartManager);
   }

   private static IMvcCoreBuilder AddControllersCore(IServiceCollection services)
   {
      // this method excludes all of the view-related services by default.
      var builder = services
          .AddMvcCore()
          .AddApiExplorer()
          .AddAuthorization()
          .AddCors()
          .AddDataAnnotations()
          .AddFormatterMappings();
 
      if (MetadataUpdater.IsSupported)
      {
         services.TryAddEnumerable(ServiceDescriptor.Singleton<IActionDescriptorChangeProvider, HotReloadService>());
      }
 
      return builder;
   }
}
//-----------------Ʌ

//-----------------V
public static class ControllerEndpointRouteBuilderExtensions
{
   public static ControllerActionEndpointConventionBuilder MapControllers(this IEndpointRouteBuilder endpoints)
   {
      EnsureControllerServices(endpoints);
      return GetOrCreateDataSource(endpoints).DefaultBuilder;
   }

}
//-----------------Ʌ
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