using System;
using UIControlLibrary;

namespace MyControlLibary
{
   public class MyControl : UIControl
   {
      public void TestCustom()
      {
         CustomPrivate();
      }

      public void MyRun()
      {
         string str = "This is from MyClass";
         MainRun(str);
      }

      private void CustomPrivate()
      {

      }
   }
}
