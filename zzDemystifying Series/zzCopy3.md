```C#


public abstract class ModelBinderProviderContext
{
   public abstract IModelBinder CreateBinder(ModelMetadata metadata);

   public virtual IModelBinder CreateBinder(ModelMetadata metadata, BindingInfo bindingInfo)
   {
      throw new NotSupportedException();
   }

   public abstract BindingInfo BindingInfo { get; }

   public abstract ModelMetadata Metadata { get; }

   public abstract IModelMetadataProvider MetadataProvider { get; }

   public virtual IServiceProvider Services { get; } = default!;
}
```



```C#

private readonly struct BinderItem
{
   public BinderItem(IModelBinder modelBinder, ModelMetadata modelMetadata)
   {
      ModelBinder = modelBinder;
      ModelMetadata = modelMetadata;
   }

   public IModelBinder ModelBinder { get; }

   public ModelMetadata ModelMetadata { get; }
}


private sealed class DefaultModelBinderProviderContext : ModelBinderProviderContext
{
   private readonly ModelBinderFactory _factory;

   public DefaultModelBinderProviderContext(ModelBinderFactory factory, ModelBinderFactoryContext factoryContext)
   {
      _factory = factory;
      Metadata = factoryContext.Metadata;
      BindingInfo bindingInfo;
      if (factoryContext.BindingInfo != null)
      {
         bindingInfo = new BindingInfo(factoryContext.BindingInfo);
      }
      else
      {
         bindingInfo = new BindingInfo();
      }
 
      bindingInfo.TryApplyBindingInfo(Metadata);
      BindingInfo = bindingInfo;
 
      MetadataProvider = _factory._metadataProvider;
      Visited = new Dictionary<Key, IModelBinder?>();
   }

   private DefaultModelBinderProviderContext(DefaultModelBinderProviderContext parent, ModelMetadata metadata, BindingInfo bindingInfo)
   {
      Metadata = metadata;
      _factory = parent._factory;
      MetadataProvider = parent.MetadataProvider;
      Visited = parent.Visited;
      BindingInfo = bindingInfo;
   }

   public override BindingInfo BindingInfo { get; }

   public override ModelMetadata Metadata { get; }

   public override IModelMetadataProvider MetadataProvider { get; }

   public Dictionary<Key, IModelBinder?> Visited { get; }

   public override IServiceProvider Services => _factory._serviceProvider;

   public override IModelBinder CreateBinder(ModelMetadata metadata)
   {
      var bindingInfo = new BindingInfo();
      bindingInfo.TryApplyBindingInfo(metadata);

      return CreateBinder(metadata, bindingInfo);
   }

   public override IModelBinder CreateBinder(ModelMetadata metadata, BindingInfo bindingInfo)
   {
      var token = metadata;

      var nestedContext = new DefaultModelBinderProviderContext(this, metadata, bindingInfo);
      return _factory.CreateBinderCoreCached(nestedContext, token);
   }
}


```

```C#

internal static class ControllerBinderDelegateProvider       
{
   public static ControllerBinderDelegate? CreateBinderDelegate(   
      ParameterBinder parameterBinder,
      IModelBinderFactory modelBinderFactory,
      IModelMetadataProvider modelMetadataProvider,
      ControllerActionDescriptor actionDescriptor,
      MvcOptions mvcOptions)
   {
      var parameterBindingInfo = GetParameterBindingInfo(modelBinderFactory, modelMetadataProvider, actionDescriptor);
      var propertyBindingInfo = GetPropertyBindingInfo(modelBinderFactory, modelMetadataProvider, actionDescriptor);
 
      if (parameterBindingInfo == null && propertyBindingInfo == null)
      {
         return null;
      }

      var parameters = actionDescriptor.Parameters switch
      {
         List<ParameterDescriptor> list => list.ToArray(),
         _ => actionDescriptor.Parameters.ToArray()
      };
 
      var properties = actionDescriptor.BoundProperties switch
      {
         List<ParameterDescriptor> list => list.ToArray(),
         _ => actionDescriptor.BoundProperties.ToArray()
      };
 
      return Bind;

      async Task Bind(ControllerContext controllerContext, object controller, Dictionary<string, object?> arguments)
      {
         var (success, valueProvider) = await CompositeValueProvider.TryCreateAsync(controllerContext, controllerContext.ValueProviderFactories);
         if (!success)
         {
            return;
         }

         for (var i = 0; i < parameters.Length; i++)
         {
            var parameter = parameters[i];
            var bindingInfo = parameterBindingInfo![i];
            var modelMetadata = bindingInfo.ModelMetadata;
 
            if (!modelMetadata.IsBindingAllowed)
            {
               continue;
            }
 
            var result = await parameterBinder.BindModelAsync(
               controllerContext,
               bindingInfo.ModelBinder,
               valueProvider,
               parameter,
               modelMetadata,
               value: null,
               container: null); // Parameters do not have containers.
 
            if (result.IsModelSet)
            {
               arguments[parameter.Name] = result.Model;
            }
         }

         for (var i = 0; i < properties.Length; i++)
         {
            var property = properties[i];
            var bindingInfo = propertyBindingInfo![i];
            var modelMetadata = bindingInfo.ModelMetadata;
 
            if (!modelMetadata.IsBindingAllowed)
            {
               continue;
            }
 
            var result = await parameterBinder.BindModelAsync(
               controllerContext,
               bindingInfo.ModelBinder,
               valueProvider,
               property,
               modelMetadata,
               value: null,
               container: controller);
 
            if (result.IsModelSet)
            {
               PropertyValueSetter.SetValue(bindingInfo.ModelMetadata, controller, result.Model);
            }
         }
      }
   }
   
   private static BinderItem[]? GetParameterBindingInfo(
      IModelBinderFactory modelBinderFactory,
      IModelMetadataProvider modelMetadataProvider,
      ControllerActionDescriptor actionDescriptor)
   {
      var parameters = actionDescriptor.Parameters;
      if (parameters.Count == 0)
      {
         return null;
      }

      var parameterBindingInfo = new BinderItem[parameters.Count];
      for (var i = 0; i < parameters.Count; i++)
      {
         var parameter = parameters[i];

         ModelMetadata metadata;
         if (modelMetadataProvider is ModelMetadataProvider modelMetadataProviderBase 
             && parameter is ControllerParameterDescriptor controllerParameterDescriptor)
         {
            metadata = modelMetadataProviderBase.GetMetadataForParameter(controllerParameterDescriptor.ParameterInfo);
         }
         else
         {
            // for backward compatibility
            metadata = modelMetadataProvider.GetMetadataForType(parameter.ParameterType);
         }

         var binder = modelBinderFactory.CreateBinder(new ModelBinderFactoryContext 
         {
            BindingInfo = parameter.BindingInfo,
            Metadata = metadata,
            CacheToken = parameter,
         });

         parameterBindingInfo[i] = new BinderItem(binder, metadata);
      }

      return parameterBindingInfo;
   }

   private static BinderItem[]? GetPropertyBindingInfo(
      IModelBinderFactory modelBinderFactory,
      IModelMetadataProvider modelMetadataProvider,
      ControllerActionDescriptor actionDescriptor)
   { ... }
}

```

```C#

public readonly struct ModelBindingResult : IEquatable<ModelBindingResult>
{
   public static ModelBindingResult Failed()
   {
      return new ModelBindingResult(model: null, isModelSet: false);
   }

   public static ModelBindingResult Success(object? model)
   {
      return new ModelBindingResult(model, isModelSet: true);
   }

   private ModelBindingResult(object? model, bool isModelSet)
   {
      Model = model;
      IsModelSet = isModelSet;
   }

   public object? Model { get; }

   public bool IsModelSet { get; }

   public bool Equals(ModelBindingResult other)
   {
      return IsModelSet == other.IsModelSet && object.Equals(Model, other.Model);
   }
}

```

```C#

public abstract class ModelMetadata : IEquatable<ModelMetadata?>, IModelMetadataProvider
{
   public static readonly int DefaultOrder = 10000;

   private static readonly ParameterBindingMethodCache ParameterBindingMethodCache = new(throwOnInvalidMethod: false);

   private int? _hashCode;
   private IReadOnlyList<ModelMetadata>? _boundProperties;
   private IReadOnlyDictionary<ModelMetadata, ModelMetadata>? _parameterMapping;
   private IReadOnlyDictionary<ModelMetadata, ModelMetadata>? _boundConstructorPropertyMapping;
   private Exception? _recordTypeValidatorsOnPropertiesError;
   private bool _recordTypeConstructorDetailsCalculated;

   protected ModelMetadata(ModelMetadataIdentity identity)
   {
      Identity = identity;
      InitializeTypeInformation();
   }

   public Type? ContainerType => Identity.ContainerType;

   public virtual ModelMetadata? ContainerMetadata
   {
      get {
         throw new NotImplementedException();
      }
   }

   public ModelMetadataKind MetadataKind => Identity.MetadataKind;

   public Type ModelType => Identity.ModelType;

   public string? Name => Identity.Name;

   public string? ParameterName => MetadataKind == ModelMetadataKind.Parameter ? Identity.Name : null;

   public string? PropertyName => MetadataKind == ModelMetadataKind.Property ? Identity.Name : null;

   protected internal ModelMetadataIdentity Identity { get; }

   public abstract IReadOnlyDictionary<object, object> AdditionalValues { get; }

   public abstract ModelPropertyCollection Properties { get; }

   internal IReadOnlyList<ModelMetadata> BoundProperties
   {
      get {
         if (BoundConstructor is null)
         {
            return Properties;
         }

         if (_boundProperties is null)
         {
            var boundParameters = BoundConstructor.BoundConstructorParameters!;
            var boundProperties = new List<ModelMetadata>();
 
            foreach (var metadata in Properties)
            {
               if (!boundParameters.Any(p => string.Equals(p.ParameterName, metadata.PropertyName, StringComparison.Ordinal) 
                   && p.ModelType == metadata.ModelType))
               {
                  boundProperties.Add(metadata);
               }
            }
 
            _boundProperties = boundProperties;
         }
 
         return _boundProperties;
      }
   }

   internal IReadOnlyDictionary<ModelMetadata, ModelMetadata> BoundConstructorParameterMapping
   {
      get {
         Debug.Assert(BoundConstructor != null, "This API can be only called for types with bound constructors.");
         CalculateRecordTypeConstructorDetails();
 
         return _parameterMapping;
      }
   }

   internal IReadOnlyDictionary<ModelMetadata, ModelMetadata> BoundConstructorPropertyMapping
   {
      get {
         Debug.Assert(BoundConstructor != null, "This API can be only called for types with bound constructors.");
         CalculateRecordTypeConstructorDetails();
 
         return _boundConstructorPropertyMapping;
      }
   }

   public virtual ModelMetadata? BoundConstructor { get; }

   public virtual IReadOnlyList<ModelMetadata>? BoundConstructorParameters { get; }

   public abstract string? BinderModelName { get; }

   public abstract Type? BinderType { get; }

   public abstract BindingSource? BindingSource { get; }

   public abstract bool ConvertEmptyStringToNull { get; }

   public abstract string? DataTypeName { get; }

   public abstract string? DisplayName { get; }

   public abstract bool IsRequired { get; }
   
   // ...
}

```

```C#

//
public interface IModelMetadataProvider
{
   ModelMetadata GetMetadataForType(Type modelType);

   IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType);
}

public abstract class ModelMetadataProvider : IModelMetadataProvider
{
   public abstract IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType);
   public abstract ModelMetadata GetMetadataForType(Type modelType);
   public abstract ModelMetadata GetMetadataForParameter(ParameterInfo parameter);
   public virtual ModelMetadata GetMetadataForParameter(ParameterInfo parameter, Type modelType) => throw new NotSupportedException();
   public virtual ModelMetadata GetMetadataForProperty(PropertyInfo propertyInfo, Type modelType) => throw new NotSupportedException();
   public virtual ModelMetadata GetMetadataForConstructor(ConstructorInfo constructor, Type modelType) => throw new NotSupportedException();
}
//

public class DefaultModelMetadataProvider : ModelMetadataProvider
{
   private readonly ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataCacheEntry> _modelMetadataCache = new();
   private readonly Func<ModelMetadataIdentity, ModelMetadataCacheEntry> _cacheEntryFactory;
   private readonly ModelMetadataCacheEntry _metadataCacheEntryForObjectType;

   public DefaultModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider) : this(detailsProvider, new DefaultModelBindingMessageProvider()) { }

   public DefaultModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider, IOptions<MvcOptions> optionsAccessor) 
      : this(detailsProvider, GetMessageProvider(optionsAccessor)) { }

   private DefaultModelMetadataProvider(ICompositeMetadataDetailsProvider detailsProvider, DefaultModelBindingMessageProvider modelBindingMessageProvider)
   {
      DetailsProvider = detailsProvider;
      ModelBindingMessageProvider = modelBindingMessageProvider;
 
      _cacheEntryFactory = CreateCacheEntry;
      _metadataCacheEntryForObjectType = GetMetadataCacheEntryForObjectType();
   }

   protected ICompositeMetadataDetailsProvider DetailsProvider { get; }

   protected DefaultModelBindingMessageProvider ModelBindingMessageProvider { get; }

   internal void ClearCache() => _modelMetadataCache.Clear();

   public override IEnumerable<ModelMetadata> GetMetadataForProperties(Type modelType)
   {
      var cacheEntry = GetCacheEntry(modelType);

      if (cacheEntry.Details.Properties == null)
      {
         var key = ModelMetadataIdentity.ForType(modelType);
         var propertyDetails = CreatePropertyDetails(key);
 
         var properties = new ModelMetadata[propertyDetails.Length];
         for (var i = 0; i < properties.Length; i++)
         {
            propertyDetails[i].ContainerMetadata = cacheEntry.Metadata;
            properties[i] = CreateModelMetadata(propertyDetails[i]);
         }
 
         cacheEntry.Details.Properties = properties;
      }

      return cacheEntry.Details.Properties;
   }

   public override ModelMetadata GetMetadataForParameter(ParameterInfo parameter) 
   {
      return GetMetadataForParameter(parameter, parameter.ParameterType);
   }

   public override ModelMetadata GetMetadataForParameter(ParameterInfo parameter, Type modelType)
   {
      var cacheEntry = GetCacheEntry(parameter, modelType);
      return cacheEntry.Metadata;
   }

   public override ModelMetadata GetMetadataForType(Type modelType)
   {
      var cacheEntry = GetCacheEntry(modelType);
      return cacheEntry.Metadata;
   }

   public override ModelMetadata GetMetadataForProperty(PropertyInfo propertyInfo, Type modelType)
   {
      var cacheEntry = GetCacheEntry(propertyInfo, modelType);
      return cacheEntry.Metadata;
   }

   public override ModelMetadata GetMetadataForConstructor(ConstructorInfo constructorInfo, Type modelType)
   {
      var cacheEntry = GetCacheEntry(constructorInfo, modelType);
      return cacheEntry.Metadata;
   }

   private static DefaultModelBindingMessageProvider GetMessageProvider(IOptions<MvcOptions> optionsAccessor)
   {
      return optionsAccessor.Value.ModelBindingMessageProvider;
   }

   private ModelMetadataCacheEntry GetCacheEntry(Type modelType)
   {
      ModelMetadataCacheEntry cacheEntry;

      if (modelType == typeof(object))
      {
         cacheEntry = _metadataCacheEntryForObjectType;
      }
      else
      {
         var key = ModelMetadataIdentity.ForType(modelType);
 
         cacheEntry = _modelMetadataCache.GetOrAdd(key, _cacheEntryFactory);
      }

      return cacheEntry;
   }

   private ModelMetadataCacheEntry GetCacheEntry(ParameterInfo parameter, Type modelType)
   {
      return _modelMetadataCache.GetOrAdd(ModelMetadataIdentity.ForParameter(parameter, modelType), _cacheEntryFactory);
   }

   // ...

   private readonly struct ModelMetadataCacheEntry
   {
      public ModelMetadataCacheEntry(ModelMetadata metadata, DefaultMetadataDetails details)
      {
         Metadata = metadata;
         Details = details;
      }
 
      public ModelMetadata Metadata { get; }
 
      public DefaultMetadataDetails Details { get; }
   }
}

```

```C#

//
public interface IModelBinderFactory
{
   IModelBinder CreateBinder(ModelBinderFactoryContext context);
}
//

public partial class ModelBinderFactory : IModelBinderFactory
{
   private readonly IModelMetadataProvider _metadataProvider;
   private readonly IModelBinderProvider[] _providers;
   private readonly ConcurrentDictionary<Key, IModelBinder> _cache;
   private readonly IServiceProvider _serviceProvider;

   public ModelBinderFactory(IModelMetadataProvider metadataProvider, 
                             IOptions<MvcOptions> options, 
                             IServiceProvider serviceProvider)
   {
      _metadataProvider = metadataProvider;
      _providers = options.Value.ModelBinderProviders.ToArray();
      _serviceProvider = serviceProvider;
      _cache = new ConcurrentDictionary<Key, IModelBinder>();
   }

   public IModelBinder CreateBinder(ModelBinderFactoryContext context)
   {
      if (TryGetCachedBinder(context.Metadata, context.CacheToken, out var binder))
      {
         return binder;
      }

      var providerContext = new DefaultModelBinderProviderContext(this, context);
      binder = CreateBinderCoreUncached(providerContext, context.CacheToken);

      AddToCache(context.Metadata, context.CacheToken, binder);

      return binder;
   }

   private IModelBinder CreateBinderCoreCached(DefaultModelBinderProviderContext providerContext, object? token)
   {
      if (TryGetCachedBinder(providerContext.Metadata, token, out var binder))
      {
         return binder;
      }

      // we're definitely creating a binder for an non-root node here, so it's OK for binder creation to fail
      binder = CreateBinderCoreUncached(providerContext, token) ?? NoOpBinder.Instance;

      if (!(binder is PlaceholderBinder))
      {
         AddToCache(providerContext.Metadata, token, binder);
      }

      return binder;
   }

   private IModelBinder? CreateBinderCoreUncached(DefaultModelBinderProviderContext providerContext, object? token)
   {
      if (!providerContext.Metadata.IsBindingAllowed)
      {
         return NoOpBinder.Instance;
      }

      var key = new Key(providerContext.Metadata, token);

      var visited = providerContext.Visited;

      if (visited.TryGetValue(key, out var binder))
      {
         if (binder != null)
         {
            return binder;
         }
 
         binder = new PlaceholderBinder();
         visited[key] = binder;
         return binder;
      }

      // OK this isn't a recursive case (yet) so add an entry and then ask the providers to create the binder
      visited.Add(key, null);

      IModelBinder? result = null;

      for (var i = 0; i < _providers.Length; i++)
      {
         var provider = _providers[i];
         result = provider.GetBinder(providerContext);
         if (result != null)
         {
            break;
         }
      }

      // if the PlaceholderBinder was created, then it means we recursed. Hook it up to the 'real' binder.
      if (visited[key] is PlaceholderBinder placeholderBinder)
      {
         placeholderBinder.Inner = result ?? NoOpBinder.Instance;
      }

      if (result != null)
      {
         visited[key] = result;
      }

      return result;
   }

   private void AddToCache(ModelMetadata metadata, object? cacheToken, IModelBinder binder)
   {
      if (cacheToken == null)
         return;
      
      _cache.TryAdd(new Key(metadata, cacheToken), binder);
   }

   private bool TryGetCachedBinder(ModelMetadata metadata, object? cacheToken, [NotNullWhen(true)] out IModelBinder? binder)
   {
      if (cacheToken == null)
      {
         binder = null;
         return false;
      }
 
      return _cache.TryGetValue(new Key(metadata, cacheToken), out binder);
   }
}
```

```C#

//
public interface IValueProviderFactory
{
   Task CreateValueProviderAsync(ValueProviderFactoryContext context);
}
//

public class QueryStringValueProviderFactory : IValueProviderFactory
{
   public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
   {
      var query = context.ActionContext.HttpContext.Request.Query;
      if (query != null && query.Count > 0)
      {
         var valueProvider = new QueryStringValueProvider(BindingSource.Query, query, CultureInfo.InvariantCulture);
         context.ValueProviders.Add(valueProvider);
      }

      return Task.CompletedTask;
   }
}

public class FormValueProviderFactory 
{
   public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
   {
      var request = context.ActionContext.HttpContext.Request;
      if (request.HasFormContentType)
         return AddValueProviderAsync(context);   // allocating a Task only when the body is form data

      return Task.CompletedTask;
   }

   private static async Task AddValueProviderAsync(ValueProviderFactoryContext context)
   {
      var request = context.ActionContext.HttpContext.Request;
      IFormCollection form;

      try {
         form = await request.ReadFormAsync();
      }
      catch (InvalidDataException ex) {  
         throw new ValueProviderException(Resources.FormatFailedToReadRequestForm(ex.Message), ex);
      }
      catch (IOException ex) {
         throw new ValueProviderException(Resources.FormatFailedToReadRequestForm(ex.Message), ex);
      }

      var valueProvider = new FormValueProvider(BindingSource.Form, form, CultureInfo.CurrentCulture);
 
      context.ValueProviders.Add(valueProvider);
   }
}

```


```C#

//
public interface IValueProvider
{
   bool ContainsPrefix(string prefix);
   ValueProviderResult GetValue(string key);
}

public interface IEnumerableValueProvider : IValueProvider
{
   IDictionary<string, string> GetKeysFromPrefix(string prefix);
}

public interface IBindingSourceValueProvider : IValueProvider
{
   IValueProvider? Filter(BindingSource bindingSource);
}
//

public class QueryStringValueProvider : BindingSourceValueProvider, IEnumerableValueProvider
{
   private readonly IQueryCollection _values;
   private PrefixContainer? _prefixContainer;

   public QueryStringValueProvider(BindingSource bindingSource, IQueryCollection values, CultureInfo? culture) : base(bindingSource)
   {
      _values = values;
      Culture = culture;
   }

   public CultureInfo? Culture { get; }

   protected PrefixContainer PrefixContainer
   {
      get {
         if (_prefixContainer == null)
            _prefixContainer = new PrefixContainer(_values.Keys);
 
         return _prefixContainer;
      }
   }

   public override bool ContainsPrefix(string prefix) => PrefixContainer.ContainsPrefix(prefix);

   public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix) => PrefixContainer.GetKeysFromPrefix(prefix);

   public override ValueProviderResult GetValue(string key)
   {
      if (key.Length == 0)
         return ValueProviderResult.None;
      
      var values = _values[key];
      if (values.Count == 0)
         return ValueProviderResult.None;
      else
         return new ValueProviderResult(values, Culture);
   }
}



//
public interface IValueProvider
{
   bool ContainsPrefix(string prefix);
   ValueProviderResult GetValue(string key);
}

public interface IEnumerableValueProvider : IValueProvider
{
   IDictionary<string, string> GetKeysFromPrefix(string prefix);
}

public interface IBindingSourceValueProvider : IValueProvider
{
   IValueProvider? Filter(BindingSource bindingSource);
}
//

public class FormValueProvider : BindingSourceValueProvider, IEnumerableValueProvider
{
   private readonly IFormCollection _values;
   private readonly HashSet<string?>? _invariantValueKeys;
   private PrefixContainer? _prefixContainer;

   public FormValueProvider(BindingSource bindingSource, IFormCollection values, CultureInfo? culture) : base(bindingSource)
   {
      _values = values;
 
      if (_values.TryGetValue(FormValueHelper.CultureInvariantFieldName, out var invariantKeys) && invariantKeys.Count > 0)
      {
         _invariantValueKeys = new(invariantKeys, StringComparer.OrdinalIgnoreCase);
      }
 
      Culture = culture;
   }

   public CultureInfo? Culture { get; }

   protected PrefixContainer PrefixContainer {
      get {
         if (_prefixContainer == null)
            _prefixContainer = new PrefixContainer(_values.Keys);
         return _prefixContainer;
        }
   }

   public override bool ContainsPrefix(string prefix) => PrefixContainer.ContainsPrefix(prefix);
   
   public virtual IDictionary<string, string> GetKeysFromPrefix(string prefix) => PrefixContainer.GetKeysFromPrefix(prefix);
   
   public override ValueProviderResult GetValue(string key)
   {
      if (key.Length == 0)
         return ValueProviderResult.None;
         
      var values = _values[key];
      if (values.Count == 0)
      {
         return ValueProviderResult.None;
      }
      else
      {
         var culture = _invariantValueKeys?.Contains(key) == true ? CultureInfo.InvariantCulture : Culture;
         return new ValueProviderResult(values, culture);
      }
   }
}

```


```C#

public class BindingSource : IEquatable<BindingSource?>
{
   public static readonly BindingSource Body = new BindingSource("Body", Resources.BindingSource_Body, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource Custom = new BindingSource("Custom", Resources.BindingSource_Custom, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource Form = new BindingSource("Form", Resources.BindingSource_Form, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Header = new BindingSource("Header", Resources.BindingSource_Header, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource ModelBinding = new BindingSource("ModelBinding", Resources.BindingSource_ModelBinding, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Path = new BindingSource("Path", Resources.BindingSource_Path, isGreedy: false, isFromRequest: true);     // url request url path
   public static readonly BindingSource Query = new BindingSource("Query", Resources.BindingSource_Query, isGreedy: false, isFromRequest: true);  // query-string
   public static readonly BindingSource Services = new BindingSource("Services", Resources.BindingSource_Services, isGreedy: true, isFromRequest: false);
   public static readonly BindingSource Special = new BindingSource("Special", Resources.BindingSource_Special, isGreedy: true, isFromRequest: false);  // when not user input
   public static readonly BindingSource FormFile = new BindingSource("FormFile", Resources.BindingSource_FormFile, isGreedy: true, isFromRequest: true);

   public BindingSource(string id, string displayName, bool isGreedy, bool isFromRequest)
   {
      Id = id;
      DisplayName = displayName;
      IsGreedy = isGreedy;
      IsFromRequest = isFromRequest;
   }

   public string DisplayName { get; }
   public string Id { get; }
   public bool IsGreedy { get; }
   public bool IsFromRequest { get; }

   public virtual bool CanAcceptDataFrom(BindingSource bindingSource)
   {
      if (bindingSource is CompositeBindingSource)
         throw new ArgumentNullException(nameof(bindingSource));

      if (bindingSource is CompositeBindingSource)
      {
         var message = Resources.FormatBindingSource_CannotBeComposite(bindingSource.DisplayName, nameof(CanAcceptDataFrom));
         throw new ArgumentException(message, nameof(bindingSource));
      }

      if (this == bindingSource)
      {
         return true;
      }

      if (this == ModelBinding)
      {
         return bindingSource == Form || bindingSource == Path || bindingSource == Query;
      }
 
      return false;
   }
}


public class CompositeBindingSource : BindingSource
{
   public static CompositeBindingSource Create(IEnumerable<BindingSource> bindingSources, string displayName)
   {
      foreach (var bindingSource in bindingSources)
      {
         if (bindingSource.IsGreedy) {
            var message = Resources.FormatBindingSource_CannotBeGreedy(bindingSource.DisplayName, nameof(CompositeBindingSource));
            throw new ArgumentException(message, nameof(bindingSources));
         }

         if (!bindingSource.IsFromRequest) {
            var message = Resources.FormatBindingSource_MustBeFromRequest(bindingSource.DisplayName, nameof(CompositeBindingSource));
            throw new ArgumentException(message, nameof(bindingSources));
         }

         if (bindingSource is CompositeBindingSource) {
            var message = Resources.FormatBindingSource_CannotBeComposite(bindingSource.DisplayName, nameof(CompositeBindingSource));
            throw new ArgumentException(message, nameof(bindingSources));
         }
      }

      var id = string.Join("&", bindingSources.Select(s => s.Id).OrderBy(s => s, StringComparer.Ordinal));
      return new CompositeBindingSource(id, displayName, bindingSources);
   }

   private CompositeBindingSource(string id, string displayName, IEnumerable<BindingSource> bindingSources) : base(id, displayName, isGreedy: false, isFromRequest: true)
   {
      BindingSources = bindingSources;
   }

   public IEnumerable<BindingSource> BindingSources { get; }

   public override bool CanAcceptDataFrom(BindingSource bindingSource)
   {
      if (bindingSource is CompositeBindingSource)
        throw new ArgumentException(message, nameof(bindingSource));

      foreach (var source in BindingSources)
      {
         if (source.CanAcceptDataFrom(bindingSource))
         {
            return true;
         }
      }

      return false;
   }
}


public class BindingInfo
{
   private Type? _binderType;

   public BindingInfo() { }

   public BindingInfo(BindingInfo other)
   {
      BindingSource = other.BindingSource;
      BinderModelName = other.BinderModelName;
      BinderType = other.BinderType;
      PropertyFilterProvider = other.PropertyFilterProvider;
      RequestPredicate = other.RequestPredicate;
      EmptyBodyBehavior = other.EmptyBodyBehavior;
   }

   public BindingSource? BindingSource { get; set; }

   public string? BinderModelName { get; set; }

   public Type? BinderType
   {
      get => _binderType;
      set {
         if (value != null && !typeof(IModelBinder).IsAssignableFrom(value))
            throw new ArgumentException(Resources.FormatBinderType_MustBeIModelBinder(value.FullName, typeof(IModelBinder).FullName), nameof(value));
         
         _binderType = value;
      }
   }

   public IPropertyFilterProvider? PropertyFilterProvider { get; set; }

   public Func<ActionContext, bool>? RequestPredicate { get; set; }  // a predicate determines if the model should be bound based on state from the current request

   public EmptyBodyBehavior EmptyBodyBehavior { get; set; }  // gets or sets the value which decides if empty bodies are treated as valid inputs

   public static BindingInfo? GetBindingInfo(IEnumerable<object> attributes)
   {
      var bindingInfo = new BindingInfo();
      var isBindingInfoPresent = false;

      foreach (var binderModelNameAttribute in attributes.OfType<IModelNameProvider>())  // BinderModelName
      {
         isBindingInfoPresent = true;
         if (binderModelNameAttribute?.Name != null)
         {
            bindingInfo.BinderModelName = binderModelNameAttribute.Name;
            break;
         }
      }

      foreach (var binderTypeAttribute in attributes.OfType<IBinderTypeProviderMetadata>())  // BinderType
      {
         isBindingInfoPresent = true;
         if (binderTypeAttribute.BinderType != null)
         {
            bindingInfo.BinderType = binderTypeAttribute.BinderType;
            break;
         }
      }
    
      foreach (var bindingSourceAttribute in attributes.OfType<IBindingSourceMetadata>())  // BindingSource
      {
         isBindingInfoPresent = true;
         if (bindingSourceAttribute.BindingSource != null)
         {
            bindingInfo.BindingSource = bindingSourceAttribute.BindingSource;
            break;
         }
      }

      var propertyFilterProviders = attributes.OfType<IPropertyFilterProvider>().ToArray();  // PropertyFilterProvider
      if (propertyFilterProviders.Length == 1)
      {
         isBindingInfoPresent = true;
         bindingInfo.PropertyFilterProvider = propertyFilterProviders[0];
      }
      else if (propertyFilterProviders.Length > 1)
      {
         isBindingInfoPresent = true;
         bindingInfo.PropertyFilterProvider = new CompositePropertyFilterProvider(propertyFilterProviders);
      }

      foreach (var requestPredicateProvider in attributes.OfType<IRequestPredicateProvider>())  // RequestPredicate
      {
         isBindingInfoPresent = true;
         if (requestPredicateProvider.RequestPredicate != null)
         {
            bindingInfo.RequestPredicate = requestPredicateProvider.RequestPredicate;
            break;
         }
      }

      foreach (var configureEmptyBodyBehavior in attributes.OfType<IConfigureEmptyBodyBehavior>())
      {
         isBindingInfoPresent = true;
         bindingInfo.EmptyBodyBehavior = configureEmptyBodyBehavior.EmptyBodyBehavior;
         break;
      }
 
      return isBindingInfoPresent ? bindingInfo : null;
   }

   public static BindingInfo? GetBindingInfo(IEnumerable<object> attributes, ModelMetadata modelMetadata)
   {
      var bindingInfo = GetBindingInfo(attributes);
      var isBindingInfoPresent = bindingInfo != null;

      if (bindingInfo == null)
      {
         bindingInfo = new BindingInfo();
      }

      isBindingInfoPresent |= bindingInfo.TryApplyBindingInfo(modelMetadata);
      
      return isBindingInfoPresent ? bindingInfo : null;
   }

   public bool TryApplyBindingInfo(ModelMetadata modelMetadata)
   {
      var isBindingInfoPresent = false;
      if (BinderModelName == null && modelMetadata.BinderModelName != null)
      {
         isBindingInfoPresent = true;
         BinderModelName = modelMetadata.BinderModelName;
      }
 
      if (BinderType == null && modelMetadata.BinderType != null)
      {
         isBindingInfoPresent = true;
         BinderType = modelMetadata.BinderType;
      }
 
      if (BindingSource == null && modelMetadata.BindingSource != null)
      {
         isBindingInfoPresent = true;
         BindingSource = modelMetadata.BindingSource;
      }
 
      if (PropertyFilterProvider == null && modelMetadata.PropertyFilterProvider != null)
      {
         isBindingInfoPresent = true;
         PropertyFilterProvider = modelMetadata.PropertyFilterProvider;
      }

      if (EmptyBodyBehavior == EmptyBodyBehavior.Default && BindingSource == BindingSource.Body &&
         (modelMetadata.NullabilityState == NullabilityState.Nullable || modelMetadata.IsNullableValueType || modelMetadata.HasDefaultValue))
      {
         isBindingInfoPresent = true;
         EmptyBodyBehavior = EmptyBodyBehavior.Allow;
      }
 
      return isBindingInfoPresent;
   }

   
}



private sealed class CompositePropertyFilterProvider : IPropertyFilterProvider
{
   private readonly IEnumerable<IPropertyFilterProvider> _providers;
 
   public CompositePropertyFilterProvider(IEnumerable<IPropertyFilterProvider> providers)
   {
      _providers = providers;
   }
 
   public Func<ModelMetadata, bool> PropertyFilter => CreatePropertyFilter();

   private Func<ModelMetadata, bool> CreatePropertyFilter()
   {
      var propertyFilters = _providers.Select(p => p.PropertyFilter).Where(p => p != null);

      return (m) => 
      {
         foreach (var propertyFilter in propertyFilters) {
            if (!propertyFilter(m))
               return false;         
         }
 
         return true;
      };
   }
}


```