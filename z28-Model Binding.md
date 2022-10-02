Chapter 28- Model Binding and Validation
=================================

* Form data

* Request body (only for controllers decorated with `ApiController`)

* Routing segment variables

* Query strings

The model binding process inspects the complex type and performs the binding process on **each of the public properties** it defines

```C#
@model Instructor
<input class="form-control" asp-for="ID" />
<input class="form-control" asp-for="Name" />
<input class="form-control" asp-for="Tutor.Name" />

@model FormHandler
<input class="form-control" asp-for="Instructor.ID" />
<input class="form-control" asp-for="Instructor.Name" />
<input class="form-control" asp-for="Instructor.Tutor.Name" />

public class Instructor
{
    public int ID { get; set; }
    public string Name { get; set; }
    public Tutor Tutor { get; set; }
}

public class Tutor
{
   public string Name { get; set; }
}

// Model binding starts by looking through the sources e.g Instructor.ID. If that isn't found, it looks for ID without a prefix <-------------------------------
// However, if the property's complex type and, it won't go further look withoug prefix, e.g if you don't have the third input above, Instructor's Tutor property will be null

[BindProperty]  // won't bind data for GET request by default
public Instructor Instructor { get; set; } 
// note that you have to use [BindProperty] even the property Name match the html element name, name matching only apply for the parameters in methods

/*
[BindProperty]
public Instructor lecturer { get; set; }    // won't bind for Razor page, the propery name doesn't match  because of the prefix
*/                                          // will bind for View,  you get the idea

[BindProperty(SupportsGet = true)]  
public Instructor Instructor { get; set; } 

[BindProperty(Name = "XXX.Instructor")]  // BindProperty's Name is equivalent to Bind's Prefix, XXX here indicates it is Razor Page, you should get the idea
public Instructor Instructor { get; set; } 

public IActionResult OnPost(Instructor instructor)

public IActionResult OnPost([Bind(Prefix = "Instructor")] Instructor instructorToUpdate)  // property name (instructorToUpdate) is not the same as the type name (Instructor)
                                                                                          // so Prefix is needed, othertwise you might bind to the wrong source
```

Note that model binding uses second input to take precedence over the first input

```HTML
<input class="form-control" asp-for="Name" />
<input class="form-control" name="Instructor.Name" />
```

# Build Your Own Model Binder

```C#
public static class ServiceCollectionExtensions
{
   public static IServiceCollection AddMvcControllers(this IServiceCollection services)
   {
      return services
         .AddSingleton<IActionDescriptorCollectionProvider, DefaultActionDescriptorCollectionProvider>()
         .AddSingleton<IActionInvokerFactory, ActionInvokerFactory>()
         .AddSingleton<IActionDescriptorProvider, ControllerActionDescriptorProvider>()
         .AddSingleton<ControllerActionEndpointDataSource,ControllerActionEndpointDataSource>()
         .AddSingleton<IActionResultTypeMapper, ActionResultTypeMapper>()
         
         .AddSingleton<IValueProviderFactory, HttpHeaderValueProviderFactory>()
         .AddSingleton<IValueProviderFactory, QueryStringValueProviderFactory>()
         .AddSingleton<IValueProviderFactory, FormValueProviderFactory>()
         .AddSingleton<IModelBinderFactory, ModelBinderFactory>()
         .AddSingleton<IModelBinderProvider, SimpleTypeModelBinderProvider>()
         .AddSingleton<IModelBinderProvider, ComplexTypeModelBinderProvider>()
         .AddSingleton<IModelBinderProvider, BodyModelBinderProvider>();
   }
}
```


```C#
public interface IValueProvider
{
   bool TryGetValues(string name, out string[] values);

   bool ContainsPrefix(string prefix);
}

public class CompositeValueProvider : IValueProvider
{
   private readonly IEnumerable<IValueProvider> _providers;

   public CompositeValueProvider(IEnumerable<IValueProvider> providers) 
   {
      _providers = providers;
   }

   public bool ContainsPrefix(string prefix)
   {
      _providers.Any(it => it.ContainsPrefix(prefix));
   }

   public bool TryGetValues(string name, out string[] value)
   {
      foreach (var provider in _providers)
      {
         if (provider.TryGetValues(name, out value))
            return true;
      }
      return (value = null) != null;
   }
}

//------------------------V
public class ValueProvider : IValueProvider
{
   private readonly NameValueCollection _values; 
   public static ValueProvider Empty = new ValueProvider(new NameValueCollection());

   public ValueProvider(NameValueCollection values) => _values = new NameValueCollection(StringComparer.OrdinalIgnoreCase) { values };

   public ValueProvider(IEnumerable<KeyValuePair<string, StringValues>> values)
   {
      _values = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
      foreach (var kv in values)
      {
         foreach (var value in kv.Value)
         {
            _values.Add(kv.Key.Replace("-", ""), value);
         }
      }
   }

   public bool ContainsPrefix(string prefix)
   {
      foreach (string key in _values.Keys)
      {
         if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;
      }
      return false;
   }

   public bool TryGetValues(string name, out string[] value)
   {
      value = _values.GetValues(name);
      return value?.Any() == true;
   }
}
//------------------------Ʌ

//------------------------------------V
public interface IValueProviderFactory
{
   IValueProvider CreateValueProvider(ActionContext actionContext);
}

public class QueryStringValueProviderFactory : IValueProviderFactory
{
   public IValueProvider CreateValueProvider(ActionContext actionContext)  => new ValueProvider(actionContext.HttpContext.Request.Query);
}

public class HttpHeaderValueProviderFactory : IValueProviderFactory
{
   public IValueProvider CreateValueProvider(ActionContext actionContext)  => new ValueProvider(actionContext.HttpContext.Request.Headers);
}

public class FormValueProviderFactory : IValueProviderFactory
{
   public IValueProvider CreateValueProvider(ActionContext actionContext)
   {
      var contentType = actionContext.HttpContext.Request.GetTypedHeaders().ContentType;
      return contentType.MediaType.Equals("application/x-www-form-urlencoded") ? new ValueProvider(actionContext.HttpContext.Request.Form) : ValueProvider.Empty;
   }
}
//------------------------------------Ʌ
```

```C#
//------------------------------------<
public class ModelMetadata
{
   public ParameterInfo Parameter { get; }
   public PropertyInfo Property { get; }
   public Type ModelType { get; }
   public bool CanConvertFromString { get; }

   private ModelMetadata(ParameterInfo parameter, PropertyInfo property)
   {
       Parameter = parameter;
       Property = property;
       ModelType = parameter?.ParameterType ?? property.PropertyType;
       CanConvertFromString = TypeDescriptor.GetConverter(ModelType).CanConvertFrom(typeof(string));
   }

   public static ModelMetadata CreateByParameter(ParameterInfo parameter) => new ModelMetadata(parameter, null);

   public static ModelMetadata CreateByProperty(PropertyInfo property) => new ModelMetadata(null, property);
}

public interface IModelBinder
{
   Task BindAsync(ModelBindingContext context);
}

public class ModelBindingContext
{
   public ActionContext ActionContext { get; }
   public string ModelName { get; }
   public ModelMetadata ModelMetadata { get; }
   public IValueProvider ValueProvider { get; }

   public object Model { get; private set; }
   public bool IsModelSet { get; private set; }

   public ModelBindingContext(ActionContext actionContext, string modelName, ModelMetadata modelMetadata, IValueProvider valueProvider)
   {
      ActionContext = actionContext;
      ModelName = modelName;
      ModelMetadata = modelMetadata;
      ValueProvider = valueProvider;
   }

   public void Bind(object model)
   {
      Model = model;
      IsModelSet = true;
   }
}
//------------------------------------<
```

```C#
public interface IModelBinderProvider
{
   IModelBinder GetBinder(ModelMetadata metadata);
}

public interface IModelBinderFactory
{
   IModelBinder CreateBinder(ModelMetadata metadata);
}

public class ModelBinderFactory : IModelBinderFactory
{
   private readonly IEnumerable<IModelBinderProvider> _providers;

   public ModelBinderFactory(IEnumerable<IModelBinderProvider> providers) => _providers = providers;

   public IModelBinder CreateBinder(ModelMetadata metadata)
   {
      foreach (var provider in _providers)
      {
         var binder = provider.GetBinder(metadata);
         if (binder != null)   // IModelBinderProvider will do a precheck on ModelMetadata to return the binder, if doesn't match, return null
         {
            return binder;
         }
      }
      return null;
   }
}
```

```C#
public class SimpleTypeModelBinderProvider : IModelBinderProvider
{
   public IModelBinder GetBinder(ModelMetadata metadata) => metadata.CanConvertFromString ? new SimpleTypeModelBinder() : null;
}

public class SimpleTypeModelBinder : IModelBinder
{
   public Task BindAsync(ModelBindingContext context)
   {
      if (context.ValueProvider.TryGetValues(context.ModelName, out var values))
      {
         var model = Convert.ChangeType(values.Last(), context.ModelMetadata.ModelType);
         context.Bind(model);
      }
      return Task.CompletedTask;
   }
}

/*
public class ComplexTypeModelBinderProvider : IModelBinderProvider {
   // ...
   return new ComplexTypeModelBinder()
}

public class ComplexTypeModelBinder : IModelBinder {}
*/

public class BodyModelBinderProvider : IModelBinderProvider
{
   public IModelBinder GetBinder(ModelMetadata metadata)
   {
      return metadata.Parameter.GetCustomAttribute<FromBodyAttribute>() == null ? null : new BodyModelBinder();                
   }
}

public class BodyModelBinder : IModelBinder
{
   public async Task BindAsync(ModelBindingContext context)
   {
      var input = context.ActionContext.HttpContext.Request.Body;
      var result = await JsonSerializer.DeserializeAsync(input, context.ModelMetadata.ModelType);
      context.Bind(result);
   }
}
```

```C#
public class ControllerActionInvoker : IActionInvoker
{   
   private async Task<object[]> BindArgumentsAsync(MethodInfo methodInfo)
   {
      var parameters = methodInfo.GetParameters();
      if (parameters.Length == 0)
      {
         return new object[0];
      }
      var arguments = new object[parameters.Length];
      for (int index = 0; index < arguments.Length; index++)
      {
         var parameter = parameters[index];
         var metadata = ModelMetadata.CreateByParameter(parameter);

         var requestServices = ActionContext.HttpContext.RequestServices;

         var valueProviderFactories = requestServices.GetServices<IValueProviderFactory>();
         var valueProvider = new CompositeValueProvider(valueProviderFactories.Select(it => it.CreateValueProvider(ActionContext))); 
         
         var modelBinderFactory = requestServices.GetRequiredService<IModelBinderFactory>();
         
         var bindingContext = valueProvider.ContainsPrefix(parameter.Name)
            ? new ModelBindingContext(ActionContext, parameter.Name, metadata, valueProvider)
            : new ModelBindingContext(ActionContext, "", metadata, valueProvider);
        
         var binder = modelBinderFactory.CreateBinder(metadata);
         
         await binder.BindAsync(bindingContext);
         
         arguments[index] = bindingContext.Model;
      } 
      return arguments;
   }

   public async Task InvokeAsync()
   {
      var actionDescriptor =  (ControllerActionDescriptor)ActionContext.ActionDescriptor;
      var controllerType = actionDescriptor.ControllerType;
      var requestServies = ActionContext.HttpContext.RequestServices;
      var controllerInstance = ActivatorUtilities.CreateInstance(requestServies, controllerType);
      if (controllerInstance is Controller controller)
      {
         controller.ActionContext = ActionContext;
      }
      var actionMethod = actionDescriptor.Method;
      var arguments = await BindArgumentsAsync(actionMethod);
      var returnValue = actionMethod.Invoke(controllerInstance, arguments);
      var mapper = requestServies.GetRequiredService<IActionResultTypeMapper>();
      var actionResult = await ToActionResultAsync(returnValue, actionMethod.ReturnType, mapper);
      await actionResult.ExecuteResultAsync(ActionContext);
   }  
}
```

## Source Code

```C#
public class BindPropertyAttribute : Attribute, IBinderTypeProviderMetadata, IBindingSourceMetadata, IModelNameProvider, IRequestPredicateProvider
{
   public BindPropertyAttribute();

   public Type BinderType { get; set; }
   public virtual BindingSource BindingSource { get; protected set; }
   public string Name { get; set; }
   public bool SupportsGet { get; set; }
}

public class BindAttribute : Attribute, IModelNameProvider, IPropertyFilterProvider
{
   public BindAttribute(params string[] include);
   public string[] Include { get; }
   public string Prefix { get; set; }
   public Func<ModelMetadata, bool> PropertyFilter { get; }
}

//---------------------------------------------------------------------------------------------------------------------------------------------------------

public enum ModelValidationState
{
   Unvalidated = 0,
   Invalid = 1,
   Valid = 2,
   Skipped = 3
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