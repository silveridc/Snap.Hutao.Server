import { useMemo } from 'react';
import { Tree } from '@douyinfe/semi-ui';
import { IconFolder } from '@douyinfe/semi-icons';
import type { NormalizedDoc, Operation } from '../openapi/types';
import { MethodChip } from '../ui';

interface Props {
  doc: NormalizedDoc;
  activeId: string;
  onSelect: (op: Operation) => void;
  filter: string;
}

export default function OpTree({ doc, activeId, onSelect, filter }: Props) {
  const keyword = filter.trim().toLowerCase();

  const groups = useMemo(() => {
    if (keyword === '') {
      return doc.groups;
    }
    return doc.groups
      .map((group) => ({
        ...group,
        operations: group.operations.filter(
          (op) =>
            op.summary.toLowerCase().includes(keyword) ||
            op.path.toLowerCase().includes(keyword) ||
            op.tag.toLowerCase().includes(keyword) ||
            op.description.toLowerCase().includes(keyword),
        ),
      }))
      .filter((group) => group.operations.length > 0);
  }, [doc.groups, keyword]);

  const treeData = groups.map((group) => ({
    key: `tag:${group.tag}`,
    icon: <IconFolder size="small" />,
    label: (
      <span className="tree-tag">
        <span>{group.tag}</span>
        <span className="count">{group.operations.length}</span>
      </span>
    ),
    children: group.operations.map((op) => ({
      key: op.id,
      label: (
        <span className="tree-op">
          <MethodChip method={op.method} />
          <span className="tree-op-text" title={`${op.summary}\n${op.path}`}>
            <span className={op.deprecated ? 'deprecated' : undefined}>{op.summary}</span>
            <br />
            <span className="tree-op-path">{op.path}</span>
          </span>
        </span>
      ),
    })),
  }));

  return (
    <Tree
      treeData={treeData}
      style={{ padding: 0 }}
      blockNode
      expandAll
      selectedKeys={activeId === '' ? [] : [activeId]}
      onSelect={(keys) => {
        const id = String((Array.isArray(keys) ? keys[0] : keys) ?? '');
        if (!id || id.startsWith('tag:')) {
          return;
        }
        const op = doc.operations.find((item) => item.id === id);
        if (op) {
          onSelect(op);
        }
      }}
    />
  );
}
