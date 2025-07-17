using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WordleSolver.Services;

namespace WordleSolver.ViewModels;

public class GuessResult(string word, int[] colors)
{
    public string Word { get; set; } = word;
    public int[] Colors { get; set; } = colors;
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;

    private readonly Random _random = new();
    private List<WordEntry> _words = new();
    private List<WordEntry> _originalWords = new();
    private readonly List<GuessResult> _guessHistory = new();

    [ObservableProperty]
    private int _currentRound;

    [ObservableProperty]
    private int _wordLength = 5;
    public int[] AvailableWordLengths { get; } = [3, 4, 5, 6, 7, 8];

    [ObservableProperty]
    private string _selectedDictionary = "CET4";
    public string[] AvailableDictionaries { get; } =
    [
        "CET4", "CET6", "IELTS", "TOEFL", "SAT",
        "GMAT", "Level4", "Level8", "KaoYan", "All"
    ];

    private readonly DictionaryLoaderService _dictionaryLoader;
    private readonly WordleSolverService _solverService;
    private HashSet<string> _wordSet = new();

    [ObservableProperty]
    private string _solveButtonText = "Start Solving";

    [ObservableProperty]
    private string _inputWord;

    public ObservableCollection<CellViewModel> Cells { get; } = new();

    [ObservableProperty]
    private bool _isGameStarted;

    [ObservableProperty]
    private bool _canCycleColors;
    
    private int[]? _suspendedColors = null;
    private string? _suspendedWord = null;
    private bool _wasExplorationLastRound = false;
    
    public MainWindowViewModel(IDialogService dialogService, DictionaryLoaderService dictionaryLoader, WordleSolverService solverService)
    {
        _dialogService = dialogService;
        _dictionaryLoader = dictionaryLoader;
        _solverService = solverService;

        Task.Run(async () =>
        {
            await LoadDictionaryAsync();
        }).ContinueWith(t =>
        {
            UpdateCells();
            if (t.Exception != null)
                Debug.WriteLine(t.Exception);
        }, TaskScheduler.FromCurrentSynchronizationContext());

        PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(WordLength) || e.PropertyName == nameof(SelectedDictionary))
            {
                await LoadDictionaryAsync();
                UpdateCells();
            }
            else if (e.PropertyName == nameof(InputWord))
            {
                UpdateLetters();
                ConfirmCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(IsGameStarted))
            {
                CanCycleColors = IsGameStarted;
            }
        };
    }

    public class WordEntry
    {
        public int wordRank { get; set; }
        public string headWord { get; set; } = string.Empty;
    }

    private async Task LoadDictionaryAsync()
    {
        List<string> dictsToLoad;

        if (SelectedDictionary == "All")
        {
            dictsToLoad = AvailableDictionaries.Where(d => d != "All").ToList();
            Debug.WriteLine($"[词库] 当前选择：All，实际加载词库：{string.Join(", ", dictsToLoad)}");
        }
        else
        {
            dictsToLoad = new[] { SelectedDictionary }.ToList();
            Debug.WriteLine($"[词库] 当前选择：{SelectedDictionary}");
        }

        using var loading = await _dialogService.ShowLoadingDialog("Loading dictionary...");

        await Task.Yield();

        var result = await Task.Run(async () => await _dictionaryLoader.LoadMultipleDictionariesAsync(dictsToLoad, WordLength));

        _words = result.Words;
        _originalWords = new List<WordEntry>(_words);
        _wordSet = result.WordSet;

        Debug.WriteLine($"加载后词库大小: {_words.Count}");
    }
    
    private bool HasRepeatedLetters(string word)
    {
        return word.GroupBy(c => c).Any(g => g.Count() > 1);
    }
    
    private async Task SolveAsync()
    {
        Debug.WriteLine($"SolveAsync 被调用, Word Count: {_words.Count}, Round: {CurrentRound}");

        if (_words == null || _words.Count == 0)
        {
            Debug.WriteLine("词库为空，无法继续选词");
            return;
        }

        WordEntry word;

        if (CurrentRound == 1 || _guessHistory.Count == 0)
        {
            int wordLength = _words[0].headWord.Length;
            var positionFreqs = _solverService.CalculateLetterFrequencies(_words, wordLength);

            var scoredWords = _words.Select(w =>
            {
                double score = 0;
                string wordStr = w.headWord.ToUpperInvariant();

                for (int i = 0; i < wordStr.Length; i++)
                {
                    double freq = positionFreqs[i].GetValueOrDefault(wordStr[i], 0.0);
                    score += freq;
                }
                
                if (HasRepeatedLetters(wordStr))
                {
                    score -= 5.0;
                }

                return new { Word = w, Score = score };
            });
            
            word = scoredWords.OrderByDescending(t => t.Score).First().Word;
            Debug.WriteLine($"[回合 {CurrentRound}] 第一轮仅用频率评分选词: {word.headWord}");
        }
        else
        {
            var lastGuess = _guessHistory.Last().Word;
            var lastColors = _guessHistory.Last().Colors;

            _words = _solverService.FilterWordsByColors(_words, lastGuess, lastColors);
            Debug.WriteLine($"过滤后的词库大小: {_words.Count}");

            if (_words.Count == 0)
            {
                Debug.WriteLine("过滤后词库为空，无法继续选词");
                return;
            }

            int roundsLeft = 6 - CurrentRound + 1;
            bool candidatesFitInRounds = _words.Count <= roundsLeft;
            int grayCount = lastColors.Count(c => c == 0);
            bool shouldExplore = !candidatesFitInRounds && (grayCount == 1 || grayCount == 2) && CurrentRound < 6;
            
            if (shouldExplore && !_wasExplorationLastRound)
            {
                _suspendedColors = lastColors.ToArray();
                _suspendedWord = lastGuess;
                _wasExplorationLastRound = true;
                
                var excludedLetters = new HashSet<char>();
                foreach (var guess in _guessHistory)
                {
                    foreach (var c in guess.Word)
                    {
                        excludedLetters.Add(char.ToUpperInvariant(c));
                    }
                }

                var newLetterWords = _originalWords
                    .Where(w =>
                        w.headWord.ToUpperInvariant().All(c => !excludedLetters.Contains(c)) &&
                        !HasRepeatedLetters(w.headWord.ToUpperInvariant()))
                    .ToList();

                if (newLetterWords.Count > 0)
                {
                    word = newLetterWords[_random.Next(newLetterWords.Count)];
                    Debug.WriteLine($"[回合 {CurrentRound}] 触发探索策略，选词: {word.headWord}");
                    
                    foreach (var cell in Cells)
                    {
                        if (cell.ColorIndex != 2)
                            cell.ColorIndex = 0;
                    }
                    
                    InputWord = word.headWord.ToUpper();
                    UpdateLetters();
                    UpdateCells();
                    OnPropertyChanged(nameof(InputWord));
                    await Task.Delay(10);

                    return;
                }
                else
                {
                    Debug.WriteLine($"[回合 {CurrentRound}] 无新字母词，回退评分策略");
                    _wasExplorationLastRound = false;
                    _suspendedColors = null;
                    _suspendedWord = null;
                }
            }
            
            if (_wasExplorationLastRound && _suspendedColors != null && _suspendedWord != null)
            {
                Debug.WriteLine($"[回合 {CurrentRound}] 恢复探索策略前状态继续评分策略：{_suspendedWord}");
                _words = _solverService.FilterWordsByColors(_words, _suspendedWord, _suspendedColors);
                lastGuess = _suspendedWord;
                lastColors = _suspendedColors;

                var colorsToRestore = _suspendedColors;
                _suspendedColors = null;
                _suspendedWord = null;
                _wasExplorationLastRound = false;
                
                if (colorsToRestore != null)
                {
                    for (int i = 0; i < Cells.Count && i < colorsToRestore.Length; i++)
                    {
                        Cells[i].ColorIndex = colorsToRestore[i];
                    }
                }
            }

            var candidates = _solverService.GetNextGuessCandidates(_words, lastGuess, lastColors);
            if (candidates.Count > 0)
            {
                word = candidates[0];
                Debug.WriteLine($"[回合 {CurrentRound}] 正常评分选词: {word.headWord}");
            }
            else
            {
                if (_words.Count == 0)
                {
                    Debug.WriteLine("词库为空，无法随机选词");
                    return;
                }
                word = _words[_random.Next(_words.Count)];
                Debug.WriteLine($"[回合 {CurrentRound}] 无候选词，随机选词: {word.headWord}");
            }
        }
        
        if (!string.IsNullOrEmpty(word.headWord))
        {
            InputWord = word.headWord.ToUpper();
            UpdateLetters();
            OnPropertyChanged(nameof(InputWord));
            await Task.Delay(10);
        }
    }

    [RelayCommand]
    private async Task StartSolving()
    {
        if (!IsGameStarted)
        {
            IsGameStarted = true;
            CanCycleColors = true;
            CurrentRound = 1;
            SolveButtonText = "Solve";
            await SolveAsync();
        }
        else
        {
            await SolveAsync();
        }
    }

    private bool CanConfirm() =>
        IsGameStarted &&
        CurrentRound <= 6 &&
        !string.IsNullOrEmpty(InputWord) &&
        InputWord.Length == WordLength;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task Confirm()
    {
        bool confirmed = await _dialogService.ShowConfirmDialog("Confirm Colors", "Have you finished marking the colors for this round?");
        if (!confirmed)
            return;

        var currentColors = Cells.Select(c => c.ColorIndex).ToArray();
        _guessHistory.Add(new GuessResult(InputWord, currentColors));
        
        _words = _solverService.FilterWordsByColors(_words, InputWord, currentColors);
        _wordSet = new HashSet<string>(_words.Select(w => w.headWord.ToUpper()));

        Debug.WriteLine($"过滤后的词库大小: {_words.Count}");

        if (_words.Count == 1)
        {
            var only = _words[0].headWord.ToUpper();
            Debug.WriteLine($"[胜利] 猜中唯一词：{only}，回合数：{CurrentRound}");
            Debug.WriteLine($"[历史] 共猜测 {_guessHistory.Count + 1} 次：{string.Join(" | ", _guessHistory.Select(g => g.Word))} => {only}");

            InputWord = only;
            for (int i = 0; i < Cells.Count; ++i)
            {
                Cells[i].Letter = only[i].ToString();
                Cells[i].ColorIndex = 2;
            }

            await Task.Delay(300);
            _guessHistory.Add(new GuessResult(InputWord, Cells.Select(c => c.ColorIndex).ToArray()));
            IsGameStarted = false;
            SolveButtonText = "Start Solving";
            await _dialogService.ShowMessageDialog("Success!", $"Congratulations! The word is {InputWord}!");
            await Restart();
            return;
        }
        else if (_words.Count == 0)
        {
            Debug.WriteLine("词库为空，没有候选单词");
            await _dialogService.ShowMessageDialog("Error", "No more words in list, please restart the game.");
            await Restart();
            return;
        }

        if (currentColors.All(c => c == 2))
        {
            Debug.WriteLine($"[胜利] 全绿匹配：{InputWord}，回合数：{CurrentRound}");
            Debug.WriteLine($"[历史] 共猜测 {_guessHistory.Count + 1} 次：{string.Join(" | ", _guessHistory.Select(g => g.Word))}");

            IsGameStarted = false;
            SolveButtonText = "Start Solving";
            await _dialogService.ShowMessageDialog("Success!", $"Congratulations! The word is {InputWord}!");
            await Restart();
        }
        else if (CurrentRound == 6)
        {
            Debug.WriteLine($"[失败] 已达到最大轮数（{CurrentRound}）。最后猜测为：{InputWord}");
            Debug.WriteLine($"[历史] 猜测记录：{string.Join(" | ", _guessHistory.Select(g => g.Word))}");
            Debug.WriteLine($"[候选] 剩余 {_words.Count} 个单词。示例：{string.Join(", ", _words.Take(5).Select(w => w.headWord))}");

            IsGameStarted = false;
            SolveButtonText = "Start Solving";
            await _dialogService.ShowMessageDialog("Game Over", "Game Over! Maximum attempts reached.");
            await Restart();
        }
        else
        {
            ++CurrentRound;
            await SolveAsync();
            ResetCellColors();
        }
    }

    [RelayCommand]
    private async Task Restart()
    {
        using var loading = await _dialogService.ShowLoadingDialog("Restarting...");
        IsGameStarted = false;
        CanCycleColors = false;
        CurrentRound = 0;
        InputWord = "";
        SolveButtonText = "Start Solving";
        _guessHistory.Clear();
        await LoadDictionaryAsync();
        UpdateCells();
    }

    private void UpdateCells()
    {
        Cells.Clear();
        for (int i = 0; i < WordLength; ++i)
        {
            var cell = new CellViewModel { ColorIndex = 0 };
            Cells.Add(cell);
            Debug.WriteLine($"已添加格子，索引 {i}, ref={cell.GetHashCode()}");
        }
        Debug.WriteLine($"UpdateCells 被调用，WordLength = {WordLength}, Cells.Count = {Cells.Count}");
        UpdateLetters();
    }

    private void UpdateLetters()
    {
        for (int i = 0; i < Cells.Count; ++i)
        {
            Cells[i].Letter = InputWord != null && i < InputWord.Length
                ? InputWord[i].ToString().ToUpper()
                : "";
        }
    }

    [RelayCommand]
    private void CycleColor(CellViewModel cell)
    {
        cell.CycleColor();
    }

    private void ResetCellColors()
    {
        foreach (var cell in Cells)
        {
            if (cell.ColorIndex != 2)
                cell.ColorIndex = 0;
        }
    }
}
