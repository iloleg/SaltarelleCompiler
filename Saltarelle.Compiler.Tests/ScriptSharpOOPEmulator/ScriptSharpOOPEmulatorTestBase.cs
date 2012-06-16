﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Moq;
using NUnit.Framework;
using Saltarelle.Compiler.JSModel.Expressions;
using Saltarelle.Compiler.JSModel.Statements;
using Saltarelle.Compiler.JSModel.TypeSystem;

namespace Saltarelle.Compiler.Tests.ScriptSharpOOPEmulator {
	public class ScriptSharpOOPEmulatorTestBase {
		protected ITypeDefinition CreateMockType() {
			var typeDef = new Mock<ICSharpCode.NRefactory.TypeSystem.ITypeDefinition>(MockBehavior.Strict);
			typeDef.SetupGet(_ => _.Attributes).Returns(new IAttribute[0]);
			return typeDef.Object;
		}

		protected string Process(IEnumerable<JsType> types) {
			var proj = new CSharpProjectContent();
			var comp = proj.CreateCompilation();
			var obj = new OOPEmulator.ScriptSharpOOPEmulator();
			var rewritten = obj.Rewrite(types, tr => new JsTypeReferenceExpression(comp.MainAssembly, tr.ToString()), comp.MainAssembly);
			return string.Join("", rewritten.Select(s => OutputFormatter.Format(s, allowIntermediates: true)));
		}

		protected void AssertCorrect(string expected, IEnumerable<JsType> types) {
			var actual = Process(types);

			Assert.That(actual.Replace("\r\n", "\n"), Is.EqualTo(expected.Replace("\r\n", "\n")));
		}

		protected void AssertCorrect(string expected, params JsType[] types) {
			AssertCorrect(expected, (IEnumerable<JsType>)types);
		}
	}
}