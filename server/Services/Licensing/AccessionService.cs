using server.Core;
using server.Extension;
using server.Models.Entity;
using server.Models.OpenSource;
using server.Models.ReCaptcha;

namespace server.Services.Licensing
{
    // Scoped
    public sealed class AccessionService(IServiceProvider serviceProvider)
    {
        private readonly ReCaptchaService reCaptchaService = serviceProvider.GetRequiredService<ReCaptchaService>();
        private readonly UserManager<HutaoUser> userManager = serviceProvider.GetRequiredService<UserManager<HutaoUser>>();
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();

        public async ValueTask<ApplicationProcessResult> ProcessApplicationAsync(LicenseApplication info)
        {
            ReCaptchaResponse? response = await reCaptchaService.VerifyAsync(info.Token).ConfigureAwait(false);

            if (response is not { Success: true, Action: "ApplyOpenSourceLicense", Score: > 0.5f })
            {
                return ApplicationProcessResult.ReCaptchaVerificationFailed;
            }

            if (await userManager.FindByNameAsync(info.UserName).ConfigureAwait(false) is not HutaoUser user)
            {
                return ApplicationProcessResult.UsetNotExists;
            }

            if (await appDbContext.Licenses.AnyAsync(l => l.UserId == user.Id).ConfigureAwait(false))
            {
                return ApplicationProcessResult.AlreadyApplied;
            }

            string code = RandomHelper.GetUpperAndNumberString(32);

            LicenseApplicationRecord record = new()
            {
                UserId = user.Id,
                ProjectUrl = info.ProjectUrl,
                ApprovalCode = code,
            };
            await appDbContext.Licenses.AddAndSaveAsync(record).ConfigureAwait(false);

            return ApplicationProcessResult.Ok;
        }

        public async ValueTask<ApplicationApproveResult> ApproveApplicationAsync(string userName, string code)
        {
            if (await userManager.FindByNameAsync(userName).ConfigureAwait(false) is not HutaoUser user)
            {
                return ApplicationApproveResult.UsetNotExists;
            }

            LicenseApplicationRecord? record = await appDbContext.Licenses
                .SingleOrDefaultAsync(l => l.UserId == user.Id && l.ApprovalCode == code)
                .ConfigureAwait(false);

            if (record is null)
            {
                return ApplicationApproveResult.NoSuchApplication;
            }

            record.IsApproved = true;
            await appDbContext.Licenses.UpdateAndSaveAsync(record).ConfigureAwait(false);

            user.IsLicensedDeveloper = true;
            await userManager.UpdateAsync(user).ConfigureAwait(false);

            return ApplicationApproveResult.Ok;
        }
    }
}