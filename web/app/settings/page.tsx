import { Card, CardHeader, CardBody } from '@/components/ui/Card';

export default function SettingsPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-gray-900">Settings</h1>

      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Jobs</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Hangfire dashboard</td>
                <td className="px-4 py-2.5 text-sm text-gray-900">Background job monitoring and queue management</td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  <a
                    href="http://localhost:8080/hangfire"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1.5 text-xs text-indigo-500 hover:underline"
                  >
                    Open
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
                      <path d="M6.22 8.72a.75.75 0 0 0 1.06 1.06l5.22-5.22v1.69a.75.75 0 0 0 1.5 0v-3.5a.75.75 0 0 0-.75-.75h-3.5a.75.75 0 0 0 0 1.5h1.69L6.22 8.72Z"/>
                      <path d="M3.5 6.75c0-.69.56-1.25 1.25-1.25H7A.75.75 0 0 0 7 4H4.75A2.75 2.75 0 0 0 2 6.75v4.5A2.75 2.75 0 0 0 4.75 14h4.5A2.75 2.75 0 0 0 12 11.25V9a.75.75 0 0 0-1.5 0v2.25c0 .69-.56 1.25-1.25 1.25h-4.5c-.69 0-1.25-.56-1.25-1.25v-4.5Z"/>
                    </svg>
                  </a>
                </td>
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
