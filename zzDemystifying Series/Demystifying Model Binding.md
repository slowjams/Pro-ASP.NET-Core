Demystifying Model Binding
=================================








```C#
//---------------------------V
public class ActionDescriptor
{
   public ActionDescriptor()
   {
      Id = Guid.NewGuid().ToString();
      Properties = new Dictionary<object, object>();
      RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   
   }

   public string Id { get; }

   public IDictionary<string, string> RouteValues { get; set; }

   public AttributeRouteInfo? AttributeRouteInfo { get; set; }

   public IList<IActionConstraintMetadata>? ActionConstraints { get; set; }

   public IList<object> EndpointMetadata { get; set; } = Array.Empty<ParameterDescriptor>();

   public IList<ParameterDescriptor> Parameters { get; set; } = Array.Empty<ParameterDescriptor>();       // <-------------------

   public IList<ParameterDescriptor> BoundProperties { get; set; } = Array.Empty<ParameterDescriptor>();  // <-------------------

   public IList<FilterDescriptor> FilterDescriptors { get; set; } = Array.Empty<FilterDescriptor>();   

   public virtual string? DisplayName { get; set; }

   public IDictionary<object, object?> Properties { get; set; } = default!;

   internal IFilterMetadata[]? CachedReusableFilters { get; set; }   
}
//---------------------------Ʌ

//------------------------------V
public class ParameterDescriptor
{
   public string Name { get; set; } = default!;
   public Type ParameterType { get; set; } = default!;
   public BindingInfo? BindingInfo { get; set; }
}
//------------------------------Ʌ

public interface IBindingSourceMetadata
{
   BindingSource? BindingSource { get; }
}

//-----------------------------------------------------V
public class BindingSource : IEquatable<BindingSource?>
{
   public static readonly BindingSource Body = new BindingSource("Body", Resources.BindingSource_Body, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource Custom = new BindingSource("Custom", Resources.BindingSource_Custom, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource Form = new BindingSource("Form", Resources.BindingSource_Form, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Header = new BindingSource("Header", Resources.BindingSource_Header, isGreedy: true, isFromRequest: true);
   public static readonly BindingSource ModelBinding = new BindingSource("ModelBinding", Resources.BindingSource_ModelBinding, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Path = new BindingSource("Path", Resources.BindingSource_Path, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Query = new BindingSource("Query", Resources.BindingSource_Query, isGreedy: false, isFromRequest: true);
   public static readonly BindingSource Services = new BindingSource("Services", Resources.BindingSource_Services, isGreedy: true, isFromRequest: false);
   public static readonly BindingSource Special = new BindingSource("Special", Resources.BindingSource_Special, isGreedy: true, isFromRequest: false);
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
         throw new ArgumentException(message, nameof(bindingSource));

      if (this == bindingSource)
         return true;

      if (this == ModelBinding)
         return bindingSource == Form || bindingSource == Path || bindingSource == Query;
      
      return false;
   }

   public bool Equals(BindingSource? other) => return string.Equals(other?.Id, Id, StringComparison.Ordinal);

   public override bool Equals(object? obj) => Equals(obj as BindingSource);
  
   // ...
}
//-----------------------------------------------------Ʌ

//------------------------------V
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
      set
      {
         if (value != null && !typeof(IModelBinder).IsAssignableFrom(value))
         {
            throw new ArgumentException(...);
         }
 
         _binderType = value;
      }
   }

   public IPropertyFilterProvider? PropertyFilterProvider { get; set; }

   public Func<ActionContext, bool>? RequestPredicate { get; set; }

   public EmptyBodyBehavior EmptyBodyBehavior { get; set; }

   public static BindingInfo? GetBindingInfo(IEnumerable<object> attributes)
   {
      var bindingInfo = new BindingInfo();
      var isBindingInfoPresent = false;
 
      // BinderModelName
      foreach (var binderModelNameAttribute in attributes.OfType<IModelNameProvider>())
      {
         isBindingInfoPresent = true;
         if (binderModelNameAttribute?.Name != null)
         {
            bindingInfo.BinderModelName = binderModelNameAttribute.Name;
            break;
         }
      }

      // BinderType
      foreach (var binderTypeAttribute in attributes.OfType<IBinderTypeProviderMetadata>())
      {
         isBindingInfoPresent = true;
         if (binderTypeAttribute.BinderType != null)
         {
            bindingInfo.BinderType = binderTypeAttribute.BinderType;
            break;
         }
      }

      // BindingSource
      foreach (var bindingSourceAttribute in attributes.OfType<IBindingSourceMetadata>())
      {
         isBindingInfoPresent = true;
         if (bindingSourceAttribute.BindingSource != null)
         {
            bindingInfo.BindingSource = bindingSourceAttribute.BindingSource;
            break;
         }
      }

      // PropertyFilterProvider
      var propertyFilterProviders = attributes.OfType<IPropertyFilterProvider>().ToArray();
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

      // RequestPredicate
      foreach (var requestPredicateProvider in attributes.OfType<IRequestPredicateProvider>())
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

      if (EmptyBodyBehavior == EmptyBodyBehavior.Default &&
         BindingSource == BindingSource.Body &&
         (modelMetadata.NullabilityState == NullabilityState.Nullable || modelMetadata.IsNullableValueType || modelMetadata.HasDefaultValue))
      {
         isBindingInfoPresent = true;
         EmptyBodyBehavior = EmptyBodyBehavior.Allow;
      }

      return isBindingInfoPresent;
   }

   // inner private class
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
            foreach (var propertyFilter in propertyFilters)
            {
               if (!propertyFilter(m))
               {
                  return false;
               }
            }
 
            return true;
         };
      }
   }
}
```


```C#
public interface IMetadataDetailsProvider
{

}

//-----------------------------------------V
public class BindingMetadataProviderContext
{
   public BindingMetadataProviderContext(ModelMetadataIdentity key, ModelAttributes attributes)
   {
       Key = key;
       Attributes = attributes.Attributes;
       ParameterAttributes = attributes.ParameterAttributes;
       PropertyAttributes = attributes.PropertyAttributes;
       TypeAttributes = attributes.TypeAttributes;
 
       BindingMetadata = new BindingMetadata();
   }

   public IReadOnlyList<object> Attributes { get; }

   public ModelMetadataIdentity Key { get; }

   public IReadOnlyList<object>? ParameterAttributes { get; }

   public IReadOnlyList<object>? PropertyAttributes { get; }

   public IReadOnlyList<object>? TypeAttributes { get; }

   public BindingMetadata BindingMetadata { get; }
}
//-----------------------------------------Ʌ


//---------------------------------------V
public interface IBindingMetadataProvider : IMetadataDetailsProvider
{
   void CreateBindingMetadata(BindingMetadataProviderContext context);
}
//---------------------------------------Ʌ


//--------------------------------------------------V
internal sealed class DefaultBindingMetadataProvider : IBindingMetadataProvider
{
   public void CreateBindingMetadata(BindingMetadataProviderContext context)
   {
      // BinderModelName
      foreach (var binderModelNameAttribute in context.Attributes.OfType<IModelNameProvider>())
      {
         if (binderModelNameAttribute.Name != null)
         {
            context.BindingMetadata.BinderModelName = binderModelNameAttribute.Name;
            break;
         }
      }

      // BinderType
      foreach (var binderTypeAttribute in context.Attributes.OfType<IBinderTypeProviderMetadata>())
      {
         if (binderTypeAttribute.BinderType != null)
         {
            context.BindingMetadata.BinderType = binderTypeAttribute.BinderType;
            break;
         }
      }

      // BindingSource
      foreach (var bindingSourceAttribute in context.Attributes.OfType<IBindingSourceMetadata>())
      {
         if (bindingSourceAttribute.BindingSource != null)
         {
            context.BindingMetadata.BindingSource = bindingSourceAttribute.BindingSource;
            break;
         }
      }

      // PropertyFilterProvider
      var propertyFilterProviders = context.Attributes.OfType<IPropertyFilterProvider>().ToArray();

      if (propertyFilterProviders.Length == 0)
      {
         context.BindingMetadata.PropertyFilterProvider = null;
      }
      else if (propertyFilterProviders.Length == 1)
      {
         context.BindingMetadata.PropertyFilterProvider = propertyFilterProviders[0];
      }
      else
      {
         var composite = new CompositePropertyFilterProvider(propertyFilterProviders);
         context.BindingMetadata.PropertyFilterProvider = composite;
      }

      var bindingBehavior = FindBindingBehavior(context);
      if (bindingBehavior != null)
      {
         context.BindingMetadata.IsBindingAllowed = bindingBehavior.Behavior != BindingBehavior.Never;
         context.BindingMetadata.IsBindingRequired = bindingBehavior.Behavior == BindingBehavior.Required;
      }

      if (GetBoundConstructor(context.Key.ModelType) is ConstructorInfo constructorInfo)
      {
         context.BindingMetadata.BoundConstructor = constructorInfo;
      }
   }

   internal static ConstructorInfo? GetBoundConstructor(Type type)
   {
      if (type.IsAbstract || type.IsValueType || type.IsInterface)
         return null;

      var constructors = type.GetConstructors();
      if (constructors.Length == 0)
         return null;

      return GetRecordTypeConstructor(type, constructors);
   }

   private static ConstructorInfo? GetRecordTypeConstructor(Type type, ConstructorInfo[] constructors)
   {
      if (!IsRecordType(type))
         return null;
      
      if (constructors.Length > 1)
         return null;
      
      var constructor = constructors[0];

      var parameters = constructor.GetParameters();
      if (parameters.Length == 0)
         return null;
      
      var properties = PropertyHelper.GetVisibleProperties(type);

      for (var i = 0; i < parameters.Length; i++)
      {
         var parameter = parameters[i];
         var mappedProperty = properties.FirstOrDefault(property => 
            string.Equals(property.Name, parameter.Name, StringComparison.Ordinal) && 
            property.Property.PropertyType == parameter.ParameterType);

         if (mappedProperty is null)
         {
            // no property found, this is not a primary constructor.
            return null;
         }
      }

      return constructor;

      // inner method
      static bool IsRecordType(Type type)
      {
         var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance);
         return cloneMethod != null && (cloneMethod.ReturnType == type || cloneMethod.ReturnType == type.BaseType);
      }
   }

   private static BindingBehaviorAttribute? FindBindingBehavior(BindingMetadataProviderContext context)
   {
      switch (context.Key.MetadataKind)
      {
         case ModelMetadataKind.Property:
            var matchingAttributes = context.PropertyAttributes!.OfType<BindingBehaviorAttribute>();
            return matchingAttributes.FirstOrDefault()
               ?? context.Key.ContainerType!
                   .GetCustomAttributes(typeof(BindingBehaviorAttribute), inherit: true)
                   .OfType<BindingBehaviorAttribute>()
                   .FirstOrDefault();

         case ModelMetadataKind.Parameter:
            return context.ParameterAttributes!.OfType<BindingBehaviorAttribute>().FirstOrDefault();

         default:
            return null;
      }
   }

   /*  
   public interface IPropertyFilterProvider
   {
      Func<ModelMetadata, bool> PropertyFilter { get; }   
   }
   */

   // inner class
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
            foreach (var propertyFilter in propertyFilters)
            {
               if (!propertyFilter(m))
               {
                  return false;
               }
            }
 
            return true;
         };
      }
   }
}
//--------------------------------------------------Ʌ




//--------------------------V
public class BindingMetadata
{
   private Type? _binderType;
   private DefaultModelBindingMessageProvider? _messageProvider;

   public BindingSource? BindingSource { get; set; }
   public string? BinderModelName { get; set; }

   public Type? BinderType
   {
      get => _binderType;
      set {
         if (value != null && !typeof(IModelBinder).IsAssignableFrom(value))
           throw new ArgumentException(...);

         _binderType = value;
      }
   }

   public bool IsBindingAllowed { get; set; } = true;
   public bool IsBindingRequired { get; set; }
   public bool? IsReadOnly { get; set; }

   public DefaultModelBindingMessageProvider? ModelBindingMessageProvider
   {
      get => _messageProvider;
      set {
         _messageProvider = value;
      }
   }

   public IPropertyFilterProvider? PropertyFilterProvider { get; set; }
   public ConstructorInfo? BoundConstructor { get; set; }
}
//--------------------------Ʌ
```




















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