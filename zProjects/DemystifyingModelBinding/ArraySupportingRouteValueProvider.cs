using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DemystifyingModelBinding
{
   public class ArraySupportingRouteValueProvider : BindingSourceValueProvider
   {
      private const string ValueSeparator = ",";
      private readonly RouteValueDictionary _values;
      private readonly ActionDescriptor _actionDescriptor;
      private PrefixContainer _prefixContainer;

      public ArraySupportingRouteValueProvider(BindingSource bindingSource, RouteValueDictionary values, ActionDescriptor actionDescriptor) : base(bindingSource)
      {
         _values = values;
         _actionDescriptor = actionDescriptor;
      }

      protected PrefixContainer PrefixContainer
      {
         get 
         {
            if (_prefixContainer == null)
            {
               _prefixContainer = new PrefixContainer(_values.Keys);
            }

            return _prefixContainer;
         }
      }

      public override bool ContainsPrefix(string prefix)
      {
         return PrefixContainer.ContainsPrefix(prefix);
      }


      public override ValueProviderResult GetValue(string key)
      {
         if (key is null)
         {
            throw new ArgumentNullException(nameof(key));
         }

         if (key.Length == 0)
         {
            return ValueProviderResult.None;
         }

         if (_values.TryGetValue(key, out var value))
         {
            string stringValue = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            bool targetIsArrayParam = _actionDescriptor
                .Parameters
                .FirstOrDefault(p => p.Name.Equals(key))
                ?.ParameterType.IsArray ?? false;

            if (targetIsArrayParam)
            {
               string[] values = stringValue.Split(ValueSeparator, StringSplitOptions.RemoveEmptyEntries);
               return new ValueProviderResult(new StringValues(values), CultureInfo.InvariantCulture);
            }
            else
            {
               return new ValueProviderResult(stringValue, CultureInfo.InvariantCulture);
            }
         }
         else
         {
            return ValueProviderResult.None;
         }
      }
   }

   public class ArraySupportingRouteValueProviderFactory : IValueProviderFactory
   {
      public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
      {
         if (context is null)
         {
            throw new ArgumentNullException(nameof(context));
         }

         var zzz = context.ActionContext.RouteData.Values;

         IValueProvider valueProvider = new ArraySupportingRouteValueProvider(
           BindingSource.Path,
           context.ActionContext.RouteData.Values,
           context.ActionContext.ActionDescriptor);

         context.ValueProviders.Add(valueProvider);

         return Task.CompletedTask;
      }
   }
}
