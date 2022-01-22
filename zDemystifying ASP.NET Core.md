Demystifying ASP.NET Core
==============================

```C#
// to work on after chapter 16
public sealed class DefaultHttpContext : HttpContext {

   private const int DefaultFeatureCollectionSize = 10;
   private FeatureReferences<FeatureInterfaces> _features;
   ...
   public DefaultHttpContext() : this(new FeatureCollection(DefaultFeatureCollectionSize)) {
      Features.Set<IHttpRequestFeature>(new HttpRequestFeature());
      Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
      Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
   }

   public override IFeatureCollection Features => _features.Collection
}

public abstract class HttpContext {   // namespace Microsoft.AspNetCore.Http
   ...
   public abstract HttpRequest Request { get; }
   public abstract HttpResponse Response { get; }
   public abstract ConnectionInfo Connection { get; }
   public abstract ISession Session { get; set; }
   public abstract void Abort();

   public abstract ClaimsPrincipal User { get; set; }

   public abstract IFeatureCollection Features { get; }   // allow access to the low-level aspects of request handling

   public abstract IDictionary<object, object> Items { get; set; }  // gets or sets a key/value collection that shares data within the scope of this request.

   public abstract IServiceProvider RequestServices { get; set; }   // gets or sets the IServiceProvider that can access to the request's service container.
}

public abstract class HttpRequest {
   ...
   public abstract Stream Body { get; set; }
   public abstract string ContentType { get; set; }
   public abstract long? ContentLength { get; set; }
   public abstract IRequestCookieCollection Cookies { get; set; }
   public abstract IFormCollection Form { get; set; }
   public abstract IHeaderDictionary Headers { get; }
   public abstract bool IsHttps { get; set; }
   public abstract string Method { get; set; }
   public abstract PathString Path { get; set; }
   public abstract IQueryCollection Query { get; set; }
   public virtual RouteValueDictionary RouteValues { get; set; }
   
   public abstract HttpContext HttpContext { get; }
}

public abstract class HttpResponse {
   public abstract long? ContentLength { get; set; }
   public abstract string ContentType { get; set; }
   public abstract IResponseCookies Cookies { get; }
   public abstract bool HasStarted { get; }
   public abstract IHeaderDictionary Headers { get; }
   public abstract int StatusCode { get; set; }
   public virtual void Redirect(string location);

   public abstract HttpContext HttpContext { get; }
}

public readonly struct PathString : IEquatable<PathString> {
   internal const int StackAllocThreshold = 128;
   public static readonly PathString Empty = new(string.Empty);
   
   public PathString(string? value) {
      if (!string.IsNullOrEmpty(value) && value[0] != '/') {
         throw new ArgumentException(Resources.FormatException_PathMustStartWithSlash(nameof(value)), nameof(value));
      }
      Value = value;
   }

   public string? Value { get; }   // this is used to check equality
   ...
}

public static class HttpResponseWritingExtensions {
   public static Task WriteAsync(this HttpResponse response, string text, Encoding encoding, CancellationToken cancellationToken = default) {
      ...
      return response.Body.WriteAsync(data, 0, data.Length, cancellationToken);
   }
}

// -------------------------------------------------------------------------------------------------------------------------------------------------
public interface IApplicationBuilder {   // namespace Microsoft.AspNetCore.Builder; Assembly Microsoft.AspNetCore.Http.Abstractions
   IServiceProvider ApplicationServices { get; set; }
   IFeatureCollection ServerFeatures { get; }
   IDictionary<string, object?> Properties { get; }
   IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware);
   IApplicationBuilder New();
   RequestDelegate Build();
}

public static class UseExtensions {
   public static IApplicationBuilder Use(this IApplicationBuilder app, Func<HttpContext, Func<Task>, Task> middleware);
}

public static class RunExtensions {   // add a terminal middleware delegate to the application's request pipeline
   public static void Run(this IApplicationBuilder app, RequestDelegate handler);
}
```





<!-- <code>&lt;T&gt;<code> -->

<!-- <div class="alert alert-info p-1" role="alert">
    
</div> -->

<!-- <div class="alert alert-info pt-2 pb-0" role="alert">
    <ul class="pl-1">
      <li></li>
      <li></li>
    </ul>  
</div> -->

<!-- <ul>
  <li></li>
  <li></li>
  <li></li>
  <li></li>
</ul>  -->

<!-- <ul>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
</ul>  -->

<!-- ![alt text](./zImages/16-1.png "Title") -->

<!-- <span style="color:red">hurt</span> -->

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