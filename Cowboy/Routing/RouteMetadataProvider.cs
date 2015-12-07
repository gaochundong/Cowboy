using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Routing
{
    public interface IRouteMetadataProvider
    {
        Type GetMetadataType(Module module, RouteDescription routeDescription);

        object GetMetadata(Module module, RouteDescription routeDescription);
    }

    public interface IMetadataModuleResolver
    {
        IMetadataModule GetMetadataModule(Module module);
    }

    public interface IMetadataModule
    {
        Type MetadataType { get; }

        object GetMetadata(RouteDescription description);
    }

    public class MetadataModuleRouteMetadataProvider : IRouteMetadataProvider
    {
        private readonly IMetadataModuleResolver resolver;

        public MetadataModuleRouteMetadataProvider(IMetadataModuleResolver resolver)
        {
            this.resolver = resolver;
        }

        public Type GetMetadataType(Module module, RouteDescription routeDescription)
        {
            var metadataModule = this.resolver.GetMetadataModule(module);

            return metadataModule != null ? metadataModule.MetadataType : null;
        }

        public object GetMetadata(Module module, RouteDescription routeDescription)
        {
            var metadataModule = this.resolver.GetMetadataModule(module);

            return metadataModule != null ? metadataModule.GetMetadata(routeDescription) : null;
        }
    }

    public class DefaultMetadataModuleResolver : IMetadataModuleResolver
    {
        private readonly DefaultMetadataModuleConventions conventions;

        private readonly IEnumerable<IMetadataModule> metadataModules;

        public DefaultMetadataModuleResolver(DefaultMetadataModuleConventions conventions, IEnumerable<IMetadataModule> metadataModules)
        {
            if (conventions == null)
            {
                throw new InvalidOperationException("Cannot create an instance of DefaultMetadataModuleResolver with conventions parameter having null value.");
            }

            if (metadataModules == null)
            {
                throw new InvalidOperationException("Cannot create an instance of DefaultMetadataModuleResolver with metadataModules parameter having null value.");
            }

            this.conventions = conventions;
            this.metadataModules = metadataModules;
        }

        public IMetadataModule GetMetadataModule(Module module)
        {
            return this.conventions
                .Select(convention => this.SafeInvokeConvention(convention, module))
                .FirstOrDefault(metadataModule => metadataModule != null);
        }

        private IMetadataModule SafeInvokeConvention(Func<Module, IEnumerable<IMetadataModule>, IMetadataModule> convention, Module module)
        {
            try
            {
                return convention.Invoke(module, this.metadataModules);
            }
            catch
            {
                return null;
            }
        }
    }

    public class DefaultMetadataModuleConventions : IEnumerable<Func<Module, IEnumerable<IMetadataModule>, IMetadataModule>>
    {
        private readonly IEnumerable<Func<Module, IEnumerable<IMetadataModule>, IMetadataModule>> conventions;

        public DefaultMetadataModuleConventions()
        {
            this.conventions = this.ConfigureMetadataModuleConventions();
        }

        public IEnumerator<Func<Module, IEnumerable<IMetadataModule>, IMetadataModule>> GetEnumerator()
        {
            return this.conventions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private static string ReplaceModuleWithMetadataModule(string moduleName)
        {
            var i = moduleName.LastIndexOf("Module");
            return moduleName.Substring(0, i) + "MetadataModule";
        }

        private IEnumerable<Func<Module, IEnumerable<IMetadataModule>, IMetadataModule>> ConfigureMetadataModuleConventions()
        {
            return new List<Func<Module, IEnumerable<IMetadataModule>, IMetadataModule>>
                {
                    // 0 Handles: ./BlahModule -> ./BlahMetadataModule
                    (module, metadataModules) =>
                        {
                            var moduleType = module.GetType();
                            var moduleName = moduleType.FullName;
                            var metadataModuleName = ReplaceModuleWithMetadataModule(moduleName);

                            return metadataModules.FirstOrDefault(m =>
                                    string.Compare(m.GetType().FullName, metadataModuleName, StringComparison.OrdinalIgnoreCase) == 0);
                        },

                    // 1 Handles: ./BlahModule -> ./Metadata/BlahMetadataModule
                    (module, metadataModules) =>
                        {
                            var moduleType = module.GetType();
                            var moduleName = moduleType.FullName;
                            var parts = moduleName.Split('.').ToList();
                            parts.Insert(parts.Count - 1, "Metadata");

                            var metadataModuleName = ReplaceModuleWithMetadataModule(string.Join(".", (IEnumerable<string>)parts));

                            return metadataModules.FirstOrDefault(m =>
                                    string.Compare(m.GetType().FullName, metadataModuleName, StringComparison.OrdinalIgnoreCase) == 0);
                        },

                    // 2 Handles: ./Modules/BlahModule -> ../Metadata/BlahMetadataModule
                    (module, metadataModules) =>
                        {
                            var moduleType = module.GetType();
                            var moduleName = moduleType.FullName;
                            var parts = moduleName.Split('.').ToList();
                            parts[parts.Count - 2] = "Metadata";

                            var metadataModuleName = ReplaceModuleWithMetadataModule(string.Join(".", (IEnumerable<string>)parts));

                            return metadataModules.FirstOrDefault(m =>
                                    string.Compare(m.GetType().FullName, metadataModuleName, StringComparison.OrdinalIgnoreCase) == 0);
                        }
                };
        }
    }
}
