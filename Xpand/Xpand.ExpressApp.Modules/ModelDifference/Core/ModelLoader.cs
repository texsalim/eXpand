﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Configuration;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Core;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.DC.Xpo;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.Utils;
using DevExpress.ExpressApp.Utils.CodeGeneration;
using DevExpress.ExpressApp.Validation;
using DevExpress.Persistent.Base;
using Xpand.Persistent.Base.General;
using Xpand.Utils.Helpers;
using Fasterflect;

namespace Xpand.ExpressApp.ModelDifference.Core {
    public class ApplicationBuilder {
        string _assemblyPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        Func<string, ITypesInfo> _buildTypesInfoSystem = BuildTypesInfoSystem(true);
        string _moduleName;

        ApplicationBuilder() {
        }

        public static ApplicationBuilder Create() {
            return new ApplicationBuilder();
        }

        static Func<string, ITypesInfo> BuildTypesInfoSystem(bool tryToUseCurrentTypesInfo) {
            return moduleName => TypesInfoBuilder.Create()
                                     .FromModule(moduleName)
                                     .Build(tryToUseCurrentTypesInfo);
        }

        public ApplicationBuilder FromAssembliesPath(string path) {
            _assemblyPath = path;
            return this;
        }
        public ApplicationBuilder UsingTypesInfo(Func<string, ITypesInfo> buildTypesInfoSystem) {
            _buildTypesInfoSystem = buildTypesInfoSystem;
            return this;
        }

        public ApplicationBuilder FromModule(string moduleName) {
            _moduleName = moduleName;
            return this;
        }

        public XafApplication Build() {

            try {
                var typesInfo = _buildTypesInfoSystem.Invoke(_moduleName);
                ReflectionHelper.AddResolvePath(_assemblyPath);
                var assembly = ReflectionHelper.GetAssembly(Path.GetFileNameWithoutExtension(_moduleName), _assemblyPath);
                var assemblyInfo = typesInfo.FindAssemblyInfo(assembly);
                typesInfo.LoadTypes(assembly);
                var findTypeInfo = typesInfo.FindTypeInfo(typeof(XafApplication));
                var findTypeDescendants = ReflectionHelper.FindTypeDescendants(assemblyInfo, findTypeInfo, false);
                var instance = SecuritySystem.Instance;
                var xafApplication = ((XafApplication)Enumerator.GetFirst(findTypeDescendants).CreateInstance(new object[0]));
                SecuritySystem.SetInstance(instance);
                SetConnectionString(xafApplication);
                var objectSpaceProviders = ((IList<IObjectSpaceProvider>) xafApplication.GetFieldValue("objectSpaceProviders"));
                objectSpaceProviders.Add(new XpandObjectSpaceProvider(new DataStoreProvider(xafApplication.ConnectionString), null));
                return xafApplication;
            } finally {
                ReflectionHelper.RemoveResolvePath(_assemblyPath);
            }
        }

        void SetConnectionString(XafApplication xafApplication) {
            try {
                var connectionString = XpandModuleBase.ConnectionString;
                if (connectionString != null) {
                    (xafApplication).ConnectionString = connectionString;
                }
            }
            catch (NullReferenceException) {
            }
        }
    }

    public class TypesInfoBuilder {
        string _moduleName;

        public static TypesInfoBuilder Create() {
            return new TypesInfoBuilder();
        }

        public TypesInfoBuilder FromModule(string moduleName) {
            _moduleName = moduleName;
            return this;
        }

        public ITypesInfo Build(bool tryToUseCurrentTypesInfo) {
            return tryToUseCurrentTypesInfo
                       ? (UseCurrentTypesInfo() ? XafTypesInfo.Instance : GetTypesInfo())
                       : GetTypesInfo();
        }

        bool UseCurrentTypesInfo() {
            return _moduleName == XpandModuleBase.ManifestModuleName;
        }

        TypesInfo GetTypesInfo() {
            var typesInfo = new TypesInfo();
            typesInfo.AddEntityStore(new NonPersistentEntityStore(typesInfo));
            var xpoSource = new XpoTypeInfoSource(typesInfo);
            typesInfo.Source = xpoSource;
            typesInfo.AddEntityStore(xpoSource);
            return typesInfo;
        }

        public class TypesInfo : DevExpress.ExpressApp.DC.TypesInfo {
            public XpoTypeInfoSource Source { get; set; }
        }

    }

    internal class ModelBuilder {

        readonly string _assembliesPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        XafApplication _application;
        ITypesInfo _typesInfo;
        string _moduleName;
        ApplicationModulesManager _modulesManager;
        

        ModelBuilder() {
        }

        private IEnumerable<string> GetAspects(string configFileName) {
            if (!string.IsNullOrEmpty(configFileName) && configFileName.EndsWith(".config")) {
                var exeConfigurationFileMap = new ExeConfigurationFileMap { ExeConfigFilename = configFileName };
                Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, ConfigurationUserLevel.None);
                KeyValueConfigurationElement languagesElement = configuration.AppSettings.Settings["Languages"];
                if (languagesElement != null) {
                    string languages = languagesElement.Value;
                    return languages.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            return Enumerable.Empty<string>();
        }

        public static ModelBuilder Create() {
            return new ModelBuilder();
        }

        string GetConfigPath() {
            string path = Path.Combine(_assembliesPath, _moduleName);
            string config = path + ".config";
            if (File.Exists(_assembliesPath + "web.config"))
                config = Path.Combine(_assembliesPath, "web.config");
            return config;
        }

        private string[] GetModulesFromConfig(XafApplication application) {
            Configuration config;
            if (application is IWinApplication) {
                config = ConfigurationManager.OpenExeConfiguration(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + _moduleName);
            } else {
                var mapping = new WebConfigurationFileMap();
                mapping.VirtualDirectories.Add("/Dummy", new VirtualDirectoryMapping(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, true));
                config = WebConfigurationManager.OpenMappedWebConfiguration(mapping, "/Dummy");
            }

            if (config.AppSettings.Settings["Modules"] != null) {
                return config.AppSettings.Settings["Modules"].Value.Split(';');
            }

            return null;
        }
        

        ModelApplicationBase BuildModel(XafApplication application, string configFileName, ApplicationModulesManager applicationModulesManager) {
            XpandModuleBase.CallMonitor.Clear();
            var ruleBaseDescantans = RemoveRuntimeTypeFromIModelRuleBaseDescantans();
            var modelAssemblyFile = typeof(XafApplication).Invoke(application, "GetModelAssemblyFilePath") as string;
            applicationModulesManager.TypesInfo.AssignAsInstance();
            var modelApplication = ModelApplicationHelper.CreateModel(applicationModulesManager.TypesInfo, applicationModulesManager.DomainComponents, applicationModulesManager.Modules,
                                                                                       applicationModulesManager.ControllersManager, application.ResourcesExportedToModel, GetAspects(configFileName), modelAssemblyFile, null);
//            ((ITypesInfoProvider)modelApplication).TypesInfo = applicationModulesManager.TypesInfo;
            var modelApplicationBase = modelApplication.CreatorInstance.CreateModelApplication();
            modelApplicationBase.Id = "After Setup";
            ModelApplicationHelper.AddLayer(modelApplication, modelApplicationBase);
            AddRuntimeTypesToIModelRuleBaseDescenants(ruleBaseDescantans);
            return modelApplication;
        }

        void AddRuntimeTypesToIModelRuleBaseDescenants(List<KeyValuePair<TypeInfo, TypeInfo>> ruleBaseDescantans) {
            ModifyIModelRuleBaseDescantans((infos, pair) => infos.Add(pair.Key, pair.Value), ruleBaseDescantans);
        }

        List<KeyValuePair<TypeInfo, TypeInfo>> RemoveRuntimeTypeFromIModelRuleBaseDescantans() {
            return ModifyIModelRuleBaseDescantans((infos, pair) => infos.Remove(pair.Key));
        }

        List<KeyValuePair<TypeInfo, TypeInfo>> ModifyIModelRuleBaseDescantans(Action<Dictionary<TypeInfo, TypeInfo>, KeyValuePair<TypeInfo, TypeInfo>> action, List<KeyValuePair<TypeInfo, TypeInfo>> keyValuePairs = null) {
            var typeInfo = (TypeInfo)XafTypesInfo.Instance.FindTypeInfo(typeof(IModelRuleBase));
            FieldInfo fieldInfo = typeInfo.GetType().GetField("descendants", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo != null) {
                var dictionary = (Dictionary<TypeInfo, TypeInfo>)fieldInfo.GetValue(typeInfo);
                if (keyValuePairs == null)
                    keyValuePairs = dictionary.Where(info => info.Value.Type != null && info.Value.Type.GetType().Name == "RuntimeType").ToList();
                foreach (KeyValuePair<TypeInfo, TypeInfo> keyValuePair in keyValuePairs) {
                    action.Invoke(dictionary, keyValuePair);
                }
            }
            return keyValuePairs;
        }

        ApplicationModulesManager CreateModulesManager(XafApplication application, string configFileName, string assembliesPath, ITypesInfo typesInfo) {
            if (!string.IsNullOrEmpty(configFileName)) {
                bool isWebApplicationModel = String.Compare(Path.GetFileNameWithoutExtension(configFileName), "web", StringComparison.OrdinalIgnoreCase) == 0;
                if (string.IsNullOrEmpty(assembliesPath)) {
                    assembliesPath = Path.GetDirectoryName(configFileName);
                    if (isWebApplicationModel) {
                        assembliesPath = Path.Combine(assembliesPath + "", "Bin");
                    }
                }
            }
            ReflectionHelper.AddResolvePath(assembliesPath);
            ITypesInfo synchronizeTypesInfo = null;
            try {
                var applicationModulesManager = new ApplicationModulesManager(new ControllersManager(), assembliesPath);
                if (application != null) {
                    foreach (ModuleBase module in application.Modules) {
                        applicationModulesManager.AddModule(module);
                    }
                    applicationModulesManager.Security = application.Security;
                }
                if (!string.IsNullOrEmpty(configFileName)) {
                    applicationModulesManager.AddModuleFromAssemblies(GetModulesFromConfig(application));
                }
                var loadTypesInfo = typesInfo != XafTypesInfo.Instance;
                synchronizeTypesInfo = XafTypesInfo.Instance;
                typesInfo.AssignAsInstance();
                applicationModulesManager.TypesInfo = typesInfo;
                applicationModulesManager.Load(typesInfo, loadTypesInfo);
                return applicationModulesManager;
            } finally {
                synchronizeTypesInfo.AssignAsInstance();
                ReflectionHelper.RemoveResolvePath(assembliesPath);
            }

        }

        public ModelBuilder WithApplication(XafApplication xafApplication) {
            _application = xafApplication;
            return this;
        }

        public ModelApplicationBase Build(bool rebuild) {
            string config = GetConfigPath();
            if (!rebuild)
                _modulesManager = CreateModulesManager(_application, config, _assembliesPath, _typesInfo);
            return BuildModel(_application, config, _modulesManager);
        }

        public ModelBuilder UsingTypesInfo(ITypesInfo typesInfo) {
            _typesInfo = typesInfo;
            return this;
        }

        public ModelBuilder FromModule(string moduleName) {
            _moduleName = moduleName;
            return this;
        }
    }
    public class ModelLoader {
        public static bool IsDebug { get; set; }

        readonly string _moduleName;
        readonly ITypesInfo _instance;
        ITypesInfo _typesInfo;
        XafApplication _xafApplication;
        ModelBuilder _modelBuilder;

        public ModelLoader(string moduleName, ITypesInfo instance) {
            _moduleName = moduleName;
            _instance = instance;
        }

        public ModelApplicationBase ReCreate() {
            return GetMasterModelCore(true);
        }
        public ModelApplicationBase GetMasterModel(bool tryToUseCurrentTypesInfo,Action<ITypesInfo> action=null) {
            _typesInfo = TypesInfoBuilder.Create()
                .FromModule(_moduleName)
                .Build(tryToUseCurrentTypesInfo);
            _xafApplication = ApplicationBuilder.Create().
                UsingTypesInfo(s => _typesInfo).
                FromModule(_moduleName).
                Build();
            var modelApplicationBase = GetMasterModelCore(false);
            if (action != null) action.Invoke(_instance);
            return modelApplicationBase;
        }

        ModelApplicationBase GetMasterModelCore(bool rebuild) {
            ModelApplicationBase modelApplicationBase;
            try {
                _modelBuilder = !rebuild ? ModelBuilder.Create() : _modelBuilder;
                modelApplicationBase = _modelBuilder
                    .UsingTypesInfo(_typesInfo)
                    .FromModule(_moduleName)
                    .WithApplication(_xafApplication)
                    .Build(rebuild);
            } catch (CompilerErrorException e) {
                Tracing.Tracer.LogSeparator("CompilerErrorException");
                Tracing.Tracer.LogError(e);
                Tracing.Tracer.LogValue("Source Code", e.SourceCode);
                throw;
            }
            return modelApplicationBase;
        }

        public ModelApplicationBase GetLayer(Type modelApplicationFromStreamStoreBaseType, bool tryToUseCurrentTypesInfo, Action<ITypesInfo> action ) {
            var masterModel = GetMasterModel(tryToUseCurrentTypesInfo,action);
            var layer = masterModel.CreatorInstance.CreateModelApplication();

            masterModel.AddLayerBeforeLast(layer);
            var storeBase = (ModelApplicationFromStreamStoreBase)modelApplicationFromStreamStoreBaseType.CreateInstance();
            storeBase.Load(layer);
            return layer;
        }
    }
}
