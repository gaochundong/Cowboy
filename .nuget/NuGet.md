Commands
------------
nuget setApiKey xxx-xxx-xxxx-xxxx

nuget push .\packages\Cowboy.Sockets.1.0.0.0.nupkg
nuget pack ..\Cowboy\Cowboy.Sockets\Cowboy.Sockets.csproj -IncludeReferencedProjects -Symbols -Build -Prop Configuration=Release -OutputDirectory ".\packages"
