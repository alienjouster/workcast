import type { Metadata } from 'next';
import Link from 'next/link';
import './globals.css';
import { Providers } from './providers';
import { NavJobAdsLink } from '@/components/ui/NavJobAdsLink';

export const metadata: Metadata = {
  title: 'Workcast',
  description: 'AI-powered job board aggregation platform',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-gray-50 text-gray-900">
        <Providers>
          <nav className="bg-white border-b border-gray-200">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
              <div className="flex items-center justify-between h-16">
                <div className="flex items-center gap-8">
                  <Link
                    href="/boards"
                    className="text-xl font-bold text-indigo-600"
                  >
                    Workcast
                  </Link>
                  <div className="flex items-center gap-6">
                    <Link
                      href="/boards"
                      className="text-sm font-medium text-gray-700 hover:text-indigo-600 transition-colors"
                    >
                      Boards
                    </Link>
                    <NavJobAdsLink />
                  </div>
                </div>
                <a
                  href="http://localhost:8080/hangfire"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-sm font-medium text-gray-500 hover:text-indigo-600 transition-colors"
                >
                  Hangfire Dashboard ↗
                </a>
              </div>
            </div>
          </nav>
          <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
            {children}
          </main>
        </Providers>
      </body>
    </html>
  );
}
