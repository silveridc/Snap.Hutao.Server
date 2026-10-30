interface Grecaptcha {
  ready(callback: () => void): void;
  execute(siteKey: string, options: { action: string }): Promise<string>;
}

let scriptPromise: Promise<void> | null = null;
let loadedSiteKey = '';

function ensureScript(siteKey: string): Promise<void> {
  if (!scriptPromise || loadedSiteKey !== siteKey) {
    loadedSiteKey = siteKey;
    scriptPromise = new Promise((resolve, reject) => {
      const script = document.createElement('script');
      // recaptcha.net 在境内可访问，与 Google 域名的验证结果互通
      script.src = `https://www.recaptcha.net/recaptcha/api.js?render=${encodeURIComponent(siteKey)}`;
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Failed to load reCAPTCHA'));
      document.head.appendChild(script);
    });
  }
  return scriptPromise;
}

export async function getReCaptchaToken(siteKey: string, action: string): Promise<string> {
  await ensureScript(siteKey);
  const grecaptcha = (window as Window & { grecaptcha?: Grecaptcha }).grecaptcha;
  if (!grecaptcha) {
    throw new Error('reCAPTCHA is unavailable');
  }

  return new Promise<string>((resolve, reject) => {
    grecaptcha.ready(() => {
      grecaptcha.execute(siteKey, { action }).then(resolve).catch(reject);
    });
  });
}
