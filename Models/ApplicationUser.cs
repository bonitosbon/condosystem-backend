using Microsoft.AspNetCore.Identity;

namespace CondoSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
    }
}
