using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LakerProxy.Models;

// ReSharper disable once CheckNamespace
namespace Lakerfield
{
  public static class ContextExtensions
  {
    public static Context Add(this Context context, ApiUser apiUser)
    {
      return context.Add("User", apiUser.Id);
    }
  }
}
