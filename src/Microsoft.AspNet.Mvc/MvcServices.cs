// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNet.Mvc.ApplicationModels;
using Microsoft.AspNet.Mvc.Description;
using Microsoft.AspNet.Mvc.Filters;
using Microsoft.AspNet.Mvc.Internal;
using Microsoft.AspNet.Mvc.ModelBinding;
using Microsoft.AspNet.Mvc.OptionDescriptors;
using Microsoft.AspNet.Mvc.Razor;
using Microsoft.AspNet.Mvc.Razor.Directives;
using Microsoft.AspNet.Mvc.Razor.OptionDescriptors;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Mvc.Routing;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.NestedProviders;
using Microsoft.Framework.OptionsModel;

namespace Microsoft.AspNet.Mvc
{
    public class MvcServices
    {
        public static IEnumerable<IServiceDescriptor> GetDefaultServices()
        {
            return GetDefaultServices(null);
        }

        public static IEnumerable<IServiceDescriptor> GetDefaultServices(IConfiguration configuration)
        {
            var describe = new ServiceDescriber(configuration);

            // Options and core services.
            yield return describe.Transient<IConfigureOptions<MvcOptions>, MvcOptionsSetup>();
            yield return describe.Transient<IConfigureOptions<RazorViewEngineOptions>, RazorViewEngineOptionsSetup>();
            yield return describe.Transient<IAssemblyProvider, DefaultAssemblyProvider>();

            yield return describe.Transient<MvcMarkerService, MvcMarkerService>();
            yield return describe.Singleton(typeof(ITypeActivatorCache), typeof(DefaultTypeActivatorCache));
            yield return describe.Scoped(typeof(IScopedInstance<>), typeof(ScopedInstance<>));

            // Core action discovery, filters and action execution.

            // These are consumed only when creating action descriptors, then they can be de-allocated
            yield return describe.Transient<IControllerTypeProvider, DefaultControllerTypeProvider>();
            yield return describe.Transient<IControllerModelBuilder, DefaultControllerModelBuilder>();
            yield return describe.Transient<IActionModelBuilder, DefaultActionModelBuilder>();

            // This has a cache, so it needs to be a singleton
            yield return describe.Singleton<IControllerFactory, DefaultControllerFactory>();

            yield return describe.Transient<IControllerActivator, DefaultControllerActivator>();

            // This accesses per-reqest services
            yield return describe.Transient<IActionInvokerFactory, ActionInvokerFactory>();

            // This provider needs access to the per-request services, but might be used many times for a given
            // request.
            yield return describe.Transient<INestedProviderManager<ActionConstraintProviderContext>,
                NestedProviderManager<ActionConstraintProviderContext>>();
            yield return describe.Transient<INestedProvider<ActionConstraintProviderContext>,
                DefaultActionConstraintProvider>();

            yield return describe.Singleton<IActionSelectorDecisionTreeProvider, ActionSelectorDecisionTreeProvider>();
            yield return describe.Singleton<IActionSelector, DefaultActionSelector>();
            yield return describe.Transient<IControllerActionArgumentBinder, DefaultControllerActionArgumentBinder>();
            yield return describe.Transient<IObjectModelValidator, DefaultObjectValidator>();

            yield return describe.Transient<INestedProviderManager<ActionDescriptorProviderContext>,
                                NestedProviderManager<ActionDescriptorProviderContext>>();
            yield return describe.Transient<INestedProvider<ActionDescriptorProviderContext>,
                                ControllerActionDescriptorProvider>();

            yield return describe.Transient<INestedProviderManager<ActionInvokerProviderContext>,
                                NestedProviderManager<ActionInvokerProviderContext>>();
            yield return describe.Transient<INestedProvider<ActionInvokerProviderContext>,
                                            ControllerActionInvokerProvider>();

            yield return describe.Singleton<IActionDescriptorsCollectionProvider,
                DefaultActionDescriptorsCollectionProvider>();

            // The IGlobalFilterProvider is used to build the action descriptors (likely once) and so should
            // remain transient to avoid keeping it in memory.
            yield return describe.Transient<IGlobalFilterProvider, DefaultGlobalFilterProvider>();
            yield return describe.Transient<INestedProviderManager<FilterProviderContext>,
                NestedProviderManager<FilterProviderContext>>();
            yield return describe.Transient<INestedProvider<FilterProviderContext>, DefaultFilterProvider>();

            yield return describe.Transient<FormatFilter, FormatFilter>();
            // Dataflow - ModelBinding, Validation and Formatting
            // The DataAnnotationsModelMetadataProvider does significant caching of reflection/attributes
            // and thus needs to be singleton. 
            yield return describe.Singleton<IModelMetadataProvider, DataAnnotationsModelMetadataProvider>();

            yield return describe.Transient<IInputFormatterSelector, DefaultInputFormatterSelector>();
            yield return describe.Scoped<IInputFormattersProvider, DefaultInputFormattersProvider>();

            yield return describe.Transient<IModelBinderProvider, DefaultModelBindersProvider>();
            yield return describe.Transient<IValueProviderFactoryProvider, DefaultValueProviderFactoryProvider>();
            yield return describe.Transient<IOutputFormattersProvider, DefaultOutputFormattersProvider>();
            yield return describe.Instance(new JsonOutputFormatter());

            yield return describe.Transient<IModelValidatorProviderProvider, DefaultModelValidatorProviderProvider>();
            yield return describe.Transient<IValidationExcludeFiltersProvider,
                DefaultValidationExcludeFiltersProvider>();

            // Razor, Views and runtime compilation

            // The provider is inexpensive to initialize and provides ViewEngines that may require request
            // specific services.
            yield return describe.Scoped<ICompositeViewEngine, CompositeViewEngine>();
            yield return describe.Transient<IViewEngineProvider, DefaultViewEngineProvider>();
            // Transient since the IViewLocationExpanders returned by the instance is cached by view engines.
            yield return describe.Transient<IViewLocationExpanderProvider, DefaultViewLocationExpanderProvider>();
            // Caches view locations that are valid for the lifetime of the application.
            yield return describe.Singleton<IViewLocationCache, DefaultViewLocationCache>();
            yield return describe.Singleton<ICodeTreeCache>(serviceProvider =>
            {
                var cachedFileProvider = serviceProvider.GetRequiredService<IOptions<RazorViewEngineOptions>>();
                return new DefaultCodeTreeCache(cachedFileProvider.Options.FileProvider);
            });

            // The host is designed to be discarded after consumption and is very inexpensive to initialize.
            yield return describe.Transient<IMvcRazorHost, MvcRazorHost>();
            
            // Caches compilation artifacts across the lifetime of the application.
            yield return describe.Singleton<ICompilerCache, CompilerCache>();

            // This caches compilation related details that are valid across the lifetime of the application
            // and is required to be a singleton.
            yield return describe.Singleton<ICompilationService, RoslynCompilationService>();

            // Both the compiler cache and roslyn compilation service hold on the compilation related
            // caches. RazorCompilation service is just an adapter service, and it is transient to ensure
            // the IMvcRazorHost dependency does not maintain state.
            yield return describe.Transient<IRazorCompilationService, RazorCompilationService>();

            // The ViewStartProvider needs to be able to consume scoped instances of IRazorPageFactory
            yield return describe.Scoped<IViewStartProvider, ViewStartProvider>();
            yield return describe.Transient<IRazorViewFactory, RazorViewFactory>();
            yield return describe.Singleton<IRazorPageActivator, RazorPageActivator>();

            // Virtual path view factory needs to stay scoped so views can get get scoped services.
            yield return describe.Scoped<IRazorPageFactory, VirtualPathRazorPageFactory>();

            // View and rendering helpers
            yield return describe.Transient<IHtmlHelper, HtmlHelper>();
            yield return describe.Transient(typeof(IHtmlHelper<>), typeof(HtmlHelper<>));
            yield return describe.Scoped<IUrlHelper, UrlHelper>();

            // Only want one ITagHelperActivator so it can cache Type activation information. Types won't conflict.
            yield return describe.Singleton<ITagHelperActivator, DefaultTagHelperActivator>();

            // Consumed by the Cache tag helper to cache results across the lifetime of the application.
            yield return describe.Singleton<IMemoryCache, MemoryCache>();

            // DefaultHtmlGenerator is pretty much stateless but depends on Scoped services such as IUrlHelper and
            // IActionBindingContextProvider. Therefore it too is scoped.
            yield return describe.Transient<IHtmlGenerator, DefaultHtmlGenerator>();

            // These do caching so they should stay singleton
            yield return describe.Singleton<IViewComponentSelector, DefaultViewComponentSelector>();
            yield return describe.Singleton<IViewComponentActivator, DefaultViewComponentActivator>();

            yield return describe.Transient<IViewComponentInvokerFactory, DefaultViewComponentInvokerFactory>();
            yield return describe.Transient<INestedProviderManager<ViewComponentInvokerProviderContext>,
                NestedProviderManager<ViewComponentInvokerProviderContext>>();
            yield return describe.Transient<INestedProvider<ViewComponentInvokerProviderContext>,
                DefaultViewComponentInvokerProvider>();
            yield return describe.Transient<IViewComponentHelper, DefaultViewComponentHelper>();

            // Security and Authorization
            yield return describe.Singleton<IClaimUidExtractor, DefaultClaimUidExtractor>();
            yield return describe.Singleton<AntiForgery, AntiForgery>();
            yield return describe.Singleton<IAntiForgeryAdditionalDataProvider,
                DefaultAntiForgeryAdditionalDataProvider>();

            // Api Description
            yield return describe.Transient<INestedProviderManager<ApiDescriptionProviderContext>,
                NestedProviderManager<ApiDescriptionProviderContext>>();
            yield return describe.Singleton<IApiDescriptionGroupCollectionProvider,
                ApiDescriptionGroupCollectionProvider>();
            yield return describe.Transient<INestedProvider<ApiDescriptionProviderContext>,
                DefaultApiDescriptionProvider>();
        }
    }
}
