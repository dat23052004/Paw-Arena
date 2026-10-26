using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace PawArena.Editor.Characters
{
    /// <summary>Run isolated NUnit fixture assertions without changing the open scene.</summary>
    public static class CharacterFoundationChecks
    {
        private const string Request = "Library/CharacterFoundationChecks.request";
        private static double _nextPoll;
        private static bool _refreshed;
        [InitializeOnLoadMethod] private static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) { return; } _nextPoll = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request) || File.ReadAllText(Request).Trim() != "test") { return; }
            if (!_refreshed) { _refreshed = true; AssetDatabase.Refresh(); return; }
            _refreshed = false; Run();
        }
        [MenuItem("Tools/Paw Arena/Characters/Run Foundation Checks")]
        public static void Run()
        {
            File.WriteAllText(Request, "running"); var report = new XElement("character-checks"); var passed = 0; var failed = 0;
            try
            {
                // Script recompilation can briefly leave an older test assembly loaded; choose the richest current fixture.
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("PawArena.Tests.Characters.CharacterFoundationTests"))
                    .Where(t => t != null)
                    .OrderByDescending(t => t.GetMethods().Count(m => Has(m, "TestAttribute")))
                    .First();
                foreach (var method in type.GetMethods().Where(m => Has(m, "TestAttribute")))
                {
                    var fixture = Activator.CreateInstance(type); Exception error = null;
                    try { type.GetMethod("SetUp").Invoke(fixture, null); method.Invoke(fixture, null); }
                    catch (Exception exception) { error = exception.InnerException ?? exception; }
                    finally { try { type.GetMethod("TearDown").Invoke(fixture, null); } catch (Exception exception) { error ??= exception; } }
                    var item = new XElement("test", new XAttribute("name", method.Name), new XAttribute("passed", error == null));
                    if (error == null) { passed++; } else { failed++; item.Add(new XElement("failure", error)); }
                    report.Add(item);
                }
            }
            catch (Exception error) { failed++; report.Add(new XElement("failure", error)); }
            report.SetAttributeValue("passed", passed); report.SetAttributeValue("failed", failed);
            Directory.CreateDirectory("Logs"); report.Save("Logs/CharacterFoundationChecks.xml");
            File.WriteAllText(Request, $"{passed} passed, {failed} failed");
        }
        private static bool Has(MemberInfo method, string name) => method.GetCustomAttributes(false).Any(a => a.GetType().Name == name);
    }
}
