﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace WinFormsComInterop.SourceGenerator
{
    /// <summary>
    /// Generator for COM interface proxies, used by ComWrappers.
    /// </summary>
    [Generator]
    public class Generator : ISourceGenerator
    {
        private const string AttributeSource = @"// <auto-generated>
// Code generated by COM interface proxies Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>
#nullable disable

[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple=true)]
internal sealed class ComCallableWrapperAttribute: System.Attribute
{
    public ComCallableWrapperAttribute(System.Type interfaceType, string alias = null)
    {
        this.InterfaceType = interfaceType;
        this.Alias = alias;
    }

    public System.Type InterfaceType { get; }

    public string Alias { get; }
}

[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple=true)]
internal sealed class RuntimeCallableWrapperAttribute: System.Attribute
{
    public RuntimeCallableWrapperAttribute(System.Type interfaceType, string alias = null)
    {
        this.InterfaceType = interfaceType;
        this.Alias = alias;
    }

    public System.Type InterfaceType { get; }

    public string Alias { get; }
}
";

        private static string ComCallableWrapperAttributeName = "ComCallableWrapperAttribute";

        private static string RuntimeCallableWrapperAttributeName = "RuntimeCallableWrapperAttribute";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization((pi) => pi.AddSource("ComProxyAttribute.cs", AttributeSource));
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Retrieve the populated receiver
            if (context.SyntaxContextReceiver is not SyntaxReceiver receiver)
            {
                return;
            }

            var wrapperContext = new WrapperGenerationContext(context);
            foreach (var classSymbol in receiver.CCWDeclarations)
            {
                ProcessCCWDeclaration(classSymbol, wrapperContext);
            }

            foreach (var classSymbol in receiver.RCWDeclarations)
            {
                ProcessRCWDeclaration(classSymbol, wrapperContext);
            }

            // Uncomment line below to trace resulting codegen.
            // context.AddSource("DebugOutput.cs", wrapperContext.DebugOutput());
        }
        private string ProcessCCWDeclaration(ClassDeclaration classSymbol, INamedTypeSymbol interfaceTypeSymbol, WrapperGenerationContext context)
        {
            string namespaceName = classSymbol.Type.ContainingNamespace.ToDisplayString();
            IndentedStringBuilder source = new IndentedStringBuilder($@"// <auto-generated>
// Code generated by COM Proxy Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>
#nullable enable
");
            var aliasSymbol = context.GetAlias(interfaceTypeSymbol);
            if (!string.IsNullOrWhiteSpace(aliasSymbol))
            {
                source.AppendLine($"extern alias {aliasSymbol};");
            }

            source.AppendLine("using ComInterfaceDispatch = System.Runtime.InteropServices.ComWrappers.ComInterfaceDispatch;");
            source.AppendLine("using Marshal = System.Runtime.InteropServices.Marshal;");
            source.Append($@"
namespace {namespaceName}
{{
");
            source.PushIndent();
            source.AppendLine("[System.Runtime.Versioning.SupportedOSPlatform(\"windows\")]");
            var typeName = $"{interfaceTypeSymbol.Name}Proxy";
            if (!string.IsNullOrWhiteSpace(aliasSymbol))
            {
                typeName = aliasSymbol.Substring(0,1).ToUpperInvariant() + aliasSymbol.Substring(1) + typeName;
            }

            context.AddDebugLine(interfaceTypeSymbol.ToDisplayString());
            context.AddDebugLine(interfaceTypeSymbol.ContainingAssembly.ToDisplayString());
            context.AddDebugLine(context.GetAlias(interfaceTypeSymbol) + "_" + aliasSymbol);
            source.AppendLine($"unsafe partial class {typeName}");
            source.AppendLine("{");
            source.PushIndent();
            foreach (var member in interfaceTypeSymbol.GetMembers())
            {
                var preserveSigAttribute = member.GetAttributes().FirstOrDefault(ad =>
                {
                    string attributeName = ad.AttributeClass?.ToDisplayString();
                    return attributeName == "System.Runtime.InteropServices.PreserveSigAttribute"
                    || attributeName == "PreserveSigAttribute";
                });
                var preserveSignature = preserveSigAttribute != null;
                context.AddDebugLine(member.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "=" + preserveSignature.ToString() + " " + member.GetAttributes().Length);

                switch (member)
                {
                    case IMethodSymbol methodSymbol:
                        {
                            preserveSignature |= (methodSymbol.MethodImplementationFlags & System.Reflection.MethodImplAttributes.PreserveSig) == System.Reflection.MethodImplAttributes.PreserveSig;
                            var methodContext = context.CreateMethodGenerationContext(methodSymbol, preserveSignature);
                            GenerateCCWMethod(source, interfaceTypeSymbol, methodContext);
                        }
                        break;
                    case IPropertySymbol propertySymbol:
                        break;
                }
            }

            source.PopIndent();
            source.AppendLine("}");
            source.PopIndent();

            source.Append("}");
            return source.ToString();
        }

        private void ProcessCCWDeclaration(ClassDeclaration classSymbol, WrapperGenerationContext context)
        {
            var proxyDeclarations = classSymbol.Type.GetAttributes().Where(ad => ad.AttributeClass?.ToDisplayString() == ComCallableWrapperAttributeName);
            foreach (var proxyAttribute in proxyDeclarations)
            {
                var interfaceTypeSymbol = proxyAttribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
                if (interfaceTypeSymbol == null)
                {
                    continue;
                }

                var sourceCode = ProcessCCWDeclaration(classSymbol, interfaceTypeSymbol, context);
                var key = (INamedTypeSymbol)classSymbol.Type;
                context.AddDebugLine(interfaceTypeSymbol.ContainingAssembly.Identity.GetDisplayName());
                context.AddSource(key, interfaceTypeSymbol, SourceText.From(sourceCode, Encoding.UTF8));
            }
        }
        private string ProcessRCWDeclaration(ClassDeclaration classSymbol, INamedTypeSymbol interfaceTypeSymbol, WrapperGenerationContext context)
        {
            string namespaceName = classSymbol.Type.ContainingNamespace.ToDisplayString();
            IndentedStringBuilder source = new IndentedStringBuilder($@"// <auto-generated>
// Code generated by COM Proxy Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>
#nullable enable
");
            var aliasSymbol = context.GetAlias(interfaceTypeSymbol);
            if (!string.IsNullOrWhiteSpace(aliasSymbol))
            {
                source.AppendLine($"extern alias {aliasSymbol};");
            }

            source.AppendLine("using Marshal = System.Runtime.InteropServices.Marshal;");
            source.Append($@"
namespace {namespaceName}
{{
");
            source.PushIndent();
            source.AppendLine("[System.Runtime.Versioning.SupportedOSPlatform(\"windows\")]");
            var typeName = $"{classSymbol.Type.Name}";

            source.AppendLine($"unsafe partial class {classSymbol.Type.Name} : {interfaceTypeSymbol.FormatType(aliasSymbol)}");
            source.AppendLine("{");
            source.PushIndent();
            int slotNumber = 3; /* Starting with slot after IUnknown */
            foreach (var member in interfaceTypeSymbol.GetMembers())
            {
                var preserveSigAttribute = member.GetAttributes().FirstOrDefault(ad =>
                {
                    string attributeName = ad.AttributeClass?.ToDisplayString();
                    return attributeName == "System.Runtime.InteropServices.PreserveSigAttribute"
                    || attributeName == "PreserveSigAttribute";
                });
                var preserveSignature = preserveSigAttribute != null;
                context.AddDebugLine(member.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "=" + preserveSignature.ToString() + " " + member.GetAttributes().Length);

                switch (member)
                {
                    case IMethodSymbol methodSymbol:
                        {
                            preserveSignature |= (methodSymbol.MethodImplementationFlags & System.Reflection.MethodImplAttributes.PreserveSig) == System.Reflection.MethodImplAttributes.PreserveSig;
                            var methodContext = context.CreateMethodGenerationContext(methodSymbol, preserveSignature, slotNumber);
                            GenerateRCWMethod(source, interfaceTypeSymbol, methodContext);
                            slotNumber++;
                        }
                        break;
                    case IPropertySymbol propertySymbol:
                        break;
                }
            }

            source.PopIndent();
            source.AppendLine("}");
            source.PopIndent();

            source.Append("}");
            return source.ToString();
        }

        private void ProcessRCWDeclaration(ClassDeclaration classSymbol, WrapperGenerationContext context)
        {
            var proxyDeclarations = classSymbol.Type.GetAttributes().Where(ad => ad.AttributeClass?.ToDisplayString() == RuntimeCallableWrapperAttributeName);
            foreach (var proxyAttribute in proxyDeclarations)
            {
                var interfaceTypeSymbol = proxyAttribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
                if (interfaceTypeSymbol == null)
                {
                    continue;
                }

                var sourceCode = ProcessRCWDeclaration(classSymbol, interfaceTypeSymbol, context);
                var key = (INamedTypeSymbol)classSymbol.Type;
                context.AddDebugLine(interfaceTypeSymbol.ContainingAssembly.Identity.GetDisplayName());
                context.AddSource(key, interfaceTypeSymbol, SourceText.From(sourceCode, Encoding.UTF8));
            }
        }

        private Marshaller CreateMarshaller(IParameterSymbol parameterSymbol, MethodGenerationContext context)
        {
            Marshaller marshaller = CreateMarshaller(parameterSymbol.Type);
            marshaller.Name = parameterSymbol.Name;
            marshaller.Type = parameterSymbol.Type;
            marshaller.RefKind = parameterSymbol.RefKind;
            marshaller.Index = parameterSymbol.Ordinal;
            marshaller.TypeAlias = context.GetAlias(parameterSymbol.Type as INamedTypeSymbol);
            return marshaller;
        }

        private Marshaller CreateReturnMarshaller(ITypeSymbol parameterSymbol, MethodGenerationContext context)
        {
            Marshaller marshaller = CreateMarshaller(parameterSymbol);
            marshaller.Name = "retVal";
            marshaller.Type = parameterSymbol;
            marshaller.RefKind = RefKind.None;
            marshaller.Index = -1;
            marshaller.TypeAlias = context.GetAlias(parameterSymbol as INamedTypeSymbol);
            return marshaller;
        }

        private Marshaller CreateMarshaller(ITypeSymbol parameterSymbol)
        {
            if (parameterSymbol.IsEnum() || parameterSymbol.TypeKind == TypeKind.Enum)
            {
                return new EnumMarshaller();
            }

            if (parameterSymbol.TypeKind == TypeKind.Interface || parameterSymbol.SpecialType == SpecialType.System_Object)
            {
                return new ComInterfaceMarshaller();
            }

            return new BlittableMarshaller();
        }

        private void GenerateCCWMethod(IndentedStringBuilder source, INamedTypeSymbol interfaceSymbol, MethodGenerationContext context)
        {
            source.AppendLine("[System.Runtime.InteropServices.UnmanagedCallersOnly]");
            var method = context.Method;
            var marshallers = method.Parameters.Select(_ =>
            {
                var marshaller = CreateMarshaller(_, context);
                return marshaller;
            });
            var preserveSignature = context.PreserveSignature;
            var parametersList = marshallers.Select(_ => _.GetUnmanagedParameterDeclaration()).ToList();
            parametersList.Insert(0, "System.IntPtr thisPtr");
            var returnMarshaller = CreateReturnMarshaller(method.ReturnType, context);
            if (!preserveSignature)
            {
                if (method.ReturnType.SpecialType != SpecialType.System_Void)
                {
                    parametersList.Add(returnMarshaller.GetReturnDeclaration());
                }
            }

            var parametersListString = string.Join(", ", parametersList);
            source.AppendLine($"public static int {method.Name}({parametersListString})");
            source.AppendLine("{");
            source.PushIndent();

            source.AppendLine("try");
            source.AppendLine("{");
            source.PushIndent();

            source.AppendLine($"var inst = ComInterfaceDispatch.GetInstance<{interfaceSymbol.FormatType(context.GetAlias(interfaceSymbol))}>((ComInterfaceDispatch*)thisPtr);");
            foreach (var p in marshallers)
            {
                p.DeclareLocalParameter(source);
            }
            var parametersInvocationList = string.Join(", ", marshallers.Select(_ => _.GetParameterInvocation()));

            if (!preserveSignature)
            {
                if (method.ReturnType.SpecialType == SpecialType.System_Void)
                {
                    source.AppendLine($"inst.{method.Name}({parametersInvocationList});");
                }
                else
                {
                    string invocationExpression;
                    if (method.MethodKind == MethodKind.PropertyGet)
                    {
                        invocationExpression = $"inst.{method.AssociatedSymbol.Name}";
                    }
                    else
                    {
                        invocationExpression = $"inst.{method.Name}({parametersInvocationList})";
                    }

                    returnMarshaller.GetReturnValue(source, invocationExpression);
                }
            }
            else
            {
                source.AppendLine($"return (int)inst.{method.Name}({parametersInvocationList});");
            }

            source.PopIndent();
            source.AppendLine("}");
            source.AppendLine("catch (System.Exception __e)");
            source.AppendLine("{");
            source.PushIndent();
            source.AppendLine("return __e.HResult;");
            source.PopIndent();
            source.AppendLine("}");
            if (!preserveSignature)
            {
                source.AppendLine();
                source.AppendLine("return 0; // S_OK;");
            }

            source.PopIndent();
            source.AppendLine("}");
        }

        private void GenerateRCWMethod(IndentedStringBuilder source, INamedTypeSymbol interfaceSymbol, MethodGenerationContext context)
        {
            var method = context.Method;
            var marshallers = method.Parameters.Select(_ =>
            {
                var marshaller = CreateMarshaller(_, context);
                return marshaller;
            });
            var preserveSignature = context.PreserveSignature;
            var parametersList = method.Parameters.Select(_ => $"{_.Type} {_.Name}").ToList();
            var returnMarshaller = CreateReturnMarshaller(method.ReturnType, context);

            var parametersListString = string.Join(", ", parametersList);
            source.AppendLine($"public {method.ReturnType} {method.Name}({parametersListString})");
            source.AppendLine("{");
            source.PushIndent();

            source.AppendLine($"var targetInterface = new System.Guid(\"{interfaceSymbol.GetTypeGuid()}\");");
            source.AppendLine("var result = Marshal.QueryInterface(this.instance, ref targetInterface, out var thisPtr);");
            source.AppendLine("if (result != 0)");
            source.AppendLine("{");
            source.PushIndent();
            source.AppendLine("throw new InvalidCastException();");
            source.PopIndent();
            source.AppendLine("}");
            source.AppendLine();
            source.AppendLine("IntPtr* comDispatch = (IntPtr*)thisPtr;");
            source.AppendLine("IntPtr* vtbl = (IntPtr*)comDispatch[0];");
            var parametersCallList = string.Join(", ", context.Method.Parameters.Select(_ => _.Name));
            source.AppendLine($"(({context.UnmanagedDelegateSignature})vtbl[{context.ComSlotNumber}])(thisPtr, {parametersCallList});");
            source.PopIndent();
            source.AppendLine("}");
        }

        internal class ClassDeclaration
        {
            public ITypeSymbol Type { get; set; }

            public string Alias { get; set; }
        }

        internal class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<ClassDeclaration> CCWDeclarations { get; } = new List<ClassDeclaration>();
            public List<ClassDeclaration> RCWDeclarations { get; } = new List<ClassDeclaration>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                // any field with at least one attribute is a candidate for property generation
                if (context.Node is ClassDeclarationSyntax classDeclarationSyntax
                    && classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    // Get the symbol being declared by the field, and keep it if its annotated
                    var classSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as ITypeSymbol;
                    if (classSymbol == null)
                    {
                        return;
                    }

                    if (classSymbol.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == ComCallableWrapperAttributeName))
                    {
                        var argumentExpression = classDeclarationSyntax.AttributeLists[0].Attributes[0].ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                        var typeNameSyntax = argumentExpression.Type as QualifiedNameSyntax;
                        var alias = "";
                        this.CCWDeclarations.Add(new ClassDeclaration { Type = classSymbol, Alias = alias });
                    }

                    if (classSymbol.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == RuntimeCallableWrapperAttributeName))
                    {
                        var argumentExpression = classDeclarationSyntax.AttributeLists[0].Attributes[0].ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                        var typeNameSyntax = argumentExpression.Type as QualifiedNameSyntax;
                        var alias = "";
                        this.RCWDeclarations.Add(new ClassDeclaration { Type = classSymbol, Alias = alias });
                    }
                }
            }
        }
    }
}
