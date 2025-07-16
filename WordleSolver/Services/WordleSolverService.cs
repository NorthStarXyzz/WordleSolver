using System;
using System.Collections.Generic;
using System.Linq;
using WordleSolver.ViewModels;

namespace WordleSolver.Services;

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
        {
            wordLetterCounts[word[i] - 'A']++;
        }

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
}
