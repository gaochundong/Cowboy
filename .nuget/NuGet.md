Commands
------------
nuget setApiKey xxx-xxx-xxxx-xxxx

nuget push .\packages\Cowboy.Sockets.a.b.c.d.nupkg
nuget push .\packages\Cowboy.WebSockets.a.b.c.d.nupkg

nuget pack ..\Cowboy\Cowboy.Sockets\Cowboy.Sockets.csproj -IncludeReferencedProjects -Symbols -Build -Prop Configuration=Release -OutputDirectory ".\packages"
nuget pack ..\Cowboy\Cowboy.WebSockets\Cowboy.WebSockets.csproj -IncludeReferencedProjects -Symbols -Build -Prop Configuration=Release -OutputDirectory ".\packages"

nuget push .\packages\Cowboy.1.0.0.0.nupkg
nuget pack ..\Cowboy\Cowboy.Sockets\Cowboy.Sockets.csproj -IncludeReferencedProjects -Symbols -Build -Prop Configuration=Release -OutputDirectory ".\packages"
