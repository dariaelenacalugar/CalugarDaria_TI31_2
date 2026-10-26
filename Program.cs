using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Lab3
{
    public class Article
    {
        public string ArticleId { get; set; }
        public string SourceFolder { get; set; }
        public List<string> ProcessedTerms { get; set; } = new List<string>();
        public List<string> Topics { get; set; } = new List<string>();
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Identificăm folderul proiectului Lab3
                string currentDir = Directory.GetCurrentDirectory();
                DirectoryInfo dir = new DirectoryInfo(currentDir);

                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Lab3.csproj")))
                {
                    if (dir.Parent == null) break;
                    dir = dir.Parent;
                }

                string projectFolder = dir?.FullName ?? currentDir;
                Console.WriteLine($"[1] Folder proiect: {projectFolder}");

                // Căutare fișier stopwords (cu sau fără extensie .txt)
                string stopwordsPath = Path.Combine(projectFolder, "stopwords.txt");
                if (!File.Exists(stopwordsPath)) stopwordsPath = Path.Combine(projectFolder, "stopwords");
                if (!File.Exists(stopwordsPath)) stopwordsPath = Path.Combine(currentDir, "stopwords.txt");
                if (!File.Exists(stopwordsPath)) stopwordsPath = Path.Combine(currentDir, "stopwords");

                if (!File.Exists(stopwordsPath))
                {
                    Console.WriteLine($"[EROARE] Nu am găsit fișierul 'stopwords' în {projectFolder}!");
                    PauseConsole();
                    return;
                }
                Console.WriteLine($"[2] Stopwords găsit la: {stopwordsPath}");

                HashSet<string> stopwords = LoadStopwords(stopwordsPath);

                // Căutare folder ReutersDataSet
                string datasetPath = Path.Combine(projectFolder, "ReutersDataSet");
                if (!Directory.Exists(datasetPath)) datasetPath = Path.Combine(currentDir, "ReutersDataSet");

                if (!Directory.Exists(datasetPath))
                {
                    Console.WriteLine($"[EROARE] Nu am găsit folderul 'ReutersDataSet' la: {datasetPath}");
                    PauseConsole();
                    return;
                }
                Console.WriteLine($"[3] ReutersDataSet găsit la: {datasetPath}");

                // Extragere fișiere XML
                Console.WriteLine("[4] Se caută fișierele XML...");
                string[] xmlFiles = Directory.GetFiles(datasetPath, "*.xml", SearchOption.AllDirectories);
                Console.WriteLine($"[5] S-au găsit {xmlFiles.Length} fișiere XML.");

                if (xmlFiles.Length == 0)
                {
                    Console.WriteLine("[EROARE] Nu există fișiere .xml în ReutersDataSet!");
                    PauseConsole();
                    return;
                }

                // Procesare articole
                List<Article> articles = ProcessXmlFiles(xmlFiles, stopwords);
                Console.WriteLine($"[6] S-au procesat {articles.Count} articole.");

                // Dicționar global
                Console.WriteLine("[7] Se generează dicționarul global...");
                List<string> globalDictionary = articles
                    .SelectMany(a => a.ProcessedTerms)
                    .Distinct()
                    .OrderBy(w => w)
                    .ToList();

                Console.WriteLine($"[8] Dimensiune dicționar: {globalDictionary.Count} cuvinte.");

                Dictionary<string, int> termToIndex = globalDictionary
                    .Select((word, index) => new { word, index })
                    .ToDictionary(x => x.word, x => x.index);

                // Salvare rezultat în folderul Lab3
                string outputPath = Path.Combine(projectFolder, "output_vectors.txt");
                Console.WriteLine("[9] Se scrie fișierul output_vectors.txt...");
                SaveOutputFile(outputPath, globalDictionary, articles, termToIndex);

                Console.WriteLine("\n==========================================");
                Console.WriteLine($"SUCCES! Fișierul a fost creat la:\n{outputPath}");
                Console.WriteLine("==========================================");

                // Deschide automat folderul și selectează fișierul generat
                Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[EROARE FATALĂ] {ex.Message}");
            }

            PauseConsole();
        }

        static void PauseConsole()
        {
            Console.WriteLine("\nApasă orice tastă pentru a închide consola...");
            Console.ReadKey();
        }

        static HashSet<string> LoadStopwords(string path)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(path))
            {
                string word = line.Trim().ToLower();
                if (!string.IsNullOrEmpty(word))
                    set.Add(word);
            }
            return set;
        }

        static List<Article> ProcessXmlFiles(string[] files, HashSet<string> stopwords)
        {
            List<Article> result = new List<Article>();

            foreach (var file in files)
            {
                try
                {
                    XDocument doc = XDocument.Load(file);
                    XElement root = doc.Root;
                    if (root == null) continue;

                    string itemId = root.Attribute("itemid")?.Value ?? Path.GetFileNameWithoutExtension(file);

                    string title = root.Element("title")?.Value ?? "";
                    string text = string.Join(" ", root.Element("text")?.Elements("p").Select(p => p.Value) ?? new List<string>());

                    List<string> terms = TokenizeAndProcess(title + " " + text, stopwords);

                    List<string> topics = new List<string>();
                    var topicNode = root.Element("metadata")
                        ?.Elements("codes")
                        .FirstOrDefault(c => (string)c.Attribute("class") == "bip:topics:1.0");

                    if (topicNode != null)
                    {
                        topics = topicNode.Elements("code")
                            .Select(c => (string)c.Attribute("code"))
                            .Where(c => !string.IsNullOrEmpty(c))
                            .ToList();
                    }

                    string folderLabel = file.ToLower().Contains("testing") ? "Testing" : "Training";

                    result.Add(new Article
                    {
                        ArticleId = itemId,
                        SourceFolder = folderLabel,
                        ProcessedTerms = terms,
                        Topics = topics
                    });
                }
                catch
                {
                    // Ignorăm fișierele corupte
                }
            }

            return result;
        }

        static List<string> TokenizeAndProcess(string text, HashSet<string> stopwords)
        {
            List<string> terms = new List<string>();
            string clean = Regex.Replace(text.ToLower(), @"[^a-z\s]", " ");
            string[] words = clean.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            PorterStemmer stemmer = new PorterStemmer();

            foreach (var w in words)
            {
                if (w.Length <= 1 || stopwords.Contains(w)) continue;

                string stemmed = stemmer.StemWord(w);
                if (!string.IsNullOrEmpty(stemmed))
                {
                    terms.Add(stemmed);
                }
            }

            return terms;
        }

        static void SaveOutputFile(string outputPath, List<string> globalDict, List<Article> articles, Dictionary<string, int> termToIndex)
        {
            using (StreamWriter writer = new StreamWriter(outputPath))
            {
                foreach (var term in globalDict)
                {
                    writer.WriteLine($"@attribute {term}");
                }

                writer.WriteLine("@data");

                foreach (var art in articles)
                {
                    var counts = art.ProcessedTerms
                        .GroupBy(t => t)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var sparseVector = counts
                        .Where(kv => termToIndex.ContainsKey(kv.Key))
                        .Select(kv => new { Index = termToIndex[kv.Key], Count = kv.Value })
                        .OrderBy(x => x.Index)
                        .Select(x => $"{x.Index}:{x.Count}");

                    string vectorStr = string.Join(" ", sparseVector);
                    string topicsStr = string.Join(" ", art.Topics);

                    writer.WriteLine($"{art.ArticleId} # {vectorStr} # {topicsStr} # {art.SourceFolder}");
                }
            }
        }
    }

    public class PorterStemmer
    {
        public string StemWord(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length <= 2) return word;

            char[] stem = word.ToCharArray();
            int k = stem.Length - 1;
            int k0 = 0;

            if (Ends(stem, k, "sses")) k -= 2;
            else if (Ends(stem, k, "ies")) SetEnd(stem, ref k, "i");
            else if (Ends(stem, k, "ss")) { }
            else if (Ends(stem, k, "s")) k--;

            bool extra = false;
            if (Ends(stem, k, "eed"))
            {
                if (M(stem, k0, k - 3) > 0) k--;
            }
            else if ((Ends(stem, k, "ed") && VowelInStem(stem, k0, k - 2)) ||
                     (Ends(stem, k, "ing") && VowelInStem(stem, k0, k - 3)))
            {
                k = Ends(stem, k, "ed") ? k - 2 : k - 3;
                extra = true;
            }

            if (extra)
            {
                if (Ends(stem, k, "at") || Ends(stem, k, "bl") || Ends(stem, k, "iz"))
                {
                    SetEnd(stem, ref k, "e");
                }
                else if (DoubleConsonant(stem, k))
                {
                    k--;
                    char ch = stem[k];
                    if (ch == 'l' || ch == 's' || ch == 'z') k++;
                }
                else if (M(stem, k0, k) == 1 && Cvc(stem, k))
                {
                    SetEnd(stem, ref k, "e");
                }
            }

            if (Ends(stem, k, "y") && VowelInStem(stem, k0, k - 1))
            {
                stem[k] = 'i';
            }

            return new string(stem, 0, k + 1);
        }

        private bool Consonant(char[] b, int i)
        {
            switch (b[i])
            {
                case 'a': case 'e': case 'i': case 'o': case 'u': return false;
                case 'y': return (i == 0) ? true : !Consonant(b, i - 1);
                default: return true;
            }
        }

        private int M(char[] b, int k0, int k)
        {
            int n = 0, i = k0;
            while (true) { if (i > k) return n; if (!Consonant(b, i)) break; i++; }
            i++;
            while (true)
            {
                while (true) { if (i > k) return n; if (Consonant(b, i)) break; i++; }
                i++; n++;
                while (true) { if (i > k) return n; if (!Consonant(b, i)) break; i++; }
                i++;
            }
        }

        private bool VowelInStem(char[] b, int k0, int k)
        {
            for (int i = k0; i <= k; i++) if (!Consonant(b, i)) return true;
            return false;
        }

        private bool DoubleConsonant(char[] b, int i)
        {
            if (i < 1 || b[i] != b[i - 1]) return false;
            return Consonant(b, i);
        }

        private bool Cvc(char[] b, int i)
        {
            if (i < 2 || !Consonant(b, i) || Consonant(b, i - 1) || !Consonant(b, i - 2)) return false;
            int ch = b[i];
            if (ch == 'w' || ch == 'x' || ch == 'y') return false;
            return true;
        }

        private bool Ends(char[] b, int k, string s)
        {
            int length = s.Length;
            if (s[length - 1] != b[k] || k < length - 1) return false;
            for (int i = 0; i < length; i++)
                if (b[k - length + 1 + i] != s[i]) return false;
            return true;
        }

        private void SetEnd(char[] b, ref int k, string s)
        {
            int length = s.Length;
            int del = k;
            for (int i = 0; i < length; i++) b[k - length + 1 + i] = s[i];
            k = k - del + length - 1;
        }
    }
}
