using Yeek.Security.Model;

namespace Yeek.FileHosting.JavaScript.Objects;

public class JsUser
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public int TrustLevel { get; private set; }

    internal JsUser(User user)
    {
        UserId = user.Id;
        Name = user.DisplayName;
        TrustLevel = (int)user.TrustLevel;
    }

    public override string ToString()
    {
        return $"{UserId} ({TrustLevel}) - {Name}";
    }
}