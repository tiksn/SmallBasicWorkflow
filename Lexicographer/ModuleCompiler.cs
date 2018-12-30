using Microsoft.SmallBasic;
using Microsoft.SmallBasic.Library;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace TIKSN.SmallBasicWorkflow.Lexicographer
{
    public class ModuleCompiler
    {
        private List<Error> _errors = new List<Error>();
        private List<string> _libraryFiles = new List<string>();
        private Parser _parser;
        private List<Assembly> _referenceAssemblies;
        private List<string> _references = new List<string>();
        private TypeInfoBag _typeInfoBag;

        public ModuleCompiler()
        {
            this._parser = new Parser(this._errors);
            this._typeInfoBag = new TypeInfoBag();
            this.Initialize();
        }

        public Parser Parser
        {
            get
            {
                return this._parser;
            }
        }

        public List<string> References
        {
            get
            {
                return this._references;
            }
        }

        public TypeInfoBag TypeInfoBag
        {
            get
            {
                return this._typeInfoBag;
            }
        }

        public List<Error> Build(TextReader source, string outputName, string directory)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (outputName == null)
                throw new ArgumentNullException(nameof(outputName));
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));
            this.Compile(source);
            if (this._errors.Count > 0)
                return this._errors;
            new ModuleGenerator(this._parser, this._typeInfoBag, outputName, directory).GenerateModule();
            this.CopyLibraryAssemblies(directory);
            return this._errors;
        }

        public List<Error> Compile(TextReader source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            this._errors.Clear();
            this._parser.Parse(source);
            new SemanticAnalyzer(this._parser, this._typeInfoBag).Analyze();
            return this._errors;
        }

        public void Initialize()
        {
            this.PopulateReferences();
            this.PopulateClrSymbols();
            this.PopulatePrimitiveMethods();
        }

        private bool AddAssemblyTypesToList(Assembly assembly)
        {
            if (assembly == (Assembly)null)
                return false;
            bool flag = false;
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(SmallBasicTypeAttribute), false).Length > 0 && type.IsVisible)
                {
                    this.AddTypeToList(type);
                    flag = true;
                }
            }
            return flag;
        }

        private void AddTypeToList(Type type)
        {
            var typeInfo = new Microsoft.SmallBasic.TypeInfo()
            {
                Type = type,
                HideFromIntellisense = type.GetCustomAttributes(typeof(HideFromIntellisenseAttribute), false).Length > 0
            };
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (this.CanAddMethod(method) && !typeInfo.Methods.ContainsKey(method.Name.ToLower(CultureInfo.CurrentUICulture)))
                    typeInfo.Methods.Add(method.Name.ToLower(CultureInfo.CurrentUICulture), method);
            }
            Dictionary<string, PropertyInfo> dictionary1 = new Dictionary<string, PropertyInfo>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                if (this.CanAddProperty(property))
                    typeInfo.Properties.Add(property.Name.ToLower(CultureInfo.CurrentUICulture), property);
            }
            Dictionary<string, EventInfo> dictionary2 = new Dictionary<string, EventInfo>();
            foreach (EventInfo eventInfo in type.GetEvents(BindingFlags.Static | BindingFlags.Public))
            {
                if (this.CanAddEvent(eventInfo))
                    typeInfo.Events.Add(eventInfo.Name.ToLower(CultureInfo.CurrentUICulture), eventInfo);
            }
            if (typeInfo.Events.Count <= 0 && typeInfo.Methods.Count <= 0 && typeInfo.Properties.Count <= 0)
                return;
            this._typeInfoBag.Types[type.Name.ToLower(CultureInfo.CurrentUICulture)] = typeInfo;
        }

        private bool CanAddEvent(EventInfo eventInfo)
        {
            if (!eventInfo.IsSpecialName)
                return eventInfo.EventHandlerType == typeof(SmallBasicCallback);
            return false;
        }

        private bool CanAddMethod(MethodInfo methodInfo)
        {
            if (methodInfo.IsGenericMethod || methodInfo.IsConstructor || (methodInfo.ContainsGenericParameters || methodInfo.IsSpecialName) || !(methodInfo.ReturnType == typeof(void)) && !(methodInfo.ReturnType == typeof(Primitive)))
                return false;
            foreach (ParameterInfo parameter in methodInfo.GetParameters())
            {
                if (parameter.ParameterType != typeof(Primitive))
                    return false;
            }
            return true;
        }

        private bool CanAddProperty(PropertyInfo propertyInfo)
        {
            if (!propertyInfo.IsSpecialName)
                return propertyInfo.PropertyType == typeof(Primitive);
            return false;
        }

        private void CopyLibraryAssemblies(string directory)
        {
            string location = typeof(Primitive).Assembly.Location;
            string fileName1 = Path.GetFileName(location);
            try
            {
                System.IO.File.Copy(location, Path.Combine(directory, fileName1), true);
            }
            catch
            {
            }
            foreach (string libraryFile in this._libraryFiles)
            {
                try
                {
                    string fileName2 = Path.GetFileName(libraryFile);
                    System.IO.File.Copy(libraryFile, Path.Combine(directory, fileName2), true);
                }
                catch
                {
                }
            }
        }

        private void LoadAssembliesFromAppData()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Small Basic", "Lib");
            if (!Directory.Exists(path))
                return;
            foreach (string file in Directory.GetFiles(path, "*.dll"))
            {
                try
                {
                    this.AddAssemblyTypesToList(Assembly.LoadFile(file));
                    this._libraryFiles.Add(file);
                }
                catch
                {
                }
            }
        }

        private void PopulateClrSymbols()
        {
            foreach (Assembly referenceAssembly in this._referenceAssemblies)
                this.AddAssemblyTypesToList(referenceAssembly);
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lib");
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.dll"))
                {
                    try
                    {
                        this.AddAssemblyTypesToList(Assembly.LoadFile(file));
                        this._libraryFiles.Add(file);
                    }
                    catch
                    {
                    }
                }
            }
            this.LoadAssembliesFromAppData();
        }

        private void PopulatePrimitiveMethods()
        {
            Type type = typeof(Primitive);
            this._typeInfoBag.StringToPrimitive = type.GetMethod("op_Implicit", new Type[1]
            {
        typeof (string)
            });
            this._typeInfoBag.NumberToPrimitive = type.GetMethod("op_Implicit", new Type[1]
            {
        typeof (double)
            });
            this._typeInfoBag.PrimitiveToBoolean = type.GetMethod("ConvertToBoolean");
            this._typeInfoBag.Negation = type.GetMethod("op_UnaryNegation");
            this._typeInfoBag.Add = type.GetMethod("op_Addition");
            this._typeInfoBag.Subtract = type.GetMethod("op_Subtraction");
            this._typeInfoBag.Multiply = type.GetMethod("op_Multiply");
            this._typeInfoBag.Divide = type.GetMethod("op_Division");
            this._typeInfoBag.GreaterThan = type.GetMethod("op_GreaterThan");
            this._typeInfoBag.GreaterThanOrEqualTo = type.GetMethod("op_GreaterThanOrEqual");
            this._typeInfoBag.LessThan = type.GetMethod("op_LessThan");
            this._typeInfoBag.LessThanOrEqualTo = type.GetMethod("op_LessThanOrEqual");
            this._typeInfoBag.EqualTo = type.GetMethod("op_Equality", new Type[2]
            {
        typeof (Primitive),
        typeof (Primitive)
            });
            this._typeInfoBag.NotEqualTo = type.GetMethod("op_Inequality", new Type[2]
            {
        typeof (Primitive),
        typeof (Primitive)
            });
            this._typeInfoBag.And = type.GetMethod("op_And", new Type[2]
            {
        typeof (Primitive),
        typeof (Primitive)
            });
            this._typeInfoBag.Or = type.GetMethod("op_Or", new Type[2]
            {
        typeof (Primitive),
        typeof (Primitive)
            });
            this._typeInfoBag.GetArrayValue = type.GetMethod("GetArrayValue");
            this._typeInfoBag.SetArrayValue = type.GetMethod("SetArrayValue");
        }

        private void PopulateReferences()
        {
            this._referenceAssemblies = new List<Assembly>();
            this._referenceAssemblies.Add(typeof(Primitive).Assembly);
            foreach (string reference in this.References)
            {
                try
                {
                    this._referenceAssemblies.Add(Assembly.LoadFile(reference));
                }
                catch
                {
                    throw new InvalidOperationException(string.Format(ResourceHelper.GetString("LoadReferenceFailed"), (object)reference));
                }
            }
        }
    }
}