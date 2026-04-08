using friends.web.Models;

namespace friends.web.Services;

public class AppSession
{
    public Guid? CurrentUserId { get; private set; }
    public string CurrentUserName { get; private set; } = string.Empty;

    public bool HasUser => CurrentUserId is not null;

    public void SetCurrentUser(User user)
    {
        CurrentUserId = user.Id;
        CurrentUserName = user.Name;
    }

    public void SetCurrentUser(UserProfileSummary user)
    {
        CurrentUserId = user.Id;
        CurrentUserName = user.Name;
    }

    public void Clear()
    {
        CurrentUserId = null;
        CurrentUserName = string.Empty;
    }
}
