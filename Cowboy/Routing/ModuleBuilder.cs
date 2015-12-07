using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Routing
{
    /// <summary>
    /// Default implementation for building a full configured <see cref="INancyModule"/> instance.
    /// </summary>
    public class ModuleBuilder
    {
        //private readonly IViewFactory viewFactory;
        //private readonly IResponseFormatterFactory responseFormatterFactory;
        //private readonly IModelBinderLocator modelBinderLocator;
        //private readonly IModelValidatorLocator validatorLocator;

        //public ModuleBuilder(IViewFactory viewFactory, IResponseFormatterFactory responseFormatterFactory, IModelBinderLocator modelBinderLocator, IModelValidatorLocator validatorLocator)
        //{
        //    this.viewFactory = viewFactory;
        //    this.responseFormatterFactory = responseFormatterFactory;
        //    this.modelBinderLocator = modelBinderLocator;
        //    this.validatorLocator = validatorLocator;
        //}

        //public INancyModule BuildModule(INancyModule module, NancyContext context)
        //{
        //    module.Context = context;
        //    module.Response = this.responseFormatterFactory.Create(context);
        //    module.ViewFactory = this.viewFactory;
        //    module.ModelBinderLocator = this.modelBinderLocator;
        //    module.ValidatorLocator = this.validatorLocator;

        //    return module;
        //}
    }
}
