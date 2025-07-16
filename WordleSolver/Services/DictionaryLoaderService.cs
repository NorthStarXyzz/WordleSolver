using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WordleSolver.ViewModels;

namespace WordleSolver.Services;

public class DictionaryLoaderService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private readonly ConcurrentDictionary<(string dictName, int length), Task<List<MainWindowViewModel.WordEntry>>> _cache
        = new();

    public async Task<(List<MainWindowViewModel.WordEntry> Words, HashSet<string> WordSet)> LoadMultipleDictionariesAsync(
        IEnumerable<string> dictionaries, int wordLength)
    {
        var tasks = new List<Task<List<MainWindowViewModel.WordEntry>>>();

        foreach (var dict in dictionaries)
        {
            tasks.Add(LoadSingleDictionaryCachedAsync(dict, wordLength));
        }

        var results = await Task.WhenAll(tasks);

        var finalWords = new List<MainWindowViewModel.WordEntry>();
        var wordSet = new HashSet<string>();

        foreach (var list in results)
        {
            foreach (var entry in list)
            {
                string wordUpper = entry.headWord.ToUpperInvariant();
                if (wordSet.Add(wordUpper))
                {
                    finalWords.Add(entry);
                }
            }
        }

        return (finalWords, wordSet);
    }
    
    private Task<List<MainWindowViewModel.WordEntry>> LoadSingleDictionaryCachedAsync(string dictName, int wordLength)
    {
        return _cache.GetOrAdd((dictName, wordLength), _ => LoadSingleDictionaryAsync(dictName, wordLength));
    }

    private async Task<List<MainWindowViewModel.WordEntry>> LoadSingleDictionaryAsync(string dictName, int wordLength)
    {
        var result = new List<MainWindowViewModel.WordEntry>();

        string dictFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Dictionary");
        string path = Path.Combine(dictFolder, $"{dictName}.json");

        if (!File.Exists(path))
            return result;

        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            using var reader = new StreamReader(fs);

            while (await reader.ReadLineAsync() is { } line)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<MainWindowViewModel.WordEntry>(line, _jsonOptions);
                    if (entry != null && entry.headWord.Length == wordLength)
                    {
                        result.Add(entry);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载词库失败: {dictName} - {ex.Message}");
        }

        return result;
    }
}
