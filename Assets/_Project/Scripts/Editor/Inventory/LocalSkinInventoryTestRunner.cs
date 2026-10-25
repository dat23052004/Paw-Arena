using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace PawArena.Editor.Inventory
{
    /// <summary>Runs synchronous NUnit assertion fixtures on the editor thread without saving or switching user scenes.</summary>
    public static class LocalSkinInventoryTestRunner
    {
        private const string RequestPath = "Library/LocalSkinInventoryChecks.request";
        private const string ResultPath = "Logs/LocalSkinInventoryChecks.xml";

        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.delayCall += RunRequest;
        private static void RunRequest()
        {
            if (!File.Exists(RequestPath) || File.ReadAllText(RequestPath).Trim() != "test") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RunRequest; return; }
            Run();
        }

        /// <summary>Runs synchronous NUnit assertion methods with setup/teardown, without Unity Test Runner scene management.</summary>
        [MenuItem("Tools/Paw Arena/Inventory/Run Local Inventory Checks")]
        public static void Run()
        {
            File.WriteAllText(RequestPath, "running");
            var report = new XElement("inventory-checks", new XAttribute("mode", "Main-thread NUnit fixture assertions; not Play Mode"));
            var errors = new List<string>();
            Application.LogCallback collect = (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors.Add(message); }
            };
            Application.logMessageReceived += collect;
            var passed = 0;
            var failed = 0;
            try
            {
                foreach (var typeName in new[] { "PawArena.Tests.EditMode.SkinInventorySessionTests", "PawArena.Tests.Inventory.CatSkinIntegrationTests",
                    "PawArena.Tests.EditMode.CatGearSessionTests", "PawArena.Tests.Inventory.CatGearIntegrationTests" })
                {
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
                    if (type == null) { throw new InvalidOperationException("Test fixture not compiled: " + typeName); }
                    var methods = type.GetMethods();
                    var setup = methods.Where(m => HasAttribute(m, "SetUpAttribute")).ToArray();
                    var teardown = methods.Where(m => HasAttribute(m, "TearDownAttribute")).ToArray();
                    foreach (var method in methods.Where(m => HasAttribute(m, "TestAttribute")))
                    {
                        var fixture = Activator.CreateInstance(type);
                        var result = new XElement("check", new XAttribute("name", typeName + "." + method.Name));
                        Exception failure = null;
                        errors.Clear();
                        try
                        {
                            foreach (var initialize in setup) { initialize.Invoke(fixture, null); }
                            method.Invoke(fixture, null);
                        }
                        catch (Exception exception) { failure = exception.InnerException ?? exception; }
                        finally
                        {
                            foreach (var cleanup in teardown)
                            {
                                try { cleanup.Invoke(fixture, null); }
                                catch (Exception exception) { failure ??= exception.InnerException ?? exception; }
                            }
                        }
                        if (failure == null && errors.Count == 0) { passed++; result.SetAttributeValue("result", "Passed"); }
                        else
                        {
                            failed++;
                            result.SetAttributeValue("result", "Failed");
                            result.Add(new XElement("failure", failure?.ToString() ?? string.Join("\n", errors)));
                        }
                        report.Add(result);
                    }
                }
            }
            catch (Exception exception) { failed++; report.Add(new XElement("failure", exception)); }
            finally
            {
                Application.logMessageReceived -= collect;
                report.SetAttributeValue("passed", passed);
                report.SetAttributeValue("failed", failed);
                report.Save(ResultPath);
                var message = $"{passed} passed, {failed} failed";
                File.WriteAllText(RequestPath, message);
                Debug.Log("[Local Skin Inventory Checks] " + message);
            }
        }

        private static bool HasAttribute(MemberInfo method, string name) =>
            method.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == name);
    }
}
