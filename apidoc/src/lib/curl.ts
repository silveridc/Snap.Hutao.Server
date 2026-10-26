function quote(value: string): string {
  return `'${value.replace(/'/g, `'\\''`)}'`;
}

export interface CurlInput {
  method: string;
  url: string;
  headers: Record<string, string>;
  body: string;
}

export function buildCurl({ method, url, headers, body }: CurlInput): string {
  const lines = [`curl --location --request ${method.toUpperCase()} ${quote(url)}`];
  for (const [name, value] of Object.entries(headers)) {
    if (value !== '') {
      lines.push(`  --header ${quote(`${name}: ${value}`)}`);
    }
  }
  if (body.trim() !== '') {
    lines.push(`  --data-raw ${quote(body)}`);
  }
  return lines.join(' \\\n');
}
