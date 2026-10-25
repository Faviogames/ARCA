using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

#pragma warning disable CS1591
namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class TranslationTests
    {
        [TearDown]
        public void BackToSpanish() { Tr.Override = Constants.Windows.Locale.Name.Spanish; }

        [Test]
        public void EveryTextInTheSourceHasAnEnglishTranslation()
        {
            var source = SourceFolder();
            Assume.That(source != null, "Source folder not found next to the build output.");

            var missing = TextsInSource(source).Where(text => !Tr.English.ContainsKey(text)).ToList();
            CollectionAssert.IsEmpty(missing, "Texts without English: " + string.Join(" | ", missing));
        }

        [Test]
        public void EnglishKeepsEveryPlaceholder()
        {
            foreach (var entry in Tr.English)
                CollectionAssert.AreEquivalent(Placeholders(entry.Key), Placeholders(entry.Value), entry.Key);
        }

        [Test]
        public void CategoryAndStateNamesHaveEnglish()
        {
            Tr.Override = Constants.Windows.Locale.Name.Spanish;
            var names = Enum.GetValues(typeof(CleanCategory)).Cast<CleanCategory>().Where(c => c != CleanCategory.None).Select(CleanText.Name)
                .Concat(Enum.GetValues(typeof(CleanTaskState)).Cast<CleanTaskState>().Where(s => s != CleanTaskState.Idle).Select(OneClickCleanService.StateText));
            foreach (var name in names) Assert.IsTrue(Tr.English.ContainsKey(name), name);
        }

        [Test]
        public void TextsAndNumbersFollowTheLanguage()
        {
            var size = 1288490189L; // 1.2 GB

            Tr.Override = Constants.Windows.Locale.Name.English;
            Assert.AreEqual("1.2 GB", CleanText.Size(size));
            Assert.AreEqual("3 skipped", Tr.Format("{0} omitidos", 3));
            Assert.AreEqual("Extreme cleaning", CleanText.Name(CleanCategory.Extreme));
            Assert.AreEqual("Not in the dictionary", Tr.Get("Not in the dictionary"));

            Tr.Override = Constants.Windows.Locale.Name.Spanish;
            Assert.AreEqual("1,2 GB", CleanText.Size(size));
            Assert.AreEqual("3 omitidos", Tr.Format("{0} omitidos", 3));
        }

        [Test]
        public void OnlySpanishAndEnglishAreOffered()
        {
            CollectionAssert.AreEquivalent(new[] { Constants.Windows.Locale.Name.English, Constants.Windows.Locale.Name.Spanish }, Localizer.Languages.Select(l => l.Name));
        }

        private static string SourceFolder()
        {
            for (var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "WinMemoryCleaner.csproj"))) return dir.FullName;
            return null;
        }

        // Same rules as the code: {local:Tr '...'} in XAML, and every literal in the first argument of Tr.Get/Tr.Format.
        private static IEnumerable<string> TextsInSource(string source)
        {
            var texts = new HashSet<string>();
            foreach (var file in Directory.GetFiles(source, "*.xaml", SearchOption.AllDirectories).Where(IsAppFile))
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\{local:Tr '([^']*)'\}")) texts.Add(m.Groups[1].Value);
            foreach (var file in Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories).Where(IsAppFile))
            {
                var code = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(code, @"Tr\.(Get|Format)\("))
                    foreach (Match literal in Regex.Matches(FirstArgument(code, m.Index + m.Length), @"""((?:[^""\\]|\\.)*)"""))
                        texts.Add(literal.Groups[1].Value.Replace("\\\"", "\""));
            }
            return texts;
        }

        private static bool IsAppFile(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar);
            return !parts.Any(p => p == "Test" || p == "packages" || p == "obj" || p == "bin") && Path.GetFileName(path) != "Tr.cs";
        }

        private static string FirstArgument(string code, int start)
        {
            int depth = 0;
            var inString = false;
            for (var i = start; i < code.Length; i++)
            {
                var c = code[i];
                if (inString) { if (c == '\\') i++; else if (c == '"') inString = false; }
                else if (c == '"') inString = true;
                else if (c == '(') depth++;
                else if (c == ')') { if (depth == 0) return code.Substring(start, i - start); depth--; }
                else if (c == ',' && depth == 0) return code.Substring(start, i - start);
            }
            return code.Substring(start);
        }

        private static IList<string> Placeholders(string text)
        {
            return Regex.Matches(text, @"\{\d+\}").Cast<Match>().Select(m => m.Value).ToList();
        }
    }
}
#pragma warning restore CS1591
