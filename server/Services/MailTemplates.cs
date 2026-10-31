namespace server.Services;

public static class MailTemplates
{
    public const string DefaultHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Snap Hutao Mail Template</title>
            <style>
                #mail-table {
                    margin: auto;
                    display: flex;
                    flex-direction: column;
                    justify-content: center;
                    align-items: center;
                    row-gap: 10px;
                    padding: 20px;
                }

                #mail-body {
                    border: 1px solid #dadce0;
                    border-radius: 5px;
                    padding: 40px 20px;
                    line-height: 20px;
                    text-align: center;
                    display: flex;
                    row-gap: 16px;
                    flex-direction: column;
                    justify-content: flex-start;
                    align-items: center;
                }

                #mail-logo {
                    width: auto;
                    height: 80px;
                    object-fit: contain;
                }

                #mail-title {
                    font-family: 'Google Sans', Roboto, RobotoDraft, Helvetica, Arial, sans-serif;
                    font-size: 24px;
                    color: rgba(0, 0, 0);
                    padding-bottom: 32px;
                    text-align: center;
                    border-bottom: thin solid #dadce0;
                    line-height: 1.5;
                }

                #mail-content {
                    width: 100%;
                    display: flex;
                    white-space: pre-wrap;
                    flex-direction: column;
                    align-items: flex-start;
                    justify-content: center;
                    text-align: left;
                }

                .mail-code {
                    font-family: 'Monaco', monospace;
                    border: 1px solid #DAE1E9;
                    letter-spacing: 4px;
                    padding: 20px 8px;
                    border-radius: 4px;
                    background-color: #F4F7FA;
                    color: #3a9aed;
                    margin: auto;
                    display: block;
                    width: 60%;
                    text-align: center;
                    font-weight: bold;
                    font-size: 30px;
                }

                .mail-date {
                    position: relative;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    font-family: 'Monaco', monospace;
                    border: 1px solid #DAE1E9;
                    padding: 20px 8px;
                    border-radius: 4px;
                    background-color: #F4F7FA;
                    color: #febd2b;
                    width: 100%;
                    box-sizing: border-box;
                    text-align: center;
                    font-weight: bold;
                    font-size: 20px;
                }

                .mail-hint {
                    font-weight: bold;
                    color: #fd6243;
                }

                #mail-footer {
                    font-family: Roboto-Regular, Helvetica, Arial, sans-serif;
                    width: 100%;
                    display: flex;
                    flex-direction: column;
                    text-align: center;
                    font-size: 11px;
                    color: rgb(0 0 0/0.54);
                    line-height: 18px;
                }
            </style>
        </head>
        <body>
        <div id="mail-table">
            <div id="mail-body">
                <img alt="Snap Hutao"
                     id="dgp-logo"
                     src="https://img.alicdn.com/imgextra/i3/1797064093/O1CN01sjsnty1g6e14c9BwZ_!!1797064093.png">
                <div id="mail-title">{{options.Title}}</div>
                <div id="mail-content">{{options.RawContent}}</div>
            </div>
            <div id="mail-footer">
                <span>{{options.Footer}}</span>
                <span>&copy; 2026 <a href="https://github.com/silveridc">zhaoyj</a> | <a href="https://github.com/silveridc/Snap.Hutao">Snap Hutao</a> Dev Team. All rights reserved.</span>
            </div>
        </div>
        </body>
        </html>
        """;

    /// <summary>模板占位符：缺少任何一项都会让邮件内容不完整，保存时逐项校验.</summary>
    public static readonly string[] Placeholders = ["{{options.Title}}", "{{options.RawContent}}", "{{options.Footer}}"];
}
