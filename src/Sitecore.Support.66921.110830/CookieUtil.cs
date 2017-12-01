using System;
using System.Web;

namespace Sitecore.Form.Core.Utility
{
  public class CookieUtil
  {
    private static readonly string KeyPrefix = "scWffm_";

    public static void SetCookie(string cookieKey, string cookieValue, TimeSpan? expires = null)
    {
      var cookieOld = HttpContext.Current.Request.Cookies[KeyPrefix + cookieKey];
      if (cookieOld != null)
      {
        cookieOld.Value = cookieValue;
        if (expires != null)
          cookieOld.Expires = DateTime.Now.Add(expires.Value);
        HttpContext.Current.Response.Cookies.Add(cookieOld);
      }
      else
      {
        var cookie = new HttpCookie(KeyPrefix + cookieKey, cookieValue);
        if (expires != null)
          cookie.Expires = DateTime.Now.Add(expires.Value);
        HttpContext.Current.Response.Cookies.Add(cookie);
      }
    }

    public static string GetCookieValue(string cookieName)
    {
      return HttpContext.Current.Request.Cookies[KeyPrefix + cookieName]?.Value ?? null;
    }
  }
}