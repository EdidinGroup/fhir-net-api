﻿/* 
 * Copyright (c) 2015, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/fhir-net-api/master/LICENSE
 */

// To introduce the DSTU2 FHIR specification
// extern alias dstu2;

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Model.Primitives;
using Hl7.FhirPath.Functions;
using Hl7.FhirPath.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks.Dataflow;

namespace Hl7.FhirPath.R4.Tests
{
    [TestClass]
    public class FhirPathTest
    {
        /// <summary>
        ///  Gets or sets the test context which provides
        ///  information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void ConvertToInteger()
        {
            Assert.AreEqual(1L, ElementNode.ForPrimitive(1).ToInteger());
            Assert.AreEqual(2L, ElementNode.ForPrimitive("2").ToInteger());
            Assert.IsNull(ElementNode.ForPrimitive("2.4").ToInteger());
            Assert.AreEqual(1L, ElementNode.ForPrimitive(true).ToInteger());
            Assert.AreEqual(0L, ElementNode.ForPrimitive(false).ToInteger());
            Assert.IsNull(ElementNode.ForPrimitive(2.4m).ToInteger());
            Assert.IsNull(ElementNode.ForPrimitive(DateTimeOffset.Now).ToInteger());
        }

        [TestMethod]
        public void ConvertToString()
        {
            Assert.AreEqual("hoi", ElementNode.ForPrimitive("hoi").ToString());
            Assert.AreEqual("3.4", ElementNode.ForPrimitive(3.4m).ToString());
            Assert.AreEqual("4", ElementNode.ForPrimitive(4L).ToString());
            Assert.AreEqual("true", ElementNode.ForPrimitive(true).ToString());
            Assert.AreEqual("false", ElementNode.ForPrimitive(false).ToString());
            Assert.IsNotNull(ElementNode.ForPrimitive(DateTimeOffset.Now).ToString());
        }

        [TestMethod]
        public void ConvertToDecimal()
        {
            Assert.AreEqual(1m, ElementNode.ForPrimitive(1m).ToDecimal());
            Assert.AreEqual(2.01m, ElementNode.ForPrimitive("2.01").ToDecimal());
            Assert.AreEqual(1L, ElementNode.ForPrimitive(true).ToDecimal());
            Assert.AreEqual(0L, ElementNode.ForPrimitive(false).ToDecimal());
            Assert.IsNull(ElementNode.ForPrimitive(2).ToDecimal());
            //            Assert.Null(ElementNode.ForPrimitive("2").ToDecimal());   Not clear according to spec
            Assert.IsNull(ElementNode.ForPrimitive(DateTimeOffset.Now).ToDecimal());
        }

        [TestMethod]
        public void CheckTypeDetermination()
        {
            var values = ElementNode.CreateList(1, true, "hi", 4.0m, 4.0f, PartialDateTime.Now());


            Test.IsInstanceOfType(values.Item(0).Single().Value, typeof(Int64));
            Test.IsInstanceOfType(values.Item(1).Single().Value, typeof(Boolean));
            Test.IsInstanceOfType(values.Item(2).Single().Value, typeof(String));
            Test.IsInstanceOfType(values.Item(3).Single().Value, typeof(Decimal));
            Test.IsInstanceOfType(values.Item(4).Single().Value, typeof(Decimal));
            Test.IsInstanceOfType(values.Item(5).Single().Value, typeof(PartialDateTime));
        }


        [TestMethod]
        public void TestItemSelection()
        {
            var values = ElementNode.CreateList(1, 2, 3, 4, 5, 6, 7);

            Assert.AreEqual((Int64)1, values.Item(0).Single().Value);
            Assert.AreEqual((Int64)3, values.Item(2).Single().Value);
            Assert.AreEqual((Int64)1, values.First().Value);
            Assert.IsFalse(values.Item(100).Any());
        }

        [TestMethod]
        public void TypeInfoEquality()
        {
            Assert.AreEqual(TypeInfo.Boolean, TypeInfo.Boolean);
            Assert.IsTrue(TypeInfo.Decimal == TypeInfo.ByName("decimal"));
            Assert.AreNotEqual(TypeInfo.Boolean, TypeInfo.String);
            Assert.IsTrue(TypeInfo.Decimal == TypeInfo.ByName("decimal"));
            Assert.AreEqual(TypeInfo.ByName("something"), TypeInfo.ByName("something"));
            Assert.AreNotEqual(TypeInfo.ByName("something"), TypeInfo.ByName("somethingElse"));
            Assert.IsTrue(TypeInfo.ByName("something") == TypeInfo.ByName("something"));
            Assert.IsTrue(TypeInfo.ByName("something") != TypeInfo.ByName("somethingElse"));
        }

        [TestMethod]
        public void TestFhirPathPolymporphism()
        {
            var patient = new Patient() { Active = false };
            patient.Meta = new Meta() { LastUpdated = new DateTimeOffset(2018, 5, 24, 14, 48, 0, TimeSpan.Zero) };
            var nav = patient.ToTypedElement();

            var result = nav.Select("Resource.meta.lastUpdated");
            Assert.IsNotNull(result.FirstOrDefault());
            Assert.AreEqual(PartialDateTime.Parse("2018-05-24T14:48:00+00:00"), result.First().Value);
        }

        [TestMethod]
        public void TestFhirPathTrace()
        {
            var patient = new Hl7.Fhir.Model.Patient() { Id = "pat45", Active = false };
            patient.Meta = new Meta() { LastUpdated = new DateTimeOffset(2018, 5, 24, 14, 48, 0, TimeSpan.Zero) };
            var nav = patient.ToTypedElement();

            EvaluationContext ctx = new FhirEvaluationContext();
            var result = nav.Select("Resource.meta.trace('log').lastUpdated", ctx);
            Assert.IsNotNull(result.FirstOrDefault());
            Assert.AreEqual(PartialDateTime.Parse("2018-05-24T14:48:00+00:00"), result.First().Value);

            bool traced = false;
            ctx.Tracer = (string name, System.Collections.Generic.IEnumerable<ITypedElement> results) =>
            {
                System.Diagnostics.Trace.WriteLine($"{name}");
                Assert.AreEqual("log", name);
                foreach (ITypedElement item in results)
                {
                    var fhirValue = item.Annotation<IFhirValueProvider>();
                    System.Diagnostics.Trace.WriteLine($"--({fhirValue.FhirValue.GetType().Name}): {item.Value} {fhirValue.FhirValue}");
                    Assert.AreEqual(patient.Meta, fhirValue.FhirValue);
                    traced = true;
                }
            };
            result = nav.Select("Resource.meta.trace('log').lastUpdated", ctx);
            Assert.IsNotNull(result.FirstOrDefault());
            Assert.AreEqual(PartialDateTime.Parse("2018-05-24T14:48:00+00:00"), result.First().Value);
            Assert.IsTrue(traced);

            traced = false;
            ctx.Tracer = (string name, System.Collections.Generic.IEnumerable<ITypedElement> results) =>
            {
                System.Diagnostics.Trace.WriteLine($"{name}");
                Assert.IsTrue(name == "id" || name == "log");
                foreach (ITypedElement item in results)
                {
                    var fhirValue = item.Annotation<IFhirValueProvider>();
                    System.Diagnostics.Trace.WriteLine($"--({fhirValue.FhirValue.GetType().Name}): {item.Value} {fhirValue.FhirValue}");
                    traced = true;
                }
            };
            result = nav.Select("Resource.trace('id', id).meta.trace('log', lastUpdated).lastUpdated", ctx);
            Assert.IsNotNull(result.FirstOrDefault());
            Assert.AreEqual(PartialDateTime.Parse("2018-05-24T14:48:00+00:00"), result.First().Value);
            Assert.IsTrue(traced);
        }

        [TestMethod]
        public void TestFhirPathCombine2()
        {
            var cs = new Hl7.Fhir.Model.CodeSystem() { Id = "pat45" };
            cs.Concept.Add(new CodeSystem.ConceptDefinitionComponent()
            {
                Code = "5",
                Display = "Five"
            });
            var nav = cs.ToTypedElement();

            EvaluationContext ctx = new EvaluationContext();
            var result = nav.Predicate("concept.code.combine($this.descendants().concept.code).isDistinct()", ctx);
            Assert.IsTrue(result);

            cs.Concept.Add(new CodeSystem.ConceptDefinitionComponent()
            {
                Code = "5",
                Display = "Five"
            });
            result = nav.Predicate("concept.code.combine($this.descendants().concept.code).isDistinct()", ctx);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TestFhirPathCombine()
        {
            var patient = new Patient();

            patient.Identifier.Add(new Identifier("http://nu.nl", "id1"));
            patient.Identifier.Add(new Identifier("http://nu.nl", "id2"));

            var result = patient.Predicate("identifier[0].value.combine($this.identifier[1].value).isDistinct()");
            Assert.IsTrue(result);

            result = patient.Predicate("identifier[0].value.combine($this.identifier[0].value).isDistinct()");
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("LongRunner")]
        public void TestFhirPathScalarInParallel()
        {
            var patient = new Patient();

            patient.Identifier.Add(new Identifier("http://nu.nl", "id1"));

            var element = patient.ToTypedElement();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var actionBlock = new ActionBlock<string>(
                 (input =>
                 {
                     element.Select(input);
                 }),
                 new ExecutionDataflowBlockOptions()
                 {
                     MaxDegreeOfParallelism = 20
                 });


            for (int i = 0; i < 20_000; i++)
            {
                actionBlock.Post($"identifier[{i}]");
            }
            actionBlock.Complete();
            actionBlock.Completion.Wait();

            stopwatch.Stop();

            TestContext.WriteLine($"Select in 20.000 : {stopwatch.ElapsedMilliseconds}ms");
        }

        //[TestMethod]
        //public void TypeInfoAndNativeMatching()
        //{
        //    Assert.True(TypeInfo.Decimal.MapsToNative(typeof(decimal)));
        //    Assert.False(TypeInfo.Decimal.MapsToNative(typeof(long)));
        //    Assert.False(TypeInfo.Any.CanBeCastTo(typeof(long)));
        //}

        [TestMethod]
        public void TestFhirPathRootResource()
        {
            var bundle = new Bundle() { Type = Bundle.BundleType.Collection, Id = "bundle-1" };
            var patient = new Patient() { Id = "patient-1" };
            var containedPat = new Patient() { Id = "contained-1" };
            patient.Contained.Add(containedPat);
            patient.Link.Add(new Patient.LinkComponent() { Other = new ResourceReference("#contained-1"), Type = Patient.LinkType.Seealso });
            bundle.AddResourceEntry(patient, "http://example.com/patient1");

            var patBundle = new ScopedNode(bundle.ToTypedElement());

            // focus on the contained resource
            EvaluationContext ctx = new FhirEvaluationContext(patBundle.Select("entry.first().resource.contained")?.FirstOrDefault());
            Assert.AreEqual("contained-1", patBundle.Scalar("%resource.id", ctx));
            Assert.AreEqual("patient-1", patBundle.Scalar("%rootResource.id", ctx));

            // focus on the property of a contained resource
            ctx = new FhirEvaluationContext(patBundle.Select("entry.first().resource.id")?.FirstOrDefault());
            Assert.AreEqual("patient-1", patBundle.Scalar("%resource", ctx));
            Assert.AreEqual("patient-1", patBundle.Scalar("%rootResource", ctx));


            //focus on bundle entry
            ctx = new FhirEvaluationContext(patBundle.Select("entry.first().resource")?.FirstOrDefault());
            Assert.AreEqual("patient-1", patBundle.Scalar("%resource.id", ctx));
            Assert.AreEqual("patient-1", patBundle.Scalar("%rootResource.id", ctx));

            // focus on bundle 
            ctx = new FhirEvaluationContext(patBundle);
            Assert.AreEqual("bundle-1", patBundle.Scalar("%resource.id", ctx));
            Assert.AreEqual("bundle-1", patBundle.Scalar("%rootResource.id", ctx));

            // Testing %context and $this
            var node = patBundle.Select("entry.first().resource.contained")?.FirstOrDefault();
            Assert.AreEqual("contained-1", node.Scalar("%context.id", ctx));
            Assert.AreEqual("contained-1", node.Scalar("$this.id", ctx));
        }

        [TestMethod]
        public void TestBackTick()
        {
            var patient = new Patient() { Text = new Narrative() { Div = "<p>test</p>" } };

            var nav = patient.ToTypedElement();

            var divExists = nav.Scalar("text.`div`.exists()");

            Assert.AreEqual(true, divExists);
        }
    }
}