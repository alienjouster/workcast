import type { Metadata } from 'next';
import { ApplicationsClient } from './ApplicationsClient';

export const metadata: Metadata = { title: 'Applications — Workcast' };

export default function ApplicationsPage() {
  return <ApplicationsClient />;
}
