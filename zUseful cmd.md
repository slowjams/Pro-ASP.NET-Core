Useful Commands
==============================

Create a template project

```s

dotnet new globaljson --sdk-version 3.1.101 --output WorkerService  

dotnet new sln -n WorkerService

cd WorkerService

dotnet new worker -n WorkerService

dotnet sln add WorkerService/WorkerService.csproj

dotnet new gitignore

dotnet build

dotnet run --project WorkerService
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