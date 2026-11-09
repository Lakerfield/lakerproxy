using System;
using Lakerfield;

namespace LakerProxy.Models
{
  public class WebsiteData : IId
  {
    public ShortGuid Id { get; set; }
    public string Name { get; set; }
    public DateTime Date { get; set; }
    public string Version { get; set; }
    public byte[] Data { get; set; }
  }
}
