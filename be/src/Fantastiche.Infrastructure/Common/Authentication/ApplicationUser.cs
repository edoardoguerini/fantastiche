using Microsoft.AspNetCore.Identity;

namespace Fantastiche.Infrastructure.Common.Authentication;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string DisplayName { get; set; } = string.Empty;
}
