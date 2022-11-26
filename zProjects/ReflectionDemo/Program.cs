using MyControlLibary;
using System;
using System.Reflection;

namespace ReflectionDemo
{
   internal class Program
   {
      static void Main(string[] args)
      {
         //var assembly = Assembly.LoadFrom(@"C:\zIT Study\zTextbooks\Pro ASP.NET Core MVC 3\zCode\zPro ASP.NET Core MVC-Walkthrough\zProjects\ReflectionDemo\MyControlLibary.dll");

         //var types = assembly.GetTypes();

         //MyControl aa = new MyControl();

         //TypeInfo type = typeof(MyControl).GetTypeInfo();

         //Type type = typeof(MyControl);

         //Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

         //Assembly ss = assemblies[5];

         //bool isSame = assembly == ss;

         //var many = assembly.ExportedTypes;

         TypeInfo typeInfo = typeof(MyControl).GetTypeInfo();


         Console.ReadLine();
      }
   }
}
