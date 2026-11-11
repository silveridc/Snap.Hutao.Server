import type { ReactNode } from 'react';

import { Typography } from '@douyinfe/semi-ui';

export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div>
      <Typography.Text type="tertiary" className="mb-1 block !text-xs">
        {label}
      </Typography.Text>
      {children}
      {hint && (
        <Typography.Text type="quaternary" className="mt-1 block !text-xs">
          {hint}
        </Typography.Text>
      )}
    </div>
  );
}
