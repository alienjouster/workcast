'use client';

import { useState, useRef, useCallback, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

type TooltipPosition = 'top' | 'bottom' | 'bottom-start' | 'right' | 'left';

interface Coords { top: number; left: number; transform: string }

const GAP = 8;

function calcCoords(rect: DOMRect, position: TooltipPosition): Coords {
  switch (position) {
    case 'top':
      return { top: rect.top - GAP, left: rect.left + rect.width / 2, transform: 'translate(-50%, -100%)' };
    case 'bottom':
      return { top: rect.bottom + GAP, left: rect.left + rect.width / 2, transform: 'translateX(-50%)' };
    case 'bottom-start':
      return { top: rect.bottom + GAP, left: rect.left, transform: 'none' };
    case 'right':
      return { top: rect.top + rect.height / 2, left: rect.right + GAP, transform: 'translateY(-50%)' };
    case 'left':
      return { top: rect.top + rect.height / 2, left: rect.left - GAP, transform: 'translate(-100%, -50%)' };
  }
}

interface TooltipProps {
  content: ReactNode;
  children: ReactNode;
  position?: TooltipPosition;
  wrapperAs?: 'div' | 'span';
  /** Set true when content is long and should wrap (overrides default nowrap). */
  wrap?: boolean;
  /** Extra classes for the tooltip bubble (e.g. max-width). */
  tooltipClassName?: string;
  /** Extra classes for the wrapper element (layout, display). */
  className?: string;
}

export function Tooltip({
  content,
  children,
  position = 'top',
  wrapperAs = 'div',
  wrap = false,
  tooltipClassName,
  className,
}: TooltipProps) {
  const [coords, setCoords] = useState<Coords | null>(null);
  const ref = useRef<HTMLElement | null>(null);

  const onMouseEnter = useCallback(() => {
    if (!ref.current || !content) return;
    setCoords(calcCoords(ref.current.getBoundingClientRect(), position));
  }, [position, content]);

  const onMouseLeave = useCallback(() => setCoords(null), []);

  if (!content) return <>{children}</>;

  const bubbleCls = [
    'pointer-events-none fixed rounded bg-gray-900 px-2.5 py-1.5 text-xs text-white z-[9999]',
    wrap ? 'whitespace-normal' : 'whitespace-nowrap',
    tooltipClassName ?? '',
  ].join(' ');

  const portal = coords && typeof document !== 'undefined'
    ? createPortal(
        <span className={bubbleCls} style={{ top: coords.top, left: coords.left, transform: coords.transform }}>
          {content}
        </span>,
        document.body,
      )
    : null;

  if (wrapperAs === 'span') {
    return (
      <span
        ref={ref as React.RefObject<HTMLSpanElement>}
        className={className}
        onMouseEnter={onMouseEnter}
        onMouseLeave={onMouseLeave}
      >
        {children}
        {portal}
      </span>
    );
  }
  return (
    <div
      ref={ref as React.RefObject<HTMLDivElement>}
      className={className}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
    >
      {children}
      {portal}
    </div>
  );
}
