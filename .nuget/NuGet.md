Commands
------------
nuget pack ../Cowboy.Hosting.Self/Cowboy.Hosting.Self.csproj -IncludeReferencedProjects -Prop Configuration=Release
nuget pack ../Cowboy.Sockets/Cowboy.Sockets.csproj -IncludeReferencedProjects -Prop Configuration=Release
nuget setApiKey xxx-xxx
nuget push Cowboy.Sockets.1.0.0.0.nupkg
