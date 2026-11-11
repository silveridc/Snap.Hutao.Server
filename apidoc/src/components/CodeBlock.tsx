import { Button } from '@douyinfe/semi-ui';
import { IconCopy } from '@douyinfe/semi-icons';
import { copyText } from '../ui';

interface Props {
  text: string;
  title?: string;
  light?: boolean;
  maxHeight?: number;
}

export default function CodeBlock({ text, title, light = false, maxHeight = 460 }: Props) {
  return (
    <div className={`code${light ? ' code-light' : ''}`} style={{ maxHeight }}>
      <Button
        className="code-copy"
        size="small"
        theme="borderless"
        icon={<IconCopy size="small" />}
        onClick={() => void copyText(text)}
      >
        {title ?? '复制'}
      </Button>
      {text}
    </div>
  );
}
