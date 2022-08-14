Chapter 25-Using Tag Helper
=================================

```C#
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, WebApp
@model Product
<table class="table table-striped table-bordered table-sm">
    <thead>
        <tr bg-color="info" text-color="white">  // <------------------- TaskOne
            <th colspan="2">Product Summary</th>
        </tr>                                    // <------------------- TaskTwo
    </thead>
    <tbody>                
        <tr><th>Name</th><td>@Model.Name</td></tr>   // <------------------- TaskThree
        <tr>
            <th>Price</th>
            <td>@Model.Price.ToString("c")</td>
        </tr>
        <tr><th>Category ID</th><td>@Model.CategoryId</td></tr>
    </tbody>
</table>
```

----------------------------------------------------------------------------------------------------------------------------
TaskOne:

```HTML
<tr bg-color="info" text-color="white">
    <th colspan="2">Product Summary</th>
</tr>

to

<tr class="bg-info text-center text-white">
   <th colspan="2">Product Summary</th>
</tr>
```

```C#
[HtmlTargetElement("tr", Attributes = "bg-color,text-color", ParentTag ="thead")]  
public class TrTagHelper : TagHelper   // if you don't use HtmlTargetElement attribute to controler the scope, then all Tr element will be applied with the rule, 
{                                      // somehow asp.net core will retrieve the [tagname]TagHelper by the class name
   
   // those property will be automatically set by asp.net core to match the html attribute value with a norm e.g bg-color on client end match BgColor on backend
   public string BgColor { get; set; } = "dark";      // "dark" is default value, the new value "info" can be reassign to the property
   public string TextColor { get; set; } = "white";
   //

   public override void Process(TagHelperContext context, TagHelperOutput output)
   {
      output.Attributes.SetAttribute("class", $"bg-{BgColor} text-center text-{TextColor}");
   }
}
```
------------------------------------------------------------------------------------------------------------------------------------

----------------------------------------------------------
TaskTwo

```HTML
<table class="table table-striped table-bordered table-sm">
    <tablehead bg-color="dark">Product Summary</tablehead>
    <thead>
        <tr bg-color="info" text-color="white">  
            <th colspan="2">Product Summary</th>
        </tr>                                    
    </thead>
    ...
</table>

to

<table class="table table-striped table-bordered table-sm">
    <thead>
        <tr bg-color="info" text-color="white">  
            <th colspan="2">Product Summary</th>
        </tr>                                    
    </thead>
    ...
</table>
```

```C#
[HtmlTargetElement("tablehead")]
public class TableHeadTagHelper : TagHelper
{
   public string BgColor { get; set; } = "light";

   public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
   {
      output.TagName = "thread";
      output.TagMode = TagMode.StartTagAndEndTag;
      output.Attributes.SetAttribute("class", $"bg-{BgColor} text-white text-center");
      // outputContent.SetHtmlContent($"<tr><th colspan=\"2\">{content}</th></tr>");   // not type safe, should use TagBuilder

      TagHelperContent tagContent = await output.GetChildContentAsync();
      string content = tagContent.GetContent();
      TagHelperContent outputContent = output.Content;
      
      TagBuilder header = new TagBuilder("th");
      header.Attributes["colspan"] = "2";
      header.InnerHtml.Append(content);

      TagBuilder row = new TagBuilder("tr");
      row.InnerHtml.AppendHtml(header);

      output.Content.SetHtmlContent(row);
   }
}
```

----------------------------------------------------------

---------------------------------------------------------------------------------------

TaskThree

```HTML
@model Product

<table class="table table-striped table-bordered table-sm">
    <tablehead bg-color="dark">Product Summary</tablehead>
    <tbody>
        <tr for="Name" />
        <tr for="Price" format="c" />
        <tr for="CategoryId" />
    </tbody>
</table>

to

<table class="table table-striped table-bordered table-sm">
    <tablehead bg-color="dark">Product Summary</tablehead>
    <tbody>                
        <tr>
           <th>Name</th>
           <td>@Model.Name</td>
        </tr>
        <tr>
            <th>Price</th>
            <td>@Model.Price.ToString("c")</td>
        </tr>
        <tr>
           <th>Category ID</th>
           <td>@Model.CategoryId</td>
       </tr>
    </tbody>
</table>
```

```C#
[HtmlTargetElement("tr", Attributes = "for")]
public class ModelRowTagHelper : TagHelper
{
   public string Format { get; set; }
   public ModelExpression For { get; set; }  // <------------------------------------ asp.net uses Reflection with @model

   public override void Process(TagHelperContext context, TagHelperOutput output)
   {
      output.TagMode = TagMode.StartTagAndEndTag;
      TagBuilder th = new TagBuilder("th");
      th.InnerHtml.Append(For.Name);
      output.Content.AppendHtml(th);

      TagBuilder td = new TagBuilder("td");
      if (Format != null && For.Metadata.ModelType == typeof(decimal))
      {
         td.InnerHtml.Append(((decimal)For.Model).ToString(Format));
      }
      else
      {
         td.InnerHtml.Append(For.Model.ToString());
      }
      output.Content.AppendHtml(td);
   }
}
```
---------------------------------------------------------------------------------------

## Built-in TagHelper

```HTML
@model Product
<input class="form-control" asp-for="Name" /> <!--translated into below -->
<input class="form-control" type="text" id="Name" name="Name" value="Kayak">

<form asp-action="submitform" method="post" id="htmlform">  <!--asp-controller attribute has not been used, so current controller is used -->
    <div class="form-group">
        <label>Name</label>
        <input class="form-control" name="Name" value="@Model.Name" />  <!--translated into below -->    
        <input class="form-control" asp-for="Name" />
    </div>
    <button type="submit" class="btn btn-primary">Submit</button>
</form> <!--translated into below -->

<form method="post" action="Home/Form/submitform" id="htmlform">...</form>
```

note that for Razor pages, you will need to specify `name` attribute explicitly as

```C#
<input class="form-control" type="text" asp-for="Product.Name" name="Name">  // need to specify name="Name"

@functions {
    public class FormHandlerModel : PageModel{
        ...
        public Product Product { get; set; }
    }
}

// translated into
<input class="form-control" type="text" id="Product_Name" name="Name" value="Kayak">

// if you don't specify name="Name", it will be, which affect parameter model binding, but if you use [BindProperty] on Product property, then you don't need name="Name"
<input class="form-control" type="text" id="Product_Name" name="Product.Name" value="Kayak">
```

**Source Code**

```C#
public interface ITagHelperComponent
{
   int Order { get; }
   void Init(TagHelperContext context);
   void Process(TagHelperContext context, TagHelperOutput output);
   Task ProcessAsync(TagHelperContext context, TagHelperOutput output);
}

public interface ITagHelper : ITagHelperComponent { }

public class TagHelperContext
{
   public TagHelperContext(TagHelperAttributeList allAttributes, IDictionary<object, object> items, string uniqueId);
   public TagHelperContext(string tagName, TagHelperAttributeList allAttributes, IDictionary<object, object> items, string uniqueId);

   public ReadOnlyTagHelperAttributeList AllAttributes { get; }
   public IDictionary<object, object> Items { get; }
   public string TagName { get; }
   public string UniqueId { get; }

   public void Reinitialize(IDictionary<object, object> items, string uniqueId);
   public void Reinitialize(string tagName, IDictionary<object, object> items, string uniqueId);
}

public class TagHelperOutput : IHtmlContentContainer
{
   public TagHelperOutput(string tagName, TagHelperAttributeList attributes, Func<bool, HtmlEncoder, Task<TagHelperContent>> getChildContentAsync);

   public TagHelperContent PreElement { get; }
   public TagHelperContent PreContent { get; }
   public TagHelperContent PostElement { get; }
   public TagHelperContent PostContent { get; }
   public bool IsContentModified { get; }
   public TagHelperContent Content { get; set; }
   public TagMode TagMode { get; set; }
   public TagHelperAttributeList Attributes { get; }
   public string TagName { get; set; }

   public Task<TagHelperContent> GetChildContentAsync(HtmlEncoder encoder);
   public Task<TagHelperContent> GetChildContentAsync(bool useCachedResult, HtmlEncoder encoder);
   public Task<TagHelperContent> GetChildContentAsync(bool useCachedResult);
   public Task<TagHelperContent> GetChildContentAsync();
   public void Reinitialize(string tagName, TagMode tagMode);
   public void WriteTo(TextWriter writer, HtmlEncoder encoder);
   public void SuppressOutput();
}

public sealed class ModelExpression
{
   public ModelExpression(string name, ModelExplorer modelExplorer);
   public ModelMetadata Metadata { get; }
   public object Model { get; }
   public ModelExplorer ModelExplorer { get; }
   public string Name { get; }
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