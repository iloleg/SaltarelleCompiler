﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Saltarelle.Compiler.Compiler;
using Saltarelle.Compiler.JSModel.Expressions;
using Saltarelle.Compiler.JSModel.Statements;
using Saltarelle.Compiler.ScriptSemantics;

namespace Saltarelle.Compiler {
	public static class Utils {
		/// <summary>
		/// A type is externally visible if it and all its declaring types are public or protected (or protected internal).
		/// A member is externally visible if it is public or protected (or protected internal) and its declaring type is externally visible.
		/// </summary>
		public static bool IsExternallyVisible(this ISymbol type) {
			while (type != null) {
				bool isPublic = (type.DeclaredAccessibility == Accessibility.NotApplicable || type.DeclaredAccessibility == Accessibility.Public || type.DeclaredAccessibility == Accessibility.Protected || type.DeclaredAccessibility == Accessibility.ProtectedOrInternal);
				if (!isPublic)
					return false;
				type = type.ContainingSymbol;
			}
			return true;
		}

		private static void FindTypeUsageErrors(IEnumerable<ITypeSymbol> types, IMetadataImporter metadataImporter, HashSet<INamedTypeSymbol> usedUnusableTypes, HashSet<INamedTypeSymbol> mutableValueTypesBoundToTypeArguments) {
			#warning TODO
			//foreach (var t in types) {
			//	if (t is INamedTypeSymbol) {
			//		if (metadataImporter.GetTypeSemantics((INamedTypeSymbol)t).Type == TypeScriptSemantics.ImplType.NotUsableFromScript)
			//			usedUnusableTypes.Add((INamedTypeSymbol)t);
			//	}
			//	else if (t is ParameterizedType) {
			//		var pt = (ParameterizedType)t;
			//		foreach (var ta in pt.TypeArguments) {
			//			if (ta.Kind == TypeKind.Struct && metadataImporter.GetTypeSemantics(ta.GetDefinition()).Type == TypeScriptSemantics.ImplType.MutableValueType)
			//				mutableValueTypesBoundToTypeArguments.Add(ta.GetDefinition());
			//		}
			//		FindTypeUsageErrors(new[] { pt.GetDefinition() }.Concat(pt.TypeArguments), metadataImporter, usedUnusableTypes, mutableValueTypesBoundToTypeArguments);
			//	}
			//}
		}

		public class UnusableTypesResult {
			public IList<ITypeSymbol> UsedUnusableTypes { get; private set; }
			public IList<ITypeSymbol> MutableValueTypesBoundToTypeArguments { get; private set; }

			public bool HasErrors {
				get { return UsedUnusableTypes.Count > 0 || MutableValueTypesBoundToTypeArguments.Count > 0; }
			}

			public UnusableTypesResult(IList<ITypeSymbol> usedUnusableTypes, IList<ITypeSymbol> mutableValueTypesBoundToTypeArguments) {
				UsedUnusableTypes = usedUnusableTypes;
				MutableValueTypesBoundToTypeArguments = mutableValueTypesBoundToTypeArguments;
			}
		}

		public static UnusableTypesResult FindGenericInstantiationErrors(IEnumerable<ITypeSymbol> typeArguments, IMetadataImporter metadataImporter) {
			if (!(typeArguments is ICollection<ITypeSymbol>))
				typeArguments = typeArguments.ToList();

			var usedUnusableTypes = new HashSet<INamedTypeSymbol>();
			var mutableValueTypesBoundToTypeArguments = new HashSet<INamedTypeSymbol>();

			foreach (var ta in typeArguments) {
				if (ta.TypeKind == TypeKind.Struct && metadataImporter.GetTypeSemantics((INamedTypeSymbol)ta.OriginalDefinition).Type == TypeScriptSemantics.ImplType.MutableValueType)
					mutableValueTypesBoundToTypeArguments.Add((INamedTypeSymbol)ta.OriginalDefinition);
			}

			FindTypeUsageErrors(typeArguments, metadataImporter, usedUnusableTypes, mutableValueTypesBoundToTypeArguments);
			return new UnusableTypesResult(usedUnusableTypes.Count > 0 ? usedUnusableTypes.ToList<ITypeSymbol>() : (IList<ITypeSymbol>)ImmutableArray<ITypeSymbol>.Empty, mutableValueTypesBoundToTypeArguments.Count > 0 ? mutableValueTypesBoundToTypeArguments.ToList<ITypeSymbol>() : (IList<ITypeSymbol>)ImmutableArray<ITypeSymbol>.Empty);
		}

		public static UnusableTypesResult FindTypeUsageErrors(IEnumerable<ITypeSymbol> types, IMetadataImporter metadataImporter) {
			var usedUnusableTypes = new HashSet<INamedTypeSymbol>();
			var mutableValueTypesBoundToTypeArguments = new HashSet<INamedTypeSymbol>();
			FindTypeUsageErrors(types, metadataImporter, usedUnusableTypes, mutableValueTypesBoundToTypeArguments);
			return new UnusableTypesResult(usedUnusableTypes.Count > 0 ? usedUnusableTypes.ToList<ITypeSymbol>() : (IList<ITypeSymbol>)ImmutableArray<ITypeSymbol>.Empty, mutableValueTypesBoundToTypeArguments.Count > 0 ? mutableValueTypesBoundToTypeArguments.ToList<ITypeSymbol>() : (IList<ITypeSymbol>)ImmutableArray<ITypeSymbol>.Empty);
		}

		/// <summary>
		/// For generic types, returns a ParameterizedType with each type argument being a TypeParameter with the name of the type parameter in the type definition. Returns the TypeDefinition itself for non-generic types.
		/// </summary>
		public static ITypeSymbol SelfParameterize(INamedTypeSymbol type) {
			#warning TODO (perhaps remove)
			//return type.TypeParameterCount == 0 ? (ITypeSymbol)type : new ParameterizedType(type, type.TypeParameters.Select(tp => new DefaultTypeParameter(type, tp.Index, tp.Name)));
			return type;
		}

		public static void CreateTemporariesForAllExpressionsThatHaveToBeEvaluatedBeforeNewExpression(IList<JsStatement> statementList, IList<JsExpression> expressions, JsExpression newExpression, Func<string> createTemporaryVariable) {
			CreateTemporariesForAllExpressionsThatHaveToBeEvaluatedBeforeNewExpression(statementList, expressions, new ExpressionCompileResult(newExpression, new JsStatement[0]), createTemporaryVariable);
		}

		public static void CreateTemporariesForAllExpressionsThatHaveToBeEvaluatedBeforeNewExpression(IList<JsStatement> statementList, IList<JsExpression> expressions, ExpressionCompileResult newExpressions, Func<string> createTemporaryVariable) {
			for (int i = 0; i < expressions.Count; i++) {
				if (ExpressionOrderer.DoesOrderMatter(expressions[i], newExpressions)) {
					var temp = createTemporaryVariable();
					statementList.Add(JsStatement.Var(temp, expressions[i]));
					expressions[i] = JsExpression.Identifier(temp);
				}
			}
		}

		public static JsExpression EnsureCanBeEvaluatedMultipleTimes(IList<JsStatement> statementList, JsExpression expression, IList<JsExpression> expressionsThatMustBeEvaluatedBefore, Func<string> createTemporaryVariable) {
			if (IsJsExpressionComplexEnoughToGetATemporaryVariable.Analyze(expression)) {
				CreateTemporariesForAllExpressionsThatHaveToBeEvaluatedBeforeNewExpression(statementList, expressionsThatMustBeEvaluatedBefore, expression, createTemporaryVariable);
				var temp = createTemporaryVariable();
				statementList.Add(JsStatement.Var(temp, expression));
				return JsExpression.Identifier(temp);
			}
			else
				return expression;
		}

		public static JsExpression ResolveTypeParameter(ITypeParameterSymbol tp, INamedTypeSymbol currentType, IMethodSymbol currentMethod, IMetadataImporter metadataImporter, IErrorReporter errorReporter, INamer namer) {
			bool unusable = false;
			switch (tp.TypeParameterKind) {
				case TypeParameterKind.Type:
					unusable = metadataImporter.GetTypeSemantics(currentType).IgnoreGenericArguments;
					break;
				case TypeParameterKind.Method: {
					var sem = metadataImporter.GetMethodSemantics(currentMethod);
					unusable = sem.Type != MethodScriptSemantics.ImplType.InlineCode && metadataImporter.GetMethodSemantics(currentMethod).IgnoreGenericArguments;
					break;
				}
				default:
					errorReporter.InternalError("Invalid owner " + tp.TypeParameterKind + " for type parameter " + tp);
					return JsExpression.Null;
			}
			if (unusable) {
				errorReporter.Message(Messages._7536, tp.Name, tp.TypeParameterKind == TypeParameterKind.Type ? "type" : "method", tp.TypeParameterKind == TypeParameterKind.Type ? currentType.Name : currentMethod.Name);
				return JsExpression.Null;
			}
			return JsExpression.Identifier(namer.GetTypeParameterName(tp));
		}

		public static JsExpression MaybeCloneValueType(JsExpression input, ExpressionSyntax csharpInput, ITypeSymbol type, IMetadataImporter metadataImporter, IRuntimeLibrary runtimeLibrary, IRuntimeContext runtimeContext, bool forceClone = false) {
			#warning TODO
			//if (!forceClone) {
			//	if (input is JsInvocationExpression)
			//		return input;	// The clone was already performed when the callee returned
			//
			//	var actualInput = csharpInput;
			//	var crr = actualInput as ConversionResolveResult;
			//	if (crr != null) {
			//		actualInput = crr.Input;
			//	}
			//
			//	if (actualInput is InvocationResolveResult) {
			//		return input;
			//	}
			//}
			//
			//type = NullableType.GetUnderlyingType(type);
			//if (!IsMutableValueType(type, metadataImporter))
			//	return input;
			//
			return runtimeLibrary.CloneValueType(input, type, runtimeContext);
		}

		public static bool IsMutableValueType(ITypeSymbol type, IMetadataImporter metadataImporter) {
			return type.TypeKind == TypeKind.Struct && metadataImporter.GetTypeSemantics((INamedTypeSymbol)type.OriginalDefinition).Type == TypeScriptSemantics.ImplType.MutableValueType;
		}

		public static IEnumerable<ITypeSymbol> GetAllBaseTypes(this ITypeSymbol type)
		{
			foreach (var i in type.AllInterfaces)
				yield return i;

			while (type != null) {
				yield return type;
				type = type.BaseType;
			}
		}

		public static Location GetLocation(this ISymbol symbol)
		{
			if (symbol.DeclaringSyntaxReferences.Length == 0)
				return null;
			var syntax = symbol.DeclaringSyntaxReferences[0].GetSyntax();
			if (syntax == null)
				return null;
			return syntax.GetLocation();
		}
	}
}
