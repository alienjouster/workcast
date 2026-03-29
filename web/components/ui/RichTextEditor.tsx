'use client';

import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Underline from '@tiptap/extension-underline';
import { Color } from '@tiptap/extension-color';
import { TextStyle } from '@tiptap/extension-text-style';
import FontFamily from '@tiptap/extension-font-family';
import Highlight from '@tiptap/extension-highlight';
import { useEffect, useRef } from 'react';
import { Tooltip } from '@/components/ui/Tooltip';

const FONTS = [
  { label: 'Default',          value: '' },
  { label: 'Arial',            value: 'Arial, sans-serif' },
  { label: 'Georgia',          value: 'Georgia, serif' },
  { label: 'Times New Roman',  value: '"Times New Roman", serif' },
  { label: 'Courier New',      value: '"Courier New", monospace' },
  { label: 'Verdana',          value: 'Verdana, sans-serif' },
  { label: 'Trebuchet MS',     value: '"Trebuchet MS", sans-serif' },
];

// ── Toolbar button ─────────────────────────────────────────────────────────────

function ToolbarBtn({
  onClick,
  active,
  title,
  children,
}: {
  onClick: () => void;
  active?: boolean;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Tooltip content={title} position="top" wrapperAs="span">
      <button
        type="button"
        onMouseDown={(e) => { e.preventDefault(); onClick(); }}
        className={`px-1.5 py-0.5 rounded text-xs font-medium transition-colors select-none ${
          active
            ? 'bg-indigo-100 text-indigo-700'
            : 'text-gray-600 hover:bg-gray-200'
        }`}
      >
        {children}
      </button>
    </Tooltip>
  );
}

function Divider() {
  return <div className="w-px h-4 bg-gray-200 mx-0.5" />;
}

// ── Editor ─────────────────────────────────────────────────────────────────────

interface RichTextEditorProps {
  value: string;
  onChange: (html: string) => void;
  minHeight?: number;
}

export function RichTextEditor({ value, onChange, minHeight = 400 }: RichTextEditorProps) {
  const lastHtml = useRef(value);

  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit,
      Underline,
      TextStyle,
      Color,
      FontFamily,
      Highlight.configure({ multicolor: true }),
    ],
    content: value,
    onUpdate: ({ editor }) => {
      const html = editor.getHTML();
      lastHtml.current = html;
      onChange(html);
    },
  });

  // Sync external value changes (e.g. when startEdit resets draft)
  useEffect(() => {
    if (editor && value !== lastHtml.current) {
      editor.commands.setContent(value, { emitUpdate: false });
      lastHtml.current = value;
    }
  }, [value, editor]);

  if (!editor) return null;

  const currentFont = editor.getAttributes('textStyle').fontFamily ?? '';

  return (
    <div className="border border-gray-200 rounded-md overflow-hidden">
      {/* ── Toolbar ─────────────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-0.5 px-2 py-1.5 bg-gray-50 border-b border-gray-200">

        {/* History */}
        <ToolbarBtn onClick={() => editor.chain().focus().undo().run()} title="Undo">↩</ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().redo().run()} title="Redo">↪</ToolbarBtn>
        <Divider />

        {/* Headings */}
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleHeading({ level: 1 }).run()}
          active={editor.isActive('heading', { level: 1 })}
          title="Heading 1"
        >H1</ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}
          active={editor.isActive('heading', { level: 2 })}
          title="Heading 2"
        >H2</ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}
          active={editor.isActive('heading', { level: 3 })}
          title="Heading 3"
        >H3</ToolbarBtn>
        <Divider />

        {/* Inline formatting */}
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleBold().run()}
          active={editor.isActive('bold')}
          title="Bold"
        ><strong>B</strong></ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleItalic().run()}
          active={editor.isActive('italic')}
          title="Italic"
        ><em>I</em></ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleUnderline().run()}
          active={editor.isActive('underline')}
          title="Underline"
        ><span className="underline">U</span></ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleStrike().run()}
          active={editor.isActive('strike')}
          title="Strikethrough"
        ><span className="line-through">S</span></ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleCode().run()}
          active={editor.isActive('code')}
          title="Inline code"
        ><span className="font-mono">{'{}'}</span></ToolbarBtn>
        <Divider />

        {/* Lists */}
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleBulletList().run()}
          active={editor.isActive('bulletList')}
          title="Bullet list"
        >• –</ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleOrderedList().run()}
          active={editor.isActive('orderedList')}
          title="Ordered list"
        >1.</ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().toggleBlockquote().run()}
          active={editor.isActive('blockquote')}
          title="Blockquote"
        >❝</ToolbarBtn>
        <Divider />

        {/* Font family */}
        <Tooltip content="Font family" position="top" wrapperAs="span">
          <select
            value={currentFont}
            onChange={(e) => {
              const font = e.target.value;
              if (font) {
                editor.chain().focus().setFontFamily(font).run();
              } else {
                editor.chain().focus().unsetFontFamily().run();
              }
            }}
            className="text-xs text-gray-600 bg-transparent border border-gray-200 rounded px-1 py-0.5 focus:outline-none focus:ring-1 focus:ring-indigo-300"
          >
            {FONTS.map((f) => (
              <option key={f.value} value={f.value}>{f.label}</option>
            ))}
          </select>
        </Tooltip>
        <Divider />

        {/* Text color */}
        <Tooltip content="Text color" position="top" wrapperAs="span">
          <label className="flex items-center gap-1 cursor-pointer text-xs text-gray-600 px-1.5 py-0.5 rounded hover:bg-gray-200">
            <span style={{ borderBottom: `2px solid ${editor.getAttributes('textStyle').color ?? '#000000'}` }}>A</span>
            <input
              type="color"
              defaultValue="#000000"
              onChange={(e) => editor.chain().focus().setColor(e.target.value).run()}
              className="w-0 h-0 opacity-0 absolute"
            />
          </label>
        </Tooltip>

        {/* Highlight color */}
        <Tooltip content="Highlight color" position="top" wrapperAs="span">
          <label className="flex items-center gap-1 cursor-pointer text-xs text-gray-600 px-1.5 py-0.5 rounded hover:bg-gray-200">
            <span
              className="px-0.5"
              style={{ backgroundColor: editor.isActive('highlight') ? (editor.getAttributes('highlight').color ?? '#fef08a') : '#fef08a' }}
            >ab</span>
            <input
              type="color"
              defaultValue="#fef08a"
              onChange={(e) => editor.chain().focus().toggleHighlight({ color: e.target.value }).run()}
              className="w-0 h-0 opacity-0 absolute"
            />
          </label>
        </Tooltip>
        <Divider />

        {/* Clear formatting */}
        <ToolbarBtn
          onClick={() => editor.chain().focus().unsetAllMarks().clearNodes().run()}
          title="Clear formatting"
        >✕ fmt</ToolbarBtn>
      </div>

      {/* ── Content area ────────────────────────────────────────────────────── */}
      <EditorContent
        editor={editor}
        style={{ minHeight }}
        className="prose prose-sm max-w-none px-4 py-3 focus-within:outline-none [&_.ProseMirror]:outline-none [&_.ProseMirror]:min-h-[inherit]"
      />
    </div>
  );
}
