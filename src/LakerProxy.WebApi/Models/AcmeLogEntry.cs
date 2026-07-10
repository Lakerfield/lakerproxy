using System;

namespace LakerProxy.WebApi.Models
{
  public class AcmeLogEntry
  {
    public string Id { get; set; } = "";
    public DateTime Date { get; set; }
    public string Machine { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Step { get; set; } = "";
    public string Message { get; set; } = "";
  }
}
