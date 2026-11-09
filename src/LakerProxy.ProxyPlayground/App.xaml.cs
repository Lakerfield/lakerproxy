using System;
using System.Text;
using System.Windows;

namespace LakerProxy.ProxyPlayground
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {

    public App()
    {
      InitializeComponent();


      //You can trap unhandled exceptions at three levels:

      //1.AppDomain.UnhandledException From all threads in the AppDomain.
      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

      //2.Dispatcher.UnhandledException From a single specific UI dispatcher thread.
      //Dispatcher.UnhandledException += new System.Windows.Threading.DispatcherUnhandledExceptionEventHandler(Dispatcher_UnhandledException);

      //3.Application.DispatcherUnhandledException From the main UI dispatcher thread in your WPF application.
      this.DispatcherUnhandledException += new System.Windows.Threading.DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);

    }


    void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
      if (e.Handled)
        return;

      //Globals.State.ShowError(e.Exception);
      ShowException(@"Unhandled exception", e.Exception);
      e.Handled = true;
    }

    void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
      if (e.Handled)
        return;

      //Globals.State.ShowError(e.Exception);
      ShowException(@"Unhandled exception", e.Exception);
      e.Handled = true;
    }

    void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      var exception = e.ExceptionObject as Exception;
      if (exception == null)
        return;

      ShowException(@"Unhandled exception in appdomain", exception);
      //Globals.State.ShowError(exception);
    }

    public static void ShowException(string title, Exception exception)
    {
      var sb = new StringBuilder();

      AddExceptionToStringBuilder(sb, exception);

#if DEBUG
      //sb.AppendFormat(@"{1}Stacktrace:{1}{0}", aException.StackTrace, Environment.NewLine);
#endif

      MessageBox.Show(sb.ToString(), title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void AddExceptionToStringBuilder(StringBuilder sb, Exception exception, string prefix = @"")
    {
      sb.Append($@"{prefix} {exception.Message}");
      sb.AppendLine();

      if (exception.InnerException != null)
        AddExceptionToStringBuilder(sb, exception.InnerException, $"{prefix}*");
    }

  }
}
