Commands
------------
nuget setApiKey xxx-xxx-xxxx-xxxx

nuget push .\packages\Cowboy.Sockets.a.b.c.d.nupkg
nuget push .\packages\Cowboy.WebSockets.a.b.c.d.nupkg

nuget pack ..\Cowboy.Sockets\Cowboy.Sockets.csproj -IncludeReferencedProjects -Build -Prop Configuration=Release -OutputDirectory ".\packages"
nuget pack ..\Cowboy.WebSockets\Cowboy.WebSockets.csproj -IncludeReferencedProjects -Build -Prop Configuration=Release -OutputDirectory ".\packages"
