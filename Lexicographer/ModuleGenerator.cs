using Microsoft.SmallBasic;
using Microsoft.SmallBasic.Library;
using Microsoft.SmallBasic.Statements;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TIKSN.SmallBasicWorkflow.Lexicographer
{
    public class ModuleGenerator
    {
        private CodeGenScope _currentScope;
        private string _directory;
        private MethodInfo _entryPoint;
        private string _outputName;
        private Parser _parser;
        private SymbolTable _symbolTable;
        private TypeInfoBag _typeInfoBag;

        public ModuleGenerator(
          Parser parser,
          TypeInfoBag typeInfoBag,
          string outputName,
          string directory)
        {
            this._parser = parser ?? throw new ArgumentNullException(nameof(parser));
            this._symbolTable = this._parser.SymbolTable;
            this._typeInfoBag = typeInfoBag ?? throw new ArgumentNullException(nameof(typeInfoBag));
            this._outputName = outputName;
            this._directory = directory;
        }

        public bool GenerateModule()
        {
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName()
            {
                Name = this._outputName
            }, AssemblyBuilderAccess.Save, this._directory);
            if (!this.EmitModule(assemblyBuilder.DefineDynamicModule(this._outputName + ".dll", true)))
                return false;
            assemblyBuilder.SetEntryPoint(this._entryPoint, PEFileKinds.Dll);
            assemblyBuilder.Save(this._outputName + ".dll");
            return true;
        }

        private void BuildFields(TypeBuilder typeBuilder)
        {
            foreach (string key in this._parser.SymbolTable.Variables.Keys)
            {
                FieldInfo fieldInfo = (FieldInfo)typeBuilder.DefineField(key, typeof(Primitive), FieldAttributes.Private | FieldAttributes.Static);
                this._currentScope.Fields.Add(key, fieldInfo);
            }
        }

        private void EmitIL()
        {
            foreach (Statement statement in this._parser.ParseTree)
                statement.PrepareForEmit(this._currentScope);
            foreach (Statement statement in this._parser.ParseTree)
                statement.EmitIL(this._currentScope);
        }

        private bool EmitModule(ModuleBuilder moduleBuilder)
        {
            TypeBuilder typeBuilder = moduleBuilder.DefineType("_SmallBasicProgram", TypeAttributes.Sealed);
            MethodBuilder methodBuilder = typeBuilder.DefineMethod("_Main", MethodAttributes.Static);
            this._entryPoint = (MethodInfo)methodBuilder;
            ILGenerator ilGenerator = methodBuilder.GetILGenerator();
            this._currentScope = new CodeGenScope()
            {
                ILGenerator = ilGenerator,
                MethodBuilder = methodBuilder,
                TypeBuilder = typeBuilder,
                SymbolTable = this._symbolTable,
                TypeInfoBag = this._typeInfoBag
            };
            this.BuildFields(typeBuilder);
            this.EmitIL();
            ilGenerator.Emit(OpCodes.Ret);
            typeBuilder.CreateType();
            return true;
        }
    }
}