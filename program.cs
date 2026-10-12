using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ExtragereCuvinte
{
    class Program
    {
        static void Main(string[] args)
        {
            string filename = "2504NEWS.txt";
            string currentDir = Directory.GetCurrentDirectory();
            string inputFilePath = Path.Combine(currentDir, filename);

            if (!File.Exists(inputFilePath))
            {
                inputFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            }

            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine("[EROARE] Nu s-a găsit fișierul " + filename + " în folderul: " + currentDir);
                return;
            }

            string rawContent = File.ReadAllText(inputFilePath, Encoding.UTF8);
            List<string> extractedWords = new List<string>();

            // 1. Extragere Titlu
            Match titleMatch = Regex.Match(rawContent, @"<title[^>]>(.?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                extractedWords.AddRange(ExtractWords(titleMatch.Groups[1].Value));
            }

            // 2. Extragere Text
            Match textMatch = Regex.Match(rawContent, @"<text[^>]>(.?)</text>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (textMatch.Success)
            {
                extractedWords.AddRange(ExtractWords(textMatch.Groups[1].Value));
            }

            // 3. Extragere Metadata 
            Match metadataMatch = Regex.Match(rawContent, @"<metadata[^>]>(.?)</metadata>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (metadataMatch.Success)
            {
                string metadataContent = metadataMatch.Groups[1].Value;
                MatchCollection codesMatches = Regex.Matches(metadataContent, @"<codes[^>]+class=[""'].?[""'][^>]>(.*?)</codes>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match codeBlock in codesMatches)
                {
                    MatchCollection codeAttrMatches = Regex.Matches(codeBlock.Value, @"code=[""'](.*?)[""']", RegexOptions.IgnoreCase);
                    foreach (Match attr in codeAttrMatches)
                    {
                        string val = attr.Groups[1].Value;
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            extractedWords.AddRange(ExtractWords(val));
                        }
                    }
                }
            }

            if (extractedWords.Count == 0)
            {
                extractedWords = ExtractWords(rawContent);
            }

            // 4. Aplicare PorterStemmer pe cuvintele curate (litere mici)
            PorterStemmer stemmer = new PorterStemmer();
            List<string> stemmedWords = new List<string>();

            foreach (string word in extractedWords)
            {
                string stemmed = stemmer.StemWord(word);
                if (!string.IsNullOrWhiteSpace(stemmed))
                {
                    stemmedWords.Add(stemmed);
                }
            }

            string outputFilePath = Path.Combine(currentDir, "cuvinte_extrase.txt");
            File.WriteAllLines(outputFilePath, stemmedWords.ToArray());

            Console.WriteLine("[GATA] S-au extras " + stemmedWords.Count + " cuvinte (fără cifre/punctuație, doar litere mici) în 'cuvinte_extrase.txt'!");
        }

        static List<string> ExtractWords(string input)
        {
            // Eliminare tag-uri XML/HTML
            string cleanText = Regex.Replace(input, "<.*?>", " ");

            // Expresie regulată care potrivește doar secvențe de LITERE [a-zA-Z]
            // Ignoră automat cifrele (1996, 55, 11 etc.) și semnele de punctuație
            MatchCollection matches = Regex.Matches(cleanText, @"[a-zA-Z]+");

            List<string> words = new List<string>();
            foreach (Match match in matches)
            {
                // Conversie la litere mici
                words.Add(match.Value.ToLower());
            }
            return words;
        }
    }

    class PorterStemmer
    {
        private char[] wordArray;
        private int endIndex;
        private int stemIndex;

        public string StemWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 2) return word;

            wordArray = word.ToCharArray();
            stemIndex = 0;
            endIndex = word.Length - 1;

            Step1();
            Step2();
            Step3();
            Step4();
            Step5();
            Step6();

            return new String(wordArray, 0, endIndex + 1);
        }

        private void Step1()
        {
            if (wordArray[endIndex] == 's')
            {
                if (EndsWith("sses")) endIndex -= 2;
                else if (EndsWith("ies")) SetEnd("i");
                else if (wordArray[endIndex - 1] != 's') endIndex--;
            }
            if (EndsWith("eed"))
            {
                if (MeasureConsontantSequence() > 0) endIndex--;
            }
            else if ((EndsWith("ed") || EndsWith("ing")) && VowelInStem())
            {
                endIndex = stemIndex;
                if (EndsWith("at")) SetEnd("ate");
                else if (EndsWith("bl")) SetEnd("ble");
                else if (EndsWith("iz")) SetEnd("ize");
                else if (IsDoubleConsontant(endIndex))
                {
                    endIndex--;
                    int ch = wordArray[endIndex];
                    if (ch == 'l' || ch == 's' || ch == 'z') endIndex++;
                }
                else if (MeasureConsontantSequence() == 1 && IsCVC(endIndex)) SetEnd("e");
            }
        }

        private void Step2()
        {
            if (EndsWith("y") && VowelInStem()) wordArray[endIndex] = 'i';
        }

        private void Step3()
        {
            if (endIndex == 0) return;
            switch (wordArray[endIndex - 1])
            {
                case 'a':
                    if (EndsWith("ational")) ReplaceEnd("ate");
                    else if (EndsWith("tional")) ReplaceEnd("tion");
                    break;
                case 'c':
                    if (EndsWith("enci")) ReplaceEnd("ence");
                    else if (EndsWith("anci")) ReplaceEnd("ance");
                    break;
                case 'e':
                    if (EndsWith("izer")) ReplaceEnd("ize");
                    break;
                case 'l':
                    if (EndsWith("bli")) ReplaceEnd("ble");
                    else if (EndsWith("alli")) ReplaceEnd("al");
                    else if (EndsWith("entli")) ReplaceEnd("ent");
                    else if (EndsWith("eli")) ReplaceEnd("e");
                    else if (EndsWith("ousli")) ReplaceEnd("ous");
                    break;
                case 'o':
                    if (EndsWith("ization")) ReplaceEnd("ize");
                    else if (EndsWith("ation")) ReplaceEnd("ate");
                    else if (EndsWith("ator")) ReplaceEnd("ate");
                    break;
                case 's':
                    if (EndsWith("alism")) ReplaceEnd("al");
                    else if (EndsWith("iveness")) ReplaceEnd("ive");
                    else if (EndsWith("fulness")) ReplaceEnd("ful");
                    else if (EndsWith("ousness")) ReplaceEnd("ous");
                    break;
                case 't':
                    if (EndsWith("aliti")) ReplaceEnd("al");
                    else if (EndsWith("iviti")) ReplaceEnd("ive");
                    else if (EndsWith("biliti")) ReplaceEnd("ble");
                    break;
                case 'g':
                    if (EndsWith("logi")) ReplaceEnd("log");
                    break;
            }
        }

        private void Step4()
        {
            switch (wordArray[endIndex])
            {
                case 'e':
                    if (EndsWith("icate")) ReplaceEnd("ic");
                    else if (EndsWith("ative")) ReplaceEnd("");
                    else if (EndsWith("alize")) ReplaceEnd("al");
                    break;
                case 'i':
                    if (EndsWith("iciti")) ReplaceEnd("ic");
                    break;
                case 'l':
                    if (EndsWith("ical")) ReplaceEnd("ic");
                    else if (EndsWith("ful")) ReplaceEnd("");
                    break;
                case 's':
                    if (EndsWith("ness")) ReplaceEnd("");
                    break;
            }
        }

        private void Step5()
        {
            if (endIndex == 0) return;
            switch (wordArray[endIndex - 1])
            {
                case 'a': if (EndsWith("al")) break; return;
                case 'c': if (EndsWith("ance") || EndsWith("ence")) break; return;
                case 'e': if (EndsWith("er")) break; return;
                case 'i': if (EndsWith("ic")) break; return;
                case 'l': if (EndsWith("able") || EndsWith("ible")) break; return;
                case 'n': if (EndsWith("ant") || EndsWith("ement") || EndsWith("ment") || EndsWith("ent")) break; return;
                case 'o': if (EndsWith("ion") && stemIndex >= 0 && (wordArray[stemIndex] == 's' || wordArray[stemIndex] == 't')) break; if (EndsWith("ou")) break; return;
                case 's': if (EndsWith("ism")) break; return;
                case 't': if (EndsWith("ate") || EndsWith("iti")) break; return;
                case 'u': if (EndsWith("ous")) break; return;
                case 'v': if (EndsWith("ive")) break; return;
                case 'z': if (EndsWith("ize")) break; return;
                default: return;
            }
            if (MeasureConsontantSequence() > 1) endIndex = stemIndex;
        }

        private void Step6()
        {
            stemIndex = endIndex;
            if (wordArray[endIndex] == 'e')
            {
                var a = MeasureConsontantSequence();
                if (a > 1 || (a == 1 && !IsCVC(endIndex - 1))) endIndex--;
            }
            if (wordArray[endIndex] == 'l' && IsDoubleConsontant(endIndex) && MeasureConsontantSequence() > 1) endIndex--;
        }

        private bool IsConsonant(int index)
        {
            var c = wordArray[index];
            if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u') return false;
            return c != 'y' || (index == 0 || !IsConsonant(index - 1));
        }

        private int MeasureConsontantSequence()
        {
            var n = 0;
            var index = 0;
            while (true)
            {
                if (index > stemIndex) return n;
                if (!IsConsonant(index)) break;
                index++;
            }
            index++;
            while (true)
            {
                while (true)
                {
                    if (index > stemIndex) return n;
                    if (IsConsonant(index)) break;
                    index++;
                }
                index++;
                n++;
                while (true)
                {
                    if (index > stemIndex) return n;
                    if (!IsConsonant(index)) break;
                    index++;
                }
                index++;
            }
        }

        private bool VowelInStem()
        {
            for (int i = 0; i <= stemIndex; i++) if (!IsConsonant(i)) return true;
            return false;
        }

        private bool IsDoubleConsontant(int index)
        {
            if (index < 1) return false;
            return wordArray[index] == wordArray[index - 1] && IsConsonant(index);
        }

        private bool IsCVC(int index)
        {
            if (index < 2 || !IsConsonant(index) || IsConsonant(index - 1) || !IsConsonant(index - 2)) return false;
            var c = wordArray[index];
            return c != 'w' && c != 'x' && c != 'y';
        }

        private bool EndsWith(string s)
        {
            var length = s.Length;
            var index = endIndex - length + 1;
            if (index < 0) return false;
            for (var i = 0; i < length; i++) if (wordArray[index + i] != s[i]) return false;
            stemIndex = endIndex - length;
            return true;
        }

        private void SetEnd(string s)
        {
            var length = s.Length;
            var index = stemIndex + 1;
            for (var i = 0; i < length; i++) wordArray[index + i] = s[i];
            endIndex = stemIndex + length;
        }

        private void ReplaceEnd(string s)
        {
            if (MeasureConsontantSequence() > 0) SetEnd(s);
        }
    }
}