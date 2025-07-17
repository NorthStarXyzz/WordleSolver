using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WordleSolver.ViewModels;

namespace WordleSolver.Services
{
    public class WordleSolverService
    {
        public List<MainWindowViewModel.WordEntry> FilterWordsByColors(
            List<MainWindowViewModel.WordEntry> words,
            string guessWord,
            int[] colors)
        {
            guessWord = guessWord.ToUpperInvariant();
            return words
                .Where(w => IsWordMatch(w.headWord, guessWord, colors))
                .ToList();
        }

        private bool IsWordMatch(string rawWord, string guess, int[] colors)
        {
            string word = rawWord.ToUpperInvariant();
            int length = guess.Length;

            Span<bool> used = stackalloc bool[8];
            Span<int> wordLetterCounts = stackalloc int[26];

            for (int i = 0; i < length; i++)
                wordLetterCounts[word[i] - 'A']++;

            for (int i = 0; i < length; i++)
            {
                if (colors[i] == 2)
                {
                    if (word[i] != guess[i])
                        return false;
                    used[i] = true;
                    wordLetterCounts[guess[i] - 'A']--;
                }
            }

            for (int i = 0; i < length; i++)
            {
                if (colors[i] == 1)
                {
                    bool matched = false;
                    char ch = guess[i];
                    for (int j = 0; j < length; j++)
                    {
                        if (!used[j] && word[j] == ch && guess[j] != ch)
                        {
                            used[j] = true;
                            matched = true;
                            wordLetterCounts[ch - 'A']--;
                            break;
                        }
                    }
                    if (!matched) return false;
                }
            }

            for (int i = 0; i < length; i++)
            {
                if (colors[i] == 0)
                {
                    char ch = guess[i];
                    if (wordLetterCounts[ch - 'A'] > 0)
                        return false;
                }
            }

            return true;
        }

        public Dictionary<int, Dictionary<char, double>> CalculateLetterFrequencies(List<MainWindowViewModel.WordEntry> words, int wordLength)
        {
            var positionCounts = new Dictionary<int, Dictionary<char, int>>();
            var positionTotals = new Dictionary<int, int>();

            for (int i = 0; i < wordLength; i++)
            {
                positionCounts[i] = new Dictionary<char, int>();
                positionTotals[i] = 0;
            }

            foreach (var wordEntry in words)
            {
                string w = wordEntry.headWord.ToUpperInvariant();
                for (int i = 0; i < wordLength; i++)
                {
                    char c = w[i];
                    if (!positionCounts[i].ContainsKey(c))
                        positionCounts[i][c] = 0;
                    positionCounts[i][c]++;
                    positionTotals[i]++;
                }
            }

            var positionFreqs = new Dictionary<int, Dictionary<char, double>>();

            for (int i = 0; i < wordLength; i++)
            {
                positionFreqs[i] = new Dictionary<char, double>();
                int total = positionTotals[i];
                foreach (var kv in positionCounts[i])
                {
                    positionFreqs[i][kv.Key] = (double)kv.Value / total;
                }
            }

            Debug.WriteLine("[频率统计]");
            foreach (var pos in positionFreqs)
            {
                Debug.Write($"Pos {pos.Key}: ");
                foreach (var pair in pos.Value.OrderByDescending(p => p.Value).Take(5))
                {
                    Debug.Write($"{pair.Key}:{pair.Value:F2} ");
                }
                Debug.WriteLine("");
            }

            return positionFreqs;
        }
        
        public double CalculateWordScore(string word, Dictionary<int, Dictionary<char, double>> positionFreqs)
        {
            double score = 0.0;
            word = word.ToUpperInvariant();
            int length = word.Length;

            for (int i = 0; i < length; i++)
            {
                char c = word[i];
                double freq = positionFreqs.TryGetValue(i, out var dict) && dict.TryGetValue(c, out var f) ? f : 0.0;
                score += freq;
            }

            Debug.WriteLine($"[{word}] Frequency Score = {score:F2}");
            return score;
        }
        
        public double CalculateWordScore(string word, int[] colors, Dictionary<int, Dictionary<char, double>> positionFreqs)
        {
            double score = 0.0;
            word = word.ToUpperInvariant();
            int length = word.Length;
            
            var yellowLetters = new Dictionary<char, List<int>>();
            for (int i = 0; i < length; i++)
            {
                if (colors[i] == 1)
                {
                    char ch = word[i];
                    if (!yellowLetters.ContainsKey(ch))
                        yellowLetters[ch] = new List<int>();
                    yellowLetters[ch].Add(i);
                }
            }

            for (int i = 0; i < length; i++)
            {
                char c = word[i];
                double freq = positionFreqs.TryGetValue(i, out var dict) && dict.TryGetValue(c, out var f) ? f : 0.0;

                double colorWeight = colors[i] switch
                {
                    2 => 1.0,   
                    1 => 0.6,    
                    0 => 0.3,    
                    _ => 0.0
                };

                score += freq * colorWeight;
            }
            
            foreach (var kv in yellowLetters)
            {
                char ch = kv.Key;
                var yellowPositions = kv.Value;

                for (int pos = 0; pos < length; pos++)
                {
                    if (colors[pos] == 0) continue;
                    if (colors[pos] == 2 && word[pos] == ch) continue;
                    if (yellowPositions.Contains(pos)) continue;

                    double freq = positionFreqs.TryGetValue(pos, out var dict) && dict.TryGetValue(ch, out var f) ? f : 0.0;
                    score += freq * 0.4;
                }
            }

            Debug.WriteLine($"[{word}] Total Score = {score:F2}");
            return score;
        }
        
        public List<MainWindowViewModel.WordEntry> GetNextGuessCandidates(
            List<MainWindowViewModel.WordEntry> currentWords,
            string? lastGuess,
            int[]? lastColors,
            bool isFirstRound = false)
        {
            List<MainWindowViewModel.WordEntry> filteredWords;
            
            if (isFirstRound)
            {
                filteredWords = currentWords;
            }
            else
            {
                if (lastGuess == null || lastColors == null)
                    throw new ArgumentNullException("lastGuess 和 lastColors 不能为空，除非是第一轮");

                filteredWords = FilterWordsByColors(currentWords, lastGuess, lastColors);
            }

            if (filteredWords.Count == 0)
                return filteredWords;

            int wordLength = filteredWords[0].headWord.Length;
            var positionFreqs = CalculateLetterFrequencies(filteredWords, wordLength);

            var scoredWords = filteredWords.Select(w =>
                new
                {
                    Word = w,
                    Score = isFirstRound
                        ? CalculateWordScore(w.headWord, positionFreqs)
                        : CalculateWordScore(w.headWord, lastColors!, positionFreqs)
                });

            var sorted = scoredWords.OrderByDescending(t => t.Score).ToList();

            Debug.WriteLine("[候选单词评分排名] 前10：");
            foreach (var entry in sorted.Take(10))
            {
                Debug.WriteLine($"{entry.Word.headWord} -> {entry.Score:F2}");
            }

            return sorted.Select(t => t.Word).ToList();
        }
    }
}
