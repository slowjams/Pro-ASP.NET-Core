Chapter 14-Using Dependency Injection
==============================

```C#
// asp.net core application ----------V
public class Startup {
   public void ConfigureServices(IServiceCollection services) {     
      services.AddScoped<IMessageSender, EmailSender>();
      services.AddSingleton<NetworkClient>();     
   }

   public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
      // ...
   }
} //----------------------------------Ʌ

// console application ---------------V
public class Program {
   public static void Main(string[] args) {
      var services = new ServiceCollection();                 // <--------------A

      services.AddScoped<IMessageSender, EmailSender>();      // <--------------B
      services.AddSingleton<NetworkClient>();                 // <--------------B
      
      ServiceProvider rootContainer = services.BuildServiceProvider(true);    // <--------------C

      using (IServiceScope scope = rootContainer.CreateScope()) { // <--------------D
         IMessageSender meal = scope.ServiceProvider.GetRequiredService<IMessageSender>();  // <--------------E
      }
   }
} //---------------------------------Ʌ
```
For asp.net core application, each HTTP request results a new `ServiceProviderEngineScope` (IServiceScope) instance created (created by `ServiceProviderEngine`). Note that it doesn't support nested scoped chain like Autofac, also the latest asp.net DI has been rewrited, the source code in this chapter is older version, which should suffice for understanding DI.

```C# exampleSourceCode
public interface IMessageSender {
   void SendMessage(string msg);
} 

public class EmailSender : IMessageSender {
   private readonly NetworkClient _client;

   public EmailSender(NetworkClient client) {
      _client = client
   }

   public void SendMessage(string msg) { ... }
}

public class SmsSender : IMessageSender {
   public void SendMessage(string msg) { ... }
}

public class NetworkClient {
   // ...
}
```

## Demystifying Dependency Injection

**a**: an instance of `ServiceCollection` is created and it contains a list of `ServiceDescriptor` internally.


**b1**: `ServiceCollectionServiceExtensions.AddScoped<TService, TImplementation>()` is called, this method is just a helper method to save you some keystroke of typeof

**b2**: A instance of `ServiceDescriptor` is created and addded to the `ServiceCollection`

**c1**: `ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(validateScopes:true)` is called

When validateScopes is set to true, the default service provider performs checks to verify that:

<ul>
  <li>Scoped services aren't directly or indirectly resolved from the root service provider.</li>
  <li>Scoped services aren't directly or indirectly injected into singletons.</li>
</ul> 

Note that it doesn't check if Transient services injected into singletons or resoved from the root container because transient services are supposed to live together with its consumers.

**c2**: ...


## Source Code
```C#
//  Describes a service with its service type, implementation, and lifetime. sdsd
public class ServiceDescriptor {
   
   public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime) : this(serviceType, lifetime) { 
      ImplementationType = implementationType;
   }

   public ServiceDescriptor(Type serviceType, object instance) : this(serviceType, ServiceLifetime.Singleton) {
      ImplementationInstance = instance;
   }

   public ServiceDescriptor(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime) : this(serviceType, lifetime) {
      ImplementationFactory = factory;
   }

   private ServiceDescriptor(Type serviceType, ServiceLifetime lifetime) {
      Lifetime = lifetime;
      ServiceType = serviceType;
   }

   public ServiceLifetime Lifetime { get; }
   public Type ServiceType { get; }
   public Type ImplementationType { get; }
   public object ImplementationInstance { get; }   // if you don't provde an instance for singleton, I mean the singleton will be created on the runtime 
                                                   // when you first access this service
   public Func<IServiceProvider, object> ImplementationFactory { get; }

   internal Type GetImplementationType() {
      if (ImplementationType != null) {
         return ImplementationType;
      } else if (ImplementationInstance != null) {
         return ImplementationInstance.GetType();
      } else if (ImplementationFactory != null) {
         var typeArguments = ImplementationFactory.GetType().GenericTypeArguments;
         return typeArguments[1];
      }
      return null;
   }

   // Creates an instance of ServiceDescriptor with the specified TService, TImplementation and ServiceLifetime.Transient lifetime
   public static ServiceDescriptor Transient<TService, TImplementation>() where TService : class where TImplementation : class, TService {
      return Describe<TService, TImplementation>(ServiceLifetime.Transient);
   }

   public static ServiceDescriptor Transient(Type service, Type implementationType) {
      return Describe(service, implementationType, ServiceLifetime.Transient);
   }

   public static ServiceDescriptor Transient(Type service, Func<IServiceProvider, object> implementationFactory) {
      return Describe(service, implementationFactory, ServiceLifetime.Transient);
   }
   ...
   public static ServiceDescriptor Describe(Type serviceType, Type implementationType, ServiceLifetime lifetime) {
      return new ServiceDescriptor(serviceType, implementationType, lifetime);
   }
}

//--------------------------------------------------------------------------------------------------------------------------------------------

public interface IServiceCollection : IList<ServiceDescriptor>, ICollection<ServiceDescriptor>, IEnumerable<ServiceDescriptor>, IEnumerable { 
  
}

public class ServiceCollection : IServiceCollection {
   private readonly List<ServiceDescriptor> _descriptors = new List<ServiceDescriptor>();  //-----------------a1
   public int Count => _descriptors.Count;
   public bool IsReadOnly => false;
   public ServiceDescriptor this[int index] {
      get {
         return _descriptors[index];
      }
      set {
         _descriptors[index] = value;
      }
   }
   ...
   void ICollection<ServiceDescriptor>.Add(ServiceDescriptor item) {
       _descriptors.Add(item);
   }
}

public static class ServiceCollectionServiceExtensions
{
   ...
   // Note that all AddXXX are extension methods from ServiceCollectionServiceExtensions
   
   ///--------------------V
   public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services) {  // <------------------b1
      return services.AddScoped(typeof(TService), typeof(TImplementation));
   }

   public static IServiceCollection AddScoped(this IServiceCollection services, Type serviceType, Type implementationType) {
      return Add(services, serviceType, implementationType, ServiceLifetime.Scoped);    // <------------------b1
   }
   //--------------------Ʌ
   public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, TService implementationInstance) where TService : class { 
      return services.AddSingleton(typeof(TService), implementationInstance);
   }

   public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, object implementationInstance) {
      var serviceDescriptor = new ServiceDescriptor(serviceType, implementationInstance);
      services.Add(serviceDescriptor);
      return services;
   }

   public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services)
      where TService : class
      where TImplementation : class, TService {
      if (services == null) {
         throw new ArgumentNullException(nameof(services));
      }

      return services.AddTransient(typeof(TService), typeof(TImplementation));
   }

   public static IServiceCollection AddTransient(this IServiceCollection services, Type serviceType, Type implementationType) {
      return Add(services, serviceType, implementationType, ServiceLifetime.Transient);
   }

   public static IServiceCollection AddTransient<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory) {  
      return services.AddTransient(typeof(TService), implementationFactory);
   }

   private static IServiceCollection Add(IServiceCollection collection, Type serviceType, Type implementationType, ServiceLifetime lifetime) {
      var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime);    // <------------------------------------------------b2
      collection.Add(descriptor);  
      return collection;
   }
}

//-------------------------------------------------------------V
public static class ServiceCollectionContainerBuilderExtensions {
   public static ServiceProvider BuildServiceProvider(this IServiceCollection services) {
      return BuildServiceProvider(services, ServiceProviderOptions.Default);
   }

   public static ServiceProvider BuildServiceProvider(this IServiceCollection services, bool validateScopes) {  // <------------------------------c1
      return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = validateScopes });
   }

   public static ServiceProvider BuildServiceProvider(this IServiceCollection services, ServiceProviderOptions options) {
      return new ServiceProvider(services, options);   // <------------------------------c2
   }
}

public class ServiceProviderOptions {
   internal static readonly ServiceProviderOptions Default = new ServiceProviderOptions();

   // true to perform check if scoped services never gets resolved from root provider
   public bool ValidateScopes { get; set; }

   // true to perform check verifying that all services can be created during BuildServiceProvider
   // NOTE: this check doesn't verify open generics services.
   public bool ValidateOnBuild { get; set; }
}
//--------------------------------------------------------------Ʌ

public interface IServiceProvider {
   object GetService(Type serviceType);
}

public interface IServiceScope : IDisposable {
   IServiceProvider ServiceProvider { get; }
}

public interface IServiceScopeFactory {
   IServiceScope CreateScope();
}

internal interface IServiceProviderEngineCallback {
   void OnCreate(ServiceCallSite callSite);
   void OnResolve(Type serviceType, IServiceScope scope);
}

//--------------------------------------------------V
public static class ServiceProviderServiceExtensions {
   // return null if there is no such service
   public static T GetService<T>(this IServiceProvider provider) {
      return (T)provider.GetService(typeof(T));
   }
   
   public static T GetRequiredService<T>(this IServiceProvider provider) {
      return (T)provider.GetRequiredService(typeof(T));
   }
   
   public static object GetRequiredService(this IServiceProvider provider, Type serviceType) {
      ...
      var service = provider.GetService(serviceType);
      if (service == null) {
        throw new InvalidOperationException(...);
      }

      return service;
   }

   public static IServiceScope CreateScope(this IServiceProvider provider) {
      return provider.GetRequiredService<IServiceScopeFactory>().CreateScope();   
   }
}
//--------------------------------------------------Ʌ
//-----------------------------------------------------------------------------------V
public sealed class ServiceProvider : IServiceProvider, IDisposable, IServiceProviderEngineCallback {  //-----------------------------c2
   private readonly IServiceProviderEngine _engine;
   private readonly CallSiteValidator _callSiteValidator;
   
                                                       // serviceDescriptors is actually ServiceCollection
   internal ServiceProvider(IEnumerable<ServiceDescriptor> serviceDescriptors, ServiceProviderOptions options) {
      IServiceProviderEngineCallback callback = null;
      if (options.ValidateScopes) {
         callback = this;   // pass itself to ServiceProviderEngine
         _callSiteValidator = new CallSiteValidator();
      }
      switch (options.Mode) {   // note that a ServiceProviderEngine contains the root scope internally via its Root setter property
         case ServiceProviderMode.Dynamic:
            _engine = new DynamicServiceProviderEngine(serviceDescriptors, callback);
            break;
         case ServiceProviderMode.Runtime:
            _engine = new RuntimeServiceProviderEngine(serviceDescriptors, callback);
            break;
         case ServiceProviderMode.ILEmit:
            _engine = new ILEmitServiceProviderEngine(serviceDescriptors, callback);
            break;
         case ServiceProviderMode.Expressions:
            _engine = new ExpressionsServiceProviderEngine(serviceDescriptors, callback);
            break;
         default:
            throw new NotSupportedException(nameof(options.Mode));
      }
   }

   public object GetService(Type serviceType) => _engine.GetService(serviceType);   // ServiceProviderEngine is the one that actually resolves services
                                                                                    // Those services are resolved from root container (pass only one argument)

   public void Dispose() => _engine.Dispose();

   void IServiceProviderEngineCallback.OnCreate(ServiceCallSite callSite) {
       _callSiteValidator.ValidateCallSite(callSite);
   }

   void IServiceProviderEngineCallback.OnResolve(Type serviceType, IServiceScope scope) {
       _callSiteValidator.ValidateResolution(serviceType, scope, _engine.RootScope);  // I think this is about how a specific scope resolve a singleton service
   }
}
//-----------------------------------------------------------------------------------Ʌ

//-----------------------------------------------------------------------------------V
internal abstract class ServiceProviderEngine : IServiceProviderEngine, IServiceScopeFactory {  // note that ServiceProviderEngine can create a new scope
   private readonly IServiceProviderEngineCallback _callback;
   private readonly Func<Type, Func<ServiceProviderEngineScope, object>> _createServiceAccessor;
   private bool _disposed;

   protected ServiceProviderEngine(IEnumerable<ServiceDescriptor> serviceDescriptors, IServiceProviderEngineCallback callback) {  // callback is actually ServiceProvider
      _createServiceAccessor = CreateServiceAccessor;
      _callback = callback;
      Root = new ServiceProviderEngineScope(this);   // root scope, represented by a new ServiceProviderEngineScope instance with ServiceProviderEngine itself
                                                     // so root scope is created by ServiceProviderEngine you can say
      RuntimeResolver = new CallSiteRuntimeResolver();
      CallSiteFactory = new CallSiteFactory(serviceDescriptors);
      CallSiteFactory.Add(typeof(IServiceProvider), new ServiceProviderCallSite());   // <-----------------this is why we can inject IServiceProvider into our services

      // this is related to the ServiceProviderServiceExtensions.CreateScope method
      CallSiteFactory.Add(typeof(IServiceScopeFactory), new ConstantCallSite(typeof(IServiceScopeFactory), Root)); 
      
      RealizedServices = new ConcurrentDictionary<Type, Func<ServiceProviderEngineScope, object>>();
   }

   // use ConcurrentDictionary multithreading purpose I think
   internal ConcurrentDictionary<Type, Func<ServiceProviderEngineScope, object>> RealizedServices { get; }  

   internal CallSiteFactory CallSiteFactory { get; }
   protected CallSiteRuntimeResolver RuntimeResolver { get; }
   public ServiceProviderEngineScope Root { get; }
   public IServiceScope RootScope => Root;
   public object GetService(Type serviceType) => GetService(serviceType, Root);   // get service from root scope; note that one argument means resolve from 
                                                                                  // root container; two arguments means resolve from a specific scope

   internal object GetService(Type serviceType, ServiceProviderEngineScope serviceProviderEngineScope) {   // get service from scope
      if (_disposed)
         ThrowHelper.ThrowObjectDisposedException();
      var realizedService = RealizedServices.GetOrAdd(serviceType, _createServiceAccessor);
      _callback?.OnResolve(serviceType, serviceProviderEngineScope);
      return realizedService.Invoke(serviceProviderEngineScope);
   }

   protected abstract Func<ServiceProviderEngineScope, object> RealizeService(ServiceCallSite callSite);

   public void Dispose() {
      _disposed = true;
      Root.Dispose();
   }

   public IServiceScope CreateScope() {
      if (_disposed) 
         ThrowHelper.ThrowObjectDisposedException();
      
      return new ServiceProviderEngineScope(this);   // note that same engine is attached to new scope everytime a new scope is created, there is always one engine.
   }
   
   private Func<ServiceProviderEngineScope, object> CreateServiceAccessor(Type serviceType) {
      var callSite = CallSiteFactory.GetCallSite(serviceType, new CallSiteChain());
      if (callSite != null) {
         _callback?.OnCreate(callSite);
         return RealizeService(callSite);
      }
      return _ => null;
   }
}
//-----------------------------------------------------------------------------------Ʌ

/*
public interface IServiceProvider {
   object GetService(Type serviceType);
}

public interface IServiceScope : IDisposable {
   IServiceProvider ServiceProvider { get; }
}
*/
internal class ServiceProviderEngineScope : IServiceScope, IServiceProvider {   // ServiceProviderEngineScope is also ServiceProvider
   private List<IDisposable> _disposables;
   private bool _disposed;

   public ServiceProviderEngineScope(ServiceProviderEngine engine) {   // ehgine is always the engie when the first ServiceProvider is created
      Engine = engine;
   }

   internal Dictionary<ServiceCacheKey, object> ResolvedServices { get; } = new Dictionary<ServiceCacheKey, object>();

   public ServiceProviderEngine Engine { get; }

   public object GetService(Type serviceType) {
      if (_disposed)
         ThrowHelper.ThrowObjectDisposedException();
      return Engine.GetService(serviceType, this);   // asks engine finds the service in a specified scope
   }

   public IServiceProvider ServiceProvider => this;  // it returns itself so it can be used as: scope.ServiceProvider.GetRequiredService<SauceBéarnaise>();
                                                     // but why adds extra step? why we need to call `scope.ServiceProvider.GetServices<IIngredient>();`? 
                                                     // why not just `scope.GetServices<IIngredient>()`?
                                                     
   public void Dispose() {
      lock (ResolvedServices) {
         if (_disposed) {
            return;  
         }
         _disposed = true;
         if (_disposables != null) {
            for (var i = _disposables.Count - 1; i >= 0; i--) {
               var disposable = _disposables[i];
               disposable.Dispose();
            }
            _disposables.Clear();
         }
         ResolvedServices.Clear();
      }
   }

   internal object CaptureDisposable(object service) {
      if (!ReferenceEquals(this, service)) {
         if (service is IDisposable disposable) {
            lock (ResolvedServices) {
               if (_disposables == null) {
                  _disposables = new List<IDisposable>();
               }
               _disposables.Add(disposable);
            }
         }
      }
      return service;
   }
}
```
```C#
//-------------------------------------V
// looks like CallSiteValidator just do validate scope check, not important can ignore it 
internal sealed class CallSiteValidator: CallSiteVisitor<CallSiteValidator.CallSiteValidatorState, Type?> {
   // keys are services being resolved via GetService, values - first scoped service in their call site tree
   private readonly ConcurrentDictionary<Type, Type> _scopedServices = new ConcurrentDictionary<Type, Type>();

   public void ValidateCallSite(ServiceCallSite callSite) {
      Type? scoped = VisitCallSite(callSite, default);
      if (scoped != null) {
         _scopedServices[callSite.ServiceType] = scoped;
      }
   }

   public void ValidateResolution(Type serviceType, IServiceScope scope, IServiceScope rootScope) {
      if (ReferenceEquals(scope, rootScope) && _scopedServices.TryGetValue(serviceType, out Type? scopedService)) {
         if (serviceType == scopedService) {
            throw new InvalidOperationException("DirectScopedResolvedFromRootException ...");
         }

         throw new InvalidOperationException("ScopedResolvedFromRootException");
      }
   }

   protected override Type? VisitConstructor(ConstructorCallSite constructorCallSite, CallSiteValidatorState state) {
      Type? result = null;
      foreach (ServiceCallSite parameterCallSite in constructorCallSite.ParameterCallSites) {
         Type? scoped =  VisitCallSite(parameterCallSite, state);
         if (result == null) {
            result = scoped;
         }
      }
      return result;
   }

   protected override Type? VisitIEnumerable(IEnumerableCallSite enumerableCallSite, CallSiteValidatorState state) {
      Type? result = null;
      foreach (ServiceCallSite serviceCallSite in enumerableCallSite.ServiceCallSites) {
         Type? scoped = VisitCallSite(serviceCallSite, state);
         if (result == null) {
            result = scoped;
         }
      }
      return result;
   }

   protected override Type? VisitRootCache(ServiceCallSite singletonCallSite, CallSiteValidatorState state) {
      state.Singleton = singletonCallSite;
      return VisitCallSiteMain(singletonCallSite, state);
   }

   protected override Type? VisitScopeCache(ServiceCallSite scopedCallSite, CallSiteValidatorState state) {
      // we are fine with having ServiceScopeService requested by singletons
      if (scopedCallSite.ServiceType == typeof(IServiceScopeFactory)) {
         return null;
      }

      if (state.Singleton != null) {
         throw new InvalidOperationException("ScopedInSingletonException");
      }

      VisitCallSiteMain(scopedCallSite, state);
      return scopedCallSite.ServiceType;
   }

   protected override Type? VisitConstant(ConstantCallSite constantCallSite, CallSiteValidatorState state) => null;

   protected override Type? VisitServiceProvider(ServiceProviderCallSite serviceProviderCallSite, CallSiteValidatorState state) => null;

   protected override Type? VisitFactory(FactoryCallSite factoryCallSite, CallSiteValidatorState state) => null;

   internal struct CallSiteValidatorState {
      public ServiceCallSite? Singleton { get; set; }
   }
}
//-------------------------------------Ʌ
```

```C#
// namespace Microsoft.Extensions.DependencyInjection
public static class ActivatorUtilities  // Activator.CreateInstance + IServiceProvider
{     
   // public delegate object ObjectFactory(IServiceProvider serviceProvider, object?[]? arguments);
   public static ObjectFactory CreateFactory(Type instanceType, Type[] argumentTypes) // create a delegate that instantiate a type with constructor arguments provided directly
   {
      FindApplicableConstructor(instanceType, argumentTypes, out ConstructorInfo? constructor, out int[] parameterMap);
      ParameterExpression provider = Expression.Parameter(typeof(IServiceProvider), "provider");
      ParameterExpression argumentArray = Expression.Parameter(typeof(object[]), "argumentArray");
      Expression factoryExpressionBody = BuildFactoryExpression(constructor, parameterMap, provider, argumentArray);

      var factoryLambda = Expression.Lambda<Func<IServiceProvider, object?[]?, object>>(factoryExpressionBody, provider, argumentArray);
      Func<IServiceProvider, object?[]?, object>? result = factoryLambda.Compile();
      return result.Invoke;
   }

   public static object CreateInstance(IServiceProvider provider, Type instanceType, params object[] parameters);

   public static T CreateInstance<T>(IServiceProvider provider, params object[] parameters);

   public static object GetServiceOrCreateInstance(IServiceProvider provider, Type type);
   
   public static T GetServiceOrCreateInstance<T>(IServiceProvider provider);
}
// ActivatorUtilities is smart enough to pick up a suitable constructor, interanlly it uses a complex process to check object[] parameters
// you don't need to provde arguments in correct order plus any missing arguments will be resolved from service provider
// but it looks like it either requires you provide all arguments or none (IServiceProvider will do the job),
// and you can't mix them like provide some arguments manually and let IServiceProvider search the rest of arguments (not that smart)
```

an example to use `ActivatorUtilities.CreateFactory()` :

```C#
public delegate object ObjectFactory(IServiceProvider serviceProvider, object?[]? arguments);

ObjectFactory PersonFactory = ActivatorUtilities.CreateFactory(typeof(Person), new Type[] { typeof(string) });
Person natashaFromHr = PersonFactory.Invoke(serviceProvider, new object[] { "John" }) as Person;;
```

There are two important things to notice:

1. Both `ServiceProvider` and `ServiceProviderEngineScope` implement `IServiceProvider`:

```C#
public interface IServiceProvider {
   object GetService(Type serviceType);
}

public sealed class ServiceProvider : IServiceProvider, IDisposable, IServiceProviderEngineCallback {
   ...
}
internal class ServiceProviderEngineScope : IServiceScope, IServiceProvider { 
   ...
}
```
so why we call `ServiceProvider` root container, what makes it a root container?
The answer is how they asks Engine to resolve services. For `ServiceProvider`, it does `public object GetService(Type serviceType) => _engine.GetService(serviceType);`; for `ServiceProviderEngineScope`, it does `Engine.GetService(serviceType, this);`, if you look at the Engine source code:
```C#
internal abstract class ServiceProviderEngine : IServiceProviderEngine ... {
   ...
   public ServiceProviderEngineScope Root { get; }
   public IServiceScope RootScope => Root;

   public object GetService(Type serviceType) => GetService(serviceType, Root);
   internal object GetService(Type serviceType, ServiceProviderEngineScope serviceProviderEngineScope) { ... }
}
```
See `ServiceProvider` eventually only pass one argument to `GetService` and `ServiceProviderEngineScope` pass two arguments to `GetService`, now you see why `ServiceProvider` is root container :)

2. From the source code you can see that `ServiceProviderEngineScope` is actually an `IServiceProvider`, when you create multiple scopes from the roon container `ServiceProvider`, you actually create multiple `ServiceProviderEngineScope` instances, and all those instances shared a same `ServiceProviderEngine` instance. Now you get the idea that when you access the `IServiceProvider` via `HttpContext`:
```C#
public abstract class HttpContext {
   ...
   public abstract HttpRequest Request { get; }

   public abstract HttpResponse Response { get; }

   public abstract IServiceProvider RequestServices { get; set; }  
}
```
You actually gets an `ServiceProviderEngineScope` that asp.net core creates for the request.

If you check the source code carefully, you might ask:
`"How can the scope associated with a request resolves singleton service"?`
becuase if you look at the code (`Engine.GetService(serviceType, this)`)", the scope is always passed as itself for the Engine to resolve service, it doesn't pass the the root scope obviously, so how does Engine resolve singleton service? I think it works in this way, Engine calls `_callback?.OnResolve(serviceType, serviceProviderEngineScope);` first where `_callback` is actually the root container ServiceProvider, then it eventually calls `_callSiteValidator.ValidateResolution(serviceType, scope, _engine.RootScope)`, since both the specific scope and root scope are passed, I think that's how a specific child scope resolves singleton instances. 

<div class="alert alert-info pt-2 pb-0" role="alert">
   If you want to know the difference between GetService method and GetRequiredService (recommended to use) method, check this article: https://andrewlock.net/the-difference-between-getservice-and-getrquiredservice-in-asp-net-core/
</div>

## Introducing Microsoft.Extensions.DependencyInjection

Autofac two-step process:

![alt text](./zImages/15-1.png "Title")

MS.DI this two-step process:

![alt text](./zImages/15-2.png "Title")

![alt text](./zImages/15-3.png "Title")

MS.DI's ServiceCollection is the equivalent of Autofac's ContainerBuilder

## Resolving Objects

```C#
var services = new ServiceCollection();    // 1

services.AddTransient<SauceBéarnaise>();   // 2

ServiceProvider container = services.BuildServiceProvider(validateScopes: true);   // 3, root container (ServiceProvider) is also root scope

IServiceScope scope = container.CreateScope();   // 4, CreateScope is an extension method from ServiceProviderServiceExtensions

SauceBéarnaise sauce = scope.ServiceProvider.GetRequiredService<SauceBéarnaise>();   // 5, scope.ServiceProvider is actually scope itself
```

Combined with the source code, the process is:

1. new up an instance of `ServiceCollection` which contains a list of `List<ServiceDescriptor>` internally.

2. register service type on the `ServiceCollection` instance via `ServiceCollectionServiceExtensions.AddTransient<TService>` method. Mote that `ServiceCollectionServiceExtensions` contains generic `AddTransient<TService>(this IServiceCollection services)` and non-generic `AddTransient(this IServiceCollection services, Type serviceType)`, generic methods actually call non-generic methods by adding `typeof(T)`, so using generic method save you some keystrokes by using `typeof(T)` for you. The methods in `ServiceCollectionServiceExtensions` eventually adds a new `ServiceDescriptor` to `ServiceCollection` via `ServiceCollectionServiceExtensions`'s `Add(IServiceCollection collection, Type serviceType, Type implementationType, ServiceLifetime lifetime)` method.

3. Create a `ServiceProvider` instance via `ServiceCollectionContainerBuilderExtensions.BuildServiceProvider` method:
```C#
public static class ServiceCollectionContainerBuilderExtensions {
   ...
   public static ServiceProvider BuildServiceProvider(this IServiceCollection services, ServiceProviderOptions options) {
      return new ServiceProvider(services, options);
   }
}
```
 `ServiceProvider` contains `IServiceProviderEngine` internally, this engine is actually the one that resolve services.

4. Create a new scope (`ServiceProviderEngineScope`) from `ServiceProvider`. The sequence is:  ServiceProviderServiceExtensions's `CreateScope` method is called, then somehow the `ServiceProvider` instance's engine instance (`ServiceProviderEngine`) is accessed (via `provider.GetRequiredService<IServiceScopeFactory>()`, don't know why not just access the engine directly via property, but it is advance code, must be some other reason, don't worry about it now). Then the ServiceProviderEngine's `CreateScope()` method is called (`IServiceScopeFactory` interface contains `CreateScope()` method, and `ServiceProviderEngineScope` implements `IServiceScopeFactory`). Note that `ServiceProviderEngine` create a new `ServiceProviderEngineScope` and pass itself as the argument

5. Get the service from `ServiceProviderEngineScope`'s `ServiceProvider` property, note that the source code is `public IServiceProvider ServiceProvider => this;`, so `ServiceProviderEngineScope` acts as a ServiceProvider, because `ServiceProviderEngineScope` implements `IServiceProvider` too, so you can get requried service from it. Now you should see that it is the `ServiceProviderEngineScope` (acts as a ServiceProvider) that resolve the service you request by calling `Engine.GetService(serviceType, this)`, you see `this` is a `ServiceProviderEngineScope` itself, now you see how MSDI resolve service in different scope,  **that is how and why you should resolve service via a scope (not from root scope unless it is a singleton service)**

Note that the whole process only involve a singe `ServiceProviderEngine`, which is created when the first `ServiceProvider` instance is created:
```C#
public static class ServiceCollectionContainerBuilderExtensions {
   ...
   public static ServiceProvider BuildServiceProvider(this IServiceCollection services, ServiceProviderOptions options) {
      return new ServiceProvider(services, options);
   }
}

public sealed class ServiceProvider : IServiceProvider, IDisposable, IServiceProviderEngineCallback {
   private readonly IServiceProviderEngine _engine;
   ...
   switch (options.Mode) {
      case ServiceProviderMode.Dynamic:
         _engine = new DynamicServiceProviderEngine(serviceDescriptors, callback);
         break;
         ...
   }
   ...
}
```

In a nutshell, you should get services from scope, not from root container which is also a ServiceProvider.

A side note below shows the difference on how root container (first `ServiceProvider`) resovle a service from root container itself and how `ServiceProviderEngineScope` resolve a service in a specficed scope:
```C#
public sealed class ServiceProvider : IServiceProvider, IDisposable {
   private readonly IServiceProviderEngine _engine;
   ...
   public object GetService(Type serviceType) => _engine.GetService(serviceType);   // called GetService(serviceType, Root) internally, that's why you always get a                                                                                // serivce root scope if you reslove service from root container
}

internal class ServiceProviderEngineScope : IServiceScope, IServiceProvider {
   ...
   public object GetService(Type serviceType) {
      return Engine.GetService(serviceType, this);  
   }
}

internal abstract class ServiceProviderEngine : IServiceProviderEngine, IServiceScopeFactory {
   protected ServiceProviderEngine(IEnumerable<ServiceDescriptor> serviceDescriptors, IServiceProviderEngineCallback callback) {
      ...
      Root = new ServiceProviderEngineScope(this);
      ...
   }

   public object GetService(Type serviceType) => GetService(serviceType, Root);

   internal object GetService(Type serviceType, ServiceProviderEngineScope serviceProviderEngineScope) {   // you provide a specific scope
      ...
   }
}
```

As a safety measure, always build the ServiceProvider using the BuildServiceProvider overload with the validateScopes argument set to true.

When validateScopes is set to true, the default service provider performs checks to verify that:

<ul>
  <li>Scoped services aren't directly or indirectly resolved from the root service provider.</li>
  <li>Scoped services aren't directly or indirectly injected into singletons.</li>
</ul> 

Note that it doesn't check if Transient services injected into singletons or resoved from the root container because transient services are supposed to live together with its consumers.

With the introduction of ASP.NET Core 2.0, validateScopes is automatically set to true by the framework when the application is running in the development environment, but it's best to enable validation even outside the development environment as well. This means you’ll have to call BuildServiceProvider(true) manually.

## Mapping Abstractions to concrete type

```C#
var services = new ServiceCollection();

services.AddTransient<IIngredient, SauceBéarnaise>();

var container = services.BuildServiceProvider(true);

IServiceScope scope = container.CreateScope();

IIngredient sauce = scope.ServiceProvider.GetRequiredService<IIngredient>();
```

## Configuring the ServiceCollection using Configuration as Code

```C#
services.AddTransient<SauceBéarnaise>();
services.AddTransient<IIngredient>(sp => sp.GetRequiredService<SauceBéarnaise>());   // sp is the correct ServiceProviderEngineScope to be used in the future
```
You might think why we can't do:
![alt text](./zImages/0-bad.png "Title")
```C#
services.AddTransient<SauceBéarnaise>();
services.AddTransient<IIngredient, SauceBéarnaise>();
```
Again, it will cause **torn lifestyles**. This becomes apparent when you change the Lifestyle from Transient to, for instance, Singleton:
```C#
services.AddSingleton<SauceBéarnaise>();
services.AddSingleton<IIngredient, SauceBéarnaise>();
```
Although you might expect there to only be one SauceBéarnaise instance for the lifetime of the container, splitting up the registration causes MS.DI to create a separate instance per AddSingleton call. The Lifestyle of SauceBéarnaise is therefore considered to be torn.

<div class="alert alert-info p-1" role="alert">
    Each call to one of the AddScoped and AddSingleton methods results in its own unique cache. Having multiple Add... calls can, therefore, result in multiple instances per scope or per container. To prevent this, register a delegate that resolves the concrete instance.
</div>

## Configuring ServiceCollection using Auto-Registration

```C#
Assembly ingredientsAssembly = typeof(Steak).Assembly;

// register with interface
var ingredientTypes =
   from type in ingredientsAssembly.GetTypes()
   where !type.IsAbstract
   where typeof(IIngredient).IsAssignableFrom(type)
   where type.Name.StartsWith("Sauce")  
   select type;

foreach (var type in ingredientTypes) {
   services.AddTransient(typeof(IIngredient), type);
}

Assembly policiesAssembly = typeof(DiscountPolicy).Assembly;

// register with abstract classes
var policyTypes =
   from type in policiesAssembly.GetTypes()
   where type.Name.EndsWith("Policy")
   select type;

foreach (var type in policyTypes) {
   services.AddTransient(type.BaseType, type);
}
```

## Auto-Registration of generic Abstractions

The AdjustInventoryService from chapter 10:
```C#
public interface ICommandService<TCommand> {
   void Execute(TCommand command);
}

public class AdjustInventoryService : ICommandService<AdjustInventory>
{
   private readonly IInventoryRepository repository;

   public AdjustInventoryService(IInventoryRepository repository)
      this.repository = repository;
   }

   public void Execute(AdjustInventory command) {
      var productId = command.ProductId;
      ...
   }
}
```
Auto-Registration of `ICommandService<TCommand>` implementations:
```C#
Assembly assembly = typeof(AdjustInventoryService).Assembly;

var mappings =
   from type in assembly.GetTypes()
   where !type.IsAbstract
   where !type.IsGenericType
   from i in type.GetInterfaces()   // i is ICommandService<AdjustInventory>
   where i.IsGenericType
   where i.GetGenericTypeDefinition() == typeof(ICommandService<>)
   select new { service = i, type };

foreach (var mapping in mappings) {
   services.AddTransient(mapping.service, mapping.type);
}
```

## Registering objects with code blocks

Another option for creating a component with a primitive value is to use one of the Add... methods, which let you supply a delegate that creates the component:
```C#
services.AddTransient<ICourse>(sp => new ChiliConCarne(Spiciness.Hot));   // sp is ServiceProvider
```

Another use case is sometimes a type's constructor is private or internal, Autofac only looks after public constructors, you can only construct and provide an instance from a factory:
```C#
public class JunkFood : IMeal {
   internal JunkFood(string name) {
      ...
   }
}

public static class JunkFoodFactory {
   public static JunkFood Create(string name) {
      return new JunkFood(name);
   }
}

services.AddTransient<IMeal>(sp => JunkFoodFactory.Create("chicken meal"));
```

## Selecting among multiple candidates

As you already know that you can register multiple implementations of the same interface:
```C#
services.AddTransient<IIngredient, SauceBéarnaise>();
services.AddTransient<IIngredient, Steak>();
```
You can also ask the container to resolve all IIngredient components. MS.DI has adedicated method to do that, called GetServices:
```C#
IEnumerable<IIngredient> ingredients = scope.ServiceProvider.GetServices<IIngredient>();
// or
IEnumerable<IIngredient> ingredients = scope.ServiceProvider.GetRequiredService<IEnumerable<IIngredient>>();
```
Notice that you use the normal GetRequiredService method, but that you request `IEnumerable<IIngredient>`. The container interprets this as a convention and gives you all the IIngredient components it has.

## Removing ambiguity using code blocks

As useful as Auto-Wiring is, sometimes you need to override the normal behavior to provide fine-grained control over which Dependencies go where, but it may also be that you need to address an ambiguous API. As an example, consider this constructor:
```C#
public ThreeCourseMeal(ICourse entrée, ICourse mainCourse, ICourse dessert)
```
In this case, you have three identically typed Dependencies, each of which represents a different concept. In most cases, you want to map each of the Dependencies to aseparate type.

As stated previously, when compared to Autofac, MS.DI is limited in functionality. Where Autofac provides keyed registrations, MS.DI falls short in this respect. There isn’t any built-in functionality to do this. To wire up such an ambiguous API with MS.DI, you have to revert to using a code block:

```C#
services.AddTransient<IMeal>(sp => new ThreeCourseMeal(
   entrée: sp.GetRequiredService<Rillettes>(),
   mainCourse: sp.GetRequiredService<CordonBleu>(),
   dessert: sp.GetRequiredService<CrèmeBrûlée>()));
```

MS.DI contains a utility class called `ActivatorUtilities` (that use System.Reflection.ConstructorInfo's Invoke instance method internally) that allows Auto-Wiring a class's Dependencies:
```C#
public static class ActivatorUtilities {
   ...
   public static T CreateInstance<T>(IServiceProvider provider, params object[] parameters);
}

services.AddTransient<IMeal>(c => ActivatorUtilities.CreateInstance<ThreeCourseMeal>(
   sp,
   new object[] {
      sp.GetRequiredService<Rillettes>(),
      sp.GetRequiredService<CordonBleu>(),
      sp.GetRequiredService<MousseAuChocolat>()
   }));
```

## Wiring non-generic Composit

Let's take a look at how you can register Composites, such as the CompositeNotificationService of chapter 6 in MS.DI. The following listing shows this class:
```C#
public class CompositeNotificationService : INotificationService {
   private readonly IEnumerable<INotificationService> services;

   public CompositeNotificationService(IEnumerable<INotificationService> services) {
      this.services = services;
   }

   public void OrderApproved(Order order) {
      foreach (INotificationService service in this.services) {
         service.OrderApproved(order);
      }
   }
}
```
Registering a Composite requires it to be added as a default registration, while injecting it with a sequence of resolved instances:
```C#
services.AddTransient<OrderApprovedReceiptSender>();
services.AddTransient<AccountingNotifier>();
services.AddTransient<OrderFulfillment>();

services.AddTransient<INotificationService>(sp =>
   new CompositeNotificationService(
      new INotificationService[] {
         sp.GetRequiredService<OrderApprovedReceiptSender>(),
         sp.GetRequiredService<AccountingNotifier>(),
         sp.GetRequiredService<OrderFulfillment>(),
      }));
```

## Some Interesting Things to Note

**Fact One**: What if we inject into a Controller? (it is anti-pattern anyway, but what if we do it)
```C#
public class HomeController : Controller {

   public HomeController(IServiceProvider sp) {  // sp is request scope, not root container's associated scope
      // ...
   }
}
```
Another question is, what if we want to access root scope/container to resolve singleton services? well, the question is, do you really need to access root scope to do that? The answer is no, we can resolve singleton service from request scope, check the source code of both versions of MSDI, you will also see if you want to create a new scope, `rootScope.CreateScope()` is the same as `requestScope.CreateScope()`.

**Fact Two**: Root ServiceProvder is actully not the one get passed

```C#
class Program {
   static void Main(string[] args) {
      var services = new ServiceCollection();
     
      services.AddScoped(sp => { 
         return new MyService();   
         // breakpointerB
      });

      var container = services.BuildServiceProvider();

      IServiceScope requestScope = container.CreateScope();

      var myService1 = container.GetRequiredService<MyService>();

      var myService2 = requestScope.ServiceProvider.GetRequiredService<MyService>();   // breakpointerA

      var myService3 = requestScope.ServiceProvider.GetRequiredService<MyService>();   // scopedDelegate won't invoke, a "cache" one will be used
   }
}

class MyService {
   public MyService() { }
}
```
If you put the breakpointerA and breakpointerB, you might expect that `container` and `sp` are the same object (by checking memory address of them using immediate window). But actually their addresses are different, if you see the new version of MSDI source code, you will see that it is `new ServiceProviderEngineScope(this, isRootScope: true)` get passed.

Also note that breakpointerB only breaks twice, one for `container.GetRequiredService<MyService>()`, the other for `var myService2 = scope.ServiceProvider.GetRequiredService<MyService>()`, because we resolve them in different scope. However, `var myService3 = scope.ServiceProvider.GetRequiredService<MyService>()` won't hit the breakpointB for a third time, because MSDI uses `Func<ServiceProviderEngineScope, object?>>`, as long as the ServiceProviderEngineScope is the same, the subsequent request will get the "cache" one.

**Fact Three**: what if you resolve a singleton service from request scope?

```C#
class Program {
   static void Main(string[] args) {
      var services = new ServiceCollection();
     
      services.AddSingleton(sp1 => {   // <-------------a  sp1 is root scope 
         return new MyServiceOne();
      });

      services.AddSingleton(sp2 => {   // <-------------b  sp2 is still root scope, this is very dangerous if you think sp2 is requestScope because you resolve from request scope,
         return new MyServiceTwo();    // if MyServiceTwo takes an argument that should be resolved from a request scope, you actually end up with resolving it from root scope
                                       // so it is an error if you do `return new MyServiceTwo(sp.ServiceProvider.GetRequiredService<XXX>())`
      });

      services.AddScoped(sp3 => {      // <-------------c  sp3 is request scope
         return new MyServiceThree();
      });

      var container = services.BuildServiceProvider();

      IServiceScope requestScope = container.CreateScope();

      var myService1 = container.GetRequiredService<MyServiceOne>();                        // a

      var myService2 = requestScope.ServiceProvider.GetRequiredService<MyServiceTwo>();     // b

      var myService3 = requestScope.ServiceProvider.GetRequiredService<MyServiceThree>();   // c
   }
}

class MyServiceOne {
   public MyServiceOne() { }
}

class MyServiceTwo {
   public MyServiceTwo() { }
}

class MyServiceThree {
   public MyServiceThree() { }
}
```
`sp1` is the container's root scope (`ServiceProvider.Root`) for sure and `sp3` is request scope for sure, what about `sp2`?  **`sp2` is root scope too**. 


## New Version of MSDI- To work in the future to understand everything (probably after reviewing CLR via C#'s last two chapters)

Microsoft team rewrite the MSDI, so there are some minor differences compared to previous version

```C#
public sealed class ServiceProvider : IServiceProvider, IDisposable, IAsyncDisposable {
   // ...
   private readonly Func<Type, Func<ServiceProviderEngineScope, object?>> _createServiceAccessor;
   internal ServiceProviderEngineScope Root { get; }

   internal ServiceProvider(ICollection<ServiceDescriptor> serviceDescriptors, ServiceProviderOptions options) {
      // note that Root needs to be set before calling GetEngine(), because the engine may need to access Root
      Root = new ServiceProviderEngineScope(this, isRootScope: true);
      // ...
   }

   public object? GetService(Type serviceType) => GetService(serviceType, Root);   // not GetService(serviceType, this);

   internal IServiceScope CreateScope() {
      return new ServiceProviderEngineScope(this, isRootScope: false);
   }
   // ...
}

internal sealed class ServiceProviderEngineScope : IServiceScope, IServiceProvider, IAsyncDisposable, IServiceScopeFactory {
   internal IList<object> Disposables => _disposables ?? (IList<object>)Array.Empty<object>();
   
   private bool _disposed;
   private List<object>? _disposables;

   public ServiceProviderEngineScope(ServiceProvider provider, bool isRootScope) {
      ResolvedServices = new Dictionary<ServiceCacheKey, object?>();
      RootProvider = provider;
      IsRootScope = isRootScope;
   }

   internal Dictionary<ServiceCacheKey, object?> ResolvedServices { get; }

   public bool IsRootScope { get; }

   internal ServiceProvider RootProvider { get; }

   public object? GetService(Type serviceType) {
      return RootProvider.GetService(serviceType, this);
   }

   public IServiceProvider ServiceProvider => this;

   public IServiceScope CreateScope() => RootProvider.CreateScope();   // now you see why MSDI doesn't support nested scopes
                                                                       // because a new scope is created from ServiceProvider(RootProvider)
}
```


<!-- ![alt text](./zImages/15-0.png "Title") -->

<!-- <code>&lt;T&gt;</code> -->

<!-- <div class="alert alert-info pt-2 pb-0" role="alert">
    <ul class="pl-1">
      <li></li>
      <li></li>
    </ul>  
</div> -->

<!-- <ul>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
</ul>  -->

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










































Q1-Verify that `ActivatorUtilities.CreateInstance(app.ApplicationServices, middleware, ctorArgs)` can automatically detect args, even the provided args  doesn't match the sequence order, is ActivatorUtilities that smart?
https://source.dot.net/#Microsoft.AspNetCore.Http.Abstractions/Extensions/UseMiddlewareExtensions.cs,94

question orgins from 
https://source.dot.net/#Microsoft.AspNetCore.Routing/Builder/EndpointRoutingApplicationBuilderExtensions.cs,60

 EndpointRoutingMiddleware has 5 dependency, but only one dependency is provided `IEndpointRouteBuilder`


















<!-- <div class="alert alert-info p-1" role="alert">
    
</div> -->

<!-- ![alt text](./zImages/17-6.png "Title") -->

<!-- <code>&lt;T&gt;</code> -->

<!-- <div class="alert alert-info pt-2 pb-0" role="alert">
    <ul class="pl-1">
      <li></li>
      <li></li>
    </ul>  
</div> -->

<!-- <ul>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
  <li><b></b></li>
</ul>  -->

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