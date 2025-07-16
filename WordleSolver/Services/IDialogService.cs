using System;
using System.Threading.Tasks;

namespace WordleSolver.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmDialog(string title, string message);
    Task ShowMessageDialog(string title, string message);
    Task<IDisposable> ShowLoadingDialog(string message);
}
