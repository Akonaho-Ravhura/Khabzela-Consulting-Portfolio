using KhabzelaConsulting.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KhabzelaConsulting.Services
{
    // Team invitations: a one-time "set your password" link that stays valid for 3 days
    // (password-reset links expire after 30 minutes).
    public class InviteTokenProviderOptions : DataProtectionTokenProviderOptions
    {
        public InviteTokenProviderOptions()
        {
            Name = InviteTokenProvider.ProviderName;
            TokenLifespan = TimeSpan.FromDays(3);
        }
    }

    public class InviteTokenProvider : DataProtectorTokenProvider<ApplicationUser>
    {
        public const string ProviderName = "Invite";
        public const string Purpose = "TeamInvite";

        public InviteTokenProvider(IDataProtectionProvider dataProtectionProvider, IOptions<InviteTokenProviderOptions> options,
            ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
            : base(dataProtectionProvider, options, logger) { }
    }
}
