using server.Models.Mail;

namespace server.Services.Mail;

public static class MailContents
{
    public static readonly string[] Languages = ["CHS", "EN"];

    public static readonly MailContentKind[] Kinds =
    [
        new()
        {
            Key = "RegisterCode",
            Variables = ["code"],
            Chs = new()
            {
                Subject = "{{sysname}} 邮箱验证邮件",
                Title = "{{sysname}} 邮箱验证邮件",
                RawContent = $$$"""
                    <p>您好，您正在进行邮箱验证，以下是您的验证码：</p>
                    <p><span class="mail-code">{{code}}</span></p>
                    {{{_defaultMsg}}}
                    """,
                Footer = _footer,
            },
            En = new()
            {
                Subject = "{{sysname}} Email Verification",
                Title = "{{sysname}} Email Verification",
                RawContent = $$$"""
                    <p>Hello, you are currently verifying your email address. Below is your verification code:</p>
                    <p><span class="mail-code">{{code}}</span></p>
                    {{{_defaultMsgEng}}}
                    """,
                Footer = _footerEng,
            },
        },
        new()
        {
            Key = "ResetPasswordLink",
            Variables = ["token"],
            Chs = new()
            {
                Subject = "{{sysname}} 密码重置邮件",
                Title = "{{sysname}} 密码重置邮件",
                RawContent = $$$"""
                    <p>您好，您正在重置登录密码，请点击下方按钮设置新密码：</p>
                    <p><a class="mail-code" style="text-decoration:none;font-size:20px;letter-spacing:0" href="{{sysHost}}/reset-password.html?token={{token}}">重置密码</a></p>
                    <p>该链接仅能使用一次，请勿向他人转发。</p>
                    {{{_defaultMsg}}}
                    """,
                Footer = _footer,
            },
            En = new()
            {
                Subject = "{{sysname}} Password Reset",
                Title = "{{sysname}} Password Reset",
                RawContent = $$$"""
                    <p>Hello, you are resetting your sign-in password. Click the button below to set a new password:</p>
                    <p><a class="mail-code" style="text-decoration:none;font-size:20px;letter-spacing:0" href="{{sysHost}}/reset-password.html?token={{token}}">Reset password</a></p>
                    <p>This link can be used only once. Do not forward it to anyone else.</p>
                    {{{_defaultMsgEng}}}
                    """,
                Footer = _footerEng,
            },
        },
        new()
        {
            Key = "ResetUsernameCode",
            Variables = ["code"],
            Chs = new()
            {
                Subject = "{{sysname}} 邮箱修改验证邮件",
                Title = "{{sysname}} 邮箱修改验证邮件",
                RawContent = $$$"""
                    <p>您好，您正在修改通行证邮箱，以下是您的验证码：</p>
                    <p><span class="mail-code">{{code}}</span></p>
                    {{{_defaultMsg}}}
                    """,
                Footer = _footer,
            },
            En = new()
            {
                Subject = "{{sysname}} Email Change Verification",
                Title = "{{sysname}} Email Change Verification",
                RawContent = $$$"""
                    <p>Hello, you are changing your passport email address. Below is your verification code:</p>
                    <p><span class="mail-code">{{code}}</span></p>
                    {{{_defaultMsgEng}}}
                    """,
                Footer = _footerEng,
            },
        },
        new()
        {
            Key = "CancelRegistrationCode",
            Variables = ["code"],
            Chs = new()
            {
                Subject = "{{sysname}} 通行证注销验证邮件",
                Title = "{{sysname}} 通行证注销验证邮件",
                RawContent = $$$"""
                    <p><b class="mail-hint" style="font-size:20px">请注意：注销通行证的操作是不可逆的。</b></p>
                    <p>您好，您正在注销通行证，以下是您的验证码：</p>
                    <p><span class="mail-code">{{code}}</span></p>
                    {{{_defaultMsg}}}
                    """,
                Footer = _footer,
            },
            En = new()
            {
                Subject = "{{sysname}} Account Deletion Verification",
                Title = "{{sysname}} Account Deletion Verification",
                RawContent = $$$"""
                    <p><b class="mail-hint" style="font-size:20px">Please note: Deleting your passport is irreversible.</b></p>
                    <p>Hello, you are deleting your passport. Below is your verification code:</p>
                    <p><span class="mail-code">{{code}}</span></p>
                    {{{_defaultMsgEng}}}
                    """,
                Footer = _footerEng,
            },
        },
        new()
        {
            Key = "GachaStorageRedeem",
            Variables = ["redeemCode", "tradeNumber"],
            Chs = new()
            {
                Subject = "{{sysname}} 祈愿记录存储兑换邮件",
                Title = "{{sysname}} 祈愿记录存储兑换邮件",
                RawContent = """
                    <p>您好，感谢您赞助 {{sysname}} 祈愿记录上传服务。</p>
                    <p>由于您尚未注册通行证，现向您发放兑换码，有效期为 <span class="mail-hint">{{expireDays}} 天</span>：</p>
                    <p><span class="mail-date">{{redeemCode}}</span></p>
                    <p>您可以在 {{sysname}} 内的通行证页面注册后进行兑换。</p>
                    <p>请妥善保存此邮件，订单编号：<span class="mail-hint">{{tradeNumber}}</span></p>
                    """,
                Footer = _footer,
            },
            En = new()
            {
                Subject = "{{sysname}} Wish Record Storage Redemption",
                Title = "{{sysname}} Wish Record Storage Redemption",
                RawContent = """
                    <p>Hello, thank you for sponsoring the {{sysname}} Wish Record Backup Service.</p>
                    <p>Since you have not registered for a passport, we are issuing you a redeem code, valid for <span class="mail-hint">{{expireDays}} days</span>:</p>
                    <p><span class="mail-date">{{redeemCode}}</span></p>
                    <p>You can redeem it after registering on the passport page in {{sysname}}.</p>
                    <p>Please keep this email safe, order number: <span class="mail-hint">{{tradeNumber}}</span></p>
                    """,
                Footer = _footerEng,
            },
        },
        new()
        {
            Key = "CdnRedeem",
            Variables = ["redeemCode", "tradeNumber"],
            Chs = new()
            {
                Subject = "{{sysname}} CDN 更新加速兑换邮件",
                Title = "{{sysname}} CDN 更新加速兑换邮件",
                RawContent = """
                    <p>您好，感谢您赞助 {{sysname}} CDN 更新加速服务。</p>
                    <p>由于您尚未注册通行证，现向您发放兑换码，有效期为 <span class="mail-hint">{{expireDays}} 天</span>：</p>
                    <p><span class="mail-date">{{redeemCode}}</span></p>
                    <p>您可以在 {{sysname}} 内的通行证页面注册后进行兑换。</p>
                    <p>请妥善保存此邮件，订单编号：<span class="mail-hint">{{tradeNumber}}</span></p>
                    """,
                Footer = _footer,
            },
            En = new()
            {
                Subject = "{{sysname}} CDN Update Acceleration Redemption",
                Title = "{{sysname}} CDN Update Acceleration Redemption",
                RawContent = """
                    <p>Hello, thank you for sponsoring the {{sysname}} CDN Update Acceleration Service.</p>
                    <p>Since you have not registered for a passport, we are issuing you a redeem code, valid for <span class="mail-hint">{{expireDays}} days</span>:</p>
                    <p><span class="mail-date">{{redeemCode}}</span></p>
                    <p>You can redeem it after registering on the passport page in {{sysname}}.</p>
                    <p>Please keep this email safe, order number: <span class="mail-hint">{{tradeNumber}}</span></p>
                    """,
                Footer = _footerEng,
            },
        },
    ];

    private const string _defaultMsg = """
        <p>验证信息在 <span class="mail-hint">{{expireMinutes}} 分钟</span>内有效。</p>
        <p>如果本次请求并非由您发起，请务必告知我们，由此给您带来的不便敬请谅解。</p>
        """;

    private const string _defaultMsgEng = """
        <p>This verification information is valid for <span class="mail-hint">{{expireMinutes}} minutes</span>.</p>
        <p>If you did not initiate this request, please let us know. We apologize for any inconvenience caused.</p>
        """;

    private const string _footer = "本邮件为系统自动发送邮件，请勿回复。";
    private const string _footerEng = "This email was sent automatically by the system, please do not reply.";

    private static readonly Dictionary<string, MailContentKind> _map = Kinds.ToDictionary(kind => kind.Key, StringComparer.OrdinalIgnoreCase);

    public static MailContentKind Get(string key)
    {
        return _map[key];
    }

    public static bool TryGet(string key, out MailContentKind? kind)
    {
        if (_map.TryGetValue(key, out MailContentKind? value))
        {
            kind = value;
            return true;
        }

        kind = null;
        return false;
    }
}
