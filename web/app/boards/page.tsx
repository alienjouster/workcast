import type { Metadata } from 'next';
import { BoardsClient } from './BoardsClient';

export const metadata: Metadata = { title: 'Job Boards — Workcast' };

export default function BoardsPage() {
  return <BoardsClient />;
}
