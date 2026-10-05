using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using LakerProxy.WebApi;
using LakerProxy.ProxyPlayground.Tools.DnsCheck;
using LakerProxy.ProxyPlayground.ViewModels;
using LakerProxy.WebApi.Models;

namespace LakerProxy.ProxyPlayground.Tools;

public static class ComparerHelpers
{
  public static string DomainNameReorder(string input)
  {
    if (string.IsNullOrWhiteSpace(input))
      return input;

    var parts = input.Split('.');
    return parts.Length switch
    {
      1 => input,
      2 => $"{parts[^2]}.{parts[^1]}",
      _ => $"{parts[^2]}.{parts[^1]}.{string.Join(".", parts.SkipLast(2).Reverse())}"
    };
  }
}

public class HostnameComparer : IComparer<string>
{
  public int Compare(string? x, string? y)
  {
    var a = ComparerHelpers.DomainNameReorder(x);
    var b = ComparerHelpers.DomainNameReorder(y);

    return string.CompareOrdinal(a, b);
  }
}

public class WebsiteServersComparer : IComparer<WebsiteViewModel>, IComparer
{
  public ListSortDirection SortDirection { get; set; }

  public WebsiteServersComparer(ListSortDirection sortDirection)
  {
    SortDirection = sortDirection;
  }

  public int Compare(WebsiteViewModel x, WebsiteViewModel y)
  {
    //var converter = new YourConverter();  // Replace YourConverter with your converter class

    //var valueX = converter.Convert(x.YourProperty, null, null, CultureInfo.CurrentCulture);  // Replace YourProperty with the property you're sorting on
    //var valueY = converter.Convert(y.YourProperty, null, null, CultureInfo.CurrentCulture);  // Replace YourProperty with the property you're sorting on

    var valueX = x?.Entity.Servers.FirstOrDefault();
    var valueY = y?.Entity.Servers.FirstOrDefault();

    return SortDirection == ListSortDirection.Ascending
      ? Comparer.Default.Compare(valueX, valueY)
      : Comparer.Default.Compare(valueY, valueX);
  }

  public int Compare(object x, object y)
  {
    return Compare(x as WebsiteViewModel, y as WebsiteViewModel);
  }
}

public class WebsiteHostnameComparer : IComparer<WebsiteViewModel>, IComparer
{
  public ListSortDirection SortDirection { get; set; }

  public WebsiteHostnameComparer(ListSortDirection sortDirection)
  {
    SortDirection = sortDirection;
  }

  public int Compare(WebsiteViewModel x, WebsiteViewModel y)
  {
    var valueX = ComparerHelpers.DomainNameReorder(x?.Entity.Hostname);
    var valueY = ComparerHelpers.DomainNameReorder(y?.Entity.Hostname);

    return SortDirection == ListSortDirection.Ascending
      ? Comparer.Default.Compare(valueX, valueY)
      : Comparer.Default.Compare(valueY, valueX);
  }

  public int Compare(object x, object y)
  {
    return Compare(x as WebsiteViewModel, y as WebsiteViewModel);
  }

}

/// <summary>
/// Sorts on DNS check status (problems first), then on hostname
/// </summary>
public class WebsiteDnsComparer : IComparer<WebsiteViewModel>, IComparer
{
  private readonly Func<WebsiteViewModel, DnsCheckResult> _selector;
  public ListSortDirection SortDirection { get; set; }

  public WebsiteDnsComparer(Func<WebsiteViewModel, DnsCheckResult> selector, ListSortDirection sortDirection)
  {
    _selector = selector;
    SortDirection = sortDirection;
  }

  public int Compare(WebsiteViewModel x, WebsiteViewModel y)
  {
    var rankX = DnsCheckResult.Rank(x == null ? DnsCheckStatus.NotChecked : _selector(x)?.Status ?? DnsCheckStatus.NotChecked);
    var rankY = DnsCheckResult.Rank(y == null ? DnsCheckStatus.NotChecked : _selector(y)?.Status ?? DnsCheckStatus.NotChecked);

    var result = SortDirection == ListSortDirection.Ascending
      ? rankX.CompareTo(rankY)
      : rankY.CompareTo(rankX);
    if (result != 0)
      return result;

    return Comparer.Default.Compare(
      ComparerHelpers.DomainNameReorder(x?.Entity.Hostname),
      ComparerHelpers.DomainNameReorder(y?.Entity.Hostname));
  }

  public int Compare(object x, object y)
  {
    return Compare(x as WebsiteViewModel, y as WebsiteViewModel);
  }
}

public class WebsiteCertificateExpireDateComparer : IComparer<WebsiteViewModel>, IComparer
{
  public ListSortDirection SortDirection { get; set; }

  public WebsiteCertificateExpireDateComparer(ListSortDirection sortDirection)
  {
    SortDirection = sortDirection;
  }

  public int Compare(WebsiteViewModel x, WebsiteViewModel y)
  {
    var valueX = GetExpireDate(x?.Entity);
    var valueY = GetExpireDate(y?.Entity);

    return SortDirection == ListSortDirection.Ascending
      ? Comparer.Default.Compare(valueX, valueY)
      : Comparer.Default.Compare(valueY, valueX);

    DateTime GetExpireDate(Website entity)
    {
      if (entity?.Certificates == null)
        return DateTime.MinValue;

      var certificates = entity.Certificates.OrderByDescending(c => c.ValidTill).ToArray();

      var certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Active);
      if (certificate != null)
        return certificate.ValidTill;

      certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Requesting);
      if (certificate != null)
        return certificate.ValidTill;

      certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Expired);
      if (certificate != null)
        return certificate.ValidTill;

      return DateTime.MinValue;
    }
  }

  public int Compare(object x, object y)
  {
    return Compare(x as WebsiteViewModel, y as WebsiteViewModel);
  }

}
