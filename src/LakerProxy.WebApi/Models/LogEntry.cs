using System;

namespace LakerProxy.WebApi.Models
{
  public class LogEntry
  {
    public string Id { get; set; } = "";
    public DateTime Date { get; set; }
    public string Machine { get; set; } = "";
    public string Application { get; set; } = "";
    public int ProcessId { get; set; }
    public string LogLevel { get; set; } = "";
    public string Message { get; set; } = "";
    public string Owner { get; set; } = "";
  }
}
