Chapter 14-Using Dependency Injection
==============================

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