using System.Windows.Media;
using Entity = LakerProxy.WebApi.Models.LogEntry;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class LogEntryViewModel
  {
    public Entity Entity { get; }

    public LogEntryViewModel(Entity entity)
    {
      Entity = entity;
    }

    public Brush LevelBrush => Entity.LogLevel switch
    {
      "Trace" => Brushes.Gray,
      "Debug" => Brushes.DarkGray,
      "Info" => Brushes.Black,
      "Warn" => Brushes.DarkOrange,
      "Error" => Brushes.Red,
      "Fatal" => Brushes.DarkRed,
      _ => Brushes.Black,
    };

  }
}
