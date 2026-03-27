import type { Metadata } from 'next';
import { AdsClient } from './AdsClient';

export const metadata: Metadata = { title: 'Job Ads — Workcast' };

export default function AdsPage() {
  return <AdsClient />;
}
