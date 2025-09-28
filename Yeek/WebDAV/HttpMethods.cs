using Microsoft.AspNetCore.Mvc.Routing;

namespace Yeek.WebDAV;

public class HttpPropFindAttribute(string template) : HttpMethodAttribute(SupportedMethods, template)
{
    private static readonly IEnumerable<string> SupportedMethods = ["PROPFIND"];
}

public class HttpMkColAttribute(string template) : HttpMethodAttribute(SupportedMethods, template)
{
    private static readonly IEnumerable<string> SupportedMethods = ["MKCOL"];
}

public class HttpMoveAttribute(string template) : HttpMethodAttribute(SupportedMethods, template)
{
    private static readonly IEnumerable<string> SupportedMethods = ["MOVE"];
}

public class HttpCopyAttribute(string template) : HttpMethodAttribute(SupportedMethods, template)
{
    private static readonly IEnumerable<string> SupportedMethods = ["COPY"];
}

public class HttpLockAttribute(string template) : HttpMethodAttribute(SupportedMethods, template)
{
    private static readonly IEnumerable<string> SupportedMethods = ["LOCK"];
}