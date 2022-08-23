Chapter 30- Filters
=================================


## Source Code

```C#
public interface IFilterMetadata  // marker interface for filters handled in the MVC request pipeline
{

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
   void OnResourceExecuting(ResourceExecutingContext context);
   void OnResourceExecuted(ResourceExecutedContext context);
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

   public virtual IActionResult? Result { get; set; }

   public virtual Exception Exception { get; set; }

   public virtual ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

}
//----------------------------------------Ʌ
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